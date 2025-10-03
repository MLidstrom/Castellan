using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Data;
using Castellan.Worker.Infrastructure;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Services.ConnectionPools;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for database services
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Adds database services including connection pooling and repositories
    /// </summary>
    public static IServiceCollection AddCastellanDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Get database path from configuration
        var dbPathFromConfig = configuration["Database:Path"] ?? "data/castellan.db";

        // Resolve relative paths from content root (not hard-coded path hops)
        var dbPath = Path.IsPathRooted(dbPathFromConfig)
            ? dbPathFromConfig
            : Path.Combine(environment.ContentRootPath, dbPathFromConfig);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        // Configure database connection pooling
        services.Configure<DatabaseConnectionPoolOptions>(
            configuration.GetSection("ConnectionPools:Database"));

        var dbPoolOptions = configuration
            .GetSection("ConnectionPools:Database")
            .Get<DatabaseConnectionPoolOptions>() ?? new DatabaseConnectionPoolOptions();

        // Register pooled DbContext factory
        services.AddPooledDbContextFactory<CastellanDbContext>((serviceProvider, options) =>
        {
            var sqliteOptimizations = dbPoolOptions.SQLiteOptimizations ?? new SQLiteOptimizationOptions();

            // SQLite connection string (pooling is handled by EF Core's PooledDbContextFactory)
            var connectionString = $"Data Source={dbPath};" +
                                 $"Cache=Shared;" +
                                 $"Mode=ReadWriteCreate;";

            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.CommandTimeout(300); // Increased to 5 minutes for large queries
                sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            // Common EF Core optimizations
            options.EnableThreadSafetyChecks(false); // Disable for performance (contexts are short-lived)

            // Enable sensitive data logging in development only
            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        }, poolSize: dbPoolOptions.MaxPoolSize);

        // Register connection pool manager
        services.AddSingleton<DatabaseConnectionPoolManager>();

        // Keep scoped DbContext for controllers (uses pooled factory internally)
        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<CastellanDbContext>>();
            return factory.CreateDbContext();
        });

        // Store database path in configuration for other services to use
        services.AddSingleton(new DatabaseConfiguration { Path = dbPath });

        // Add database services
        services.AddScoped<ApplicationService>();
        services.AddScoped<MitreService>();
        services.AddScoped<SecurityEventService>();
        services.AddScoped<SystemConfigurationService>();
        services.AddScoped<MitreAttackImportService>();
        services.AddScoped<DatabaseSchemaUpdateService>();
        services.AddScoped<IEventBookmarkStore, DatabaseEventBookmarkStore>();

        return services;
    }
}
