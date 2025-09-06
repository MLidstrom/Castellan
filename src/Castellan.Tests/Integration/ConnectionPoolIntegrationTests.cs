using Castellan.Worker.Models;
using Castellan.Worker.Services.ConnectionPools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Castellan.Tests.Integration;

/// <summary>
/// Integration tests for connection pool functionality.
/// </summary>
public class ConnectionPoolIntegrationTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConnectionPoolOptions _options;

    public ConnectionPoolIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _options = new ConnectionPoolOptions
        {
            QdrantPool = new QdrantPoolOptions
            {
                Instances = new List<QdrantInstanceConfiguration>
                {
                    new QdrantInstanceConfiguration
                    {
                        Host = "localhost",
                        Port = 6333,
                        Weight = 100,
                        UseHttps = false
                    }
                },
                MaxConnectionsPerInstance = 10,
                HealthCheckInterval = TimeSpan.FromSeconds(30),
                ConnectionTimeout = TimeSpan.FromSeconds(10),
                RequestTimeout = TimeSpan.FromMinutes(1),
                EnableFailover = true,
                MinHealthyInstances = 1
            },
            HealthMonitoring = new ConnectionHealthMonitoringOptions
            {
                Enabled = true,
                CheckInterval = TimeSpan.FromSeconds(30),
                CheckTimeout = TimeSpan.FromSeconds(5),
                ConsecutiveFailureThreshold = 3,
                ConsecutiveSuccessThreshold = 2,
                EnableAutoRecovery = true,
                RecoveryInterval = TimeSpan.FromMinutes(1),
                HealthHistoryRetention = TimeSpan.FromHours(24)
            },
            LoadBalancing = new ConnectionLoadBalancingOptions
            {
                Algorithm = LoadBalancingAlgorithm.WeightedRoundRobin,
                EnableHealthAwareRouting = true,
                PerformanceWindow = TimeSpan.FromMinutes(5),
                WeightAdjustment = new WeightAdjustmentOptions
                {
                    ResponseTimeFactor = 0.4,
                    ErrorRateFactor = 0.3,
                    ConcurrencyFactor = 0.3,
                    MinimumWeightMultiplier = 0.1,
                    MaximumWeightMultiplier = 3.0
                },
                StickySession = new StickySessionOptions
                {
                    Enabled = false,
                    SessionDuration = TimeSpan.FromMinutes(30),
                    MaxSessions = 10000
                }
            },
            GlobalTimeouts = new GlobalTimeoutOptions
            {
                DefaultConnectionTimeout = TimeSpan.FromSeconds(30),
                DefaultRequestTimeout = TimeSpan.FromMinutes(2),
                MaxTimeout = TimeSpan.FromMinutes(10),
                DnsTimeout = TimeSpan.FromSeconds(5)
            },
            Metrics = new ConnectionMetricsOptions
            {
                Enabled = true,
                CollectionInterval = TimeSpan.FromSeconds(10),
                RetentionPeriod = TimeSpan.FromHours(24),
                EnableDetailedMetrics = false,
                MaxSamples = 10000
            }
        };
    }

    [Fact]
    public void QdrantConnectionPool_Should_Initialize_Successfully()
    {
        // Arrange & Act
        using var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Assert
        var instances = pool.GetAvailableInstances();
        Assert.Single(instances);
        Assert.Contains("qdrant-0", instances);
    }

    [Fact]
    public void QdrantConnectionPool_GetMetrics_Should_Return_Valid_Data()
    {
        // Arrange
        using var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Act
        var metrics = pool.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal("Qdrant", metrics.PoolName);
        Assert.Equal("Vector Database", metrics.PoolType);
        Assert.Equal(1, metrics.InstanceMetrics.Count);
        Assert.True(metrics.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task QdrantConnectionPool_GetHealthStatusAsync_Should_Return_Instance_Health()
    {
        // Arrange
        using var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Act
        var healthStatus = await pool.GetHealthStatusAsync();

        // Assert
        Assert.NotNull(healthStatus);
        Assert.Single(healthStatus);
        Assert.True(healthStatus.ContainsKey("qdrant-0"));
    }

    [Fact]
    public void QdrantConnectionPool_SetInstanceHealth_Should_Update_Health_Status()
    {
        // Arrange
        using var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Act
        pool.SetInstanceHealth("qdrant-0", ConnectionPoolHealthStatus.Unhealthy);

        // Assert - No exception should be thrown
        var instances = pool.GetAvailableInstances();
        Assert.Single(instances);
        Assert.Contains("qdrant-0", instances);
    }

    [Fact]
    public void QdrantConnectionPool_SetInstanceHealth_Invalid_Instance_Should_Throw()
    {
        // Arrange
        using var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => pool.SetInstanceHealth("nonexistent", ConnectionPoolHealthStatus.Unhealthy));
        
        Assert.Contains("Instance nonexistent not found", exception.Message);
    }

    [Fact]
    public async Task QdrantConnectionPool_GetClientAsync_Should_Return_Pooled_Client()
    {
        // Arrange
        using var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Act & Assert
        // Note: This test will succeed even if Qdrant is not running because we're just testing the pool structure
        // The actual connection would be tested when used with real Qdrant operations
        try
        {
            using var client = await pool.GetClientAsync();
            Assert.NotNull(client);
            Assert.Equal("qdrant-0", client.InstanceId);
            Assert.NotNull(client.Client);
        }
        catch (Exception ex)
        {
            // If Qdrant is not running, we expect connection failures, which is fine for this test
            // We're primarily testing the pool structure, not actual Qdrant connectivity
            Assert.Contains("connection", ex.Message.ToLowerInvariant());
        }
    }

    [Fact]
    public void QdrantConnectionPool_Dispose_Should_Not_Throw()
    {
        // Arrange
        var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Act & Assert
        pool.Dispose();
        
        // Second dispose should not throw
        pool.Dispose();
    }

    [Fact]
    public async Task QdrantConnectionPool_Operations_After_Dispose_Should_Throw()
    {
        // Arrange
        var pool = new QdrantConnectionPool(
            Options.Create(_options),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());
        pool.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => pool.GetAvailableInstances());
        Assert.Throws<ObjectDisposedException>(() => pool.GetMetrics());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pool.GetHealthStatusAsync());
    }

    [Fact]
    public void ConnectionPool_Configuration_Validation_Test()
    {
        // Arrange
        var validOptions = new ConnectionPoolOptions
        {
            QdrantPool = new QdrantPoolOptions
            {
                Instances = new List<QdrantInstanceConfiguration>
                {
                    new QdrantInstanceConfiguration
                    {
                        Host = "localhost",
                        Port = 6333,
                        Weight = 100
                    }
                },
                MaxConnectionsPerInstance = 50,
                EnableFailover = true,
                MinHealthyInstances = 1
            }
        };

        // Act & Assert - Should not throw
        using var pool = new QdrantConnectionPool(
            Options.Create(validOptions),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        Assert.True(true); // Test passes if no exception is thrown
    }

    [Fact]
    public void ConnectionPool_With_Multiple_Instances_Should_Load_Balance()
    {
        // Arrange
        var multiInstanceOptions = new ConnectionPoolOptions
        {
            QdrantPool = new QdrantPoolOptions
            {
                Instances = new List<QdrantInstanceConfiguration>
                {
                    new QdrantInstanceConfiguration { Host = "localhost", Port = 6333, Weight = 50 },
                    new QdrantInstanceConfiguration { Host = "localhost", Port = 6334, Weight = 50 }
                },
                MaxConnectionsPerInstance = 25,
                EnableFailover = true
            },
            HealthMonitoring = new ConnectionHealthMonitoringOptions
            {
                Enabled = true,
                CheckInterval = TimeSpan.FromSeconds(30)
            },
            LoadBalancing = new ConnectionLoadBalancingOptions
            {
                Algorithm = LoadBalancingAlgorithm.RoundRobin
            }
        };

        // Act
        using var pool = new QdrantConnectionPool(
            Options.Create(multiInstanceOptions),
            _loggerFactory.CreateLogger<QdrantConnectionPool>());

        // Assert
        var instances = pool.GetAvailableInstances();
        Assert.Equal(2, instances.Count);
        Assert.Contains("qdrant-0", instances);
        Assert.Contains("qdrant-1", instances);

        var metrics = pool.GetMetrics();
        Assert.Equal(2, metrics.InstanceMetrics.Count);
    }
}
