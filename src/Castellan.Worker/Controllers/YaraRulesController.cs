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
    
    public YaraRulesController(
        ILogger<YaraRulesController> logger,
        IYaraRuleStore ruleStore)
    {
        _logger = logger;
        _ruleStore = ruleStore;
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
            
            if (!string.IsNullOrEmpty(securityEventId))
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
    
    private YaraRuleDto ConvertToDto(YaraRule rule)
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
            MitreTechniques = rule.MitreTechniques,
            Tags = rule.Tags,
            IsValid = rule.IsValid,
            ValidationError = rule.ValidationError,
            Source = rule.Source
        };
    }
    
    private (bool IsValid, string? Error) ValidateYaraRule(string ruleContent)
    {
        // Basic YARA syntax validation
        // TODO: Use actual YARA compiler for validation once YaraScannerService is implemented
        
        if (string.IsNullOrWhiteSpace(ruleContent))
        {
            return (false, "Rule content cannot be empty");
        }
        
        // Check for basic YARA rule structure
        if (!ruleContent.Contains("rule "))
        {
            return (false, "Invalid YARA rule: missing 'rule' keyword");
        }
        
        if (!ruleContent.Contains("{") || !ruleContent.Contains("}"))
        {
            return (false, "Invalid YARA rule: missing braces");
        }
        
        if (!ruleContent.Contains("condition:"))
        {
            return (false, "Invalid YARA rule: missing 'condition' section");
        }
        
        return (true, null);
    }
}
