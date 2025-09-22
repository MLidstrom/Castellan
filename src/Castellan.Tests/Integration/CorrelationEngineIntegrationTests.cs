using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Tests.Integration;

public class CorrelationEngineIntegrationTests
{
    private readonly Mock<ILogger<CorrelationEngine>> _mockLogger;
    private readonly Mock<ISecurityEventStore> _mockEventStore;
    private readonly CorrelationEngine _correlationEngine;
    private readonly List<SecurityEvent> _storedEvents;

    public CorrelationEngineIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<CorrelationEngine>>();
        _mockEventStore = new Mock<ISecurityEventStore>();
        _storedEvents = new List<SecurityEvent>();

        // Setup mock to store events in our list
        _mockEventStore.Setup(x => x.AddSecurityEvent(It.IsAny<SecurityEvent>()))
            .Callback<SecurityEvent>(evt => _storedEvents.Add(evt));

        // Setup mock to return events from our list based on filters
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<int, int, Dictionary<string, object>>((page, pageSize, filters) =>
            {
                var events = _storedEvents.AsEnumerable();

                if (filters != null && filters.ContainsKey("StartTime") && filters.ContainsKey("EndTime"))
                {
                    var startTime = (DateTime)filters["StartTime"];
                    var endTime = (DateTime)filters["EndTime"];
                    events = events.Where(e => e.OriginalEvent.Time.DateTime >= startTime && e.OriginalEvent.Time.DateTime <= endTime);
                }

                return events.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            });

        _correlationEngine = new CorrelationEngine(_mockLogger.Object, _mockEventStore.Object);
    }

    [Fact]
    public async Task RealWorldScenario_BruteForceAttack_DetectsCorrelation()
    {
        // Arrange: Simulate a brute force attack scenario
        var targetHost = "DC-01.contoso.com";
        var attackerUser = "admin";
        var now = DateTime.UtcNow;

        // Add multiple failed authentication attempts
        var failedAttempts = new List<SecurityEvent>();
        for (int i = 0; i < 10; i++)
        {
            var failedEvent = CreateAuthenticationFailureEvent(targetHost, attackerUser, now.AddMinutes(-10 + i));
            failedAttempts.Add(failedEvent);
            _mockEventStore.Object.AddSecurityEvent(failedEvent);
        }

        // Add a successful authentication (typical of brute force)
        var successEvent = CreateAuthenticationSuccessEvent(targetHost, attackerUser, now);
        _mockEventStore.Object.AddSecurityEvent(successEvent);

        // Act: Analyze the successful authentication event
        var result = await _correlationEngine.AnalyzeEventAsync(successEvent);

        // Assert
        Assert.True(result.HasCorrelation);
        Assert.True(result.ConfidenceScore > 0.7);
        Assert.Contains("Brute Force Attack", result.MatchedRules);
        Assert.NotNull(result.Correlation);
        Assert.Equal("high", result.Correlation.RiskLevel);
        Assert.Contains("T1110", result.Correlation.MitreTechniques); // Brute Force MITRE technique
    }

    [Fact]
    public async Task RealWorldScenario_LateralMovement_DetectsCorrelation()
    {
        // Arrange: Simulate lateral movement across multiple hosts
        var hosts = new[] { "WS-001", "WS-002", "WS-003", "WS-004" };
        var now = DateTime.UtcNow;
        var events = new List<SecurityEvent>();

        // Simulate similar suspicious activity across multiple machines
        for (int i = 0; i < hosts.Length; i++)
        {
            var networkEvent = CreateNetworkConnectionEvent(hosts[i], "192.168.1.100", now.AddMinutes(i * 2));
            events.Add(networkEvent);
            _mockEventStore.Object.AddSecurityEvent(networkEvent);
        }

        // Act: Analyze batch of events
        var correlations = await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(30));

        // Assert
        Assert.NotEmpty(correlations);
        var lateralMovement = correlations.FirstOrDefault(c => c.CorrelationType == "LateralMovement");
        Assert.NotNull(lateralMovement);
        Assert.True(lateralMovement.ConfidenceScore > 0.75);
        Assert.Equal("high", lateralMovement.RiskLevel);
        Assert.Equal(4, lateralMovement.EventIds.Count);
    }

    [Fact]
    public async Task RealWorldScenario_TemporalBurst_DetectsAnomalousActivity()
    {
        // Arrange: Simulate a temporal burst of process creation events (potential malware)
        var targetHost = "WS-005";
        var now = DateTime.UtcNow;
        var events = new List<SecurityEvent>();

        // Create a burst of process creation events within a short timeframe
        for (int i = 0; i < 15; i++)
        {
            var processEvent = CreateProcessCreationEvent(targetHost, $"malware_{i}.exe", now.AddSeconds(i * 5));
            events.Add(processEvent);
            _mockEventStore.Object.AddSecurityEvent(processEvent);
        }

        // Act: Analyze batch for temporal patterns
        var correlations = await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(5));

        // Assert
        Assert.NotEmpty(correlations);
        var temporalBurst = correlations.FirstOrDefault(c => c.CorrelationType == "TemporalBurst");
        Assert.NotNull(temporalBurst);
        Assert.True(temporalBurst.ConfidenceScore > 0.8);
        Assert.Contains("medium", temporalBurst.RiskLevel.ToLower());
        Assert.Equal(15, temporalBurst.EventIds.Count);
    }

    [Fact]
    public async Task RealWorldScenario_PrivilegeEscalation_DetectsAttackChain()
    {
        // Arrange: Simulate a privilege escalation attack chain
        var targetHost = "WS-006";
        var now = DateTime.UtcNow;

        // Step 1: Initial authentication
        var authEvent = CreateAuthenticationSuccessEvent(targetHost, "normaluser", now);
        _mockEventStore.Object.AddSecurityEvent(authEvent);

        // Step 2: Privilege escalation attempt
        var privEscEvent = CreatePrivilegeEscalationEvent(targetHost, "normaluser", now.AddMinutes(2));
        _mockEventStore.Object.AddSecurityEvent(privEscEvent);

        // Step 3: Suspicious process creation with elevated privileges
        var processEvent = CreateProcessCreationEvent(targetHost, "powershell.exe", now.AddMinutes(3));
        _mockEventStore.Object.AddSecurityEvent(processEvent);

        var events = new List<SecurityEvent> { authEvent, privEscEvent, processEvent };

        // Act: Detect attack chains
        var chains = await _correlationEngine.DetectAttackChainsAsync(events, TimeSpan.FromMinutes(30));

        // Assert
        Assert.NotEmpty(chains);
        var chain = chains.First();
        Assert.True(chain.ConfidenceScore > 0.8);
        Assert.Equal("high", chain.RiskLevel);
        Assert.Equal(3, chain.Stages.Count);
        Assert.Contains("Privilege Escalation", chain.AttackType);
    }

    [Fact]
    public async Task PerformanceTest_HighVolumeEvents_CompletesWithinTimeLimit()
    {
        // Arrange: Create a large number of events
        var events = new List<SecurityEvent>();
        var now = DateTime.UtcNow;
        var hosts = Enumerable.Range(1, 50).Select(i => $"Host-{i:D3}").ToArray();

        for (int i = 0; i < 500; i++)
        {
            var host = hosts[i % hosts.Length];
            var eventTime = now.AddMinutes(-i);
            var evt = CreateProcessCreationEvent(host, $"process_{i}.exe", eventTime);
            events.Add(evt);
            _mockEventStore.Object.AddSecurityEvent(evt);
        }

        var startTime = DateTime.UtcNow;

        // Act: Analyze large batch
        var correlations = await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromHours(2));

        // Assert
        var duration = DateTime.UtcNow - startTime;
        Assert.True(duration.TotalSeconds < 10, $"Analysis took {duration.TotalSeconds} seconds, expected < 10 seconds");

        // Should find some temporal bursts due to the volume
        Assert.NotEmpty(correlations);
        Assert.Contains(correlations, c => c.CorrelationType == "TemporalBurst");
    }

    [Fact]
    public async Task CorrelationStatistics_WithMultipleCorrelations_ReturnsAccurateMetrics()
    {
        // Arrange: Create various types of correlations
        await CreateBruteForceScenario();
        await CreateLateralMovementScenario();
        await CreateTemporalBurstScenario();

        // Act: Get statistics
        var stats = await _correlationEngine.GetStatisticsAsync();

        // Assert
        Assert.True(stats.CorrelationsDetected >= 3);
        Assert.True(stats.AverageConfidenceScore > 0);
        Assert.NotEmpty(stats.CorrelationsByType);
        Assert.True(stats.TotalEventsProcessed > 0);
        Assert.NotEmpty(stats.TopPatterns);
    }

    private async Task CreateBruteForceScenario()
    {
        var now = DateTime.UtcNow.AddHours(-3);
        var events = new List<SecurityEvent>();

        for (int i = 0; i < 5; i++)
        {
            events.Add(CreateAuthenticationFailureEvent("Host-BF", "admin", now.AddMinutes(i)));
        }
        events.Add(CreateAuthenticationSuccessEvent("Host-BF", "admin", now.AddMinutes(6)));

        foreach (var evt in events)
        {
            _mockEventStore.Object.AddSecurityEvent(evt);
        }

        await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(30));
    }

    private async Task CreateLateralMovementScenario()
    {
        var now = DateTime.UtcNow.AddHours(-2);
        var events = new List<SecurityEvent>
        {
            CreateNetworkConnectionEvent("Host-LM-1", "10.0.0.100", now),
            CreateNetworkConnectionEvent("Host-LM-2", "10.0.0.100", now.AddMinutes(5)),
            CreateNetworkConnectionEvent("Host-LM-3", "10.0.0.100", now.AddMinutes(10))
        };

        foreach (var evt in events)
        {
            _mockEventStore.Object.AddSecurityEvent(evt);
        }

        await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(30));
    }

    private async Task CreateTemporalBurstScenario()
    {
        var now = DateTime.UtcNow.AddHours(-1);
        var events = new List<SecurityEvent>();

        for (int i = 0; i < 8; i++)
        {
            events.Add(CreateProcessCreationEvent("Host-TB", $"burst_{i}.exe", now.AddSeconds(i * 10)));
        }

        foreach (var evt in events)
        {
            _mockEventStore.Object.AddSecurityEvent(evt);
        }

        await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(5));
    }

    private SecurityEvent CreateAuthenticationFailureEvent(string host, string user, DateTime timestamp)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = SecurityEventType.AuthenticationFailure,
            OriginalEvent = new LogEvent(
                Time: new DateTimeOffset(timestamp, TimeSpan.Zero),
                Host: host,
                Channel: "Security",
                EventId: 4625,
                Level: "Information",
                User: user,
                Message: $"An account failed to log on. Account Name: {user}",
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            Summary = $"Authentication failure for user {user} on {host}",
            MitreTechniques = new[] { "T1110.001" }, // Brute Force: Password Guessing
            RiskLevel = "Medium",
            Confidence = 85
        };
    }

    private SecurityEvent CreateAuthenticationSuccessEvent(string host, string user, DateTime timestamp)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = SecurityEventType.AuthenticationSuccess,
            OriginalEvent = new LogEvent(
                Time: new DateTimeOffset(timestamp, TimeSpan.Zero),
                Host: host,
                Channel: "Security",
                EventId: 4624,
                Level: "Information",
                User: user,
                Message: $"An account was successfully logged on. Account Name: {user}",
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            Summary = $"Successful authentication for user {user} on {host}",
            MitreTechniques = new[] { "T1078" }, // Valid Accounts
            RiskLevel = "Low",
            Confidence = 90
        };
    }

    private SecurityEvent CreateNetworkConnectionEvent(string host, string destIP, DateTime timestamp)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = SecurityEventType.NetworkConnection,
            OriginalEvent = new LogEvent(
                Time: new DateTimeOffset(timestamp, TimeSpan.Zero),
                Host: host,
                Channel: "Microsoft-Windows-Sysmon/Operational",
                EventId: 3,
                Level: "Information",
                User: "SYSTEM",
                Message: $"Network connection detected to {destIP}",
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            Summary = $"Network connection from {host} to {destIP}",
            MitreTechniques = new[] { "T1021" }, // Remote Services
            RiskLevel = "Medium",
            Confidence = 75
        };
    }

    private SecurityEvent CreateProcessCreationEvent(string host, string processName, DateTime timestamp)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = SecurityEventType.ProcessCreation,
            OriginalEvent = new LogEvent(
                Time: new DateTimeOffset(timestamp, TimeSpan.Zero),
                Host: host,
                Channel: "Microsoft-Windows-Sysmon/Operational",
                EventId: 1,
                Level: "Information",
                User: "SYSTEM",
                Message: $"Process Create: {processName}",
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            Summary = $"Process created: {processName} on {host}",
            MitreTechniques = new[] { "T1059" }, // Command and Scripting Interpreter
            RiskLevel = "Medium",
            Confidence = 80
        };
    }

    private SecurityEvent CreatePrivilegeEscalationEvent(string host, string user, DateTime timestamp)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = SecurityEventType.PrivilegeEscalation,
            OriginalEvent = new LogEvent(
                Time: new DateTimeOffset(timestamp, TimeSpan.Zero),
                Host: host,
                Channel: "Security",
                EventId: 4672,
                Level: "Information",
                User: user,
                Message: $"Special privileges assigned to new logon. Account Name: {user}",
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            Summary = $"Privilege escalation detected for user {user} on {host}",
            MitreTechniques = new[] { "T1068" }, // Exploitation for Privilege Escalation
            RiskLevel = "High",
            Confidence = 95
        };
    }

}