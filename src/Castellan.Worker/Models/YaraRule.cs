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
