using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/yara-rules")]
[Authorize]
public class YaraRulesController : ControllerBase
{
    private readonly ILogger<YaraRulesController> _logger;
    private readonly IYaraRuleStore _ruleStore;
    private readonly IYaraScanService? _yaraScanService;
    
    public YaraRulesController(
        ILogger<YaraRulesController> logger,
        IYaraRuleStore ruleStore,
        IYaraScanService? yaraScanService = null)
    {
        _logger = logger;
        _ruleStore = ruleStore;
        _yaraScanService = yaraScanService;
    }
    
    /// <summary>
    /// Get all YARA rules
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRules(
        [FromQuery] string? category = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? mitreTechnique = null,
        [FromQuery] bool? enabled = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10)
    {
        try
        {
            _logger.LogInformation("Getting YARA rules - category: {Category}, tag: {Tag}, enabled: {Enabled}", 
                category, tag, enabled);
            
            IEnumerable<YaraRule> rules;
            
            // Apply filters
            if (!string.IsNullOrEmpty(category))
            {
                rules = await _ruleStore.GetRulesByCategoryAsync(category);
            }
            else if (!string.IsNullOrEmpty(tag))
            {
                rules = await _ruleStore.GetRulesByTagAsync(tag);
            }
            else if (!string.IsNullOrEmpty(mitreTechnique))
            {
                rules = await _ruleStore.GetRulesByMitreTechniqueAsync(mitreTechnique);
            }
            else if (enabled.HasValue && enabled.Value)
            {
                rules = await _ruleStore.GetEnabledRulesAsync();
            }
            else
            {
                rules = await _ruleStore.GetAllRulesAsync();
            }
            
            // Convert to DTOs
            var ruleDtos = rules.Select(ConvertToDto).ToList();
            
            // Apply pagination
            var totalCount = ruleDtos.Count;
            var pagedRules = ruleDtos
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToList();
            
            return Ok(new
            {
                data = pagedRules,
                total = totalCount,
                page,
                perPage = limit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YARA rules");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Get a specific YARA rule
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetRule(string id)
    {
        try
        {
            var rule = await _ruleStore.GetRuleByIdAsync(id);
            
            if (rule == null)
            {
                return NotFound(new { message = "Rule not found" });
            }
            
            return Ok(new { data = ConvertToDto(rule) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YARA rule: {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Create a new YARA rule
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] YaraRuleRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            // Check if rule name already exists
            if (await _ruleStore.RuleExistsAsync(request.Name))
            {
                return BadRequest(new { message = $"Rule with name '{request.Name}' already exists" });
            }
            
            // Validate YARA syntax (simplified validation for now)
            var validationResult = ValidateYaraRule(request.RuleContent);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { message = validationResult.Error });
            }
            
            // Create rule
            var rule = new YaraRule
            {
                Name = request.Name,
                Description = request.Description,
                RuleContent = request.RuleContent,
                Category = request.Category,
                Author = request.Author,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                ThreatLevel = request.ThreatLevel,
                MitreTechniques = request.MitreTechniques,
                Tags = request.Tags,
                IsValid = true,
                TestSample = request.TestSample
            };
            
            var createdRule = await _ruleStore.AddRuleAsync(rule);
            
            _logger.LogInformation("Created YARA rule: {RuleName} ({RuleId})", 
                createdRule.Name, createdRule.Id);
            
            return CreatedAtAction(nameof(GetRule), new { id = createdRule.Id }, 
                new { data = ConvertToDto(createdRule) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating YARA rule");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Update an existing YARA rule
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRule(string id, [FromBody] YaraRuleRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var existingRule = await _ruleStore.GetRuleByIdAsync(id);
            if (existingRule == null)
            {
                return NotFound(new { message = "Rule not found" });
            }
            
            // Validate YARA syntax
            var validationResult = ValidateYaraRule(request.RuleContent);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { message = validationResult.Error });
            }
            
            // Update rule
            existingRule.Name = request.Name;
            existingRule.Description = request.Description;
            existingRule.RuleContent = request.RuleContent;
            existingRule.Category = request.Category;
            existingRule.Author = request.Author;
            existingRule.IsEnabled = request.IsEnabled;
            existingRule.Priority = request.Priority;
            existingRule.ThreatLevel = request.ThreatLevel;
            existingRule.MitreTechniques = request.MitreTechniques;
            existingRule.Tags = request.Tags;
            existingRule.IsValid = true;
            existingRule.TestSample = request.TestSample;
            
            var updatedRule = await _ruleStore.UpdateRuleAsync(existingRule);
            
            _logger.LogInformation("Updated YARA rule: {RuleName} ({RuleId})", 
                updatedRule.Name, updatedRule.Id);
            
            return Ok(new { data = ConvertToDto(updatedRule) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating YARA rule: {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Delete a YARA rule
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRule(string id)
    {
        try
        {
            var deleted = await _ruleStore.DeleteRuleAsync(id);
            
            if (!deleted)
            {
                return NotFound(new { message = "Rule not found" });
            }
            
            _logger.LogInformation("Deleted YARA rule: {RuleId}", id);
            
            return Ok(new { message = "Rule deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting YARA rule: {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Test a YARA rule
    /// </summary>
    [HttpPost("test")]
    public Task<IActionResult> TestRule([FromBody] YaraTestRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }
            
            // Validate YARA syntax
            var validationResult = ValidateYaraRule(request.RuleContent);
            if (!validationResult.IsValid)
            {
                return Task.FromResult<IActionResult>(Ok(new YaraTestResponse
                {
                    IsValid = false,
                    ValidationError = validationResult.Error,
                    Matched = false
                }));
            }
            
            // TODO: Implement actual YARA scanning once YaraScannerService is created
            // For now, return a mock response
            var response = new YaraTestResponse
            {
                IsValid = true,
                Matched = false,
                ExecutionTimeMs = 10.5
            };
            
            return Task.FromResult<IActionResult>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing YARA rule");
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }
    
    /// <summary>
    /// Get recent YARA matches
    /// </summary>
    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches(
        [FromQuery] string? securityEventId = null,
        [FromQuery] int count = 100)
    {
        try
        {
            IEnumerable<YaraMatch> matches;
            
            if (!string.IsNullOrWhiteSpace(securityEventId))
            {
                matches = await _ruleStore.GetMatchesBySecurityEventAsync(securityEventId);
            }
            else
            {
                matches = await _ruleStore.GetRecentMatchesAsync(count);
            }
            
            return Ok(new
            {
                data = matches,
                total = matches.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YARA matches");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Report a false positive
    /// </summary>
    [HttpPost("{id}/false-positive")]
    public async Task<IActionResult> ReportFalsePositive(string id)
    {
        try
        {
            var rule = await _ruleStore.GetRuleByIdAsync(id);
            if (rule == null)
            {
                return NotFound(new { message = "Rule not found" });
            }
            
            await _ruleStore.RecordFalsePositiveAsync(id);
            
            _logger.LogWarning("False positive reported for rule: {RuleName} ({RuleId})", 
                rule.Name, rule.Id);
            
            return Ok(new { message = "False positive recorded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting false positive for rule: {RuleId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Get YARA rule categories
    /// </summary>
    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var categories = new[]
        {
            YaraRuleCategory.Malware,
            YaraRuleCategory.Ransomware,
            YaraRuleCategory.Trojan,
            YaraRuleCategory.Backdoor,
            YaraRuleCategory.Webshell,
            YaraRuleCategory.Cryptominer,
            YaraRuleCategory.Exploit,
            YaraRuleCategory.Suspicious,
            YaraRuleCategory.PUA,
            YaraRuleCategory.Custom
        };
        
        return Ok(new { data = categories });
    }
    
    internal YaraRuleDto ConvertToDto(YaraRule rule)
    {
        return new YaraRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            RuleContent = rule.RuleContent,
            Category = rule.Category,
            Author = rule.Author,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
            IsEnabled = rule.IsEnabled,
            Priority = rule.Priority,
            ThreatLevel = rule.ThreatLevel,
            HitCount = rule.HitCount,
            FalsePositiveCount = rule.FalsePositiveCount,
            AverageExecutionTimeMs = rule.AverageExecutionTimeMs,
            MitreTechniques = rule.MitreTechniques ?? new List<string>(),
            Tags = rule.Tags ?? new List<string>(),
            IsValid = rule.IsValid,
            ValidationError = rule.ValidationError,
            Source = rule.Source
        };
    }
    
    internal (bool IsValid, string? Error) ValidateYaraRule(string ruleContent)
    {
        // Basic YARA syntax validation
        // TODO: Use actual YARA compiler for validation once YaraScannerService is implemented
        
        if (string.IsNullOrWhiteSpace(ruleContent))
        {
            return (false, "Rule content cannot be empty");
        }
        
        // Convert to lowercase for case-insensitive matching
        var lowerContent = ruleContent.ToLowerInvariant();
        
        // Check for basic YARA rule structure
        if (!lowerContent.Contains("rule "))
        {
            return (false, "Invalid YARA rule: missing 'rule' keyword");
        }
        
        if (!ruleContent.Contains("{") || !ruleContent.Contains("}"))
        {
            return (false, "Invalid YARA rule: missing braces");
        }
        
        if (!lowerContent.Contains("condition:"))
        {
            return (false, "Invalid YARA rule: missing 'condition' section");
        }
        
        return (true, null);
    }
    
    /// <summary>
    /// Calculate effectiveness score for a YARA rule
    /// </summary>
    private static double CalculateEffectivenessScore(YaraRule rule)
    {
        if (rule.HitCount == 0) return 0.0;
        
        // Base score from hit count (logarithmic scale)
        var hitScore = Math.Log10(rule.HitCount + 1) * 10;
        
        // Penalty for false positives
        var falsePositiveRate = rule.HitCount > 0 ? (double)rule.FalsePositiveCount / rule.HitCount : 0;
        var falsePositivePenalty = falsePositiveRate * 50;
        
        // Bonus for fast execution
        var speedBonus = rule.AverageExecutionTimeMs > 0 ? Math.Max(0, (100 - rule.AverageExecutionTimeMs) / 10) : 0;
        
        return Math.Max(0, hitScore - falsePositivePenalty + speedBonus);
    }
    
    /// <summary>
    /// Parse YARA rules from raw content
    /// </summary>
    private static List<(string Name, string RuleContent, string? Description, List<string>? Tags, List<string>? MitreTechniques)> ParseYaraRules(string content)
    {
        var rules = new List<(string, string, string?, List<string>?, List<string>?)>();
        
        // Simple YARA rule parsing - split by "rule" keyword
        var ruleParts = content.Split(new[] { "\nrule ", "\r\nrule " }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < ruleParts.Length; i++)
        {
            var part = ruleParts[i];
            if (i > 0) part = "rule " + part; // Add back the "rule" keyword
            
            var lines = part.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) continue;
            
            var firstLine = lines[0].Trim();
            if (!firstLine.StartsWith("rule ", StringComparison.OrdinalIgnoreCase)) continue;
            
            // Extract rule name
            var nameMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"rule\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!nameMatch.Success) continue;
            
            var ruleName = nameMatch.Groups[1].Value;
            
            // Extract description from meta section (basic implementation)
            string? description = null;
            var tags = new List<string>();
            var mitreTechniques = new List<string>();
            
            var metaMatch = System.Text.RegularExpressions.Regex.Match(part, @"meta:\s*([^}]*?)(?:strings:|condition:)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (metaMatch.Success)
            {
                var metaContent = metaMatch.Groups[1].Value;
                var descMatch = System.Text.RegularExpressions.Regex.Match(metaContent, @"description\s*=\s*[""']([^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (descMatch.Success)
                {
                    description = descMatch.Groups[1].Value;
                }
            }
            
            rules.Add((ruleName, part.Trim(), description, tags, mitreTechniques));
        }
        
        return rules;
    }
    
    /// <summary>
    /// Generate export content in the specified format
    /// </summary>
    private string GenerateExportContent(IEnumerable<YaraRule> rules, string format, bool includeMetadata)
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                var ruleDtos = rules.Select(ConvertToDto).ToList();
                return System.Text.Json.JsonSerializer.Serialize(ruleDtos, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
            case "raw":
            case "combined":
            default:
                var sb = new System.Text.StringBuilder();
                
                if (includeMetadata)
                {
                    sb.AppendLine($"// YARA Rules Export - Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                    sb.AppendLine($"// Total rules: {rules.Count()}");
                    sb.AppendLine();
                }
                
                foreach (var rule in rules)
                {
                    if (includeMetadata)
                    {
                        sb.AppendLine($"// Rule: {rule.Name}");
                        sb.AppendLine($"// Category: {rule.Category}");
                        sb.AppendLine($"// Author: {rule.Author}");
                        sb.AppendLine($"// Created: {rule.CreatedAt:yyyy-MM-dd}");
                        if (!string.IsNullOrEmpty(rule.Description))
                            sb.AppendLine($"// Description: {rule.Description}");
                        sb.AppendLine();
                    }
                    
                    sb.AppendLine(rule.RuleContent);
                    sb.AppendLine();
                }
                
                return sb.ToString();
        }
    }
    
    /// <summary>
    /// Generate match trends for analytics
    /// </summary>
    private List<YaraMatchTrend> GenerateMatchTrends(List<YaraMatch> matches, string groupBy)
    {
        switch (groupBy.ToLowerInvariant())
        {
            case "hour":
                return matches
                    .GroupBy(m => new DateTime(m.MatchTime.Year, m.MatchTime.Month, m.MatchTime.Day, m.MatchTime.Hour, 0, 0))
                    .Select(g => new YaraMatchTrend
                    {
                        Timestamp = g.Key,
                        MatchCount = g.Count(),
                        GroupBy = "hour"
                    })
                    .OrderBy(t => t.Timestamp)
                    .ToList();
                    
            case "day":
            default:
                return matches
                    .GroupBy(m => m.MatchTime.Date)
                    .Select(g => new YaraMatchTrend
                    {
                        Timestamp = g.Key,
                        MatchCount = g.Count(),
                        GroupBy = "day"
                    })
                    .OrderBy(t => t.Timestamp)
                    .ToList();
                    
            case "week":
                return matches
                    .GroupBy(m => GetStartOfWeek(m.MatchTime))
                    .Select(g => new YaraMatchTrend
                    {
                        Timestamp = g.Key,
                        MatchCount = g.Count(),
                        GroupBy = "week"
                    })
                    .OrderBy(t => t.Timestamp)
                    .ToList();
                    
            case "month":
                return matches
                    .GroupBy(m => new DateTime(m.MatchTime.Year, m.MatchTime.Month, 1))
                    .Select(g => new YaraMatchTrend
                    {
                        Timestamp = g.Key,
                        MatchCount = g.Count(),
                        GroupBy = "month"
                    })
                    .OrderBy(t => t.Timestamp)
                    .ToList();
        }
    }
    
    /// <summary>
    /// Generate rule effectiveness metrics
    /// </summary>
    private async Task<List<YaraRuleEffectiveness>> GenerateRuleEffectiveness(List<YaraMatch> matches)
    {
        var ruleGroups = matches.GroupBy(m => m.RuleId).ToList();
        var effectiveness = new List<YaraRuleEffectiveness>();
        
        foreach (var group in ruleGroups)
        {
            var rule = await _ruleStore.GetRuleByIdAsync(group.Key);
            if (rule == null) continue;
            
            var matchCount = group.Count();
            var falsePositiveRate = rule.HitCount > 0 ? (double)rule.FalsePositiveCount / rule.HitCount : 0;
            
            effectiveness.Add(new YaraRuleEffectiveness
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                MatchCount = matchCount,
                FalsePositiveCount = rule.FalsePositiveCount,
                FalsePositiveRate = falsePositiveRate,
                EffectivenessScore = CalculateEffectivenessScore(rule),
                LastUsed = group.Max(m => m.MatchTime)
            });
        }
        
        return effectiveness.OrderByDescending(e => e.EffectivenessScore).Take(20).ToList();
    }
    
    /// <summary>
    /// Generate matches by category statistics
    /// </summary>
    private async Task<Dictionary<string, int>> GenerateMatchesByCategory(List<YaraMatch> matches)
    {
        var result = new Dictionary<string, int>();
        
        foreach (var match in matches)
        {
            var rule = await _ruleStore.GetRuleByIdAsync(match.RuleId);
            if (rule == null) continue;
            
            var category = rule.Category;
            if (result.ContainsKey(category))
                result[category]++;
            else
                result[category] = 1;
        }
        
        return result;
    }
    
    /// <summary>
    /// Get the start of the week for a given date
    /// </summary>
    private static DateTime GetStartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
    
    /// <summary>
    /// Scan uploaded content using YARA rules
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanContent([FromBody] YaraScanRequest request)
    {
        try
        {
            if (_yaraScanService == null)
            {
                return BadRequest(new { message = "YARA scanning service is not available" });
            }
            
            if (!_yaraScanService.IsHealthy)
            {
                return BadRequest(new { message = $"YARA scanning service is not healthy: {_yaraScanService.LastError}" });
            }
            
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            IEnumerable<YaraMatch> matches;
            
            if (!string.IsNullOrEmpty(request.Content))
            {
                // Scan Base64 encoded content
                try
                {
                    var bytes = Convert.FromBase64String(request.Content);
                    matches = await _yaraScanService.ScanBytesAsync(bytes, request.FileName);
                }
                catch (FormatException)
                {
                    return BadRequest(new { message = "Invalid Base64 content" });
                }
            }
            else if (!string.IsNullOrEmpty(request.FilePath) && System.IO.File.Exists(request.FilePath))
            {
                // Scan file from path
                matches = await _yaraScanService.ScanFileAsync(request.FilePath);
            }
            else
            {
                return BadRequest(new { message = "Either Content or FilePath must be provided" });
            }
            
            var result = new YaraScanResult
            {
                FileName = request.FileName ?? request.FilePath ?? "unknown",
                ScanTime = DateTime.UtcNow,
                MatchCount = matches.Count(),
                Matches = matches.Select(m => new YaraScanMatch
                {
                    RuleId = m.RuleId,
                    RuleName = m.RuleName,
                    MatchedStrings = m.MatchedStrings,
                    ExecutionTimeMs = m.ExecutionTimeMs
                }).ToList()
            };
            
            _logger.LogInformation("YARA scan completed: {FileName} - {MatchCount} matches", 
                result.FileName, result.MatchCount);
            
            return Ok(new { data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during YARA scan");
            return StatusCode(500, new { message = "Internal server error during scan" });
        }
    }
    
    /// <summary>
    /// Get YARA scanning service status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            if (_yaraScanService == null)
            {
                return Ok(new
                {
                    isAvailable = false,
                    isHealthy = false,
                    error = "YARA scanning service is not registered",
                    compiledRules = 0
                });
            }
            
            return Ok(new
            {
                isAvailable = true,
                isHealthy = _yaraScanService.IsHealthy,
                error = _yaraScanService.LastError,
                compiledRules = _yaraScanService.GetCompiledRuleCount()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YARA service status");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Perform bulk operations on YARA rules
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkOperation([FromBody] YaraBulkOperationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var response = new YaraBulkOperationResponse();
            
            foreach (var ruleId in request.RuleIds)
            {
                var result = new YaraBulkOperationResult
                {
                    RuleId = ruleId
                };
                
                try
                {
                    var rule = await _ruleStore.GetRuleByIdAsync(ruleId);
                    if (rule == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Rule not found";
                        response.FailureCount++;
                        continue;
                    }
                    
                    result.RuleName = rule.Name;
                    
                    switch (request.Operation.ToLowerInvariant())
                    {
                        case "enable":
                            rule.IsEnabled = true;
                            await _ruleStore.UpdateRuleAsync(rule);
                            result.Success = true;
                            response.SuccessCount++;
                            break;
                            
                        case "disable":
                            rule.IsEnabled = false;
                            await _ruleStore.UpdateRuleAsync(rule);
                            result.Success = true;
                            response.SuccessCount++;
                            break;
                            
                        case "delete":
                            var deleted = await _ruleStore.DeleteRuleAsync(ruleId);
                            result.Success = deleted;
                            if (deleted)
                                response.SuccessCount++;
                            else
                            {
                                result.ErrorMessage = "Failed to delete rule";
                                response.FailureCount++;
                            }
                            break;
                            
                        case "validate":
                            var validationResult = ValidateYaraRule(rule.RuleContent);
                            rule.IsValid = validationResult.IsValid;
                            rule.ValidationError = validationResult.Error;
                            rule.LastValidated = DateTime.UtcNow;
                            await _ruleStore.UpdateRuleAsync(rule);
                            result.Success = true;
                            response.SuccessCount++;
                            break;
                            
                        default:
                            result.Success = false;
                            result.ErrorMessage = $"Unknown operation: {request.Operation}";
                            response.FailureCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    response.FailureCount++;
                    _logger.LogError(ex, "Error in bulk operation {Operation} for rule {RuleId}", 
                        request.Operation, ruleId);
                }
                
                response.Results.Add(result);
            }
            
            _logger.LogInformation("Bulk operation {Operation} completed: {SuccessCount} successful, {FailureCount} failed", 
                request.Operation, response.SuccessCount, response.FailureCount);
            
            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk operation");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Import YARA rules from raw content
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportRules([FromBody] YaraImportRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var response = new YaraImportResponse();
            var rules = ParseYaraRules(request.RuleContent);
            
            foreach (var parsedRule in rules)
            {
                var result = new YaraImportResult
                {
                    RuleName = parsedRule.Name
                };
                
                try
                {
                    // Check if rule already exists
                    if (await _ruleStore.RuleExistsAsync(parsedRule.Name))
                    {
                        if (request.SkipDuplicates)
                        {
                            result.Status = "skipped";
                            result.ErrorMessage = "Rule already exists";
                            response.SkippedCount++;
                            response.Results.Add(result);
                            continue;
                        }
                        else
                        {
                            result.Status = "failed";
                            result.ErrorMessage = "Rule already exists and SkipDuplicates is false";
                            response.FailedCount++;
                            response.Results.Add(result);
                            continue;
                        }
                    }
                    
                    // Validate rule syntax
                    var validationResult = ValidateYaraRule(parsedRule.RuleContent);
                    if (!validationResult.IsValid)
                    {
                        result.Status = "failed";
                        result.ErrorMessage = $"Validation failed: {validationResult.Error}";
                        response.FailedCount++;
                        response.Results.Add(result);
                        continue;
                    }
                    
                    // Create and import rule
                    var rule = new YaraRule
                    {
                        Name = parsedRule.Name,
                        Description = parsedRule.Description ?? $"Imported rule: {parsedRule.Name}",
                        RuleContent = parsedRule.RuleContent,
                        Category = request.Category,
                        Author = request.Author,
                        Source = request.Source,
                        IsEnabled = request.EnableByDefault,
                        IsValid = true,
                        Tags = parsedRule.Tags ?? new List<string>(),
                        MitreTechniques = parsedRule.MitreTechniques ?? new List<string>()
                    };
                    
                    var createdRule = await _ruleStore.AddRuleAsync(rule);
                    
                    result.RuleId = createdRule.Id;
                    result.Status = "imported";
                    response.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.Status = "failed";
                    result.ErrorMessage = ex.Message;
                    response.FailedCount++;
                    _logger.LogError(ex, "Error importing rule: {RuleName}", parsedRule.Name);
                }
                
                response.Results.Add(result);
            }
            
            _logger.LogInformation("Import completed: {ImportedCount} imported, {SkippedCount} skipped, {FailedCount} failed", 
                response.ImportedCount, response.SkippedCount, response.FailedCount);
            
            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rule import");
            return StatusCode(500, new { message = "Internal server error during import" });
        }
    }
    
    /// <summary>
    /// Export YARA rules
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> ExportRules([FromBody] YaraExportRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            IEnumerable<YaraRule> rulesToExport;
            
            if (request.RuleIds.Any())
            {
                var rules = new List<YaraRule>();
                foreach (var ruleId in request.RuleIds)
                {
                    var rule = await _ruleStore.GetRuleByIdAsync(ruleId);
                    if (rule != null)
                    {
                        rules.Add(rule);
                    }
                }
                rulesToExport = rules;
            }
            else
            {
                rulesToExport = request.IncludeDisabled 
                    ? await _ruleStore.GetAllRulesAsync()
                    : await _ruleStore.GetEnabledRulesAsync();
            }
            
            var exportContent = GenerateExportContent(rulesToExport, request.Format, request.IncludeMetadata);
            var fileName = $"yara_rules_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            switch (request.Format.ToLowerInvariant())
            {
                case "json":
                    fileName += ".json";
                    return File(System.Text.Encoding.UTF8.GetBytes(exportContent), "application/json", fileName);
                    
                case "raw":
                case "combined":
                default:
                    fileName += ".yar";
                    return File(System.Text.Encoding.UTF8.GetBytes(exportContent), "text/plain", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rule export");
            return StatusCode(500, new { message = "Internal server error during export" });
        }
    }
    
    /// <summary>
    /// Get YARA rule statistics and analytics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var allRules = (await _ruleStore.GetAllRulesAsync()).ToList();
            
            var statistics = new YaraRuleStatistics
            {
                TotalRules = allRules.Count,
                EnabledRules = allRules.Count(r => r.IsEnabled),
                DisabledRules = allRules.Count(r => !r.IsEnabled),
                ValidRules = allRules.Count(r => r.IsValid),
                InvalidRules = allRules.Count(r => !r.IsValid),
                
                RulesByCategory = allRules
                    .GroupBy(r => r.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                    
                RulesByThreatLevel = allRules
                    .GroupBy(r => r.ThreatLevel)
                    .ToDictionary(g => g.Key, g => g.Count()),
                
                TopPerformingRules = allRules
                    .Where(r => r.HitCount > 0)
                    .OrderByDescending(r => CalculateEffectivenessScore(r))
                    .Take(10)
                    .Select(r => new YaraRulePerformance
                    {
                        RuleId = r.Id,
                        RuleName = r.Name,
                        HitCount = r.HitCount,
                        FalsePositiveCount = r.FalsePositiveCount,
                        AverageExecutionTimeMs = r.AverageExecutionTimeMs,
                        EffectivenessScore = CalculateEffectivenessScore(r)
                    })
                    .ToList(),
                    
                SlowestRules = allRules
                    .Where(r => r.AverageExecutionTimeMs > 0)
                    .OrderByDescending(r => r.AverageExecutionTimeMs)
                    .Take(10)
                    .Select(r => new YaraRulePerformance
                    {
                        RuleId = r.Id,
                        RuleName = r.Name,
                        HitCount = r.HitCount,
                        FalsePositiveCount = r.FalsePositiveCount,
                        AverageExecutionTimeMs = r.AverageExecutionTimeMs,
                        EffectivenessScore = CalculateEffectivenessScore(r)
                    })
                    .ToList()
            };
            
            return Ok(new { data = statistics });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YARA statistics");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Get match analytics
    /// </summary>
    [HttpPost("analytics/matches")]
    public async Task<IActionResult> GetMatchAnalytics([FromBody] YaraMatchAnalyticsRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            // Get recent matches (limited implementation for now)
            var allMatches = (await _ruleStore.GetRecentMatchesAsync(request.Limit * 2)).ToList();
            
            // Apply date filters
            if (request.StartDate.HasValue)
            {
                allMatches = allMatches.Where(m => m.MatchTime >= request.StartDate.Value).ToList();
            }
            if (request.EndDate.HasValue)
            {
                allMatches = allMatches.Where(m => m.MatchTime <= request.EndDate.Value).ToList();
            }
            
            // Apply rule filters
            if (request.RuleIds?.Any() == true)
            {
                allMatches = allMatches.Where(m => request.RuleIds.Contains(m.RuleId)).ToList();
            }
            
            var response = new YaraMatchAnalyticsResponse
            {
                TotalMatches = allMatches.Count,
                
                Trends = GenerateMatchTrends(allMatches, request.GroupBy ?? "day"),
                
                RuleEffectiveness = await GenerateRuleEffectiveness(allMatches),
                
                MatchesByCategory = await GenerateMatchesByCategory(allMatches)
            };
            
            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting match analytics");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Get system health status
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetSystemHealth()
    {
        try
        {
            var allRules = (await _ruleStore.GetAllRulesAsync()).ToList();
            var unhealthyRules = new List<YaraRuleHealth>();
            
            var healthyCount = 0;
            var compilationTimes = new List<double>();
            
            foreach (var rule in allRules)
            {
                var ruleHealth = new YaraRuleHealth
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    IsValid = rule.IsValid,
                    ValidationError = rule.ValidationError,
                    LastValidated = rule.LastValidated ?? DateTime.MinValue
                };
                
                // Determine health status
                if (!rule.IsValid)
                {
                    ruleHealth.HealthStatus = "Critical";
                    ruleHealth.Warnings.Add("Rule validation failed");
                    unhealthyRules.Add(ruleHealth);
                }
                else if (rule.FalsePositiveCount > rule.HitCount * 0.5) // More than 50% false positives
                {
                    ruleHealth.HealthStatus = "Warning";
                    ruleHealth.Warnings.Add($"High false positive rate: {rule.FalsePositiveCount}/{rule.HitCount}");
                    unhealthyRules.Add(ruleHealth);
                }
                else if (!rule.LastValidated.HasValue || rule.LastValidated < DateTime.UtcNow.AddDays(-30))
                {
                    ruleHealth.HealthStatus = "Warning";
                    ruleHealth.Warnings.Add("Rule not validated in the last 30 days");
                    unhealthyRules.Add(ruleHealth);
                }
                else
                {
                    ruleHealth.HealthStatus = "Healthy";
                    healthyCount++;
                }
                
                // Mock compilation time for now
                ruleHealth.CompilationTimeMs = rule.AverageExecutionTimeMs > 0 ? rule.AverageExecutionTimeMs : 5.0;
                ruleHealth.IsCompiled = rule.IsValid;
                compilationTimes.Add(ruleHealth.CompilationTimeMs);
            }
            
            var systemHealth = new YaraSystemHealth
            {
                IsHealthy = unhealthyRules.Count == 0,
                CompiledRulesCount = allRules.Count(r => r.IsValid),
                HealthyRulesCount = healthyCount,
                UnhealthyRulesCount = unhealthyRules.Count,
                AverageCompilationTime = compilationTimes.Any() ? compilationTimes.Average() : 0,
                LastCompilation = DateTime.UtcNow, // Mock value
                UnhealthyRules = unhealthyRules.Take(20).ToList() // Limit to top 20 unhealthy rules
            };
            
            if (_yaraScanService != null)
            {
                systemHealth.SystemError = _yaraScanService.LastError;
            }
            
            return Ok(new { data = systemHealth });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system health");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
    
    /// <summary>
    /// Refresh compiled rules
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshRules()
    {
        try
        {
            if (_yaraScanService == null)
            {
                return BadRequest(new { message = "YARA scanning service is not available" });
            }
            
            await _yaraScanService.RefreshRulesAsync();
            
            _logger.LogInformation("YARA rules refreshed successfully");
            
            return Ok(new 
            { 
                message = "Rules refreshed successfully",
                compiledRules = _yaraScanService.GetCompiledRuleCount(),
                refreshedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing YARA rules");
            return StatusCode(500, new { message = "Internal server error during refresh" });
        }
    }
}
