using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Castellan.Worker.Controllers;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using FluentAssertions;

namespace Castellan.Tests.Controllers;

[Collection("TestEnvironment")]
public class YaraRulesControllerTests : IDisposable
{
    private readonly Mock<ILogger<YaraRulesController>> _mockLogger;
    private readonly Mock<IYaraRuleStore> _mockRuleStore;
    private readonly YaraRulesController _controller;

    public YaraRulesControllerTests()
    {
        _mockLogger = new Mock<ILogger<YaraRulesController>>();
        _mockRuleStore = new Mock<IYaraRuleStore>();
        _controller = new YaraRulesController(_mockLogger.Object, _mockRuleStore.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesController()
    {
        // Arrange & Act
        var controller = new YaraRulesController(_mockLogger.Object, _mockRuleStore.Object);

        // Assert
        controller.Should().NotBeNull();
        controller.Should().BeAssignableTo<ControllerBase>();
    }

    [Fact]
    public async Task GetRules_NoFilters_ReturnsAllRules()
    {
        // Arrange
        var testRules = new List<YaraRule>
        {
            CreateTestYaraRule("Rule1", "First rule"),
            CreateTestYaraRule("Rule2", "Second rule")
        };
        _mockRuleStore.Setup(x => x.GetAllRulesAsync()).ReturnsAsync(testRules);

        // Act
        var result = await _controller.GetRules();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        
        // Extract the anonymous object properties using reflection
        var responseValue = okResult.Value;
        var dataProperty = responseValue?.GetType().GetProperty("data");
        var totalProperty = responseValue?.GetType().GetProperty("total");
        
        dataProperty.Should().NotBeNull();
        totalProperty.Should().NotBeNull();
        
        var data = dataProperty?.GetValue(responseValue);
        var total = totalProperty?.GetValue(responseValue);
        
        data.Should().NotBeNull();
        total.Should().Be(2);
        
        _mockRuleStore.Verify(x => x.GetAllRulesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetRules_WithCategoryFilter_ReturnsFilteredRules()
    {
        // Arrange
        var malwareRules = new List<YaraRule>
        {
            CreateTestYaraRule("MalwareRule", "Malware detection")
        };
        _mockRuleStore.Setup(x => x.GetRulesByCategoryAsync("Malware")).ReturnsAsync(malwareRules);

        // Act
        var result = await _controller.GetRules(category: "Malware");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRulesByCategoryAsync("Malware"), Times.Once);
    }

    [Fact]
    public async Task GetRules_WithTagFilter_ReturnsFilteredRules()
    {
        // Arrange
        var taggedRules = new List<YaraRule>
        {
            CreateTestYaraRule("TaggedRule", "Tagged rule")
        };
        _mockRuleStore.Setup(x => x.GetRulesByTagAsync("powershell")).ReturnsAsync(taggedRules);

        // Act
        var result = await _controller.GetRules(tag: "powershell");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRulesByTagAsync("powershell"), Times.Once);
    }

    [Fact]
    public async Task GetRules_WithMitreTechniqueFilter_ReturnsFilteredRules()
    {
        // Arrange
        var mitreRules = new List<YaraRule>
        {
            CreateTestYaraRule("MitreRule", "MITRE technique rule")
        };
        _mockRuleStore.Setup(x => x.GetRulesByMitreTechniqueAsync("T1059.001")).ReturnsAsync(mitreRules);

        // Act
        var result = await _controller.GetRules(mitreTechnique: "T1059.001");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRulesByMitreTechniqueAsync("T1059.001"), Times.Once);
    }

    [Fact]
    public async Task GetRules_WithEnabledFilter_ReturnsEnabledRules()
    {
        // Arrange
        var enabledRules = new List<YaraRule>
        {
            CreateTestYaraRule("EnabledRule", "Enabled rule")
        };
        _mockRuleStore.Setup(x => x.GetEnabledRulesAsync()).ReturnsAsync(enabledRules);

        // Act
        var result = await _controller.GetRules(enabled: true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetEnabledRulesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetRules_StoreThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockRuleStore.Setup(x => x.GetAllRulesAsync()).ThrowsAsync(new InvalidOperationException("Store error"));

        // Act
        var result = await _controller.GetRules();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var errorResult = (ObjectResult)result;
        errorResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetRule_ExistingId_ReturnsRule()
    {
        // Arrange
        var ruleId = "test-rule-id";
        var testRule = CreateTestYaraRule("TestRule", "Test description");
        testRule.Id = ruleId;
        _mockRuleStore.Setup(x => x.GetRuleByIdAsync(ruleId)).ReturnsAsync(testRule);

        // Act
        var result = await _controller.GetRule(ruleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        
        _mockRuleStore.Verify(x => x.GetRuleByIdAsync(ruleId), Times.Once);
    }

    [Fact]
    public async Task GetRule_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var ruleId = "non-existent-id";
        _mockRuleStore.Setup(x => x.GetRuleByIdAsync(ruleId)).ReturnsAsync((YaraRule?)null);

        // Act
        var result = await _controller.GetRule(ruleId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateRule_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new YaraRuleRequest
        {
            Name = "NewRule",
            Description = "New rule description",
            RuleContent = "rule NewRule { condition: true }",
            Category = "Malware",
            Author = "Test Author",
            IsEnabled = true,
            Priority = 75,
            ThreatLevel = "High",
            MitreTechniques = new List<string> { "T1059.001" },
            Tags = new List<string> { "test" }
        };

        var createdRule = CreateTestYaraRule("NewRule", "New rule description");
        createdRule.Id = "new-rule-id";

        _mockRuleStore.Setup(x => x.RuleExistsAsync("NewRule")).ReturnsAsync(false);
        _mockRuleStore.Setup(x => x.AddRuleAsync(It.IsAny<YaraRule>())).ReturnsAsync(createdRule);

        // Act
        var result = await _controller.CreateRule(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result;
        createdResult.ActionName.Should().Be("GetRule");
        createdResult.RouteValues!["id"].Should().Be("new-rule-id");

        _mockRuleStore.Verify(x => x.RuleExistsAsync("NewRule"), Times.Once);
        _mockRuleStore.Verify(x => x.AddRuleAsync(It.IsAny<YaraRule>()), Times.Once);
    }

    [Fact]
    public async Task CreateRule_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var request = new YaraRuleRequest
        {
            Name = "ExistingRule",
            Description = "Existing rule",
            RuleContent = "rule ExistingRule { condition: true }"
        };

        _mockRuleStore.Setup(x => x.RuleExistsAsync("ExistingRule")).ReturnsAsync(true);

        // Act
        var result = await _controller.CreateRule(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockRuleStore.Verify(x => x.RuleExistsAsync("ExistingRule"), Times.Once);
        _mockRuleStore.Verify(x => x.AddRuleAsync(It.IsAny<YaraRule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRule_InvalidYaraContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new YaraRuleRequest
        {
            Name = "InvalidRule",
            Description = "Invalid rule",
            RuleContent = "invalid yara content"
        };

        _mockRuleStore.Setup(x => x.RuleExistsAsync("InvalidRule")).ReturnsAsync(false);

        // Act
        var result = await _controller.CreateRule(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockRuleStore.Verify(x => x.AddRuleAsync(It.IsAny<YaraRule>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRule_ExistingRule_ReturnsOkResult()
    {
        // Arrange
        var ruleId = "existing-rule-id";
        var request = new YaraRuleRequest
        {
            Name = "UpdatedRule",
            Description = "Updated description",
            RuleContent = "rule UpdatedRule { condition: true }"
        };

        var existingRule = CreateTestYaraRule("OriginalRule", "Original description");
        existingRule.Id = ruleId;
        var updatedRule = CreateTestYaraRule("UpdatedRule", "Updated description");
        updatedRule.Id = ruleId;

        _mockRuleStore.Setup(x => x.GetRuleByIdAsync(ruleId)).ReturnsAsync(existingRule);
        _mockRuleStore.Setup(x => x.UpdateRuleAsync(It.IsAny<YaraRule>())).ReturnsAsync(updatedRule);

        // Act
        var result = await _controller.UpdateRule(ruleId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRuleByIdAsync(ruleId), Times.Once);
        _mockRuleStore.Verify(x => x.UpdateRuleAsync(It.IsAny<YaraRule>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRule_NonExistentRule_ReturnsNotFound()
    {
        // Arrange
        var ruleId = "non-existent-id";
        var request = new YaraRuleRequest
        {
            Name = "UpdatedRule",
            Description = "Updated description",
            RuleContent = "rule UpdatedRule { condition: true }"
        };

        _mockRuleStore.Setup(x => x.GetRuleByIdAsync(ruleId)).ReturnsAsync((YaraRule?)null);

        // Act
        var result = await _controller.UpdateRule(ruleId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        _mockRuleStore.Verify(x => x.UpdateRuleAsync(It.IsAny<YaraRule>()), Times.Never);
    }

    [Fact]
    public async Task DeleteRule_ExistingRule_ReturnsOkResult()
    {
        // Arrange
        var ruleId = "existing-rule-id";
        _mockRuleStore.Setup(x => x.DeleteRuleAsync(ruleId)).ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteRule(ruleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.DeleteRuleAsync(ruleId), Times.Once);
    }

    [Fact]
    public async Task DeleteRule_NonExistentRule_ReturnsNotFound()
    {
        // Arrange
        var ruleId = "non-existent-id";
        _mockRuleStore.Setup(x => x.DeleteRuleAsync(ruleId)).ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteRule(ruleId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetCategories_Always_ReturnsAvailableCategories()
    {
        // Act
        var result = _controller.GetCategories();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();

        // Extract the anonymous object properties using reflection
        var responseValue = okResult.Value;
        var dataProperty = responseValue?.GetType().GetProperty("data");
        
        dataProperty.Should().NotBeNull();
        
        var data = dataProperty?.GetValue(responseValue);
        data.Should().NotBeNull();
    }

    [Fact]
    public async Task TestRule_ValidRequest_ReturnsTestResult()
    {
        // Arrange
        var request = new YaraTestRequest
        {
            RuleContent = "rule TestRule { strings: $a = \"test\" condition: $a }",
            TestContent = "This is a test string"
        };

        // Act
        var result = await _controller.TestRule(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task TestRule_InvalidRuleContent_ReturnsValidationError()
    {
        // Arrange
        var request = new YaraTestRequest
        {
            RuleContent = "invalid yara rule",
            TestContent = "test content"
        };

        // Act
        var result = await _controller.TestRule(request);

        // Assert - TestRule returns 200 OK with IsValid=false for invalid rules
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var response = okResult.Value as YaraTestResponse;
        response.Should().NotBeNull();
        response!.IsValid.Should().BeFalse();
        response.ValidationError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReportFalsePositive_ExistingRule_ReturnsOkResult()
    {
        // Arrange
        var ruleId = "existing-rule-id";
        var existingRule = CreateTestYaraRule("TestRule", "Test description");
        existingRule.Id = ruleId;

        _mockRuleStore.Setup(x => x.GetRuleByIdAsync(ruleId)).ReturnsAsync(existingRule);
        _mockRuleStore.Setup(x => x.RecordFalsePositiveAsync(ruleId)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ReportFalsePositive(ruleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.RecordFalsePositiveAsync(ruleId), Times.Once);
    }

    [Fact]
    public async Task ReportFalsePositive_NonExistentRule_ReturnsNotFound()
    {
        // Arrange
        var ruleId = "non-existent-id";
        _mockRuleStore.Setup(x => x.GetRuleByIdAsync(ruleId)).ReturnsAsync((YaraRule?)null);

        // Act
        var result = await _controller.ReportFalsePositive(ruleId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        _mockRuleStore.Verify(x => x.RecordFalsePositiveAsync(It.IsAny<string>()), Times.Never);
    }

    private static YaraRule CreateTestYaraRule(string name, string description)
    {
        return new YaraRule
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            RuleContent = $"rule {name} {{ condition: true }}",
            Category = YaraRuleCategory.Malware,
            Author = "Test Author",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsEnabled = true,
            Priority = 50,
            ThreatLevel = "Medium",
            HitCount = 0,
            FalsePositiveCount = 0,
            AverageExecutionTimeMs = 0.0,
            MitreTechniques = new List<string> { "T1059.001" },
            Tags = new List<string> { "test" },
            IsValid = true,
            Source = "Test"
        };
    }
}
