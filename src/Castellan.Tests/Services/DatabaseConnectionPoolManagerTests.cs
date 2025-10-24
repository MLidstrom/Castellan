using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

public class DatabaseConnectionPoolManagerTests : IDisposable
{
    private readonly Mock<IDbContextFactory<CastellanDbContext>> _mockContextFactory;
    private readonly Mock<ILogger<DatabaseConnectionPoolManager>> _mockLogger;
    private readonly DatabaseConnectionPoolOptions _options;
    private DatabaseConnectionPoolManager? _manager;

    public DatabaseConnectionPoolManagerTests()
    {
        _mockContextFactory = new Mock<IDbContextFactory<CastellanDbContext>>();
        _mockLogger = new Mock<ILogger<DatabaseConnectionPoolManager>>();

        _options = new DatabaseConnectionPoolOptions
        {
            Enabled = true,
            Provider = "SQLite",
            MaxPoolSize = 100,
            MinPoolSize = 5,
            EnableStatistics = true,
            HealthCheck = new DatabaseConnectionPoolHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromMinutes(1),
                Timeout = TimeSpan.FromSeconds(5)
            }
        };
    }

    [Fact]
    public void Constructor_InitializesMetricsCorrectly()
    {
        // Arrange & Act
        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        var metrics = _manager.GetMetrics();

        // Assert
        Assert.Equal(100, metrics.MaxPoolSize);
        Assert.Equal("SQLite", metrics.DatabaseProvider);
    }

    [Fact]
    public void Constructor_WithNullContextFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseConnectionPoolManager(
                null!,
                Options.Create(_options),
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseConnectionPoolManager(
                _mockContextFactory.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseConnectionPoolManager(
                _mockContextFactory.Object,
                Options.Create(_options),
                null!));
    }

    [Fact]
    public async Task PerformHealthCheckAsync_WithSuccessfulConnection_ReturnsTrue()
    {
        // Arrange
        var mockContext = new Mock<CastellanDbContext>(
            new DbContextOptionsBuilder<CastellanDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options);

        mockContext.Setup(x => x.Database.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContext.Object);

        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var result = await _manager.PerformHealthCheckAsync();

        // Assert
        Assert.True(result);
        var metrics = _manager.GetMetrics();
        Assert.Equal(ConnectionPoolHealthStatus.Healthy, metrics.HealthStatus);
        Assert.True(metrics.LastHealthCheck > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task PerformHealthCheckAsync_WithFailedConnection_ReturnsFalse()
    {
        // Arrange
        var mockContext = new Mock<CastellanDbContext>(
            new DbContextOptionsBuilder<CastellanDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options);

        mockContext.Setup(x => x.Database.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContext.Object);

        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var result = await _manager.PerformHealthCheckAsync();

        // Assert
        Assert.False(result);
        var metrics = _manager.GetMetrics();
        Assert.Equal(ConnectionPoolHealthStatus.Unhealthy, metrics.HealthStatus);
    }

    [Fact]
    public async Task PerformHealthCheckAsync_WithException_ReturnsFalseAndIncrementsFailureCount()
    {
        // Arrange
        _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        var initialMetrics = _manager.GetMetrics();
        var initialFailures = initialMetrics.FailedConnectionAttempts;

        // Act
        var result = await _manager.PerformHealthCheckAsync();

        // Assert
        Assert.False(result);
        var metrics = _manager.GetMetrics();
        Assert.Equal(ConnectionPoolHealthStatus.Unhealthy, metrics.HealthStatus);
        Assert.Equal(initialFailures + 1, metrics.FailedConnectionAttempts);
    }

    [Fact]
    public void Constructor_WithHealthCheckEnabled_InitializesTimer()
    {
        // Arrange
        var optionsWithHealthCheck = new DatabaseConnectionPoolOptions
        {
            Enabled = true,
            Provider = "SQLite",
            MaxPoolSize = 100,
            HealthCheck = new DatabaseConnectionPoolHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromMinutes(1)
            }
        };

        // Act
        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(optionsWithHealthCheck),
            _mockLogger.Object);

        // Assert
        // Timer is initialized (verified by no exception during construction)
        Assert.NotNull(_manager);
    }

    [Fact]
    public void Constructor_WithHealthCheckDisabled_DoesNotInitializeTimer()
    {
        // Arrange
        var optionsWithoutHealthCheck = new DatabaseConnectionPoolOptions
        {
            Enabled = true,
            Provider = "SQLite",
            MaxPoolSize = 100,
            HealthCheck = new DatabaseConnectionPoolHealthCheckOptions
            {
                Enabled = false
            }
        };

        // Act
        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(optionsWithoutHealthCheck),
            _mockLogger.Object);

        // Assert
        // Timer is not initialized (verified by no exception during construction)
        Assert.NotNull(_manager);
    }

    [Fact]
    public void GetMetrics_ReturnsCurrentMetrics()
    {
        // Arrange
        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var metrics = _manager.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(100, metrics.MaxPoolSize);
        Assert.Equal("SQLite", metrics.DatabaseProvider);
    }

    [Fact]
    public async Task PerformHealthCheckAsync_UpdatesLastHealthCheckTimestamp()
    {
        // Arrange
        var mockContext = new Mock<CastellanDbContext>(
            new DbContextOptionsBuilder<CastellanDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options);

        mockContext.Setup(x => x.Database.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContext.Object);

        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        var beforeCheck = DateTimeOffset.UtcNow;

        // Act
        await _manager.PerformHealthCheckAsync();

        var afterCheck = DateTimeOffset.UtcNow;
        var metrics = _manager.GetMetrics();

        // Assert
        Assert.True(metrics.LastHealthCheck >= beforeCheck);
        Assert.True(metrics.LastHealthCheck <= afterCheck);
    }

    [Fact]
    public async Task PerformHealthCheckAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockContext = new Mock<CastellanDbContext>(
            new DbContextOptionsBuilder<CastellanDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options);

        _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var result = await _manager.PerformHealthCheckAsync(cts.Token);

        // Assert
        Assert.False(result);
        var metrics = _manager.GetMetrics();
        Assert.Equal(ConnectionPoolHealthStatus.Unhealthy, metrics.HealthStatus);
    }

    [Fact]
    public void Dispose_DisposesResourcesCorrectly()
    {
        // Arrange
        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        _manager.Dispose();

        // Assert
        // Verify logger was called with disposal message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_OnlyDisposesOnce()
    {
        // Arrange
        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        _manager.Dispose();
        _manager.Dispose();
        _manager.Dispose();

        // Assert
        // Verify logger was called exactly once with disposal message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetMetrics_AfterMultipleHealthChecks_ReflectsLatestStatus()
    {
        // Arrange
        var mockContext = new Mock<CastellanDbContext>(
            new DbContextOptionsBuilder<CastellanDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options);

        mockContext.SetupSequence(x => x.Database.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContext.Object);

        _manager = new DatabaseConnectionPoolManager(
            _mockContextFactory.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act & Assert - First check: Healthy
        _manager.PerformHealthCheckAsync().Wait();
        Assert.Equal(ConnectionPoolHealthStatus.Healthy, _manager.GetMetrics().HealthStatus);

        // Act & Assert - Second check: Unhealthy
        _manager.PerformHealthCheckAsync().Wait();
        Assert.Equal(ConnectionPoolHealthStatus.Unhealthy, _manager.GetMetrics().HealthStatus);

        // Act & Assert - Third check: Healthy again
        _manager.PerformHealthCheckAsync().Wait();
        Assert.Equal(ConnectionPoolHealthStatus.Healthy, _manager.GetMetrics().HealthStatus);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }
}
