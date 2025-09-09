using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Castellan.Worker; // for Pipeline
using Castellan.Worker.Abstractions;
using Castellan.Worker.Collectors;
using Castellan.Worker.Embeddings;
using Castellan.Worker.VectorStores;
using Castellan.Worker.Llms;
using Castellan.Worker.Models;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.Services.NotificationChannels;
using Castellan.Worker.Configuration;
using Castellan.Worker.Configuration.Validation;
using Castellan.Worker.Data;
using Castellan.Worker.Middleware;
using Castellan.Worker.Services.ConnectionPools;
using Castellan.Worker.Services.ConnectionPools.Interfaces;
using Castellan.Worker.Hubs;
using Serilog;

// Enable HTTP/2 over cleartext (h2c) for local gRPC (Qdrant 6334)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Create logs directory
Directory.CreateDirectory("logs");

// Log edition information at startup
Console.WriteLine($"Starting {EditionFeatures.GetVersionString()}");

// Configure Serilog with structured logging and correlation ID support
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Castellan")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/run-.log", 
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 2 * 1024 * 1024, // 2MB
        retainedFileCountLimit: 5,
        rollOnFileSizeLimit: true,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1),
        outputTemplate: 
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.Configure<EvtxOptions>(builder.Configuration.GetSection("Ingest:Evtx"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("LLM"));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection("Alerts"));
builder.Services.Configure<PipelineOptions>(builder.Configuration.GetSection("Pipeline"));
builder.Services.Configure<CorrelationOptions>(builder.Configuration.GetSection("Correlation"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<IPEnrichmentOptions>(builder.Configuration.GetSection("IPEnrichment"));
builder.Services.Configure<AutomatedResponseOptions>(builder.Configuration.GetSection("AutomatedResponse"));
builder.Services.Configure<ThreatScanOptions>(builder.Configuration.GetSection("ThreatScan"));
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection(AuthenticationOptions.SectionName));
builder.Services.Configure<TeamsNotificationOptions>(builder.Configuration.GetSection("Notifications:Teams"));
builder.Services.Configure<SlackNotificationOptions>(builder.Configuration.GetSection("Notifications:Slack"));

// Configure connection pools
builder.Services.Configure<ConnectionPoolOptions>(builder.Configuration.GetSection("ConnectionPools"));

// Register configuration validators
builder.Services.AddSingleton<IValidateOptions<AuthenticationOptions>, AuthenticationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<QdrantOptions>, QdrantOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<PipelineOptions>, PipelineOptionsValidator>();

// Add new security services
builder.Services.AddSingleton<IPasswordHashingService, BCryptPasswordHashingService>();
builder.Services.AddSingleton<IJwtTokenBlacklistService, MemoryJwtTokenBlacklistService>();
builder.Services.AddSingleton<IRefreshTokenService, MemoryRefreshTokenService>();

builder.Services.AddSingleton<ILogCollector, EvtxCollector>();


builder.Services.AddSingleton<INotificationService, NotificationService>();

// Add notification channels
builder.Services.AddSingleton<INotificationChannel, TeamsNotificationChannel>();
builder.Services.AddSingleton<INotificationChannel, SlackNotificationChannel>();
builder.Services.AddSingleton<INotificationManager, NotificationManager>();

// Add notification configuration store
builder.Services.AddSingleton<INotificationConfigurationStore, FileBasedNotificationConfigurationStore>();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache(); // For IP enrichment caching

// Add connection pools
builder.Services.AddSingleton<QdrantConnectionPool>();
builder.Services.AddSingleton<IQdrantConnectionPool>(provider => provider.GetRequiredService<QdrantConnectionPool>());

// Phase 2B: Intelligent Caching Layer Services
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddSingleton<TextHashingService>(provider =>
{
    var cacheOptions = provider.GetRequiredService<IOptionsMonitor<CacheOptions>>();
    var logger = provider.GetRequiredService<ILogger<TextHashingService>>();
    return new TextHashingService(cacheOptions.CurrentValue.Embedding, logger);
});
builder.Services.AddSingleton<EmbeddingCacheService>();

// Add SQLite Database
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "castellan.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<CastellanDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add database services
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddScoped<MitreService>();
builder.Services.AddScoped<SecurityEventService>();
builder.Services.AddScoped<SystemConfigurationService>();
builder.Services.AddScoped<MitreAttackImportService>();

// Register performance monitoring service
builder.Services.Configure<PerformanceMonitorOptions>(builder.Configuration.GetSection("PerformanceMonitoring"));
builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitorService>();

// Register enhanced performance services for dashboard
builder.Services.AddScoped<PerformanceMetricsService>();
builder.Services.AddScoped<PerformanceAlertService>();

// Register IP enrichment service based on configuration
var ipEnrichmentProvider = builder.Configuration["IPEnrichment:Provider"] ?? "MaxMind";
if (ipEnrichmentProvider.Equals("MaxMind", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IIPEnrichmentService, MaxMindIPEnrichmentService>();
else
    builder.Services.AddSingleton<IIPEnrichmentService, MaxMindIPEnrichmentService>(); // Default to MaxMind

// Register system health service (singleton for system-level metrics)
builder.Services.AddSingleton<SystemHealthService>();

// Register threat scanner service
builder.Services.Configure<ThreatScanOptions>(builder.Configuration.GetSection("ThreatScanner"));
// Configure threat intelligence services
builder.Services.Configure<ThreatIntelligenceOptions>(builder.Configuration.GetSection("ThreatIntelligence"));
// Register threat intelligence cache service
builder.Services.AddSingleton<IThreatIntelligenceCacheService, ThreatIntelligenceCacheService>();

// Register VirusTotal service with HttpClient
builder.Services.AddHttpClient<IVirusTotalService, VirusTotalService>();

// Register MalwareBazaar service with HttpClient
builder.Services.AddHttpClient<IMalwareBazaarService, MalwareBazaarService>();

// Register AlienVault OTX service with HttpClient
builder.Services.AddHttpClient<IOtxService, OtxService>();

// Register threat scanner service with threat intelligence
builder.Services.AddSingleton<IThreatScanner>(provider =>
{
    var options = provider.GetRequiredService<IOptions<ThreatScanOptions>>().Value;
    var logger = provider.GetRequiredService<ILogger<ThreatScannerService>>();
    var virusTotalService = provider.GetRequiredService<IVirusTotalService>();
    var malwareBazaarService = provider.GetRequiredService<IMalwareBazaarService>();
    var otxService = provider.GetRequiredService<IOtxService>();
    var cacheService = provider.GetRequiredService<IThreatIntelligenceCacheService>();
    return new ThreatScannerService(logger, options, virusTotalService, malwareBazaarService, otxService, cacheService);
});

var embedProvider = builder.Configuration["Embeddings:Provider"] ?? "Ollama";
if (embedProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmbedder, OllamaEmbedder>();
else if (embedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmbedder, OpenAIEmbedder>();
else if (embedProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmbedder, MockEmbedder>();
else
    builder.Services.AddSingleton<IEmbedder, OllamaEmbedder>(); // Default fallback

var llmProvider = builder.Configuration["LLM:Provider"] ?? "Ollama";
if (llmProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<ILlmClient, OllamaLlm>();
else
    builder.Services.AddSingleton<ILlmClient, OpenAILlm>();

// Use QdrantPooledVectorStore with connection pooling for improved performance
builder.Services.AddSingleton<IVectorStore, QdrantPooledVectorStore>();
builder.Services.AddSingleton<SecurityEventDetector>();
builder.Services.AddSingleton<RulesEngine>(); // M4: Add RulesEngine for correlation and fusion
builder.Services.AddSingleton<ISecurityEventStore, FileBasedSecurityEventStore>(); // Persistent store for API access
builder.Services.AddSingleton<IAutomatedResponseService, AutomatedResponseService>(); // Automated threat response

// Register YARA services
builder.Services.AddSingleton<IYaraRuleStore, FileBasedYaraRuleStore>();
builder.Services.AddHostedService<Pipeline>();
builder.Services.AddHostedService<StartupOrchestratorService>(); // Automatically start all required services
builder.Services.AddHostedService<MitreImportStartupService>(); // Auto-import MITRE data if needed

// Add Web API services
builder.Services.AddControllers();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add SignalR progress broadcaster service
builder.Services.AddSingleton<IScanProgressBroadcaster, ScanProgressBroadcaster>();

// Add enhanced progress tracking service
builder.Services.AddSingleton<IEnhancedProgressTrackingService, EnhancedProgressTrackingService>();

// Add CORS for frontend and SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://localhost:3000", "http://localhost:3001", "http://localhost:3002", "http://localhost:3003", "http://localhost:3004")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Required for SignalR
              .SetIsOriginAllowed(_ => true); // Allow SignalR negotiation
    });
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authOptions = builder.Configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>();
        if (authOptions?.Jwt == null || string.IsNullOrEmpty(authOptions.Jwt.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey not configured in Authentication:Jwt:SecretKey");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOptions.Jwt.Issuer,
            ValidAudience = authOptions.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authOptions.Jwt.SecretKey))
        };
    });

#if DEBUG
// Validate service registrations in development
Console.WriteLine("Validating service registrations...");
try 
{
    using var scope = builder.Services.BuildServiceProvider().CreateScope();
    
    // Test critical services can be resolved
    var criticalServices = new[]
    {
        typeof(IPasswordHashingService),
        typeof(IJwtTokenBlacklistService),
        typeof(IRefreshTokenService),
        typeof(IVectorStore),
        typeof(IEmbedder),
        typeof(ILlmClient),
        typeof(INotificationService),
        typeof(IPerformanceMonitor),
        // Phase 2B Caching Services
        typeof(ICacheService),
        typeof(TextHashingService),
        typeof(EmbeddingCacheService)
    };
    
    foreach (var serviceType in criticalServices)
    {
        var service = scope.ServiceProvider.GetRequiredService(serviceType);
        Console.WriteLine($"✅ {serviceType.Name} resolved successfully");
    }
    
    Console.WriteLine("✅ All critical services validated successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Service resolution failed: {ex.Message}");
    throw;
}

// Validate critical configuration options
Console.WriteLine("Validating configuration options...");
try
{
    using var configScope = builder.Services.BuildServiceProvider().CreateScope();
    var authOptions = configScope.ServiceProvider.GetRequiredService<IOptionsMonitor<AuthenticationOptions>>();
    var qdrantOptions = configScope.ServiceProvider.GetRequiredService<IOptionsMonitor<QdrantOptions>>();
    var pipelineOptions = configScope.ServiceProvider.GetRequiredService<IOptionsMonitor<PipelineOptions>>();
    var cacheOptions = configScope.ServiceProvider.GetRequiredService<IOptionsMonitor<CacheOptions>>();
    
    // Accessing .Value will trigger validation
    _ = authOptions.CurrentValue;
    _ = qdrantOptions.CurrentValue;
    _ = pipelineOptions.CurrentValue;
    _ = cacheOptions.CurrentValue;
    
    Console.WriteLine("✅ All configuration options validated successfully");
}
catch (OptionsValidationException ex)
{
    Console.WriteLine($"❌ Configuration validation failed: {ex.Message}");
    throw;
}
#endif

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<CastellanDbContext>();
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
        throw;
    }
}

// Configure the HTTP request pipeline

// Add correlation ID tracking (should be first)
app.UseCorrelationId();

// Add global exception handling (should be early in pipeline)
app.UseGlobalExceptionHandling();

app.UseCors();
// Add JWT validation middleware (checks token blacklist)
app.UseMiddleware<JwtValidationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map SignalR hub
app.MapHub<ScanProgressHub>("/hubs/scan-progress");

// Configure API to listen on port 5000
app.Urls.Add("http://localhost:5000");

Console.WriteLine("Starting Castellan Worker with Web API on http://localhost:5000");

await app.RunAsync();
