using System.Collections.Generic;
using System.Threading.Tasks;
using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for YARA rule storage and retrieval
/// </summary>
public interface IYaraRuleStore
{
    /// <summary>
    /// Get all YARA rules
    /// </summary>
    Task<IEnumerable<YaraRule>> GetAllRulesAsync();

    /// <summary>
    /// Get YARA rules with pagination
    /// </summary>
    Task<(IEnumerable<YaraRule> Rules, int TotalCount)> GetRulesPagedAsync(int page = 1, int limit = 25, string? category = null, string? tag = null, string? mitreTechnique = null, bool? enabled = null);
    
    /// <summary>
    /// Get enabled YARA rules
    /// </summary>
    Task<IEnumerable<YaraRule>> GetEnabledRulesAsync();
    
    /// <summary>
    /// Get a specific YARA rule by ID
    /// </summary>
    Task<YaraRule?> GetRuleByIdAsync(string id);
    
    /// <summary>
    /// Get YARA rules by category
    /// </summary>
    Task<IEnumerable<YaraRule>> GetRulesByCategoryAsync(string category);
    
    /// <summary>
    /// Add a new YARA rule
    /// </summary>
    Task<YaraRule> AddRuleAsync(YaraRule rule);
    
    /// <summary>
    /// Update an existing YARA rule
    /// </summary>
    Task<YaraRule> UpdateRuleAsync(YaraRule rule);
    
    /// <summary>
    /// Delete a YARA rule
    /// </summary>
    Task<bool> DeleteRuleAsync(string id);
    
    /// <summary>
    /// Check if a rule name already exists
    /// </summary>
    Task<bool> RuleExistsAsync(string name);
    
    /// <summary>
    /// Update rule metrics after execution
    /// </summary>
    Task UpdateRuleMetricsAsync(string ruleId, bool matched, double executionTimeMs);
    
    /// <summary>
    /// Record a false positive for a rule
    /// </summary>
    Task RecordFalsePositiveAsync(string ruleId);
    
    /// <summary>
    /// Get rules by MITRE technique
    /// </summary>
    Task<IEnumerable<YaraRule>> GetRulesByMitreTechniqueAsync(string techniqueId);
    
    /// <summary>
    /// Get rules by tag
    /// </summary>
    Task<IEnumerable<YaraRule>> GetRulesByTagAsync(string tag);
    
    /// <summary>
    /// Save YARA match result
    /// </summary>
    Task<YaraMatch> SaveMatchAsync(YaraMatch match);
    
    /// <summary>
    /// Get recent matches
    /// </summary>
    Task<IEnumerable<YaraMatch>> GetRecentMatchesAsync(int count = 100);
    
    /// <summary>
    /// Get matches by security event ID
    /// </summary>
    Task<IEnumerable<YaraMatch>> GetMatchesBySecurityEventAsync(string securityEventId);
}
