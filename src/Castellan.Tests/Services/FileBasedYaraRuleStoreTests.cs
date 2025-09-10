using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Castellan.Worker.Services;
using Castellan.Worker.Models;
using FluentAssertions;

namespace Castellan.Tests.Services;

[Collection("TestEnvironment")]
public class FileBasedYaraRuleStoreTests : IDisposable
{
    private readonly Mock<ILogger<FileBasedYaraRuleStore>> _mockLogger;
    private readonly string _testDirectory;
    private readonly FileBasedYaraRuleStore _store;

    public FileBasedYaraRuleStoreTests()
    {
        _mockLogger = new Mock<ILogger<FileBasedYaraRuleStore>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), "castellan-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        // Create a store that uses our test directory
        _store = new FileBasedYaraRuleStore(_mockLogger.Object);
        
        // Use reflection to set the private fields to our test directory
        var type = typeof(FileBasedYaraRuleStore);
        var rulesDirectoryField = type.GetField("_rulesDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rulesFilePathField = type.GetField("_rulesFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var matchesFilePathField = type.GetField("_matchesFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        rulesDirectoryField?.SetValue(_store, _testDirectory);
        rulesFilePathField?.SetValue(_store, Path.Combine(_testDirectory, "rules.json"));
        matchesFilePathField?.SetValue(_store, Path.Combine(_testDirectory, "matches.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void Constructor_ValidLogger_CreatesStore()
    {
        // Arrange & Act
        var store = new FileBasedYaraRuleStore(_mockLogger.Object);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task AddRuleAsync_ValidRule_ReturnsRuleWithId()
    {
        // Arrange
        var rule = CreateTestYaraRule("TestRule", "Test description");

        // Act
        var result = await _store.AddRuleAsync(rule);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.Name.Should().Be("TestRule");
        result.Description.Should().Be("Test description");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAllRulesAsync_EmptyStore_ReturnsEmptyCollection()
    {
        // Act
        var rules = await _store.GetAllRulesAsync();

        // Assert
        rules.Should().NotBeNull();
        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRulesAsync_WithRules_ReturnsAllRules()
    {
        // Arrange
        var rule1 = CreateTestYaraRule("Rule1", "First rule");
        var rule2 = CreateTestYaraRule("Rule2", "Second rule");
        await _store.AddRuleAsync(rule1);
        await _store.AddRuleAsync(rule2);

        // Act
        var rules = await _store.GetAllRulesAsync();

        // Assert
        rules.Should().HaveCount(2);
        rules.Select(r => r.Name).Should().Contain("Rule1", "Rule2");
    }

    [Fact]
    public async Task GetRuleByIdAsync_ExistingRule_ReturnsRule()
    {
        // Arrange
        var rule = CreateTestYaraRule("TestRule", "Test description");
        var addedRule = await _store.AddRuleAsync(rule);

        // Act
        var retrievedRule = await _store.GetRuleByIdAsync(addedRule.Id);

        // Assert
        retrievedRule.Should().NotBeNull();
        retrievedRule!.Id.Should().Be(addedRule.Id);
        retrievedRule.Name.Should().Be("TestRule");
    }

    [Fact]
    public async Task GetRuleByIdAsync_NonExistentRule_ReturnsNull()
    {
        // Act
        var rule = await _store.GetRuleByIdAsync("non-existent-id");

        // Assert
        rule.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRuleAsync_ExistingRule_UpdatesRule()
    {
        // Arrange
        var rule = CreateTestYaraRule("OriginalName", "Original description");
        var addedRule = await _store.AddRuleAsync(rule);
        
        addedRule.Name = "UpdatedName";
        addedRule.Description = "Updated description";

        // Act
        var updatedRule = await _store.UpdateRuleAsync(addedRule);

        // Assert
        updatedRule.Should().NotBeNull();
        updatedRule.Name.Should().Be("UpdatedName");
        updatedRule.Description.Should().Be("Updated description");
        updatedRule.Version.Should().Be(2); // Should increment version
        updatedRule.UpdatedAt.Should().BeAfter(updatedRule.CreatedAt);
    }

    [Fact]
    public async Task UpdateRuleAsync_NonExistentRule_ThrowsException()
    {
        // Arrange
        var rule = CreateTestYaraRule("NonExistent", "Description");
        rule.Id = "non-existent-id";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.UpdateRuleAsync(rule));
    }

    [Fact]
    public async Task DeleteRuleAsync_ExistingRule_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestYaraRule("TestRule", "Test description");
        var addedRule = await _store.AddRuleAsync(rule);

        // Act
        var result = await _store.DeleteRuleAsync(addedRule.Id);

        // Assert
        result.Should().BeTrue();
        
        // Verify rule is actually deleted
        var deletedRule = await _store.GetRuleByIdAsync(addedRule.Id);
        deletedRule.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRuleAsync_NonExistentRule_ReturnsFalse()
    {
        // Act
        var result = await _store.DeleteRuleAsync("non-existent-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RuleExistsAsync_ExistingRule_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestYaraRule("TestRule", "Test description");
        await _store.AddRuleAsync(rule);

        // Act
        var exists = await _store.RuleExistsAsync("TestRule");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RuleExistsAsync_NonExistentRule_ReturnsFalse()
    {
        // Act
        var exists = await _store.RuleExistsAsync("NonExistentRule");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetEnabledRulesAsync_MixedEnabledStatus_ReturnsOnlyEnabled()
    {
        // Arrange
        var enabledRule = CreateTestYaraRule("EnabledRule", "Enabled rule");
        enabledRule.IsEnabled = true;
        var disabledRule = CreateTestYaraRule("DisabledRule", "Disabled rule");
        disabledRule.IsEnabled = false;
        
        await _store.AddRuleAsync(enabledRule);
        await _store.AddRuleAsync(disabledRule);

        // Act
        var enabledRules = await _store.GetEnabledRulesAsync();

        // Assert
        enabledRules.Should().HaveCount(1);
        enabledRules.First().Name.Should().Be("EnabledRule");
    }

    [Fact]
    public async Task GetRulesByCategoryAsync_ExistingCategory_ReturnsMatchingRules()
    {
        // Arrange
        var malwareRule = CreateTestYaraRule("MalwareRule", "Malware detection");
        malwareRule.Category = YaraRuleCategory.Malware;
        var trojanRule = CreateTestYaraRule("TrojanRule", "Trojan detection");
        trojanRule.Category = YaraRuleCategory.Trojan;
        
        await _store.AddRuleAsync(malwareRule);
        await _store.AddRuleAsync(trojanRule);

        // Act
        var malwareRules = await _store.GetRulesByCategoryAsync(YaraRuleCategory.Malware);

        // Assert
        malwareRules.Should().HaveCount(1);
        malwareRules.First().Name.Should().Be("MalwareRule");
    }

    [Fact]
    public async Task GetRulesByTagAsync_ExistingTag_ReturnsMatchingRules()
    {
        // Arrange
        var rule1 = CreateTestYaraRule("Rule1", "First rule");
        rule1.Tags = new List<string> { "powershell", "suspicious" };
        var rule2 = CreateTestYaraRule("Rule2", "Second rule");
        rule2.Tags = new List<string> { "malware", "trojan" };
        var rule3 = CreateTestYaraRule("Rule3", "Third rule");
        rule3.Tags = new List<string> { "powershell", "legitimate" };
        
        await _store.AddRuleAsync(rule1);
        await _store.AddRuleAsync(rule2);
        await _store.AddRuleAsync(rule3);

        // Act
        var powershellRules = await _store.GetRulesByTagAsync("powershell");

        // Assert
        powershellRules.Should().HaveCount(2);
        powershellRules.Select(r => r.Name).Should().Contain("Rule1", "Rule3");
    }

    [Fact]
    public async Task GetRulesByMitreTechniqueAsync_ExistingTechnique_ReturnsMatchingRules()
    {
        // Arrange
        var rule1 = CreateTestYaraRule("Rule1", "First rule");
        rule1.MitreTechniques = new List<string> { "T1059.001", "T1027" };
        var rule2 = CreateTestYaraRule("Rule2", "Second rule");
        rule2.MitreTechniques = new List<string> { "T1055", "T1027" };
        var rule3 = CreateTestYaraRule("Rule3", "Third rule");
        rule3.MitreTechniques = new List<string> { "T1078" };
        
        await _store.AddRuleAsync(rule1);
        await _store.AddRuleAsync(rule2);
        await _store.AddRuleAsync(rule3);

        // Act
        var t1027Rules = await _store.GetRulesByMitreTechniqueAsync("T1027");

        // Assert
        t1027Rules.Should().HaveCount(2);
        t1027Rules.Select(r => r.Name).Should().Contain("Rule1", "Rule2");
    }

    [Fact]
    public async Task UpdateRuleMetricsAsync_ExistingRule_UpdatesMetrics()
    {
        // Arrange
        var rule = CreateTestYaraRule("TestRule", "Test description");
        var addedRule = await _store.AddRuleAsync(rule);
        
        // Act
        await _store.UpdateRuleMetricsAsync(addedRule.Id, true, 15.5);
        
        // Assert
        var updatedRule = await _store.GetRuleByIdAsync(addedRule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.HitCount.Should().Be(1);
        updatedRule.AverageExecutionTimeMs.Should().Be(15.5);
    }

    [Fact]
    public async Task RecordFalsePositiveAsync_ExistingRule_IncrementsCount()
    {
        // Arrange
        var rule = CreateTestYaraRule("TestRule", "Test description");
        var addedRule = await _store.AddRuleAsync(rule);
        
        // Act
        await _store.RecordFalsePositiveAsync(addedRule.Id);
        
        // Assert
        var updatedRule = await _store.GetRuleByIdAsync(addedRule.Id);
        updatedRule.Should().NotBeNull();
        updatedRule!.FalsePositiveCount.Should().Be(1);
    }

    private static YaraRule CreateTestYaraRule(string name, string description)
    {
        return new YaraRule
        {
            Name = name,
            Description = description,
            RuleContent = $"rule {name} {{ condition: true }}",
            Category = YaraRuleCategory.Malware,
            Author = "Test Author",
            IsEnabled = true,
            Priority = 50,
            ThreatLevel = "Medium",
            MitreTechniques = new List<string> { "T1059.001" },
            Tags = new List<string> { "test" },
            IsValid = true,
            Source = "Test"
        };
    }
}
