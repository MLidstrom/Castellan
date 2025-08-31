using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Moq;
using Castellan.Worker.Controllers;
using Castellan.Worker.Configuration;
using FluentAssertions;
using static Castellan.Worker.Controllers.AuthController;

namespace Castellan.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly Mock<IOptions<AuthenticationOptions>> _mockAuthOptions;
    private readonly AuthenticationOptions _authOptions;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockLogger = new Mock<ILogger<AuthController>>();
        _mockAuthOptions = new Mock<IOptions<AuthenticationOptions>>();
        
        _authOptions = new AuthenticationOptions
        {
            AdminUser = new AdminUserOptions
            {
                Username = "testuser",
                Password = "testpass",
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User"
            },
            Jwt = new JwtOptions
            {
                SecretKey = "this-is-a-test-secret-key-that-is-at-least-64-characters-long-for-testing",
                Issuer = "Castellan",
                Audience = "CastellanUsers",
                ExpirationHours = 24
            }
        };

        _mockAuthOptions.Setup(x => x.Value).Returns(_authOptions);
        _controller = new AuthController(_mockLogger.Object, _mockAuthOptions.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesController()
    {
        // Arrange & Act
        var controller = new AuthController(_mockLogger.Object, _mockAuthOptions.Object);

        // Assert
        controller.Should().NotBeNull();
        controller.Should().BeAssignableTo<ControllerBase>();
    }

    [Fact]
    public void Constructor_NullLogger_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - The actual controller doesn't validate logger parameter
        Action act = () => new AuthController(null, _mockAuthOptions.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullAuthOptions_ThrowsNullReferenceException()
    {
        // Arrange, Act & Assert - The actual controller throws NullReferenceException when accessing authOptions.Value
        Action act = () => new AuthController(_mockLogger.Object, null);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "testpass"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_InvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "wronguser",
            Password = "testpass"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "wrongpass"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_ValidRequest_ReturnsOkWithNewToken()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid-refresh-token"
        };

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        // Act
        var result = await _controller.Logout();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }
}

// DTOs are imported from Castellan.Worker.Controllers.AuthController