using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

public class SecurityEventRuleStoreTests : IDisposable
{
    private readonly IDbContextFactory<CastellanDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<SecurityEventRuleStore>> _mockLogger;
    private readonly SecurityEventRuleStore _store;
    private readonly CastellanDbContext _context;

    public SecurityEventRuleStoreTests()
    {
        var options = new DbContextOptionsBuilder<CastellanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new CastellanDbContext(options);

        _contextFactory = new TestDbContextFactory(_context);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<SecurityEventRuleStore>>();

        _store = new SecurityEventRuleStore(_contextFactory, _cache, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAllEnabledRulesAsync_ReturnsOnlyEnabledRules()
    {
        // Arrange
        var rule1 = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        var rule2 = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 20,
            IsEnabled = true,
            Summary = "Failed login"
        };

        var rule3 = new SecurityEventRuleEntity
        {
            EventId = 4648,
            Channel = "Security",
            EventType = "ExplicitLogon",
            RiskLevel = "Medium",
            Confidence = 75,
            Priority = 15,
            IsEnabled = false,
            Summary = "Explicit logon"
        };

        _context.SecurityEventRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _store.GetAllEnabledRulesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.IsEnabled));
    }

    [Fact]
    public async Task GetAllEnabledRulesAsync_OrdersByPriorityDescendingThenEventId()
    {
        // Arrange
        var rule1 = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 10,
            IsEnabled = true,
            Summary = "Failed login"
        };

        var rule2 = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 20,
            IsEnabled = true,
            Summary = "Successful login"
        };

        var rule3 = new SecurityEventRuleEntity
        {
            EventId = 4648,
            Channel = "Security",
            EventType = "ExplicitLogon",
            RiskLevel = "Medium",
            Confidence = 75,
            Priority = 20,
            IsEnabled = true,
            Summary = "Explicit logon"
        };

        _context.SecurityEventRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _store.GetAllEnabledRulesAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(20, result[0].Priority); // rule2 (EventId 4624)
        Assert.Equal(4624, result[0].EventId);
        Assert.Equal(20, result[1].Priority); // rule3 (EventId 4648)
        Assert.Equal(4648, result[1].EventId);
        Assert.Equal(10, result[2].Priority); // rule1 (EventId 4625)
    }

    [Fact]
    public async Task GetRuleAsync_ReturnsMatchingRule()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        _context.SecurityEventRules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _store.GetRuleAsync(4624, "Security");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4624, result.EventId);
        Assert.Equal("Security", result.Channel);
    }

    [Fact]
    public async Task GetRuleAsync_ReturnsNullWhenNotFound()
    {
        // Act
        var result = await _store.GetRuleAsync(9999, "NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRuleAsync_ReturnsOnlyEnabledRule()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 10,
            IsEnabled = false,
            Summary = "Failed login"
        };

        _context.SecurityEventRules.Add(rule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _store.GetRuleAsync(4625, "Security");

        // Assert
        Assert.Null(result); // Should not return disabled rules
    }

    [Fact]
    public async Task GetAllRulesAsync_ReturnsAllRules()
    {
        // Arrange
        var rule1 = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        var rule2 = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 20,
            IsEnabled = false,
            Summary = "Failed login"
        };

        _context.SecurityEventRules.AddRange(rule1, rule2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _store.GetAllRulesAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CreateRuleAsync_AddsRuleToDatabase()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        // Act
        var result = await _store.CreateRuleAsync(rule);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.NotEqual(default, result.CreatedAt);
        Assert.NotEqual(default, result.UpdatedAt);

        var dbRule = await _context.SecurityEventRules.FindAsync(result.Id);
        Assert.NotNull(dbRule);
    }

    [Fact]
    public async Task CreateRuleAsync_InvalidatesCache()
    {
        // Arrange
        var initialRule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Initial rule"
        };

        _context.SecurityEventRules.Add(initialRule);
        await _context.SaveChangesAsync();

        // Prime the cache
        var cachedRules = await _store.GetAllEnabledRulesAsync();
        Assert.Single(cachedRules);

        // Act - Create new rule
        var newRule = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 20,
            IsEnabled = true,
            Summary = "Failed login"
        };

        await _store.CreateRuleAsync(newRule);

        // Assert - Cache should be refreshed
        var updatedRules = await _store.GetAllEnabledRulesAsync();
        Assert.Equal(2, updatedRules.Count);
    }

    [Fact]
    public async Task UpdateRuleAsync_UpdatesRuleInDatabase()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Original summary"
        };

        _context.SecurityEventRules.Add(rule);
        await _context.SaveChangesAsync();

        var originalUpdatedAt = rule.UpdatedAt;

        // Act
        await Task.Delay(10); // Ensure timestamp changes
        rule.Summary = "Updated summary";
        rule.RiskLevel = "Medium";
        var result = await _store.UpdateRuleAsync(rule);

        // Assert
        Assert.Equal("Updated summary", result.Summary);
        Assert.Equal("Medium", result.RiskLevel);
        Assert.True(result.UpdatedAt > originalUpdatedAt);

        var dbRule = await _context.SecurityEventRules.FindAsync(rule.Id);
        Assert.NotNull(dbRule);
        Assert.Equal("Updated summary", dbRule.Summary);
    }

    [Fact]
    public async Task DeleteRuleAsync_RemovesRuleFromDatabase()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        _context.SecurityEventRules.Add(rule);
        await _context.SaveChangesAsync();

        var ruleId = rule.Id;

        // Act
        await _store.DeleteRuleAsync(ruleId);

        // Assert
        var dbRule = await _context.SecurityEventRules.FindAsync(ruleId);
        Assert.Null(dbRule);
    }

    [Fact]
    public async Task DeleteRuleAsync_WithNonExistentId_DoesNotThrow()
    {
        // Act & Assert
        await _store.DeleteRuleAsync(9999);
    }

    [Fact]
    public async Task RefreshCacheAsync_InvalidatesCache()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        _context.SecurityEventRules.Add(rule);
        await _context.SaveChangesAsync();

        // Prime the cache
        await _store.GetAllEnabledRulesAsync();
        await _store.GetAllRulesAsync();

        // Manually add a rule to database (bypassing the store)
        var newRule = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 20,
            IsEnabled = true,
            Summary = "Failed login"
        };

        _context.SecurityEventRules.Add(newRule);
        await _context.SaveChangesAsync();

        // Act
        await _store.RefreshCacheAsync();

        // Assert - Cache should reflect the new rule
        var allRules = await _store.GetAllEnabledRulesAsync();
        Assert.Equal(2, allRules.Count);
    }

    [Fact]
    public async Task GetAllEnabledRulesAsync_UsesCacheOnSecondCall()
    {
        // Arrange
        var rule = new SecurityEventRuleEntity
        {
            EventId = 4624,
            Channel = "Security",
            EventType = "SuccessfulLogon",
            RiskLevel = "Low",
            Confidence = 80,
            Priority = 10,
            IsEnabled = true,
            Summary = "Successful login"
        };

        _context.SecurityEventRules.Add(rule);
        await _context.SaveChangesAsync();

        // Act - First call
        var firstCall = await _store.GetAllEnabledRulesAsync();

        // Manually add a rule to database (bypassing the store and cache)
        var newRule = new SecurityEventRuleEntity
        {
            EventId = 4625,
            Channel = "Security",
            EventType = "FailedLogon",
            RiskLevel = "High",
            Confidence = 90,
            Priority = 20,
            IsEnabled = true,
            Summary = "Failed login"
        };

        _context.SecurityEventRules.Add(newRule);
        await _context.SaveChangesAsync();

        // Act - Second call (should return cached data)
        var secondCall = await _store.GetAllEnabledRulesAsync();

        // Assert - Cache should return old data (1 rule, not 2)
        Assert.Single(secondCall);
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    // Helper class for DbContextFactory
    private class TestDbContextFactory : IDbContextFactory<CastellanDbContext>
    {
        private readonly CastellanDbContext _context;

        public TestDbContextFactory(CastellanDbContext context)
        {
            _context = context;
        }

        public CastellanDbContext CreateDbContext()
        {
            return _context;
        }

        public Task<CastellanDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context);
        }
    }
}
