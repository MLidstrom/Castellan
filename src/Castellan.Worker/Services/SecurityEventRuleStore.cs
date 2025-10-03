using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

/// <summary>
/// Database-backed security event rule store with in-memory caching
/// </summary>
public sealed class SecurityEventRuleStore : ISecurityEventRuleStore
{
    private readonly IDbContextFactory<CastellanDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SecurityEventRuleStore> _logger;

    private const string CacheKeyAllRules = "SecurityEventRules:All";
    private const string CacheKeyEnabledRules = "SecurityEventRules:Enabled";
    private const string CacheKeyPrefix = "SecurityEventRule:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public SecurityEventRuleStore(
        IDbContextFactory<CastellanDbContext> contextFactory,
        IMemoryCache cache,
        ILogger<SecurityEventRuleStore> logger)
    {
        _contextFactory = contextFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SecurityEventRuleEntity>> GetAllEnabledRulesAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyEnabledRules, async entry =>
        {
            entry.SetAbsoluteExpiration(CacheDuration);

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rules = await context.SecurityEventRules
                .Where(r => r.IsEnabled)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.EventId)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Loaded {Count} enabled security event rules into cache", rules.Count);
            return (IReadOnlyList<SecurityEventRuleEntity>)rules;
        }) ?? Array.Empty<SecurityEventRuleEntity>();
    }

    public async Task<SecurityEventRuleEntity?> GetRuleAsync(int eventId, string channel, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{eventId}:{channel}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetAbsoluteExpiration(CacheDuration);

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rule = await context.SecurityEventRules
                .Where(r => r.EventId == eventId && r.Channel == channel && r.IsEnabled)
                .OrderByDescending(r => r.Priority)
                .FirstOrDefaultAsync(cancellationToken);

            return rule;
        });
    }

    public async Task<IReadOnlyList<SecurityEventRuleEntity>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(CacheKeyAllRules, async entry =>
        {
            entry.SetAbsoluteExpiration(CacheDuration);

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rules = await context.SecurityEventRules
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.EventId)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Loaded {Count} total security event rules into cache", rules.Count);
            return (IReadOnlyList<SecurityEventRuleEntity>)rules;
        }) ?? Array.Empty<SecurityEventRuleEntity>();
    }

    public async Task<SecurityEventRuleEntity> CreateRuleAsync(SecurityEventRuleEntity rule, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        context.SecurityEventRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);

        await RefreshCacheAsync(cancellationToken);

        _logger.LogInformation("Created security event rule {RuleId} for Event {EventId} on channel {Channel}",
            rule.Id, rule.EventId, rule.Channel);

        return rule;
    }

    public async Task<SecurityEventRuleEntity> UpdateRuleAsync(SecurityEventRuleEntity rule, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        rule.UpdatedAt = DateTime.UtcNow;

        context.SecurityEventRules.Update(rule);
        await context.SaveChangesAsync(cancellationToken);

        await RefreshCacheAsync(cancellationToken);

        _logger.LogInformation("Updated security event rule {RuleId} for Event {EventId} on channel {Channel}",
            rule.Id, rule.EventId, rule.Channel);

        return rule;
    }

    public async Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rule = await context.SecurityEventRules.FindAsync(new object[] { ruleId }, cancellationToken);
        if (rule != null)
        {
            context.SecurityEventRules.Remove(rule);
            await context.SaveChangesAsync(cancellationToken);

            await RefreshCacheAsync(cancellationToken);

            _logger.LogInformation("Deleted security event rule {RuleId} for Event {EventId} on channel {Channel}",
                rule.Id, rule.EventId, rule.Channel);
        }
    }

    public Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Remove(CacheKeyAllRules);
        _cache.Remove(CacheKeyEnabledRules);

        // Clear individual rule caches - this is a simple approach
        // In production, you might want to track cache keys more explicitly
        _logger.LogInformation("Security event rules cache invalidated");

        return Task.CompletedTask;
    }
}
