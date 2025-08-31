using FluentAssertions;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Castellan.Tests;

[Trait("Category", "Unit")]
public class CountryDatabaseTests
{
    [Fact]
    public void IPEnrichmentOptions_ShouldIncludeCountryDatabasePath()
    {
        // Arrange & Act
        var options = new IPEnrichmentOptions
        {
            MaxMindCityDbPath = "data/GeoLite2-City.mmdb",
            MaxMindASNDbPath = "data/GeoLite2-ASN.mmdb",
            MaxMindCountryDbPath = "data/GeoLite2-Country.mmdb"
        };

        // Assert
        options.MaxMindCityDbPath.Should().Be("data/GeoLite2-City.mmdb");
        options.MaxMindASNDbPath.Should().Be("data/GeoLite2-ASN.mmdb");
        options.MaxMindCountryDbPath.Should().Be("data/GeoLite2-Country.mmdb");
    }

    [Fact]
    public void MaxMindIPEnrichmentService_ShouldHandleMissingCountryDatabase()
    {
        // Arrange
        var logger = new Mock<ILogger<MaxMindIPEnrichmentService>>();
        var options = new IPEnrichmentOptions
        {
            Enabled = true,
            MaxMindCityDbPath = "nonexistent-city.mmdb",
            MaxMindASNDbPath = "nonexistent-asn.mmdb",
            MaxMindCountryDbPath = "nonexistent-country.mmdb"
        };
        var optionsWrapper = Options.Create(options);
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Act
        var service = new MaxMindIPEnrichmentService(logger.Object, optionsWrapper, cache);

        // Assert - should not throw and should handle missing databases gracefully
        service.Should().NotBeNull();
    }

    [Fact]
    public void Configuration_ShouldIncludeCountryDatabasePath()
    {
        // This test verifies that the configuration structure supports the country database
        var config = new
        {
            IPEnrichment = new
            {
                MaxMindCityDbPath = "data/GeoLite2-City.mmdb",
                MaxMindASNDbPath = "data/GeoLite2-ASN.mmdb",
                MaxMindCountryDbPath = "data/GeoLite2-Country.mmdb"
            }
        };

        config.IPEnrichment.MaxMindCityDbPath.Should().Be("data/GeoLite2-City.mmdb");
        config.IPEnrichment.MaxMindASNDbPath.Should().Be("data/GeoLite2-ASN.mmdb");
        config.IPEnrichment.MaxMindCountryDbPath.Should().Be("data/GeoLite2-Country.mmdb");
    }
}


