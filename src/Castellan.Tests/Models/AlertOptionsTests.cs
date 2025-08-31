using FluentAssertions;
using Castellan.Worker.Models;
using Xunit;

namespace Castellan.Tests.Models;

public class AlertOptionsTests
{
    [Fact]
    public void AlertOptions_ShouldCreateInstanceWithValidData()
    {
        // Arrange
        var minRiskLevel = "Medium";
        var enableConsoleAlerts = true;
        var enableFileLogging = true;

        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = minRiskLevel,
            EnableConsoleAlerts = enableConsoleAlerts,
            EnableFileLogging = enableFileLogging
        };

        // Assert
        alertOptions.Should().NotBeNull();
        alertOptions.MinRiskLevel.Should().Be(minRiskLevel);
        alertOptions.EnableConsoleAlerts.Should().Be(enableConsoleAlerts);
        alertOptions.EnableFileLogging.Should().Be(enableFileLogging);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Critical")]
    public void AlertOptions_ShouldHandleVariousRiskLevels(string riskLevel)
    {
        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = riskLevel
        };

        // Assert
        alertOptions.MinRiskLevel.Should().Be(riskLevel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlertOptions_ShouldHandleConsoleAlertsSetting(bool enableConsoleAlerts)
    {
        // Act
        var alertOptions = new AlertOptions
        {
            EnableConsoleAlerts = enableConsoleAlerts
        };

        // Assert
        alertOptions.EnableConsoleAlerts.Should().Be(enableConsoleAlerts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlertOptions_ShouldHandleFileLoggingSetting(bool enableFileLogging)
    {
        // Act
        var alertOptions = new AlertOptions
        {
            EnableFileLogging = enableFileLogging
        };

        // Assert
        alertOptions.EnableFileLogging.Should().Be(enableFileLogging);
    }

    [Fact]
    public void AlertOptions_ShouldHandleNullRiskLevel()
    {
        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = null!
        };

        // Assert
        alertOptions.MinRiskLevel.Should().BeNull();
    }

    [Fact]
    public void AlertOptions_ShouldHandleEmptyRiskLevel()
    {
        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = ""
        };

        // Assert
        alertOptions.MinRiskLevel.Should().Be("");
    }

    [Fact]
    public void AlertOptions_ShouldHandleDefaultValues()
    {
        // Act
        var alertOptions = new AlertOptions();

        // Assert
        alertOptions.Should().NotBeNull();
        alertOptions.MinRiskLevel.Should().Be("medium");
        alertOptions.EnableConsoleAlerts.Should().BeTrue();
        alertOptions.EnableFileLogging.Should().BeTrue();
    }

    [Fact]
    public void AlertOptions_ShouldHandleAllSettingsCombined()
    {
        // Arrange
        var minRiskLevel = "High";
        var enableConsoleAlerts = true;
        var enableFileLogging = false;

        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = minRiskLevel,
            EnableConsoleAlerts = enableConsoleAlerts,
            EnableFileLogging = enableFileLogging
        };

        // Assert
        alertOptions.Should().NotBeNull();
        alertOptions.MinRiskLevel.Should().Be(minRiskLevel);
        alertOptions.EnableConsoleAlerts.Should().Be(enableConsoleAlerts);
        alertOptions.EnableFileLogging.Should().Be(enableFileLogging);
    }

    [Fact]
    public void AlertOptions_ShouldHandleCaseInsensitiveRiskLevel()
    {
        // Arrange
        var riskLevels = new[] { "low", "LOW", "Low", "medium", "MEDIUM", "Medium", "high", "HIGH", "High", "critical", "CRITICAL", "Critical" };

        foreach (var riskLevel in riskLevels)
        {
            // Act
            var alertOptions = new AlertOptions
            {
                MinRiskLevel = riskLevel
            };

            // Assert
            alertOptions.MinRiskLevel.Should().Be(riskLevel);
        }
    }

    [Fact]
    public void AlertOptions_ShouldHandleSpecialCharactersInRiskLevel()
    {
        // Arrange
        var riskLevelWithSpecialChars = "High-Risk";

        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = riskLevelWithSpecialChars
        };

        // Assert
        alertOptions.MinRiskLevel.Should().Be(riskLevelWithSpecialChars);
    }

    [Fact]
    public void AlertOptions_ShouldHandleLongRiskLevel()
    {
        // Arrange
        var longRiskLevel = new string('A', 100);

        // Act
        var alertOptions = new AlertOptions
        {
            MinRiskLevel = longRiskLevel
        };

        // Assert
        alertOptions.MinRiskLevel.Should().Be(longRiskLevel);
        alertOptions.MinRiskLevel.Length.Should().Be(100);
    }
}

