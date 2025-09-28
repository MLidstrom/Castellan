using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Castellan.Worker.Controllers;
using Castellan.Worker.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Castellan.Worker.Abstractions;

namespace Castellan.Tests.Controllers;

/// <summary>
/// Tests for YARA rule validation and DTO conversion in YaraRulesController
/// </summary>
[Collection("TestEnvironment")]
public class YaraRulesController_ValidationTests : IDisposable
{
    private readonly Mock<ILogger<YaraRulesController>> _mockLogger;
    private readonly Mock<IYaraRuleStore> _mockRuleStore;
    private readonly YaraRulesController _controller;

    public YaraRulesController_ValidationTests()
    {
        _mockLogger = new Mock<ILogger<YaraRulesController>>();
        _mockRuleStore = new Mock<IYaraRuleStore>();
        _controller = new YaraRulesController(_mockLogger.Object, _mockRuleStore.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    #region YARA Rule Validation Tests

    [Theory]
    [InlineData("", false, "Rule content cannot be empty")]
    [InlineData("   ", false, "Rule content cannot be empty")]
    [InlineData(null, false, "Rule content cannot be empty")]
    public void ValidateYaraRule_EmptyOrNullContent_ReturnsFalse(string? ruleContent, bool expectedValid, string expectedError)
    {
        // Act
        var result = InvokeValidateYaraRule(ruleContent);

        // Assert
        result.IsValid.Should().Be(expectedValid);
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ValidateYaraRule_MissingRuleKeyword_ReturnsFalse()
    {
        // Arrange
        var ruleContent = "badstuff TestName { condition: true }";

        // Act
        var result = InvokeValidateYaraRule(ruleContent);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Invalid YARA rule: missing 'rule' keyword");
    }

    [Theory]
    [InlineData("rule TestRule condition: true }", false, "Invalid YARA rule: missing braces")]
    [InlineData("rule TestRule { condition: true", false, "Invalid YARA rule: missing braces")]
    [InlineData("rule TestRule condition: true", false, "Invalid YARA rule: missing braces")]
    public void ValidateYaraRule_MissingBraces_ReturnsFalse(string ruleContent, bool expectedValid, string expectedError)
    {
        // Act
        var result = InvokeValidateYaraRule(ruleContent);

        // Assert
        result.IsValid.Should().Be(expectedValid);
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ValidateYaraRule_MissingCondition_ReturnsFalse()
    {
        // Arrange
        var ruleContent = "rule TestRule { strings: $a = \"test\" }";

        // Act
        var result = InvokeValidateYaraRule(ruleContent);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Invalid YARA rule: missing 'condition' section");
    }

    [Theory]
    [InlineData("rule TestRule { condition: true }")]
    [InlineData("rule ComplexRule { strings: $a = \"malware\" $b = /regex/ condition: $a or $b }")]
    [InlineData(@"rule MultilineRule { 
                    meta:
                        description = ""Test rule""
                        author = ""Test""
                    strings:
                        $string1 = ""pattern1""
                        $string2 = {01 02 03 04}
                    condition:
                        any of them
                }")]
    public void ValidateYaraRule_ValidRuleContent_ReturnsTrue(string ruleContent)
    {
        // Act
        var result = InvokeValidateYaraRule(ruleContent);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ValidateYaraRule_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var ruleContent = "RULE TestRule { CONDITION: true }";

        // Act
        var result = InvokeValidateYaraRule(ruleContent);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    #endregion

    #region DTO Conversion Tests

    [Fact]
    public void ConvertToDto_CompleteYaraRule_MapsAllProperties()
    {
        // Arrange
        var yaraRule = new YaraRule
        {
            Id = "test-rule-id",
            Name = "TestRule",
            Description = "Test rule description",
            RuleContent = "rule TestRule { condition: true }",
            Category = YaraRuleCategory.Malware,
            Author = "Test Author",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            IsEnabled = true,
            Priority = 75,
            ThreatLevel = "High",
            HitCount = 10,
            FalsePositiveCount = 2,
            AverageExecutionTimeMs = 15.5,
            MitreTechniques = new List<string> { "T1059.001", "T1027" },
            Tags = new List<string> { "powershell", "obfuscation" },
            IsValid = true,
            ValidationError = null,
            Source = "Custom"
        };

        // Act
        var dto = InvokeConvertToDto(yaraRule);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be("test-rule-id");
        dto.Name.Should().Be("TestRule");
        dto.Description.Should().Be("Test rule description");
        dto.RuleContent.Should().Be("rule TestRule { condition: true }");
        dto.Category.Should().Be(YaraRuleCategory.Malware);
        dto.Author.Should().Be("Test Author");
        dto.CreatedAt.Should().Be(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        dto.UpdatedAt.Should().Be(new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc));
        dto.IsEnabled.Should().BeTrue();
        dto.Priority.Should().Be(75);
        dto.ThreatLevel.Should().Be("High");
        dto.HitCount.Should().Be(10);
        dto.FalsePositiveCount.Should().Be(2);
        dto.AverageExecutionTimeMs.Should().Be(15.5);
        dto.MitreTechniques.Should().BeEquivalentTo(new[] { "T1059.001", "T1027" });
        dto.Tags.Should().BeEquivalentTo(new[] { "powershell", "obfuscation" });
        dto.IsValid.Should().BeTrue();
        dto.ValidationError.Should().BeNull();
        dto.Source.Should().Be("Custom");
    }

    [Fact]
    public void ConvertToDto_YaraRuleWithValidationError_IncludesError()
    {
        // Arrange
        var yaraRule = new YaraRule
        {
            Id = "invalid-rule-id",
            Name = "InvalidRule",
            Description = "Invalid rule",
            RuleContent = "invalid rule content",
            IsValid = false,
            ValidationError = "Syntax error on line 1"
        };

        // Act
        var dto = InvokeConvertToDto(yaraRule);

        // Assert
        dto.IsValid.Should().BeFalse();
        dto.ValidationError.Should().Be("Syntax error on line 1");
    }

    [Fact]
    public void ConvertToDto_YaraRuleWithNullCollections_HandlesGracefully()
    {
        // Arrange
        var yaraRule = new YaraRule
        {
            Id = "test-rule-id",
            Name = "TestRule",
            Description = "Test rule",
            RuleContent = "rule TestRule { condition: true }",
            MitreTechniques = null!, // Null collection
            Tags = null! // Null collection
        };

        // Act
        var dto = InvokeConvertToDto(yaraRule);

        // Assert
        dto.Should().NotBeNull();
        dto.MitreTechniques.Should().NotBeNull();
        dto.Tags.Should().NotBeNull();
    }

    [Fact]
    public void ConvertToDto_MinimalYaraRule_MapsDefaults()
    {
        // Arrange
        var yaraRule = new YaraRule(); // Use all defaults

        // Act
        var dto = InvokeConvertToDto(yaraRule);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().NotBeNullOrEmpty(); // Should have auto-generated ID
        dto.Name.Should().BeEmpty();
        dto.Description.Should().BeEmpty();
        dto.Category.Should().Be("General");
        dto.Author.Should().Be("System");
        dto.IsEnabled.Should().BeTrue();
        dto.Priority.Should().Be(50);
        dto.ThreatLevel.Should().Be("Medium");
        dto.HitCount.Should().Be(0);
        dto.FalsePositiveCount.Should().Be(0);
        dto.AverageExecutionTimeMs.Should().Be(0.0);
        dto.IsValid.Should().BeFalse(); // Default is false
        dto.Source.Should().Be("Custom");
    }

    [Fact]
    public void ConvertToDto_PerformanceMetrics_MapsCorrectly()
    {
        // Arrange
        var yaraRule = new YaraRule
        {
            Id = "perf-test-rule",
            Name = "PerformanceRule",
            HitCount = 1000,
            FalsePositiveCount = 25,
            AverageExecutionTimeMs = 123.456
        };

        // Act
        var dto = InvokeConvertToDto(yaraRule);

        // Assert
        dto.HitCount.Should().Be(1000);
        dto.FalsePositiveCount.Should().Be(25);
        dto.AverageExecutionTimeMs.Should().Be(123.456);
    }

    #endregion

    
    #region Helper Methods for Private Method Testing

    private (bool IsValid, string? Error) InvokeValidateYaraRule(string? ruleContent)
    {
        return _controller.ValidateYaraRule(ruleContent ?? "");
    }

    private YaraRuleDto InvokeConvertToDto(YaraRule rule)
    {
        return _controller.ConvertToDto(rule);
    }

    #endregion
}
