using System;
using System.Collections.Generic;
using Castellan.Worker.Models;

namespace Castellan.Tests.TestUtilities;

public static class TestDataFactory
{
    public static LogEvent CreateSecurityEvent(int eventId, string user)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            "Security",
            eventId,
            "Information",
            user,
            $"Test security event {eventId} for user {user}",
            "{}"
        );
    }

    public static LogEvent CreateSystemEvent(int eventId, string message)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            "System",
            eventId,
            "Information",
            "SYSTEM",
            message,
            "{}"
        );
    }

    public static LogEvent CreateApplicationEvent(int eventId, string message)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            "Application",
            eventId,
            "Information",
            "Application",
            message,
            "{}"
        );
    }

    public static float[] CreateTestEmbedding(int dimensions)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var embedding = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() - 0.5);
        }
        return embedding;
    }

    public static List<(LogEvent evt, float score)> CreateTestSearchResults(int count)
    {
        var results = new List<(LogEvent evt, float score)>();
        for (int i = 0; i < count; i++)
        {
            var logEvent = CreateSecurityEvent(4624 + i, $"user{i}");
            var score = 1.0f - (i * 0.1f); // Decreasing scores
            results.Add((logEvent, score));
        }
        return results;
    }

    public static SecurityEvent CreateTestSecurityEvent(LogEvent originalEvent, SecurityEventType eventType = SecurityEventType.AuthenticationSuccess)
    {
        return SecurityEvent.CreateDeterministic(
            originalEvent,
            eventType,
            "medium",
            75,
            "Test security event",
            new[] { "T1078" },
            new[] { "Monitor user activity" }
        );
    }

    public static string CreateTestLlmResponse()
    {
        return @"{
            ""risk"": ""medium"",
            ""mitre"": [""T1078"", ""T1078.002""],
            ""confidence"": 75,
            ""summary"": ""Suspicious login activity detected"",
            ""recommended_actions"": [""Monitor user activity"", ""Enable MFA""]
        }";
    }

    public static MalwareRule CreateTestMalwareRule(string name, string description, string category = "Malware")
    {
        return new MalwareRule
        {
            Name = name,
            Description = description,
            RuleContent = $"rule {name} {{ condition: true }}",
            Category = category,
            Author = "Test Author",
            IsEnabled = true,
            Priority = 50,
            ThreatLevel = "Medium",
            MitreTechniques = new List<string> { "T1059.001" },
            Tags = new List<string> { "test" },
            IsValid = true,
            Source = "Test"
        };
    }

    public static MalwareMatch CreateTestMalwareMatch(string ruleId, string ruleName)
    {
        return new MalwareMatch
        {
            RuleId = ruleId,
            RuleName = ruleName,
            MatchTime = DateTimeOffset.UtcNow.DateTime,
            TargetFile = "C:\\test\\sample.exe",
            TargetHash = "abc123def456",
            MatchedStrings = new List<MalwareMatchString>
            {
                new MalwareMatchString
                {
                    Identifier = "$test_string",
                    Offset = 100,
                    Value = "suspicious_pattern",
                    IsHex = false
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "severity", "high" },
                { "confidence", "85" }
            },
            ExecutionTimeMs = 12.5,
            SecurityEventId = "event-123"
        };
    }

    public static MalwareRuleRequest CreateTestMalwareRuleRequest(string name, string description)
    {
        return new MalwareRuleRequest
        {
            Name = name,
            Description = description,
            RuleContent = $"rule {name} {{ condition: true }}",
            Category = "Malware",
            Author = "Test Author",
            IsEnabled = true,
            Priority = 75,
            ThreatLevel = "High",
            MitreTechniques = new List<string> { "T1059.001" },
            Tags = new List<string> { "test" }
        };
    }
}

