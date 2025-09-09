using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

/// <summary>
/// File-based implementation of YARA rule storage
/// </summary>
public class FileBasedYaraRuleStore : IYaraRuleStore
{
    private readonly ILogger<FileBasedYaraRuleStore> _logger;
    private readonly string _rulesDirectory;
    private readonly string _rulesFilePath;
    private readonly string _matchesFilePath;
    private readonly object _lock = new object();
    
    public FileBasedYaraRuleStore(ILogger<FileBasedYaraRuleStore> logger)
    {
        _logger = logger;
        _rulesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "yara");
        _rulesFilePath = Path.Combine(_rulesDirectory, "rules.json");
        _matchesFilePath = Path.Combine(_rulesDirectory, "matches.json");
        
        // Ensure directory exists
        Directory.CreateDirectory(_rulesDirectory);
        
        // Initialize files if they don't exist
        if (!File.Exists(_rulesFilePath))
        {
            SaveRules(new List<YaraRule>());
        }
        
        if (!File.Exists(_matchesFilePath))
        {
            SaveMatches(new List<YaraMatch>());
        }
    }
    
    public Task<IEnumerable<YaraRule>> GetAllRulesAsync()
    {
        lock (_lock)
        {
            var rules = LoadRules();
            return Task.FromResult<IEnumerable<YaraRule>>(rules);
        }
    }
    
    public Task<IEnumerable<YaraRule>> GetEnabledRulesAsync()
    {
        lock (_lock)
        {
            var rules = LoadRules().Where(r => r.IsEnabled);
            return Task.FromResult<IEnumerable<YaraRule>>(rules);
        }
    }
    
    public Task<YaraRule?> GetRuleByIdAsync(string id)
    {
        lock (_lock)
        {
            var rule = LoadRules().FirstOrDefault(r => r.Id == id);
            return Task.FromResult(rule);
        }
    }
    
    public Task<IEnumerable<YaraRule>> GetRulesByCategoryAsync(string category)
    {
        lock (_lock)
        {
            var rules = LoadRules().Where(r => r.Category == category);
            return Task.FromResult<IEnumerable<YaraRule>>(rules);
        }
    }
    
    public Task<YaraRule> AddRuleAsync(YaraRule rule)
    {
        lock (_lock)
        {
            var rules = LoadRules().ToList();
            
            // Ensure unique ID
            if (string.IsNullOrEmpty(rule.Id))
            {
                rule.Id = Guid.NewGuid().ToString();
            }
            
            // Set timestamps
            rule.CreatedAt = DateTime.UtcNow;
            rule.UpdatedAt = DateTime.UtcNow;
            
            rules.Add(rule);
            SaveRules(rules);
            
            _logger.LogInformation("Added YARA rule: {RuleName} ({RuleId})", rule.Name, rule.Id);
            return Task.FromResult(rule);
        }
    }
    
    public Task<YaraRule> UpdateRuleAsync(YaraRule rule)
    {
        lock (_lock)
        {
            var rules = LoadRules().ToList();
            var existingIndex = rules.FindIndex(r => r.Id == rule.Id);
            
            if (existingIndex == -1)
            {
                throw new InvalidOperationException($"Rule with ID {rule.Id} not found");
            }
            
            // Update timestamp
            rule.UpdatedAt = DateTime.UtcNow;
            
            // Increment version
            var existing = rules[existingIndex];
            rule.Version = existing.Version + 1;
            rule.PreviousVersion = JsonSerializer.Serialize(existing);
            
            rules[existingIndex] = rule;
            SaveRules(rules);
            
            _logger.LogInformation("Updated YARA rule: {RuleName} ({RuleId})", rule.Name, rule.Id);
            return Task.FromResult(rule);
        }
    }
    
    public Task<bool> DeleteRuleAsync(string id)
    {
        lock (_lock)
        {
            var rules = LoadRules().ToList();
            var removed = rules.RemoveAll(r => r.Id == id);
            
            if (removed > 0)
            {
                SaveRules(rules);
                _logger.LogInformation("Deleted YARA rule: {RuleId}", id);
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
    }
    
    public Task<bool> RuleExistsAsync(string name)
    {
        lock (_lock)
        {
            var exists = LoadRules().Any(r => r.Name == name);
            return Task.FromResult(exists);
        }
    }
    
    public Task UpdateRuleMetricsAsync(string ruleId, bool matched, double executionTimeMs)
    {
        lock (_lock)
        {
            var rules = LoadRules().ToList();
            var rule = rules.FirstOrDefault(r => r.Id == ruleId);
            
            if (rule != null)
            {
                if (matched)
                {
                    rule.HitCount++;
                }
                
                // Update average execution time
                var totalExecutions = rule.HitCount + (matched ? 0 : 1);
                rule.AverageExecutionTimeMs = 
                    ((rule.AverageExecutionTimeMs * (totalExecutions - 1)) + executionTimeMs) / totalExecutions;
                
                SaveRules(rules);
            }
            
            return Task.CompletedTask;
        }
    }
    
    public Task RecordFalsePositiveAsync(string ruleId)
    {
        lock (_lock)
        {
            var rules = LoadRules().ToList();
            var rule = rules.FirstOrDefault(r => r.Id == ruleId);
            
            if (rule != null)
            {
                rule.FalsePositiveCount++;
                SaveRules(rules);
                _logger.LogWarning("False positive recorded for rule: {RuleName} ({RuleId})", 
                    rule.Name, rule.Id);
            }
            
            return Task.CompletedTask;
        }
    }
    
    public Task<IEnumerable<YaraRule>> GetRulesByMitreTechniqueAsync(string techniqueId)
    {
        lock (_lock)
        {
            var rules = LoadRules()
                .Where(r => r.MitreTechniques != null && r.MitreTechniques.Contains(techniqueId));
            return Task.FromResult<IEnumerable<YaraRule>>(rules);
        }
    }
    
    public Task<IEnumerable<YaraRule>> GetRulesByTagAsync(string tag)
    {
        lock (_lock)
        {
            var rules = LoadRules()
                .Where(r => r.Tags != null && r.Tags.Contains(tag));
            return Task.FromResult<IEnumerable<YaraRule>>(rules);
        }
    }
    
    public Task<YaraMatch> SaveMatchAsync(YaraMatch match)
    {
        lock (_lock)
        {
            var matches = LoadMatches().ToList();
            
            // Ensure unique ID
            if (string.IsNullOrEmpty(match.Id))
            {
                match.Id = Guid.NewGuid().ToString();
            }
            
            match.MatchTime = DateTime.UtcNow;
            matches.Add(match);
            
            // Keep only recent matches (last 1000)
            if (matches.Count > 1000)
            {
                matches = matches.OrderByDescending(m => m.MatchTime).Take(1000).ToList();
            }
            
            SaveMatches(matches);
            
            _logger.LogInformation("YARA rule matched: {RuleName} on {TargetFile}", 
                match.RuleName, match.TargetFile);
            
            return Task.FromResult(match);
        }
    }
    
    public Task<IEnumerable<YaraMatch>> GetRecentMatchesAsync(int count = 100)
    {
        lock (_lock)
        {
            var matches = LoadMatches()
                .OrderByDescending(m => m.MatchTime)
                .Take(count);
            return Task.FromResult<IEnumerable<YaraMatch>>(matches);
        }
    }
    
    public Task<IEnumerable<YaraMatch>> GetMatchesBySecurityEventAsync(string securityEventId)
    {
        lock (_lock)
        {
            var matches = LoadMatches()
                .Where(m => m.SecurityEventId == securityEventId);
            return Task.FromResult<IEnumerable<YaraMatch>>(matches);
        }
    }
    
    private List<YaraRule> LoadRules()
    {
        try
        {
            if (File.Exists(_rulesFilePath))
            {
                var json = File.ReadAllText(_rulesFilePath);
                return JsonSerializer.Deserialize<List<YaraRule>>(json) ?? new List<YaraRule>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading YARA rules from file");
        }
        
        return new List<YaraRule>();
    }
    
    private void SaveRules(List<YaraRule> rules)
    {
        try
        {
            var json = JsonSerializer.Serialize(rules, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_rulesFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving YARA rules to file");
            throw;
        }
    }
    
    private List<YaraMatch> LoadMatches()
    {
        try
        {
            if (File.Exists(_matchesFilePath))
            {
                var json = File.ReadAllText(_matchesFilePath);
                return JsonSerializer.Deserialize<List<YaraMatch>>(json) ?? new List<YaraMatch>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading YARA matches from file");
        }
        
        return new List<YaraMatch>();
    }
    
    private void SaveMatches(List<YaraMatch> matches)
    {
        try
        {
            var json = JsonSerializer.Serialize(matches, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_matchesFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving YARA matches to file");
            throw;
        }
    }
}
