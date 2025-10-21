using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker; // for Pipeline
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Configuration.Validation;
using Castellan.Worker.Data;
using Castellan.Worker.Extensions;
using Castellan.Worker.Middleware;
using Castellan.Worker.Hubs;
using Castellan.Worker.Models;
using Castellan.Worker.Options;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.VectorStores;
using Castellan.Worker.Infrastructure;
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

// Configure connection pools
builder.Services.Configure<ConnectionPoolOptions>(builder.Configuration.GetSection("ConnectionPools"));

// Register configuration validators
builder.Services.AddSingleton<IValidateOptions<AuthenticationOptions>, AuthenticationOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<QdrantOptions>, QdrantOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<PipelineOptions>, PipelineOptionsValidator>();

// Add feature-specific services using extension methods
builder.Services.AddHttpClient(); // Required by multiple features
builder.Services.AddCastellanDatabase(builder.Configuration, builder.Environment);
builder.Services.AddCastellanAuthentication(builder.Configuration);
builder.Services.AddCastellanOpenTelemetry(builder.Configuration); // Phase 2 Week 4: Distributed tracing
builder.Services.AddCastellanAI(builder.Configuration);
builder.Services.AddCastellanSecurity(builder.Configuration);
builder.Services.AddCastellanThreatIntelligence(builder.Configuration);
builder.Services.AddCastellanNotifications(builder.Configuration);
builder.Services.AddCastellanMonitoring(builder.Configuration);
builder.Services.AddCastellanPipeline(builder.Configuration);
builder.Services.AddCastellanSignalR();
builder.Services.AddCastellanChat(builder.Configuration); // Phase 3 Week 8: Conversational AI
builder.Services.AddCastellanActions(builder.Configuration); // Phase 3 Week 9+: Action execution with undo/rollback

// Add Web API services with JSON configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

// Add CORS for frontend and SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow all origins for development
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Required for SignalR
              .WithExposedHeaders("*"); // Expose all headers
    });
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
        typeof(IExportService)
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

    // Accessing .Value will trigger validation
    _ = authOptions.CurrentValue;
    _ = qdrantOptions.CurrentValue;
    _ = pipelineOptions.CurrentValue;

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

        // Ensure new search tables exist (for v0.5.0)
        var schemaUpdateService = scope.ServiceProvider.GetRequiredService<DatabaseSchemaUpdateService>();
        await schemaUpdateService.EnsureTablesExistAsync();
        Console.WriteLine("Search tables schema validated");

        // Apply database performance enhancements (v0.7.0)
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var perfLogger = loggerFactory.CreateLogger("DatabasePerformance");

        try
        {
            await DatabasePerformanceEnhancements.ApplyPerformanceEnhancementsAsync(context, perfLogger);

            // Additional runtime optimizations
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_autocheckpoint=1000;");

            perfLogger.LogInformation("Database performance enhancements applied successfully");
            Console.WriteLine("Database performance enhancements applied");
        }
        catch (Exception perfEx)
        {
            perfLogger.LogError(perfEx, "Failed to apply database performance enhancements");
            Console.WriteLine($"Warning: Could not apply database performance enhancements: {perfEx.Message}");
            // Don't fail startup, continue with default settings
        }
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

// Map SignalR hub - allow anonymous negotiation
app.MapHub<ScanProgressHub>("/hubs/scan-progress");

// Configure API to listen on port 5000
app.Urls.Add("http://localhost:5000");

Console.WriteLine("Starting Castellan Worker with Web API on http://localhost:5000");

await app.RunAsync();
