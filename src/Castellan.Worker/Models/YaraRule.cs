using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Represents a YARA rule for malware detection
/// </summary>
public class YaraRule
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string RuleContent { get; set; } = string.Empty;
    
    public string Category { get; set; } = "General";
    
    public string Author { get; set; } = "System";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsEnabled { get; set; } = true;
    
    public int Priority { get; set; } = 50; // 0-100, higher = more important
    
    public string ThreatLevel { get; set; } = "Medium"; // Low, Medium, High, Critical
    
    // Performance metrics
    public int HitCount { get; set; } = 0;
    
    public int FalsePositiveCount { get; set; } = 0;
    
    public double AverageExecutionTimeMs { get; set; } = 0;
    
    // Related MITRE ATT&CK techniques
    public List<string> MitreTechniques { get; set; } = new List<string>();
    
    // Tags for organization
    public List<string> Tags { get; set; } = new List<string>();
    
    // Version control
    public int Version { get; set; } = 1;
    
    public string? PreviousVersion { get; set; }
    
    // Validation status
    public bool IsValid { get; set; } = false;
    
    public string? ValidationError { get; set; }
    
    public DateTime? LastValidated { get; set; }
    
    // Source information
    public string Source { get; set; } = "Custom"; // Custom, Import, ThreatFeed, Community
    
    public string? SourceUrl { get; set; }
    
    // Testing
    public string? TestSample { get; set; } // Base64 encoded test file/content
    
    public bool? TestResult { get; set; }
}

/// <summary>
/// YARA rule execution result
/// </summary>
public class YaraMatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string RuleId { get; set; } = string.Empty;
    
    public string RuleName { get; set; } = string.Empty;
    
    public DateTime MatchTime { get; set; } = DateTime.UtcNow;
    
    public string TargetFile { get; set; } = string.Empty;
    
    public string TargetHash { get; set; } = string.Empty;
    
    public List<YaraMatchString> MatchedStrings { get; set; } = new List<YaraMatchString>();
    
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    
    public double ExecutionTimeMs { get; set; }
    
    public string? SecurityEventId { get; set; }
}

/// <summary>
/// Represents a matched string within a YARA rule
/// </summary>
public class YaraMatchString
{
    public string Identifier { get; set; } = string.Empty;
    
    public long Offset { get; set; }
    
    public string Value { get; set; } = string.Empty;
    
    public bool IsHex { get; set; }
}

/// <summary>
/// YARA rule categories
/// </summary>
public static class YaraRuleCategory
{
    public const string Malware = "Malware";
    public const string Ransomware = "Ransomware";
    public const string Trojan = "Trojan";
    public const string Backdoor = "Backdoor";
    public const string Webshell = "Webshell";
    public const string Cryptominer = "Cryptominer";
    public const string Exploit = "Exploit";
    public const string Suspicious = "Suspicious";
    public const string PUA = "PUA"; // Potentially Unwanted Application
    public const string Custom = "Custom";
}

/// <summary>
/// DTO for YARA rule API responses
/// </summary>
public class YaraRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleContent { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public string ThreatLevel { get; set; } = string.Empty;
    public int HitCount { get; set; }
    public int FalsePositiveCount { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public List<string> MitreTechniques { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Request model for creating/updating YARA rules
/// </summary>
public class YaraRuleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string RuleContent { get; set; } = string.Empty;
    
    public string Category { get; set; } = "General";
    
    public string Author { get; set; } = "System";
    
    public bool IsEnabled { get; set; } = true;
    
    public int Priority { get; set; } = 50;
    
    public string ThreatLevel { get; set; } = "Medium";
    
    public List<string> MitreTechniques { get; set; } = new List<string>();
    
    public List<string> Tags { get; set; } = new List<string>();
    
    public string? TestSample { get; set; }
}

/// <summary>
/// Request model for testing YARA rules
/// </summary>
public class YaraTestRequest
{
    [Required]
    public string RuleContent { get; set; } = string.Empty;
    
    public string? TestContent { get; set; } // Base64 encoded content to test against
    
    public string? TestFilePath { get; set; } // Or path to file to test
}

/// <summary>
/// Response model for YARA rule testing
/// </summary>
public class YaraTestResponse
{
    public bool IsValid { get; set; }
    
    public string? ValidationError { get; set; }
    
    public bool Matched { get; set; }
    
    public List<YaraMatchString> MatchedStrings { get; set; } = new List<YaraMatchString>();
    
    public double ExecutionTimeMs { get; set; }
}

/// <summary>
/// Request model for YARA scanning
/// </summary>
public class YaraScanRequest
{
    /// <summary>
    /// Base64 encoded content to scan
    /// </summary>
    public string? Content { get; set; }
    
    /// <summary>
    /// File path to scan (alternative to Content)
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Optional filename for context
    /// </summary>
    public string? FileName { get; set; }
}

/// <summary>
/// Response model for YARA scanning
/// </summary>
public class YaraScanResult
{
    public string FileName { get; set; } = string.Empty;
    
    public DateTime ScanTime { get; set; }
    
    public int MatchCount { get; set; }
    
    public List<YaraScanMatch> Matches { get; set; } = new List<YaraScanMatch>();
}

/// <summary>
/// YARA scan match result
/// </summary>
public class YaraScanMatch
{
    public string? RuleId { get; set; }
    
    public string RuleName { get; set; } = string.Empty;
    
    public List<YaraMatchString> MatchedStrings { get; set; } = new List<YaraMatchString>();
    
    public double ExecutionTimeMs { get; set; }
}

/// <summary>
/// Request model for bulk YARA rule operations
/// </summary>
public class YaraBulkOperationRequest
{
    [Required]
    public List<string> RuleIds { get; set; } = new List<string>();
    
    /// <summary>
    /// Operation type: enable, disable, delete, validate
    /// </summary>
    [Required]
    public string Operation { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional parameters for the operation
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Response model for bulk operations
/// </summary>
public class YaraBulkOperationResponse
{
    public int SuccessCount { get; set; }
    
    public int FailureCount { get; set; }
    
    public List<YaraBulkOperationResult> Results { get; set; } = new List<YaraBulkOperationResult>();
}

/// <summary>
/// Individual result in bulk operation
/// </summary>
public class YaraBulkOperationResult
{
    public string RuleId { get; set; } = string.Empty;
    
    public string RuleName { get; set; } = string.Empty;
    
    public bool Success { get; set; }
    
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request model for rule import
/// </summary>
public class YaraImportRequest
{
    /// <summary>
    /// Raw YARA rule content (multiple rules supported)
    /// </summary>
    [Required]
    public string RuleContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Default category for imported rules
    /// </summary>
    public string Category { get; set; } = YaraRuleCategory.Custom;
    
    /// <summary>
    /// Default author for imported rules
    /// </summary>
    public string Author { get; set; } = "Imported";
    
    /// <summary>
    /// Source information
    /// </summary>
    public string Source { get; set; } = "Import";
    
    /// <summary>
    /// Whether to skip rules that already exist
    /// </summary>
    public bool SkipDuplicates { get; set; } = true;
    
    /// <summary>
    /// Whether to enable imported rules by default
    /// </summary>
    public bool EnableByDefault { get; set; } = false;
}

/// <summary>
/// Response model for rule import
/// </summary>
public class YaraImportResponse
{
    public int ImportedCount { get; set; }
    
    public int SkippedCount { get; set; }
    
    public int FailedCount { get; set; }
    
    public List<YaraImportResult> Results { get; set; } = new List<YaraImportResult>();
}

/// <summary>
/// Individual import result
/// </summary>
public class YaraImportResult
{
    public string RuleName { get; set; } = string.Empty;
    
    public string? RuleId { get; set; }
    
    public string Status { get; set; } = string.Empty; // imported, skipped, failed
    
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request model for rule export
/// </summary>
public class YaraExportRequest
{
    /// <summary>
    /// Specific rule IDs to export (if empty, exports all enabled rules)
    /// </summary>
    public List<string> RuleIds { get; set; } = new List<string>();
    
    /// <summary>
    /// Export format: raw, json, combined
    /// </summary>
    public string Format { get; set; } = "raw";
    
    /// <summary>
    /// Include disabled rules
    /// </summary>
    public bool IncludeDisabled { get; set; } = false;
    
    /// <summary>
    /// Include rule metadata
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
}

/// <summary>
/// YARA rule statistics and analytics
/// </summary>
public class YaraRuleStatistics
{
    public int TotalRules { get; set; }
    
    public int EnabledRules { get; set; }
    
    public int DisabledRules { get; set; }
    
    public int ValidRules { get; set; }
    
    public int InvalidRules { get; set; }
    
    public Dictionary<string, int> RulesByCategory { get; set; } = new Dictionary<string, int>();
    
    public Dictionary<string, int> RulesByThreatLevel { get; set; } = new Dictionary<string, int>();
    
    public List<YaraRulePerformance> TopPerformingRules { get; set; } = new List<YaraRulePerformance>();
    
    public List<YaraRulePerformance> SlowestRules { get; set; } = new List<YaraRulePerformance>();
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// YARA rule performance metrics
/// </summary>
public class YaraRulePerformance
{
    public string RuleId { get; set; } = string.Empty;
    
    public string RuleName { get; set; } = string.Empty;
    
    public int HitCount { get; set; }
    
    public int FalsePositiveCount { get; set; }
    
    public double AverageExecutionTimeMs { get; set; }
    
    public double EffectivenessScore { get; set; }
    
    public DateTime LastMatch { get; set; }
}

/// <summary>
/// Match analytics request
/// </summary>
public class YaraMatchAnalyticsRequest
{
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public List<string>? RuleIds { get; set; }
    
    public List<string>? Categories { get; set; }
    
    public string? GroupBy { get; set; } // hour, day, week, month, rule, category
    
    public int Limit { get; set; } = 100;
}

/// <summary>
/// Match analytics response
/// </summary>
public class YaraMatchAnalyticsResponse
{
    public int TotalMatches { get; set; }
    
    public List<YaraMatchTrend> Trends { get; set; } = new List<YaraMatchTrend>();
    
    public List<YaraRuleEffectiveness> RuleEffectiveness { get; set; } = new List<YaraRuleEffectiveness>();
    
    public Dictionary<string, int> MatchesByCategory { get; set; } = new Dictionary<string, int>();
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Match trend data point
/// </summary>
public class YaraMatchTrend
{
    public DateTime Timestamp { get; set; }
    
    public int MatchCount { get; set; }
    
    public string? GroupBy { get; set; }
    
    public string? GroupValue { get; set; }
}

/// <summary>
/// Rule effectiveness metrics
/// </summary>
public class YaraRuleEffectiveness
{
    public string RuleId { get; set; } = string.Empty;
    
    public string RuleName { get; set; } = string.Empty;
    
    public int MatchCount { get; set; }
    
    public int FalsePositiveCount { get; set; }
    
    public double FalsePositiveRate { get; set; }
    
    public double EffectivenessScore { get; set; }
    
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// Rule health status
/// </summary>
public class YaraRuleHealth
{
    public string RuleId { get; set; } = string.Empty;
    
    public string RuleName { get; set; } = string.Empty;
    
    public bool IsCompiled { get; set; }
    
    public bool IsValid { get; set; }
    
    public string? ValidationError { get; set; }
    
    public double CompilationTimeMs { get; set; }
    
    public DateTime LastValidated { get; set; }
    
    public string HealthStatus { get; set; } = "Unknown"; // Healthy, Warning, Critical
    
    public List<string> Warnings { get; set; } = new List<string>();
}

/// <summary>
/// System-wide YARA health status
/// </summary>
public class YaraSystemHealth
{
    public bool IsHealthy { get; set; }
    
    public int CompiledRulesCount { get; set; }
    
    public int HealthyRulesCount { get; set; }
    
    public int UnhealthyRulesCount { get; set; }
    
    public double AverageCompilationTime { get; set; }
    
    public DateTime LastCompilation { get; set; }
    
    public string? SystemError { get; set; }
    
    public List<YaraRuleHealth> UnhealthyRules { get; set; } = new List<YaraRuleHealth>();
}
