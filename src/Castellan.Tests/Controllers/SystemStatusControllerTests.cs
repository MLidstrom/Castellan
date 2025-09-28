using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using FluentAssertions;
using Castellan.Worker.Controllers;
using ControllerDto = Castellan.Worker.Controllers.SystemStatusDto;

namespace Castellan.Tests.Controllers;

public class SystemStatusControllerTests : IDisposable
{
    private readonly Mock<ILogger<SystemStatusController>> _mockLogger;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private readonly SystemStatusController _controller = null!;
#pragma warning restore CS0414

    public SystemStatusControllerTests()
    {
        _mockLogger = new Mock<ILogger<SystemStatusController>>();
        // For now, we'll test the parts of the controller that don't require SystemHealthService
        // The service dependency would require extensive mocking setup, so we focus on DTO and basic controller tests
        _controller = null!; // Will be set per test where needed
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert
        // Note: We can't fully test the constructor without SystemHealthService, 
        // but we can verify the basic parameter validation behavior
        var action = () => new SystemStatusController(null!, null!);
        action.Should().NotThrow();
    }

    #endregion

    #region GetList Tests - Skipped due to service dependency complexity

    // Note: GetList tests are skipped because SystemHealthService is a concrete class
    // that would require extensive setup of dependencies (HttpClientFactory, Options, etc.)
    // In a real-world scenario, SystemHealthService should implement an interface to allow proper mocking

    #endregion

    #region GetTest Tests

    [Fact]
    public void GetTest_Always_ReturnsOkResultWithTestData()
    {
        // Arrange
        var testController = new SystemStatusController(_mockLogger.Object, null!);
        
        // Act
        var result = testController.GetTest();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        
        // Verify test data structure
        var response = okResult.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        
        dataProperty.Should().NotBeNull();
        totalProperty.Should().NotBeNull();
        
        var total = (int)totalProperty!.GetValue(response)!;
        total.Should().Be(1);
    }

    #endregion

    #region GetOne Tests - Skipped due to service dependency

    // Note: GetOne tests are skipped for the same reason as GetList tests
    // The method depends on SystemHealthService which is not easily mockable

    #endregion

    #region GetEditionInfo Tests

    [Fact]
    public void GetEditionInfo_Always_ReturnsOkResultWithEditionData()
    {
        // Arrange
        var testController = new SystemStatusController(_mockLogger.Object, null!);
        
        // Act
        var result = testController.GetEditionInfo();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        
        // Verify edition info structure
        var response = okResult.Value;
        var editionProperty = response!.GetType().GetProperty("edition");
        var featuresProperty = response.GetType().GetProperty("features");
        var buildInfoProperty = response.GetType().GetProperty("buildInfo");
        
        editionProperty.Should().NotBeNull();
        featuresProperty.Should().NotBeNull();
        buildInfoProperty.Should().NotBeNull();
        
        var edition = (string)editionProperty!.GetValue(response)!;
        edition.Should().Be("Castellan");
    }

    #endregion

    #region SystemStatusDto Tests

    [Fact]
    public void SystemStatusDto_HealthyStatus_IsHealthyReturnsTrue()
    {
        // Arrange
        var dto = new ControllerDto
        {
            Status = "Healthy"
        };

        // Act & Assert
        dto.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void SystemStatusDto_NonHealthyStatus_IsHealthyReturnsFalse()
    {
        // Arrange
        var dto = new ControllerDto
        {
            Status = "Warning"
        };

        // Act & Assert
        dto.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void SystemStatusDto_NullStatus_IsHealthyReturnsFalse()
    {
        // Arrange
        var dto = new ControllerDto
        {
            Status = null!
        };

        // Act & Assert
        dto.IsHealthy.Should().BeFalse();
    }

    [Theory]
    [InlineData("healthy")]
    [InlineData("HEALTHY")]
    [InlineData("Healthy")]
    public void SystemStatusDto_HealthyStatusCaseInsensitive_IsHealthyReturnsTrue(string status)
    {
        // Arrange
        var dto = new ControllerDto
        {
            Status = status
        };

        // Act & Assert
        dto.IsHealthy.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static List<ControllerDto> CreateMockSystemStatuses()
    {
        return new List<ControllerDto>
        {
            new ControllerDto
            {
                Id = "1",
                Component = "Test Component 1",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow,
                ResponseTime = 10,
                Uptime = "99.9%",
                Details = "Test details 1",
                ErrorCount = 0,
                WarningCount = 0
            },
            new ControllerDto
            {
                Id = "2",
                Component = "Test Component 2",
                Status = "Warning",
                LastCheck = DateTime.UtcNow.AddMinutes(-5),
                ResponseTime = 25,
                Uptime = "95.5%",
                Details = "Test details 2",
                ErrorCount = 0,
                WarningCount = 2
            },
            new ControllerDto
            {
                Id = "3",
                Component = "Test Component 3",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow.AddMinutes(-1),
                ResponseTime = 5,
                Uptime = "100%",
                Details = "Test details 3",
                ErrorCount = 0,
                WarningCount = 0
            }
        };
    }

    #endregion
}
