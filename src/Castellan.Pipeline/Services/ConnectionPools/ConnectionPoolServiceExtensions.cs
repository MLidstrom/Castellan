using Castellan.Pipeline.Services.ConnectionPools.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Castellan.Pipeline.Services.ConnectionPools;

/// <summary>
/// Service collection extensions for connection pool services.
/// </summary>
public static class ConnectionPoolServiceExtensions
{
    /// <summary>
    /// Adds connection pool services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConnectionPools(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure connection pool options
        services.Configure<ConnectionPoolOptions>(
            configuration.GetSection("ConnectionPools"));

        // Add validation for connection pool options
        services.AddSingleton<IValidateOptions<ConnectionPoolOptions>, ConnectionPoolOptionsValidator>();

        // Register HTTP client pool
        services.AddSingleton<IHttpClientPoolManager, HttpClientPoolManager>();

        // Register Qdrant connection pool
        services.AddSingleton<IQdrantConnectionPool, QdrantConnectionPool>();

        return services;
    }

    /// <summary>
    /// Adds connection pool services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure connection pool options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConnectionPools(
        this IServiceCollection services, 
        Action<ConnectionPoolOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Add validation for connection pool options
        services.AddSingleton<IValidateOptions<ConnectionPoolOptions>, ConnectionPoolOptionsValidator>();

        // Register HTTP client pool
        services.AddSingleton<IHttpClientPoolManager, HttpClientPoolManager>();

        // Register Qdrant connection pool
        services.AddSingleton<IQdrantConnectionPool, QdrantConnectionPool>();

        return services;
    }

    /// <summary>
    /// Adds health checks for connection pools.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConnectionPoolHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<HttpClientPoolHealthCheck>("http-client-pool")
            .AddCheck<QdrantConnectionPoolHealthCheck>("qdrant-connection-pool");

        return services;
    }
}

/// <summary>
/// Validates connection pool configuration options.
/// </summary>
internal sealed class ConnectionPoolOptionsValidator : IValidateOptions<ConnectionPoolOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectionPoolOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("ConnectionPoolOptions cannot be null");
        }

        var failures = new List<string>();

        // Validate basic options
        if (options.DefaultMaxPoolSize <= 0)
        {
            failures.Add("DefaultMaxPoolSize must be greater than 0");
        }

        if (options.RequestTimeoutMs <= 0)
        {
            failures.Add("RequestTimeoutMs must be greater than 0");
        }

        if (options.MaxRetryAttempts < 0)
        {
            failures.Add("MaxRetryAttempts must be non-negative");
        }

        if (options.RetryDelayMs <= 0)
        {
            failures.Add("RetryDelayMs must be greater than 0");
        }

        // Validate circuit breaker options
        if (options.CircuitBreakerFailureThreshold <= 0)
        {
            failures.Add("CircuitBreakerFailureThreshold must be greater than 0");
        }

        if (options.CircuitBreakerTimeoutMs <= 0)
        {
            failures.Add("CircuitBreakerTimeoutMs must be greater than 0");
        }

        if (options.CircuitBreakerRetryTimeoutMs <= 0)
        {
            failures.Add("CircuitBreakerRetryTimeoutMs must be greater than 0");
        }

        // Validate HTTP client pool options
        if (options.HttpClientPools != null)
        {
            foreach (var httpPool in options.HttpClientPools)
            {
                if (string.IsNullOrWhiteSpace(httpPool.Key))
                {
                    failures.Add("HTTP client pool name cannot be null or whitespace");
                    continue;
                }

                var poolOptions = httpPool.Value;
                if (poolOptions.MaxPoolSize <= 0)
                {
                    failures.Add($"HTTP client pool '{httpPool.Key}' MaxPoolSize must be greater than 0");
                }

                if (poolOptions.MaxIdleConnections < 0)
                {
                    failures.Add($"HTTP client pool '{httpPool.Key}' MaxIdleConnections must be non-negative");
                }

                if (poolOptions.ConnectionTimeoutMs <= 0)
                {
                    failures.Add($"HTTP client pool '{httpPool.Key}' ConnectionTimeoutMs must be greater than 0");
                }
            }
        }

        // Validate Qdrant pool options
        if (options.QdrantPools != null)
        {
            foreach (var qdrantPool in options.QdrantPools)
            {
                if (string.IsNullOrWhiteSpace(qdrantPool.Key))
                {
                    failures.Add("Qdrant pool name cannot be null or whitespace");
                    continue;
                }

                var poolOptions = qdrantPool.Value;
                if (string.IsNullOrWhiteSpace(poolOptions.Host))
                {
                    failures.Add($"Qdrant pool '{qdrantPool.Key}' Host cannot be null or whitespace");
                }

                if (poolOptions.Port <= 0 || poolOptions.Port > 65535)
                {
                    failures.Add($"Qdrant pool '{qdrantPool.Key}' Port must be between 1 and 65535");
                }

                if (poolOptions.MaxPoolSize <= 0)
                {
                    failures.Add($"Qdrant pool '{qdrantPool.Key}' MaxPoolSize must be greater than 0");
                }

                if (poolOptions.MaxIdleConnections < 0)
                {
                    failures.Add($"Qdrant pool '{qdrantPool.Key}' MaxIdleConnections must be non-negative");
                }

                if (poolOptions.ConnectionTimeoutMs <= 0)
                {
                    failures.Add($"Qdrant pool '{qdrantPool.Key}' ConnectionTimeoutMs must be greater than 0");
                }
            }
        }

        // Validate health check options
        if (options.HealthCheck.HealthCheckIntervalMs <= 0)
        {
            failures.Add("HealthCheck.HealthCheckIntervalMs must be greater than 0");
        }

        if (options.HealthCheck.HealthCheckTimeoutMs <= 0)
        {
            failures.Add("HealthCheck.HealthCheckTimeoutMs must be greater than 0");
        }

        // Validate load balancing options
        if (!Enum.IsDefined(typeof(LoadBalancingStrategy), options.LoadBalancing.Strategy))
        {
            failures.Add($"LoadBalancing.Strategy '{options.LoadBalancing.Strategy}' is not valid");
        }

        if (options.LoadBalancing.WeightAdjustmentFactor < 0.1 || options.LoadBalancing.WeightAdjustmentFactor > 2.0)
        {
            failures.Add("LoadBalancing.WeightAdjustmentFactor must be between 0.1 and 2.0");
        }

        if (options.LoadBalancing.StickySessionTimeoutMs <= 0)
        {
            failures.Add("LoadBalancing.StickySessionTimeoutMs must be greater than 0");
        }

        // Validate metrics options
        if (options.Metrics.MetricsRetentionMinutes <= 0)
        {
            failures.Add("Metrics.MetricsRetentionMinutes must be greater than 0");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
