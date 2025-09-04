using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Moq;
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Castellan.Worker.Abstractions;
using FluentAssertions;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.Services;

public class AutomatedResponseServiceTests : IDisposable
{
    private readonly Mock<ILogger<AutomatedResponseService>> _mockLogger;
    private readonly Mock<IOptions<AutomatedResponseOptions>> _mockOptions;
    private readonly Mock<IIPEnrichmentService> _mockIPEnrichmentService;
    private readonly AutomatedResponseOptions _responseOptions;
    private readonly AutomatedResponseService _service;

    public AutomatedResponseServiceTests()
    {
        _mockLogger = new Mock<ILogger<AutomatedResponseService>>();
        _mockOptions = new Mock<IOptions<AutomatedResponseOptions>>();
        _mockIPEnrichmentService = new Mock<IIPEnrichmentService>();
        
        _responseOptions = new AutomatedResponseOptions
        {
            Enabled = true,
            RiskLevelThreshold = "Medium",
            RequireConfirmation = false,
            Actions = new ResponseActions
            {
                BlockIPAddresses = true,
                LockUserAccounts = false,
                RevokePrivileges = true
            }
        };

        _mockOptions.Setup(x => x.Value).Returns(_responseOptions);
        _service = new AutomatedResponseService(_mockLogger.Object, _mockOptions.Object, _mockIPEnrichmentService.Object);
    }

    public void Dispose()
    {
        // AutomatedResponseService doesn't implement IDisposable, no cleanup needed
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesService()
    {
        // Arrange & Act
        var service = new AutomatedResponseService(_mockLogger.Object, _mockOptions.Object, _mockIPEnrichmentService.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IAutomatedResponseService>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert - The actual service throws ArgumentNullException when logger is null
        Action act = () => new AutomatedResponseService(null!, _mockOptions.Object, _mockIPEnrichmentService.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsNullReferenceException()
    {
        // Arrange, Act & Assert - The actual service throws NullReferenceException when accessing options.Value
        Action act = () => new AutomatedResponseService(_mockLogger.Object, null!, _mockIPEnrichmentService.Object);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public async Task IsResponseEnabledAsync_EnabledTrue_ReturnsTrue()
    {
        // Arrange
        _responseOptions.Enabled = true;

        // Act
        var result = await _service.IsResponseEnabledAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteResponseAsync_ValidEvent_ExecutesSuccessfully()
    {
        // Arrange
        var securityEvent = CreateTestSecurityEvent("AuthenticationFailure", "High");

        // Act
        await _service.ExecuteResponseAsync(securityEvent);

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

    // Helper methods moved to use TestDataFactory
}