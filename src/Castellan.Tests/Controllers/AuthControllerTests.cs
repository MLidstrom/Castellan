using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Moq;
using Castellan.Worker.Controllers;
using Castellan.Worker.Configuration;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using FluentAssertions;
using static Castellan.Worker.Controllers.AuthController;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace Castellan.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly Mock<IOptions<AuthenticationOptions>> _mockAuthOptions;
    private readonly Mock<IPasswordHashingService> _mockPasswordHashingService;
    private readonly Mock<IRefreshTokenService> _mockRefreshTokenService;
    private readonly Mock<IJwtTokenBlacklistService> _mockJwtTokenBlacklistService;
    private readonly AuthenticationOptions _authOptions;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockLogger = new Mock<ILogger<AuthController>>();
        _mockAuthOptions = new Mock<IOptions<AuthenticationOptions>>();
        _mockPasswordHashingService = new Mock<IPasswordHashingService>();
        _mockRefreshTokenService = new Mock<IRefreshTokenService>();
        _mockJwtTokenBlacklistService = new Mock<IJwtTokenBlacklistService>();
        
        _authOptions = new AuthenticationOptions
        {
            AdminUser = new AdminUserOptions
            {
                Username = "testuser",
                Password = "$2a$11$hashed_password_example", // BCrypt hashed password
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
        
        // Setup password hashing service mocks
        _mockPasswordHashingService.Setup(x => x.VerifyPassword("testpass", "$2a$11$hashed_password_example"))
            .Returns(true);
        _mockPasswordHashingService.Setup(x => x.VerifyPassword(It.Is<string>(p => p != "testpass"), "$2a$11$hashed_password_example"))
            .Returns(false);
        
        // Setup refresh token service mocks
        _mockRefreshTokenService.Setup(x => x.GenerateRefreshTokenAsync("1", 30))
            .ReturnsAsync(new RefreshToken 
            { 
                Token = "test-refresh-token", 
                UserId = "1",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            });
        _mockRefreshTokenService.Setup(x => x.ValidateRefreshTokenAsync("valid-refresh-token"))
            .ReturnsAsync(new RefreshToken 
            { 
                Token = "valid-refresh-token", 
                UserId = "1", 
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                IsRevoked = false
            });
        _mockRefreshTokenService.Setup(x => x.RotateRefreshTokenAsync("valid-refresh-token", "1"))
            .ReturnsAsync(new RefreshToken 
            { 
                Token = "new-refresh-token", 
                UserId = "1",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            });
        _mockRefreshTokenService.Setup(x => x.RevokeAllUserTokensAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0); // Returns number of tokens revoked
        
        // Setup JWT blacklist service mocks
        _mockJwtTokenBlacklistService.Setup(x => x.BlacklistTokenAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(Task.CompletedTask);
        
        _controller = new AuthController(
            _mockLogger.Object, 
            _mockAuthOptions.Object,
            _mockPasswordHashingService.Object,
            _mockRefreshTokenService.Object,
            _mockJwtTokenBlacklistService.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesController()
    {
        // Arrange & Act
        var controller = new AuthController(
            _mockLogger.Object, 
            _mockAuthOptions.Object,
            _mockPasswordHashingService.Object,
            _mockRefreshTokenService.Object,
            _mockJwtTokenBlacklistService.Object);

        // Assert
        controller.Should().NotBeNull();
        controller.Should().BeAssignableTo<ControllerBase>();
    }

    [Fact]
    public void Constructor_NullLogger_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - The actual controller doesn't validate logger parameter
        Action act = () => new AuthController(
            null!, 
            _mockAuthOptions.Object,
            _mockPasswordHashingService.Object,
            _mockRefreshTokenService.Object,
            _mockJwtTokenBlacklistService.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullAuthOptions_ThrowsNullReferenceException()
    {
        // Arrange, Act & Assert - The actual controller throws NullReferenceException when accessing authOptions.Value
        Action act = () => new AuthController(
            _mockLogger.Object, 
            null!,
            _mockPasswordHashingService.Object,
            _mockRefreshTokenService.Object,
            _mockJwtTokenBlacklistService.Object);
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
        // Arrange - Set up HTTP context and user for the Logout action
        var mockHttpContext = new Mock<HttpContext>();
        var mockRequest = new Mock<HttpRequest>();
        var mockHeaderDictionary = new HeaderDictionary();
        
        // Set up request headers (no Authorization header to test the "no header" path)
        mockRequest.Setup(r => r.Headers).Returns(mockHeaderDictionary);
        mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
        
        // Set up user identity
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.NameIdentifier, "1")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = mockHttpContext.Object
        };

        // Act
        var result = await _controller.Logout();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }
}

// DTOs are imported from Castellan.Worker.Controllers.AuthController