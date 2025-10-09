using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Castellan.Worker.Controllers;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using FluentAssertions;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.Controllers;

public class SecurityEventsControllerTests : IDisposable
{
    private readonly Mock<ILogger<SecurityEventsController>> _mockLogger;
    private readonly Mock<ISecurityEventStore> _mockSecurityEventStore;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<IAdvancedSearchService> _mockAdvancedSearchService;
    private readonly Mock<ISearchHistoryService> _mockSearchHistoryService;
    private readonly SecurityEventsController _controller;

    public SecurityEventsControllerTests()
    {
        _mockLogger = new Mock<ILogger<SecurityEventsController>>();
        _mockSecurityEventStore = new Mock<ISecurityEventStore>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockAdvancedSearchService = new Mock<IAdvancedSearchService>();
        _mockSearchHistoryService = new Mock<ISearchHistoryService>();
        _controller = new SecurityEventsController(_mockLogger.Object, _mockVectorStore.Object, _mockSecurityEventStore.Object, _mockAdvancedSearchService.Object, _mockSearchHistoryService.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesController()
    {
        // Arrange & Act
        var controller = new SecurityEventsController(_mockLogger.Object, _mockVectorStore.Object, _mockSecurityEventStore.Object, _mockAdvancedSearchService.Object, _mockSearchHistoryService.Object);

        // Assert
        controller.Should().NotBeNull();
        controller.Should().BeAssignableTo<ControllerBase>();
    }

    [Fact]
    public void Constructor_NullLogger_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - The actual controller doesn't validate parameters
        Action act = () => new SecurityEventsController(null!, _mockVectorStore.Object, _mockSecurityEventStore.Object, _mockAdvancedSearchService.Object, _mockSearchHistoryService.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullSecurityEventStore_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - The actual controller doesn't validate parameters
        Action act = () => new SecurityEventsController(_mockLogger.Object, _mockVectorStore.Object, null!, _mockAdvancedSearchService.Object, _mockSearchHistoryService.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetSecurityEvents_DefaultParameters_ReturnsOkResult()
    {
        // Arrange
        var testEvents = new List<SecurityEvent>
        {
            CreateTestSecurityEvent("Event1", "Low"),
            CreateTestSecurityEvent("Event2", "Medium")
        };
        _mockSecurityEventStore.Setup(x => x.GetSecurityEvents(1, 10)).Returns(testEvents);
        _mockSecurityEventStore.Setup(x => x.GetTotalCount()).Returns(testEvents.Count);

        // Act
        var result = await _controller.GetList();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSecurityEvent_ExistingId_ReturnsOkResult()
    {
        // Arrange
        var eventId = "test-event-123";
        var testEvent = CreateTestSecurityEvent("TestEvent", "Medium");
        testEvent.Id = eventId;
        _mockSecurityEventStore.Setup(x => x.GetSecurityEvent(eventId)).Returns(testEvent);

        // Act
        var result = await _controller.GetOne(eventId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSecurityEvent_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var eventId = "non-existent-id";
        _mockSecurityEventStore.Setup(x => x.GetSecurityEvent(eventId)).Returns((SecurityEvent)null!);

        // Act
        var result = await _controller.GetOne(eventId);

        // Assert - The actual controller returns NotFoundObjectResult with a message
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void ParseEnrichedIPs_WithValidSingleObject_ReturnsEnrichmentDto()
    {
        // Arrange
        var enrichmentData = System.Text.Json.JsonSerializer.Serialize(new
        {
            ipAddress = "103.248.45.21",
            country = "CN",
            city = "Beijing",
            asn = "AS4134",
            isHighRisk = true
        });

        // Act
        var result = InvokeParseEnrichedIPs(_controller, enrichmentData);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].IP.Should().Be("103.248.45.21");
        result[0].Country.Should().Be("CN");
        result[0].City.Should().Be("Beijing");
        result[0].ASN.Should().Be("AS4134");
        result[0].IsHighRisk.Should().BeTrue();
    }

    [Fact]
    public void ParseEnrichedIPs_WithArray_ReturnsMultipleEnrichmentDtos()
    {
        // Arrange
        var enrichmentData = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { ipAddress = "103.248.45.21", country = "CN", city = "Beijing", asn = "AS4134", isHighRisk = true },
            new { ipAddress = "185.220.101.32", country = "NL", city = "Amsterdam", asn = "AS13335", isHighRisk = true }
        });

        // Act
        var result = InvokeParseEnrichedIPs(_controller, enrichmentData);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].IP.Should().Be("103.248.45.21");
        result[0].Country.Should().Be("CN");
        result[1].IP.Should().Be("185.220.101.32");
        result[1].Country.Should().Be("NL");
    }

    [Fact]
    public void ParseEnrichedIPs_WithCaseInsensitiveProperties_ParsesCorrectly()
    {
        // Arrange - Test with uppercase property names
        var enrichmentData = System.Text.Json.JsonSerializer.Serialize(new
        {
            IP = "103.248.45.21",
            Country = "CN",
            City = "Beijing",
            ASN = "AS4134",
            IsHighRisk = true
        });

        // Act
        var result = InvokeParseEnrichedIPs(_controller, enrichmentData);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].IP.Should().Be("103.248.45.21");
        result[0].Country.Should().Be("CN");
        result[0].City.Should().Be("Beijing");
        result[0].ASN.Should().Be("AS4134");
        result[0].IsHighRisk.Should().BeTrue();
    }

    [Fact]
    public void ParseEnrichedIPs_WithEmptyString_ReturnsEmptyArray()
    {
        // Arrange & Act
        var result = InvokeParseEnrichedIPs(_controller, "");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseEnrichedIPs_WithNull_ReturnsEmptyArray()
    {
        // Arrange & Act
        var result = InvokeParseEnrichedIPs(_controller, null);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseEnrichedIPs_WithInvalidJson_ReturnsEmptyArray()
    {
        // Arrange & Act
        var result = InvokeParseEnrichedIPs(_controller, "{ invalid json }");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseEnrichedIPs_WithMissingProperties_UsesDefaults()
    {
        // Arrange - Enrichment data with only IP address
        var enrichmentData = System.Text.Json.JsonSerializer.Serialize(new { ipAddress = "103.248.45.21" });

        // Act
        var result = InvokeParseEnrichedIPs(_controller, enrichmentData);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].IP.Should().Be("103.248.45.21");
        result[0].Country.Should().Be("Unknown");
        result[0].City.Should().Be("Unknown");
        result[0].ASN.Should().Be("Unknown");
        result[0].IsHighRisk.Should().BeFalse();
    }

    /// <summary>
    /// Helper method to invoke private ParseEnrichedIPs method via reflection
    /// </summary>
    private IPEnrichmentDto[] InvokeParseEnrichedIPs(SecurityEventsController controller, string? enrichmentData)
    {
        var method = typeof(SecurityEventsController).GetMethod(
            "ParseEnrichedIPs",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        if (method == null)
            throw new InvalidOperationException("ParseEnrichedIPs method not found");

        var result = method.Invoke(controller, new object?[] { enrichmentData });
        return (IPEnrichmentDto[])(result ?? Array.Empty<IPEnrichmentDto>());
    }

    /// <summary>
    /// Helper method to create test security events
    /// </summary>
    private static SecurityEvent CreateTestSecurityEvent(string eventTypeStr, string riskLevel)
    {
        var logEvent = TestDataFactory.CreateSecurityEvent(4625, "test-user");
        var eventType = SecurityEventType.AuthenticationFailure; // Default for test

        return SecurityEvent.CreateDeterministic(
            logEvent,
            eventType,
            riskLevel.ToLowerInvariant(),
            75,
            $"Test security event: {eventTypeStr}",
            new[] { "T1078" },
            new[] { "Monitor user activity" }
        );
    }
}