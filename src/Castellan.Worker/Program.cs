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
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;
using Castellan.Worker.Data;
using Serilog;

// Enable HTTP/2 over cleartext (h2c) for local gRPC (Qdrant 6334)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Log edition information at startup
Console.WriteLine($"Starting {EditionFeatures.GetVersionString()}");

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("run.log", 
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 2 * 1024 * 1024, // 2MB
        retainedFileCountLimit: 5,
        rollOnFileSizeLimit: true,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.Configure<EvtxOptions>(builder.Configuration.GetSection("Ingest:Evtx"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embeddings"));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("LLM"));
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection("Alerts"));
builder.Services.Configure<CorrelationOptions>(builder.Configuration.GetSection("Correlation"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<IPEnrichmentOptions>(builder.Configuration.GetSection("IPEnrichment"));
builder.Services.Configure<AutomatedResponseOptions>(builder.Configuration.GetSection("AutomatedResponse"));
builder.Services.Configure<ThreatScanOptions>(builder.Configuration.GetSection("ThreatScan"));
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection(AuthenticationOptions.SectionName));

builder.Services.AddSingleton<ILogCollector, EvtxCollector>();


builder.Services.AddSingleton<INotificationService, NotificationService>();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache(); // For IP enrichment caching

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

// Register IP enrichment service based on configuration
var ipEnrichmentProvider = builder.Configuration["IPEnrichment:Provider"] ?? "MaxMind";
if (ipEnrichmentProvider.Equals("MaxMind", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IIPEnrichmentService, MaxMindIPEnrichmentService>();
else
    builder.Services.AddSingleton<IIPEnrichmentService, MaxMindIPEnrichmentService>(); // Default to MaxMind

// Register system health service
builder.Services.AddScoped<SystemHealthService>();

// Register threat scanner service
builder.Services.Configure<ThreatScanOptions>(builder.Configuration.GetSection("ThreatScanner"));
builder.Services.AddSingleton<IThreatScanner>(provider =>
{
    var options = provider.GetRequiredService<IOptions<ThreatScanOptions>>().Value;
    var logger = provider.GetRequiredService<ILogger<ThreatScannerService>>();
    return new ThreatScannerService(logger, options);
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

// Use QdrantVectorStore now that Qdrant is running
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<SecurityEventDetector>();
builder.Services.AddSingleton<RulesEngine>(); // M4: Add RulesEngine for correlation and fusion
builder.Services.AddSingleton<ISecurityEventStore, FileBasedSecurityEventStore>(); // Persistent store for API access
builder.Services.AddSingleton<IAutomatedResponseService, AutomatedResponseService>(); // Automated threat response
builder.Services.AddHostedService<Pipeline>();
builder.Services.AddHostedService<StartupOrchestratorService>(); // Automatically start all required services
builder.Services.AddHostedService<MitreImportStartupService>(); // Auto-import MITRE data if needed

// Add Web API services
builder.Services.AddControllers();

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://localhost:3000", "http://localhost:3001", "http://localhost:3002", "http://localhost:3003", "http://localhost:3004")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Configure API to listen on port 5000
app.Urls.Add("http://localhost:5000");

Console.WriteLine("Starting Castellan Worker with Web API on http://localhost:5000");

await app.RunAsync();
