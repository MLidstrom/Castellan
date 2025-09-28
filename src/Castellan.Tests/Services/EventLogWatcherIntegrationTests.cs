using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Options;
using Castellan.Worker.Hubs;
using System.Collections.Concurrent;
using System.Diagnostics.Eventing.Reader;

namespace Castellan.Tests.Services;

/// <summary>
/// Integration tests for EventLogWatcher with ISecurityEventStore
/// </summary>
public class EventLogWatcherIntegrationTests : IClassFixture<TestEnvironmentFixture>
{
    private readonly TestEnvironmentFixture _fixture;

    public EventLogWatcherIntegrationTests(TestEnvironmentFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EventLogWatcher_IntegratesWithSecurityEventStore()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add configuration
        var options = new WindowsEventLogOptions
        {
            Enabled = true,
            Channels = new List<WindowsEventChannelOptions>
            {
                new()
                {
                    Name = "Security",
                    Enabled = true,
                    XPathFilter = "*[System[(EventID=4624)]]",
                    BookmarkPersistence = "Database",
                    MaxQueue = 100
                }
            },
            DefaultMaxQueue = 100,
            ConsumerConcurrency = 1,
            ImmediateDashboardBroadcast = false
        };
        
        services.AddSingleton(Options.Create(options));
        
        // Add mock services
        services.AddSingleton<MockSecurityEventStore>();
        services.AddSingleton<ISecurityEventStore>(provider => provider.GetRequiredService<MockSecurityEventStore>());
        services.AddSingleton<MockEventBookmarkStore>();
        services.AddSingleton<IEventBookmarkStore>(provider => provider.GetRequiredService<MockEventBookmarkStore>());
        services.AddSingleton<MockBroadcaster>();
        services.AddSingleton<IScanProgressBroadcaster>(provider => provider.GetRequiredService<MockBroadcaster>());
        services.AddSingleton<MockDashboardBroadcast>();
        services.AddSingleton<DashboardDataBroadcastService>(provider => provider.GetRequiredService<MockDashboardBroadcast>());
        
        // Add the service
        services.AddHostedService<WindowsEventLogWatcherService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var securityEventStore = serviceProvider.GetRequiredService<MockSecurityEventStore>();
        var bookmarkStore = serviceProvider.GetRequiredService<MockEventBookmarkStore>();
        
        // Act & Assert
        var host = serviceProvider.GetRequiredService<IHostedService>();
        try
        {
            // Start the service
            await host.StartAsync(CancellationToken.None);
            
            // Wait a bit for initialization
            await Task.Delay(100);
            
            // Verify that the service started and registered with bookmark store
            Assert.True(bookmarkStore.LoadCallCount > 0, "Bookmark store should have been called during startup");
            
            // Stop the service
            await host.StopAsync(CancellationToken.None);
        }
        finally
        {
            if (host is IDisposable disposable)
                disposable.Dispose();
        }
    }

    [Fact]
    public void EventNormalizationHandler_CreatesValidSecurityEvent()
    {
        // Arrange
        var rawEvent = new RawEvent
        {
            Id = "12345",
            EventId = 4624,
            ProviderName = "Microsoft-Windows-Security-Auditing",
            ChannelName = "Security",
            Level = 0,
            TimeCreated = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
            UserName = "TEST\\User",
            Opcode = 0,
            Task = 12544,
            Keywords = unchecked((long)9223372036854775808),
            Message = "An account was successfully logged on.",
            Xml = "<Event><System><EventID>4624</EventID></System></Event>"
        };

        // Act
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent, new MockLogger());

        // Assert
        Assert.NotNull(securityEvent);
        Assert.Equal("12345", securityEvent.Id);
        Assert.Equal(SecurityEventType.AuthenticationSuccess, securityEvent.EventType);
        Assert.Equal("medium", securityEvent.RiskLevel);
        Assert.True(securityEvent.Confidence > 0);
        Assert.NotEmpty(securityEvent.Summary);
        Assert.NotEmpty(securityEvent.MitreTechniques);
        Assert.NotEmpty(securityEvent.RecommendedActions);
    }

    [Fact]
    public void EventNormalizationHandler_HandlesSysmonEvents()
    {
        // Arrange
        var rawEvent = new RawEvent
        {
            Id = "67890",
            EventId = 1,
            ProviderName = "Microsoft-Windows-Sysmon",
            ChannelName = "Microsoft-Windows-Sysmon/Operational",
            Level = 0,
            TimeCreated = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
            UserName = "TEST\\User",
            Opcode = 0,
            Task = 1,
            Keywords = 0,
            Message = "Process Create:",
            Xml = "<Event><System><EventID>1</EventID></System><EventData><Data Name='Image'>C:\\Windows\\System32\\cmd.exe</Data></EventData></Event>"
        };

        // Act
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent, new MockLogger());

        // Assert
        Assert.NotNull(securityEvent);
        Assert.Equal("67890", securityEvent.Id);
        Assert.Equal(SecurityEventType.ProcessCreation, securityEvent.EventType);
        Assert.Equal("high", securityEvent.RiskLevel);
        Assert.NotEmpty(securityEvent.Summary);
    }

    [Fact]
    public void EventNormalizationHandler_HandlesPowerShellEvents()
    {
        // Arrange
        var rawEvent = new RawEvent
        {
            Id = "11111",
            EventId = 4104,
            ProviderName = "Microsoft-Windows-PowerShell",
            ChannelName = "Microsoft-Windows-PowerShell/Operational",
            Level = 0,
            TimeCreated = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
            UserName = "TEST\\User",
            Opcode = 0,
            Task = 0,
            Keywords = 0,
            Message = "Creating Scriptblock text",
            Xml = "<Event><System><EventID>4104</EventID></System></Event>"
        };

        // Act
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent, new MockLogger());

        // Assert
        Assert.NotNull(securityEvent);
        Assert.Equal("11111", securityEvent.Id);
        Assert.Equal(SecurityEventType.PowerShellExecution, securityEvent.EventType);
        Assert.Equal("high", securityEvent.RiskLevel);
        Assert.NotEmpty(securityEvent.Summary);
    }
}

/// <summary>
/// Mock implementation of ISecurityEventStore for testing
/// </summary>
public class MockSecurityEventStore : ISecurityEventStore
{
    public ConcurrentBag<SecurityEvent> AddedEvents { get; } = new();
    public int AddCallCount { get; private set; }

    public void AddSecurityEvent(SecurityEvent securityEvent)
    {
        AddCallCount++;
        AddedEvents.Add(securityEvent);
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page = 1, int pageSize = 10)
    {
        return AddedEvents.Skip((page - 1) * pageSize).Take(pageSize);
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page, int pageSize, Dictionary<string, object> filters)
    {
        return GetSecurityEvents(page, pageSize);
    }

    public SecurityEvent? GetSecurityEvent(string id)
    {
        return AddedEvents.FirstOrDefault(e => e.Id == id);
    }

    public int GetTotalCount()
    {
        return AddedEvents.Count;
    }

    public int GetTotalCount(Dictionary<string, object> filters)
    {
        return GetTotalCount();
    }

    public void Clear()
    {
        while (AddedEvents.TryTake(out _)) { }
        AddCallCount = 0;
    }
}

/// <summary>
/// Mock implementation of IEventBookmarkStore for testing
/// </summary>
public class MockEventBookmarkStore : IEventBookmarkStore
{
    public int LoadCallCount { get; private set; }
    public int SaveCallCount { get; private set; }
    private readonly Dictionary<string, EventBookmark?> _bookmarks = new();

    public Task<EventBookmark?> LoadAsync(string channelName)
    {
        LoadCallCount++;
        return Task.FromResult(_bookmarks.TryGetValue(channelName, out var bookmark) ? bookmark : null);
    }

    public Task SaveAsync(string channelName, EventBookmark bookmark)
    {
        SaveCallCount++;
        _bookmarks[channelName] = bookmark;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string channelName)
    {
        _bookmarks.Remove(channelName);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string channelName)
    {
        return Task.FromResult(_bookmarks.ContainsKey(channelName));
    }

    public Task<DateTime?> GetLastUpdatedAsync(string channelName)
    {
        return Task.FromResult(_bookmarks.ContainsKey(channelName) ? DateTime.UtcNow : (DateTime?)null);
    }
}

/// <summary>
/// Mock implementation of IScanProgressBroadcaster for testing
/// </summary>
public class MockBroadcaster : IScanProgressBroadcaster
{
    public List<object> BroadcastedEvents { get; } = new();

    public Task BroadcastScanProgress(ScanProgressUpdate update)
    {
        BroadcastedEvents.Add(update);
        return Task.CompletedTask;
    }

    public Task BroadcastScanComplete(string scanId, object result)
    {
        BroadcastedEvents.Add(new { scanId, result });
        return Task.CompletedTask;
    }

    public Task BroadcastScanError(string scanId, string error)
    {
        BroadcastedEvents.Add(new { scanId, error });
        return Task.CompletedTask;
    }

    public Task BroadcastSystemMetrics(object metrics)
    {
        BroadcastedEvents.Add(metrics);
        return Task.CompletedTask;
    }

    public Task BroadcastThreatIntelligenceStatus(object status)
    {
        BroadcastedEvents.Add(status);
        return Task.CompletedTask;
    }

    public Task BroadcastSecurityEvent(object securityEvent)
    {
        BroadcastedEvents.Add(securityEvent);
        return Task.CompletedTask;
    }

    public Task BroadcastCorrelationAlert(object correlationAlert)
    {
        BroadcastedEvents.Add(correlationAlert);
        return Task.CompletedTask;
    }

    public Task BroadcastYaraMatch(object yaraMatch)
    {
        BroadcastedEvents.Add(yaraMatch);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock implementation of DashboardDataBroadcastService for testing
/// </summary>
public class MockDashboardBroadcast : DashboardDataBroadcastService
{
    public int TriggerCallCount { get; private set; }

    public MockDashboardBroadcast() : base(null!, null!, null!)
    {
    }

    public new async Task TriggerImmediateBroadcastWithCacheInvalidation()
    {
        TriggerCallCount++;
        await Task.CompletedTask;
    }
}

/// <summary>
/// Mock logger for testing
/// </summary>
public class MockLogger : ILogger<WindowsEventLogWatcherService>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
