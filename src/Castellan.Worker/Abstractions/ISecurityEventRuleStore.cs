using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for managing security event detection rules
/// </summary>
public interface ISecurityEventRuleStore
{
    /// <summary>
    /// Gets all enabled security event rules
    /// </summary>
    Task<IReadOnlyList<SecurityEventRuleEntity>> GetAllEnabledRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets security event rules for a specific event ID and channel
    /// </summary>
    Task<SecurityEventRuleEntity?> GetRuleAsync(int eventId, string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all security event rules (enabled and disabled)
    /// </summary>
    Task<IReadOnlyList<SecurityEventRuleEntity>> GetAllRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new security event rule
    /// </summary>
    Task<SecurityEventRuleEntity> CreateRuleAsync(SecurityEventRuleEntity rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing security event rule
    /// </summary>
    Task<SecurityEventRuleEntity> UpdateRuleAsync(SecurityEventRuleEntity rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a security event rule
    /// </summary>
    Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the rule cache
    /// </summary>
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
}
