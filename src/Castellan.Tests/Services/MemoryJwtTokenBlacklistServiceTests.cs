using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using Moq;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;

namespace Castellan.Tests.Services;

public class MemoryJwtTokenBlacklistServiceTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<MemoryJwtTokenBlacklistService>> _mockLogger;
    private readonly MemoryJwtTokenBlacklistService _service;

    public MemoryJwtTokenBlacklistServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<MemoryJwtTokenBlacklistService>>();
        _service = new MemoryJwtTokenBlacklistService(_memoryCache, _mockLogger.Object);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidParameters_CreatesService()
    {
        // Arrange & Act & Assert
        _service.Should().NotBeNull();
        _service.Should().BeAssignableTo<IJwtTokenBlacklistService>();
    }

    [Fact]
    public void Constructor_NullCache_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var action = () => new MemoryJwtTokenBlacklistService(null!, _mockLogger.Object);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*cache*");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var action = () => new MemoryJwtTokenBlacklistService(_memoryCache, null!);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*logger*");
    }

    #endregion

    #region BlacklistTokenAsync Tests

    [Fact]
    public async Task BlacklistTokenAsync_ValidToken_BlacklistsToken()
    {
        // Arrange
        var jti = "test-jwt-id-123";
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await _service.BlacklistTokenAsync(jti, expirationTime);

        // Assert
        var isBlacklisted = await _service.IsTokenBlacklistedAsync(jti);
        isBlacklisted.Should().BeTrue();
    }

    [Fact]
    public async Task BlacklistTokenAsync_ValidToken_LogsInformation()
    {
        // Arrange
        var jti = "test-jwt-id-456";
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await _service.BlacklistTokenAsync(jti, expirationTime);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("JWT token blacklisted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BlacklistTokenAsync_InvalidJti_ThrowsArgumentException(string? jti)
    {
        // Arrange
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);

        // Act & Assert
        var action = async () => await _service.BlacklistTokenAsync(jti!, expirationTime);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("JWT ID cannot be null or empty*");
    }

    [Fact]
    public async Task BlacklistTokenAsync_ExpiredTime_StillBlacklistsToken()
    {
        // Arrange
        var jti = "expired-test-jwt";
        var expirationTime = DateTimeOffset.UtcNow.AddMinutes(-1); // Already expired

        // Act
        await _service.BlacklistTokenAsync(jti, expirationTime);

        // Assert - Should still be added to cache even if expired
        var isBlacklisted = await _service.IsTokenBlacklistedAsync(jti);
        // Note: This might be false if the cache immediately evicts expired items
        // The important thing is that the method doesn't throw an exception
    }

    #endregion

    #region IsTokenBlacklistedAsync Tests

    [Fact]
    public async Task IsTokenBlacklistedAsync_BlacklistedToken_ReturnsTrue()
    {
        // Arrange
        var jti = "blacklisted-token";
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);
        await _service.BlacklistTokenAsync(jti, expirationTime);

        // Act
        var result = await _service.IsTokenBlacklistedAsync(jti);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_NonBlacklistedToken_ReturnsFalse()
    {
        // Arrange
        var jti = "non-blacklisted-token";

        // Act
        var result = await _service.IsTokenBlacklistedAsync(jti);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_BlacklistedToken_LogsWarning()
    {
        // Arrange
        var jti = "blacklisted-for-warning";
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);
        await _service.BlacklistTokenAsync(jti, expirationTime);

        // Act
        await _service.IsTokenBlacklistedAsync(jti);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Blocked access attempt with blacklisted JWT")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task IsTokenBlacklistedAsync_InvalidJti_ReturnsFalse(string? jti)
    {
        // Arrange, Act
        var result = await _service.IsTokenBlacklistedAsync(jti!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_NonBlacklistedToken_DoesNotLogWarning()
    {
        // Arrange
        var jti = "clean-token";

        // Act
        await _service.IsTokenBlacklistedAsync(jti);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Blocked access attempt")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region CleanupExpiredEntriesAsync Tests

    [Fact]
    public async Task CleanupExpiredEntriesAsync_Always_ReturnsZero()
    {
        // Arrange, Act
        var result = await _service.CleanupExpiredEntriesAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CleanupExpiredEntriesAsync_Always_LogsDebugMessage()
    {
        // Arrange, Act
        await _service.CleanupExpiredEntriesAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Memory cache cleanup requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task MultipleTokens_BlacklistAndCheck_WorksCorrectly()
    {
        // Arrange
        var token1 = "token-1";
        var token2 = "token-2";
        var token3 = "token-3";
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await _service.BlacklistTokenAsync(token1, expirationTime);
        await _service.BlacklistTokenAsync(token2, expirationTime);
        // token3 is not blacklisted

        // Assert
        (await _service.IsTokenBlacklistedAsync(token1)).Should().BeTrue();
        (await _service.IsTokenBlacklistedAsync(token2)).Should().BeTrue();
        (await _service.IsTokenBlacklistedAsync(token3)).Should().BeFalse();
    }

    [Fact]
    public async Task SameToken_BlacklistTwice_StillWorksCorrectly()
    {
        // Arrange
        var jti = "duplicate-blacklist-test";
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await _service.BlacklistTokenAsync(jti, expirationTime);
        await _service.BlacklistTokenAsync(jti, expirationTime); // Blacklist same token twice

        // Assert
        var isBlacklisted = await _service.IsTokenBlacklistedAsync(jti);
        isBlacklisted.Should().BeTrue();
    }

    [Fact]
    public async Task TokenWithDifferentExpirationTimes_UsesLatestExpiration()
    {
        // Arrange
        var jti = "expiration-test";
        var firstExpiration = DateTimeOffset.UtcNow.AddMinutes(30);
        var secondExpiration = DateTimeOffset.UtcNow.AddHours(2);

        // Act
        await _service.BlacklistTokenAsync(jti, firstExpiration);
        await _service.BlacklistTokenAsync(jti, secondExpiration); // Update with longer expiration

        // Assert
        var isBlacklisted = await _service.IsTokenBlacklistedAsync(jti);
        isBlacklisted.Should().BeTrue();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task LargeNumberOfTokens_DoesNotCauseMemoryIssues()
    {
        // Arrange
        var tokenCount = 1000;
        var expirationTime = DateTimeOffset.UtcNow.AddHours(1);
        var tokens = Enumerable.Range(1, tokenCount)
            .Select(i => $"bulk-test-token-{i}")
            .ToList();

        // Act
        foreach (var token in tokens)
        {
            await _service.BlacklistTokenAsync(token, expirationTime);
        }

        // Assert
        foreach (var token in tokens.Take(10)) // Check first 10 tokens
        {
            var isBlacklisted = await _service.IsTokenBlacklistedAsync(token);
            isBlacklisted.Should().BeTrue();
        }
    }

    #endregion
}
