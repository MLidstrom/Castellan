using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
using Castellan.Worker.Options;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Data;
using Castellan.Worker.Hubs;

namespace Castellan.Tests.Services;

/// <summary>
/// Integration tests for Windows Event Log Watcher Service
/// </summary>
public class WindowsEventLogWatcherServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly CastellanDbContext _context;
    private readonly ILogger<WindowsEventLogWatcherService> _logger;

    public WindowsEventLogWatcherServiceTests()
    {
        // Set up in-memory database
        var services = new ServiceCollection();
        services.AddDbContext<CastellanDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        services.AddLogging(builder => builder.AddConsole());
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<CastellanDbContext>();
        _logger = _serviceProvider.GetRequiredService<ILogger<WindowsEventLogWatcherService>>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void EventNormalizationHandler_ShouldNormalizeRawEventToSecurityEvent()
    {
        // Arrange
        var rawEvent = new RawEvent
        {
            Id = "12345",
            EventId = 4624,
            ProviderName = "Microsoft-Windows-Security-Auditing",
            ChannelName = "Security",
            Level = 4,
            TimeCreated = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
            UserName = "TEST-USER",
            Opcode = 0,
            Task = 12544,
            Keywords = 0,
            Message = "An account was successfully logged on",
            Xml = "<Event><System><EventID>4624</EventID></System></Event>"
        };

        // Act
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent);

        // Assert
        Assert.NotNull(securityEvent);
        Assert.Equal("12345", securityEvent.Id);
        Assert.Equal(SecurityEventType.LoginSuccess, securityEvent.EventType);
        Assert.Equal("critical", securityEvent.RiskLevel);
        Assert.True(securityEvent.Confidence >= 70);
        Assert.Contains("Successful login detected", securityEvent.Summary);
        Assert.Contains("T1078", securityEvent.MitreTechniques); // Valid Accounts
        Assert.NotNull(securityEvent.RecommendedActions);
        Assert.NotEmpty(securityEvent.RecommendedActions);
    }

    [Fact]
    public void EventNormalizationHandler_ShouldHandleSysmonEvents()
    {
        // Arrange
        var rawEvent = new RawEvent
        {
            Id = "67890",
            EventId = 1,
            ProviderName = "Microsoft-Windows-Sysmon",
            ChannelName = "Microsoft-Windows-Sysmon/Operational",
            Level = 4,
            TimeCreated = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
            UserName = "SYSTEM",
            Opcode = 0,
            Task = 0,
            Keywords = 0,
            Message = "Process creation",
            Xml = "<Event><System><EventID>1</EventID></System></Event>"
        };

        // Act
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent);

        // Assert
        Assert.NotNull(securityEvent);
        Assert.Equal(SecurityEventType.ProcessCreation, securityEvent.EventType);
        Assert.Equal("high", securityEvent.RiskLevel);
        Assert.Contains("T1055", securityEvent.MitreTechniques); // Process Injection
        Assert.Contains("T1059", securityEvent.MitreTechniques); // Command and Scripting Interpreter
    }

    [Fact]
    public void EventNormalizationHandler_ShouldHandlePowerShellEvents()
    {
        // Arrange
        var rawEvent = new RawEvent
        {
            Id = "11111",
            EventId = 4104,
            ProviderName = "Microsoft-Windows-PowerShell",
            ChannelName = "Microsoft-Windows-PowerShell/Operational",
            Level = 4,
            TimeCreated = DateTime.UtcNow,
            MachineName = "TEST-MACHINE",
            UserName = "TEST-USER",
            Opcode = 0,
            Task = 0,
            Keywords = 0,
            Message = "PowerShell script block logged",
            Xml = "<Event><System><EventID>4104</EventID></System></Event>"
        };

        // Act
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent);

        // Assert
        Assert.NotNull(securityEvent);
        Assert.Equal(SecurityEventType.PowerShellScriptBlock, securityEvent.EventType);
        Assert.Equal("high", securityEvent.RiskLevel);
        Assert.Contains("T1059.001", securityEvent.MitreTechniques); // PowerShell
    }

    [Fact]
    public async Task DatabaseEventBookmarkStore_ShouldSaveAndLoadBookmarks()
    {
        // Arrange
        var bookmarkStore = new DatabaseEventBookmarkStore(_context, _logger.CreateLogger<DatabaseEventBookmarkStore>());
        var channelName = "TestChannel";
        
        // Create a mock bookmark (this would normally come from EventRecord.Bookmark)
        var bookmarkXml = "<Bookmark>test-bookmark-data</Bookmark>";
        var bookmark = EventBookmark.FromXml(bookmarkXml);

        // Act - Save bookmark
        await bookmarkStore.SaveAsync(channelName, bookmark);

        // Act - Load bookmark
        var loadedBookmark = await bookmarkStore.LoadAsync(channelName);

        // Assert
        Assert.NotNull(loadedBookmark);
        Assert.Equal(bookmarkXml, loadedBookmark.ToXml());
        Assert.True(await bookmarkStore.ExistsAsync(channelName));
        
        var lastUpdated = await bookmarkStore.GetLastUpdatedAsync(channelName);
        Assert.NotNull(lastUpdated);
        Assert.True(lastUpdated.Value > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task DatabaseEventBookmarkStore_ShouldHandleNonExistentBookmarks()
    {
        // Arrange
        var bookmarkStore = new DatabaseEventBookmarkStore(_context, _logger.CreateLogger<DatabaseEventBookmarkStore>());
        var channelName = "NonExistentChannel";

        // Act
        var loadedBookmark = await bookmarkStore.LoadAsync(channelName);
        var exists = await bookmarkStore.ExistsAsync(channelName);
        var lastUpdated = await bookmarkStore.GetLastUpdatedAsync(channelName);

        // Assert
        Assert.Null(loadedBookmark);
        Assert.False(exists);
        Assert.Null(lastUpdated);
    }

    [Fact]
    public async Task DatabaseEventBookmarkStore_ShouldDeleteBookmarks()
    {
        // Arrange
        var bookmarkStore = new DatabaseEventBookmarkStore(_context, _logger.CreateLogger<DatabaseEventBookmarkStore>());
        var channelName = "TestChannel";
        var bookmarkXml = "<Bookmark>test-bookmark-data</Bookmark>";
        var bookmark = EventBookmark.FromXml(bookmarkXml);

        await bookmarkStore.SaveAsync(channelName, bookmark);
        Assert.True(await bookmarkStore.ExistsAsync(channelName));

        // Act
        await bookmarkStore.DeleteAsync(channelName);

        // Assert
        Assert.False(await bookmarkStore.ExistsAsync(channelName));
        Assert.Null(await bookmarkStore.LoadAsync(channelName));
    }

    [Fact]
    public void WindowsEventLogOptions_ShouldBindConfigurationCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["WindowsEventLog:Enabled"] = true,
            ["WindowsEventLog:DefaultMaxQueue"] = 3000,
            ["WindowsEventLog:ConsumerConcurrency"] = 2,
            ["WindowsEventLog:ImmediateDashboardBroadcast"] = false,
            ["WindowsEventLog:Channels:0:Name"] = "Security",
            ["WindowsEventLog:Channels:0:Enabled"] = true,
            ["WindowsEventLog:Channels:0:XPathFilter"] = "*[System[(EventID=4624)]]",
            ["WindowsEventLog:Channels:0:BookmarkPersistence"] = "Database",
            ["WindowsEventLog:Channels:0:MaxQueue"] = 1000
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        var options = new OptionsWrapper<WindowsEventLogOptions>(
            configuration.GetSection("WindowsEventLog").Get<WindowsEventLogOptions>()!);

        // Assert
        Assert.True(options.Value.Enabled);
        Assert.Equal(3000, options.Value.DefaultMaxQueue);
        Assert.Equal(2, options.Value.ConsumerConcurrency);
        Assert.False(options.Value.ImmediateDashboardBroadcast);
        Assert.Single(options.Value.Channels);
        
        var channel = options.Value.Channels[0];
        Assert.Equal("Security", channel.Name);
        Assert.True(channel.Enabled);
        Assert.Equal("*[System[(EventID=4624)]]", channel.XPathFilter);
        Assert.Equal("Database", channel.BookmarkPersistence);
        Assert.Equal(1000, channel.MaxQueue);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
