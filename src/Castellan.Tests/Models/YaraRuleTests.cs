using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using Castellan.Worker.Models;
using FluentAssertions;

namespace Castellan.Tests.Models;

public class YaraRuleTests
{
    [Fact]
    public void YaraRule_DefaultConstructor_InitializesWithDefaultValues()
    {
        // Act
        var rule = new YaraRule();

        // Assert
        rule.Id.Should().NotBeNullOrEmpty();
        rule.Name.Should().BeEmpty();
        rule.Description.Should().BeEmpty();
        rule.RuleContent.Should().BeEmpty();
        rule.Category.Should().Be("General");
        rule.Author.Should().Be("System");
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        rule.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        rule.IsEnabled.Should().BeTrue();
        rule.Priority.Should().Be(50);
        rule.ThreatLevel.Should().Be("Medium");
        rule.HitCount.Should().Be(0);
        rule.FalsePositiveCount.Should().Be(0);
        rule.AverageExecutionTimeMs.Should().Be(0.0);
        rule.MitreTechniques.Should().NotBeNull().And.BeEmpty();
        rule.Tags.Should().NotBeNull().And.BeEmpty();
        rule.IsValid.Should().BeFalse(); // Default is false
        rule.Version.Should().Be(1);
        rule.Source.Should().Be("Custom");
    }

    [Fact]
    public void YaraRule_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testDateTime = DateTime.UtcNow.AddDays(-1);
        var testMitreTechniques = new List<string> { "T1059.001", "T1027" };
        var testTags = new List<string> { "powershell", "obfuscation" };

        // Act
        var rule = new YaraRule
        {
            Id = testId,
            Name = "TestRule",
            Description = "Test description",
            RuleContent = "rule TestRule { condition: true }",
            Category = YaraRuleCategory.Malware,
            Author = "Test Author",
            CreatedAt = testDateTime,
            UpdatedAt = testDateTime,
            IsEnabled = false,
            Priority = 75,
            ThreatLevel = "High",
            HitCount = 10,
            FalsePositiveCount = 2,
            AverageExecutionTimeMs = 15.5,
            MitreTechniques = testMitreTechniques,
            Tags = testTags,
            IsValid = false,
            ValidationError = "Test error",
            Version = 2,
            PreviousVersion = "{}",
            Source = "Community",
            TestSample = "Test sample"
        };

        // Assert
        rule.Id.Should().Be(testId);
        rule.Name.Should().Be("TestRule");
        rule.Description.Should().Be("Test description");
        rule.RuleContent.Should().Be("rule TestRule { condition: true }");
        rule.Category.Should().Be(YaraRuleCategory.Malware);
        rule.Author.Should().Be("Test Author");
        rule.CreatedAt.Should().Be(testDateTime);
        rule.UpdatedAt.Should().Be(testDateTime);
        rule.IsEnabled.Should().BeFalse();
        rule.Priority.Should().Be(75);
        rule.ThreatLevel.Should().Be("High");
        rule.HitCount.Should().Be(10);
        rule.FalsePositiveCount.Should().Be(2);
        rule.AverageExecutionTimeMs.Should().Be(15.5);
        rule.MitreTechniques.Should().BeEquivalentTo(testMitreTechniques);
        rule.Tags.Should().BeEquivalentTo(testTags);
        rule.IsValid.Should().BeFalse();
        rule.ValidationError.Should().Be("Test error");
        rule.Version.Should().Be(2);
        rule.PreviousVersion.Should().Be("{}");
        rule.Source.Should().Be("Community");
        rule.TestSample.Should().Be("Test sample");
    }

    [Fact]
    public void YaraMatch_DefaultConstructor_InitializesWithDefaultValues()
    {
        // Act
        var match = new YaraMatch();

        // Assert
        match.Id.Should().NotBeNullOrEmpty();
        match.RuleId.Should().BeEmpty();
        match.RuleName.Should().BeEmpty();
        match.MatchTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        match.TargetFile.Should().BeEmpty();
        match.TargetHash.Should().BeEmpty();
        match.MatchedStrings.Should().NotBeNull().And.BeEmpty();
        match.Metadata.Should().NotBeNull().And.BeEmpty();
        match.ExecutionTimeMs.Should().Be(0.0);
        match.SecurityEventId.Should().BeNull();
    }

    [Fact]
    public void YaraMatch_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testDateTime = DateTime.UtcNow.AddDays(-1);
        var testMatchedStrings = new List<YaraMatchString>
        {
            new YaraMatchString
            {
                Identifier = "$test1",
                Offset = 100,
                Value = "malicious_string",
                IsHex = false
            }
        };
        var testMetadata = new Dictionary<string, string>
        {
            { "severity", "high" },
            { "confidence", "90" }
        };

        // Act
        var match = new YaraMatch
        {
            Id = testId,
            RuleId = "rule-123",
            RuleName = "TestRule",
            MatchTime = testDateTime,
            TargetFile = "C:\\test\\malware.exe",
            TargetHash = "abcd1234",
            MatchedStrings = testMatchedStrings,
            Metadata = testMetadata,
            ExecutionTimeMs = 25.7,
            SecurityEventId = "event-456"
        };

        // Assert
        match.Id.Should().Be(testId);
        match.RuleId.Should().Be("rule-123");
        match.RuleName.Should().Be("TestRule");
        match.MatchTime.Should().Be(testDateTime);
        match.TargetFile.Should().Be("C:\\test\\malware.exe");
        match.TargetHash.Should().Be("abcd1234");
        match.MatchedStrings.Should().BeEquivalentTo(testMatchedStrings);
        match.Metadata.Should().BeEquivalentTo(testMetadata);
        match.ExecutionTimeMs.Should().Be(25.7);
        match.SecurityEventId.Should().Be("event-456");
    }

    [Fact]
    public void YaraMatchString_Properties_CanBeSetAndRetrieved()
    {
        // Act
        var matchString = new YaraMatchString
        {
            Identifier = "$suspicious_string",
            Offset = 512,
            Value = "malware_pattern",
            IsHex = true
        };

        // Assert
        matchString.Identifier.Should().Be("$suspicious_string");
        matchString.Offset.Should().Be(512);
        matchString.Value.Should().Be("malware_pattern");
        matchString.IsHex.Should().BeTrue();
    }

    [Fact]
    public void YaraRuleCategory_Constants_HaveExpectedValues()
    {
        // Assert
        YaraRuleCategory.Malware.Should().Be("Malware");
        YaraRuleCategory.Ransomware.Should().Be("Ransomware");
        YaraRuleCategory.Trojan.Should().Be("Trojan");
        YaraRuleCategory.Backdoor.Should().Be("Backdoor");
        YaraRuleCategory.Webshell.Should().Be("Webshell");
        YaraRuleCategory.Cryptominer.Should().Be("Cryptominer");
        YaraRuleCategory.Exploit.Should().Be("Exploit");
        YaraRuleCategory.Suspicious.Should().Be("Suspicious");
        YaraRuleCategory.PUA.Should().Be("PUA");
        YaraRuleCategory.Custom.Should().Be("Custom");
    }

    [Fact]
    public void YaraRuleDto_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var testDateTime = DateTime.UtcNow;
        var testMitreTechniques = new List<string> { "T1059.001" };
        var testTags = new List<string> { "test" };

        // Act
        var dto = new YaraRuleDto
        {
            Id = "test-id",
            Name = "TestRule",
            Description = "Test description",
            RuleContent = "rule TestRule { condition: true }",
            Category = YaraRuleCategory.Malware,
            Author = "Test Author",
            CreatedAt = testDateTime,
            UpdatedAt = testDateTime,
            IsEnabled = true,
            Priority = 75,
            ThreatLevel = "High",
            HitCount = 5,
            FalsePositiveCount = 1,
            AverageExecutionTimeMs = 10.5,
            MitreTechniques = testMitreTechniques,
            Tags = testTags,
            IsValid = true,
            ValidationError = null,
            Source = "Test"
        };

        // Assert
        dto.Id.Should().Be("test-id");
        dto.Name.Should().Be("TestRule");
        dto.Description.Should().Be("Test description");
        dto.RuleContent.Should().Be("rule TestRule { condition: true }");
        dto.Category.Should().Be(YaraRuleCategory.Malware);
        dto.Author.Should().Be("Test Author");
        dto.CreatedAt.Should().Be(testDateTime);
        dto.UpdatedAt.Should().Be(testDateTime);
        dto.IsEnabled.Should().BeTrue();
        dto.Priority.Should().Be(75);
        dto.ThreatLevel.Should().Be("High");
        dto.HitCount.Should().Be(5);
        dto.FalsePositiveCount.Should().Be(1);
        dto.AverageExecutionTimeMs.Should().Be(10.5);
        dto.MitreTechniques.Should().BeEquivalentTo(testMitreTechniques);
        dto.Tags.Should().BeEquivalentTo(testTags);
        dto.IsValid.Should().BeTrue();
        dto.ValidationError.Should().BeNull();
        dto.Source.Should().Be("Test");
    }

    [Fact]
    public void YaraRuleRequest_RequiredFields_AreValidated()
    {
        // Arrange
        var request = new YaraRuleRequest();
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        isValid.Should().BeFalse();
        results.Should().HaveCountGreaterThan(0);
        results.Should().Contain(r => r.MemberNames.Contains("Name"));
        results.Should().Contain(r => r.MemberNames.Contains("RuleContent"));
    }

    [Fact]
    public void YaraRuleRequest_ValidData_PassesValidation()
    {
        // Arrange
        var request = new YaraRuleRequest
        {
            Name = "ValidRule",
            Description = "Valid description",
            RuleContent = "rule ValidRule { condition: true }",
            Category = YaraRuleCategory.Malware,
            Author = "Test Author",
            IsEnabled = true,
            Priority = 75,
            ThreatLevel = "High",
            MitreTechniques = new List<string> { "T1059.001" },
            Tags = new List<string> { "test" },
            TestSample = "Test sample"
        };
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        isValid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void YaraTestRequest_RequiredField_IsValidated()
    {
        // Arrange
        var request = new YaraTestRequest();
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(request, context, results, true);

        // Assert
        isValid.Should().BeFalse();
        results.Should().HaveCount(1);
        results[0].MemberNames.Should().Contain("RuleContent");
    }

    [Fact]
    public void YaraTestResponse_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var testMatchedStrings = new List<YaraMatchString>
        {
            new YaraMatchString
            {
                Identifier = "$test",
                Offset = 0,
                Value = "test_string",
                IsHex = false
            }
        };

        // Act
        var response = new YaraTestResponse
        {
            IsValid = true,
            ValidationError = null,
            Matched = true,
            MatchedStrings = testMatchedStrings,
            ExecutionTimeMs = 12.3
        };

        // Assert
        response.IsValid.Should().BeTrue();
        response.ValidationError.Should().BeNull();
        response.Matched.Should().BeTrue();
        response.MatchedStrings.Should().BeEquivalentTo(testMatchedStrings);
        response.ExecutionTimeMs.Should().Be(12.3);
    }
}
