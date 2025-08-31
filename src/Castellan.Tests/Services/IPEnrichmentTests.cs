using FluentAssertions;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

[Trait("Category", "Unit")]
public class IPEnrichmentTests : IDisposable
{
    private readonly Mock<ILogger<MaxMindIPEnrichmentService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly IPEnrichmentOptions _options;
    private readonly MaxMindIPEnrichmentService _service;

    public IPEnrichmentTests()
    {
        _mockLogger = new Mock<ILogger<MaxMindIPEnrichmentService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _options = new IPEnrichmentOptions
        {
            Enabled = true,
            Provider = "MaxMind",
            CacheMinutes = 60,
            EnrichPrivateIPs = false,
            HighRiskCountries = new List<string> { "CN", "RU", "KP" },
            HighRiskASNs = new List<int> { 12345 }
        };

        _service = new MaxMindIPEnrichmentService(
            _mockLogger.Object,
            Options.Create(_options),
            _memoryCache);
    }

    [Fact]
    public async Task EnrichAsync_WithPrivateIP_ReturnsPrivateResult()
    {
        // Arrange
        var privateIP = "192.168.1.1";

        // Act
        var result = await _service.EnrichAsync(privateIP);

        // Assert
        result.Should().NotBeNull();
        result.IPAddress.Should().Be(privateIP);
        result.IsPrivate.Should().BeTrue();
        result.IsEnriched.Should().BeTrue();
        result.Country.Should().Be("Private Network");
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.1")]
    [InlineData("127.0.0.1")]
    public async Task EnrichAsync_WithPrivateIPs_ReturnsPrivateResult(string privateIP)
    {
        // Act
        var result = await _service.EnrichAsync(privateIP);

        // Assert
        result.IsPrivate.Should().BeTrue();
        result.IsEnriched.Should().BeTrue();
        result.Country.Should().Be("Private Network");
    }

    [Fact]
    public async Task EnrichAsync_WithEmptyIP_ReturnsFailedResult()
    {
        // Act
        var result = await _service.EnrichAsync("");

        // Assert
        result.Should().NotBeNull();
        result.IsEnriched.Should().BeFalse();
        result.Error.Should().Be("IP address is null or empty");
    }

    [Fact]
    public async Task EnrichAsync_WithInvalidIP_ReturnsFailedResult()
    {
        // Arrange
        var invalidIP = "not.an.ip.address";

        // Act
        var result = await _service.EnrichAsync(invalidIP);

        // Assert
        result.Should().NotBeNull();
        result.IPAddress.Should().Be(invalidIP);
        result.IsEnriched.Should().BeFalse();
        result.Error.Should().Be("Invalid IP address format");
    }

    [Fact]
    public async Task EnrichAsync_WhenDisabled_ReturnsFailedResult()
    {
        // Arrange
        _options.Enabled = false;
        var publicIP = "8.8.8.8";

        // Act
        var result = await _service.EnrichAsync(publicIP);

        // Assert
        result.Should().NotBeNull();
        result.IsEnriched.Should().BeFalse();
        result.Error.Should().Be("IP enrichment is disabled");
    }

    [Fact]
    public async Task EnrichAsync_WithCaching_ReturnsCachedResult()
    {
        // Arrange
        var privateIP = "192.168.1.100";

        // Act - First call
        var result1 = await _service.EnrichAsync(privateIP);
        
        // Act - Second call (should use cache)
        var result2 = await _service.EnrichAsync(privateIP);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.IPAddress.Should().Be(result2.IPAddress);
        result1.IsPrivate.Should().Be(result2.IsPrivate);
    }

    [Fact]
    public async Task EnrichBatchAsync_WithMultipleIPs_ReturnsAllResults()
    {
        // Arrange
        var ipAddresses = new[] { "192.168.1.1", "10.0.0.1", "invalid.ip" };

        // Act
        var results = await _service.EnrichBatchAsync(ipAddresses);

        // Assert
        results.Should().HaveCount(3);
        results.Keys.Should().Contain(ipAddresses);
        
        results["192.168.1.1"].IsPrivate.Should().BeTrue();
        results["10.0.0.1"].IsPrivate.Should().BeTrue();
        results["invalid.ip"].IsEnriched.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        _options.Enabled = false;

        // Act
        var isHealthy = await _service.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public void IPEnrichmentResult_Success_CreatesValidResult()
    {
        // Act
        var result = IPEnrichmentResult.Success(
            ipAddress: "8.8.8.8",
            country: "United States",
            countryCode: "US",
            city: "Mountain View",
            latitude: 37.386,
            longitude: -122.0838,
            asn: 15169,
            asnOrganization: "Google LLC",
            isHighRisk: false,
            riskFactors: new List<string>(),
            isPrivate: false
        );

        // Assert
        result.Should().NotBeNull();
        result.IPAddress.Should().Be("8.8.8.8");
        result.Country.Should().Be("United States");
        result.CountryCode.Should().Be("US");
        result.City.Should().Be("Mountain View");
        result.Latitude.Should().Be(37.386);
        result.Longitude.Should().Be(-122.0838);
        result.ASN.Should().Be(15169);
        result.ASNOrganization.Should().Be("Google LLC");
        result.IsHighRisk.Should().BeFalse();
        result.IsPrivate.Should().BeFalse();
        result.IsEnriched.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void IPEnrichmentResult_Failed_CreatesFailedResult()
    {
        // Arrange
        var ip = "1.2.3.4";
        var error = "Database not found";

        // Act
        var result = IPEnrichmentResult.Failed(ip, error);

        // Assert
        result.Should().NotBeNull();
        result.IPAddress.Should().Be(ip);
        result.IsEnriched.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void IPEnrichmentResult_Private_CreatesPrivateResult()
    {
        // Arrange
        var privateIP = "192.168.1.1";

        // Act
        var result = IPEnrichmentResult.Private(privateIP);

        // Assert
        result.Should().NotBeNull();
        result.IPAddress.Should().Be(privateIP);
        result.IsPrivate.Should().BeTrue();
        result.IsEnriched.Should().BeTrue();
        result.Country.Should().Be("Private Network");
        result.RiskFactors.Should().BeEmpty();
    }

    [Fact]
    public void IPEnrichmentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new IPEnrichmentOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Provider.Should().Be("MaxMind");
        options.CacheMinutes.Should().Be(60);
        options.MaxCacheEntries.Should().Be(10000);
        options.TimeoutMs.Should().Be(5000);
        options.EnrichPrivateIPs.Should().BeFalse();
        options.HighRiskCountries.Should().NotBeEmpty();
        options.HighRiskASNs.Should().BeEmpty();
        options.EnableDebugLogging.Should().BeFalse();
    }

    public void Dispose()
    {
        _service?.Dispose();
        _memoryCache?.Dispose();
    }
}
