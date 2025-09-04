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
    private readonly SecurityEventsController _controller;

    public SecurityEventsControllerTests()
    {
        _mockLogger = new Mock<ILogger<SecurityEventsController>>();
        _mockSecurityEventStore = new Mock<ISecurityEventStore>();
        _mockVectorStore = new Mock<IVectorStore>();
        _controller = new SecurityEventsController(_mockLogger.Object, _mockVectorStore.Object, _mockSecurityEventStore.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesController()
    {
        // Arrange & Act
        var controller = new SecurityEventsController(_mockLogger.Object, _mockVectorStore.Object, _mockSecurityEventStore.Object);

        // Assert
        controller.Should().NotBeNull();
        controller.Should().BeAssignableTo<ControllerBase>();
    }

    [Fact]
    public void Constructor_NullLogger_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - The actual controller doesn't validate parameters
        Action act = () => new SecurityEventsController(null!, _mockVectorStore.Object, _mockSecurityEventStore.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullSecurityEventStore_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - The actual controller doesn't validate parameters
        Action act = () => new SecurityEventsController(_mockLogger.Object, _mockVectorStore.Object, null!);
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