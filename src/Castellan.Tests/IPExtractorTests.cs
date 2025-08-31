using FluentAssertions;
using Castellan.Worker.Services;
using Xunit;

namespace Castellan.Tests;

[Trait("Category", "Unit")]
public class IPExtractorTests
{
    [Fact]
    public void ExtractIPAddresses_ShouldFindValidIPs()
    {
        // Arrange
        var message = "Connection from 192.168.1.100 to server 10.0.0.1 via 8.8.8.8";

        // Act
        var result = IPExtractor.ExtractIPAddresses(message);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("192.168.1.100");
        result.Should().Contain("10.0.0.1");
        result.Should().Contain("8.8.8.8");
    }

    [Fact]
    public void ExtractIPAddresses_ShouldIgnoreInvalidIPs()
    {
        // Arrange
        var message = "Invalid IPs: 0.0.0.0, 255.255.255.255, 999.999.999.999";

        // Act
        var result = IPExtractor.ExtractIPAddresses(message);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAuthenticationIPs_ShouldFindSourceAddresses_Event4625()
    {
        // Arrange - simulated failed logon event message
        var message = @"An account failed to log on.

Subject:
    Security ID:        S-1-0-0
    Account Name:       -
    Account Domain:     -
    Logon ID:           0x0

Logon Type:         3

Account For Which Logon Failed:
    Security ID:        S-1-0-0
    Account Name:       testuser
    Account Domain:     .

Failure Information:
    Failure Reason:     Unknown user name or bad password.
    Status:             0xC000006D
    Sub Status:         0xC000006A

Process Information:
    Caller Process ID:  0x0
    Caller Process Name: -

Network Information:
    Workstation Name:   TESTPC
    Source Network Address: 203.0.113.45
    Source Port:        49152";

        // Act
        var result = IPExtractor.ExtractAuthenticationIPs(message, 4625);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("203.0.113.45");
    }

    [Fact]
    public void GetPrimaryIP_ShouldPreferPublicOverPrivate()
    {
        // Arrange
        var ipList = new List<string> { "192.168.1.1", "8.8.8.8", "10.0.0.1" };

        // Act
        var result = IPExtractor.GetPrimaryIP(ipList);

        // Assert
        result.Should().Be("8.8.8.8");
    }

    [Fact]
    public void GetPrimaryIP_ShouldHandleOnlyPrivateIPs()
    {
        // Arrange
        var ipList = new List<string> { "192.168.1.1", "10.0.0.1" };

        // Act
        var result = IPExtractor.GetPrimaryIP(ipList);

        // Assert
        result.Should().Be("192.168.1.1");
    }

    [Fact]
    public void GetPrimaryIP_ShouldReturnNullForEmptyList()
    {
        // Arrange
        var ipList = new List<string>();

        // Act
        var result = IPExtractor.GetPrimaryIP(ipList);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractAuthenticationIPs_ShouldHandleMultipleSourceFormats()
    {
        // Arrange - different formats of source address fields
        var message1 = "Source Network Address: 198.51.100.10";
        var message2 = "Client Address: 198.51.100.20";
        var message3 = "Source IP: 198.51.100.30";

        // Act
        var result1 = IPExtractor.ExtractAuthenticationIPs(message1, 4624);
        var result2 = IPExtractor.ExtractAuthenticationIPs(message2, 4624);
        var result3 = IPExtractor.ExtractAuthenticationIPs(message3, 4624);

        // Assert
        result1.Should().Contain("198.51.100.10");
        result2.Should().Contain("198.51.100.20");
        result3.Should().Contain("198.51.100.30");
    }

    [Fact]
    public void ExtractIPAddresses_ShouldHandleEmptyMessage()
    {
        // Arrange
        var message = "";

        // Act
        var result = IPExtractor.ExtractIPAddresses(message);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractIPAddresses_ShouldHandleNullMessage()
    {
        // Arrange
        string? message = null;

        // Act
        var result = IPExtractor.ExtractIPAddresses(message!);

        // Assert
        result.Should().BeEmpty();
    }
}
