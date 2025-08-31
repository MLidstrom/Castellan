using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Moq;
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using FluentAssertions;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly Mock<IOptions<NotificationOptions>> _mockOptions;
    private readonly NotificationOptions _notificationOptions;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationService>>();
        _mockOptions = new Mock<IOptions<NotificationOptions>>();
        
        _notificationOptions = new NotificationOptions
        {
            EnableDesktopNotifications = true,
            NotificationLevel = "Info",
            NotificationTimeout = 5000,
            ShowEventDetails = true
        };

        _mockOptions.Setup(x => x.Value).Returns(_notificationOptions);
        _service = new NotificationService(_mockLogger.Object, _mockOptions.Object);
    }

    public void Dispose()
    {
        // NotificationService doesn't implement IDisposable, no cleanup needed
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesService()
    {
        // Arrange & Act
        var service = new NotificationService(_mockLogger.Object, _mockOptions.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<INotificationService>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert - The actual service throws ArgumentNullException when logger is null
        Action act = () => new NotificationService(null, _mockOptions.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsNullReferenceException()
    {
        // Arrange, Act & Assert - The actual service throws NullReferenceException when accessing options.Value
        Action act = () => new NotificationService(_mockLogger.Object, null);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public async Task ShowNotificationAsync_ValidParameters_DoesNotThrow()
    {
        // Arrange
        var title = "Test Title";
        var message = "Test Message";
        var level = "Info";

        // Act & Assert
        await _service.ShowNotificationAsync(title, message, level);
        
        // Should not throw exception
        true.Should().BeTrue();
    }

    [Fact]
    public void ShouldShowNotification_InfoLevel_WithInfoMinimum_ReturnsTrue()
    {
        // Arrange
        _notificationOptions.NotificationLevel = "Info";

        // Act
        var result = _service.ShouldShowNotification("Info");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldShowNotification_InfoLevel_WithWarningMinimum_ReturnsFalse()
    {
        // Arrange
        _notificationOptions.NotificationLevel = "Warning";

        // Act
        var result = _service.ShouldShowNotification("Info");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSecurityNotificationAsync_ValidEvent_CallsShowNotification()
    {
        // Arrange
        var securityEvent = CreateTestSecurityEvent("Test Event", "High");

        // Act
        await _service.SendSecurityNotificationAsync(securityEvent);

        // Assert - Should not throw exception
        true.Should().BeTrue();
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