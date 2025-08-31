using System.Text.Json;

namespace Castellan.Worker.Models;

public sealed class SecurityEvent
{
    public string Id { get; set; } = string.Empty;
    public required LogEvent OriginalEvent { get; init; }
    public required string RiskLevel { get; init; }
    public required int Confidence { get; init; }
    public required string Summary { get; init; }
    public required string[] MitreTechniques { get; init; }
    public required string[] RecommendedActions { get; init; }
    public required SecurityEventType EventType { get; init; }
    public required bool IsDeterministic { get; init; }
    public string? EnrichmentData { get; init; }
    
    // M4: Correlation and fusion properties
    public double CorrelationScore { get; init; }
    public double BurstScore { get; init; }
    public double AnomalyScore { get; init; }
    public bool IsCorrelationBased { get; init; }
    public bool IsEnhanced { get; init; }

    public static SecurityEvent CreateDeterministic(
        LogEvent originalEvent,
        SecurityEventType eventType,
        string riskLevel,
        int confidence,
        string summary,
        string[] mitreTechniques,
        string[] recommendedActions,
        string? enrichmentData = null)
    {
        return new SecurityEvent
        {
            OriginalEvent = originalEvent,
            EventType = eventType,
            RiskLevel = riskLevel,
            Confidence = confidence,
            Summary = summary,
            MitreTechniques = mitreTechniques,
            RecommendedActions = recommendedActions,
            IsDeterministic = true,
            EnrichmentData = enrichmentData,
            CorrelationScore = 0.0,
            BurstScore = 0.0,
            AnomalyScore = 0.0,
            IsCorrelationBased = false,
            IsEnhanced = false
        };
    }

    public static SecurityEvent CreateFromLlmResponse(
        LogEvent originalEvent,
        string llmResponse,
        SecurityEventType eventType = SecurityEventType.Unknown)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(llmResponse);
            var root = jsonDoc.RootElement;

            return new SecurityEvent
            {
                OriginalEvent = originalEvent,
                EventType = eventType,
                RiskLevel = root.GetProperty("risk").GetString() ?? "unknown",
                Confidence = root.GetProperty("confidence").GetInt32(),
                Summary = root.GetProperty("summary").GetString() ?? "",
                MitreTechniques = root.GetProperty("mitre").EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray(),
                RecommendedActions = root.GetProperty("recommended_actions").EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray(),
                IsDeterministic = false,
                CorrelationScore = 0.0,
                BurstScore = 0.0,
                AnomalyScore = 0.0,
                IsCorrelationBased = false,
                IsEnhanced = false
            };
        }
        catch (JsonException)
        {
            // Fallback for malformed JSON
            return new SecurityEvent
            {
                OriginalEvent = originalEvent,
                EventType = SecurityEventType.Unknown,
                RiskLevel = "unknown",
                Confidence = 0,
                Summary = "Failed to parse LLM response",
                MitreTechniques = Array.Empty<string>(),
                RecommendedActions = Array.Empty<string>(),
                IsDeterministic = false,
                CorrelationScore = 0.0,
                BurstScore = 0.0,
                AnomalyScore = 0.0,
                IsCorrelationBased = false,
                IsEnhanced = false
            };
        }
    }

    // M4: New factory methods for correlation-based and enhanced events
    public static SecurityEvent CreateCorrelationBased(
        LogEvent originalEvent,
        string eventType,
        string riskLevel,
        int confidence,
        string summary,
        string[] mitreTechniques,
        string[] recommendedActions,
        double correlationScore,
        double burstScore,
        double anomalyScore)
    {
        return new SecurityEvent
        {
            OriginalEvent = originalEvent,
            EventType = ParseEventType(eventType),
            RiskLevel = riskLevel,
            Confidence = confidence,
            Summary = summary,
            MitreTechniques = mitreTechniques,
            RecommendedActions = recommendedActions,
            IsDeterministic = false,
            CorrelationScore = correlationScore,
            BurstScore = burstScore,
            AnomalyScore = anomalyScore,
            IsCorrelationBased = true,
            IsEnhanced = false
        };
    }

    public static SecurityEvent CreateEnhanced(
        LogEvent originalEvent,
        SecurityEventType eventType,
        string riskLevel,
        int confidence,
        string summary,
        string[] mitreTechniques,
        string[] recommendedActions,
        bool isDeterministic,
        double correlationScore,
        double burstScore,
        double anomalyScore)
    {
        return new SecurityEvent
        {
            OriginalEvent = originalEvent,
            EventType = eventType,
            RiskLevel = riskLevel,
            Confidence = confidence,
            Summary = summary,
            MitreTechniques = mitreTechniques,
            RecommendedActions = recommendedActions,
            IsDeterministic = isDeterministic,
            CorrelationScore = correlationScore,
            BurstScore = burstScore,
            AnomalyScore = anomalyScore,
            IsCorrelationBased = false,
            IsEnhanced = true
        };
    }

    private static SecurityEventType ParseEventType(string eventType)
    {
        return eventType.ToLowerInvariant() switch
        {
            "burstactivity" => SecurityEventType.BurstActivity,
            "correlatedactivity" => SecurityEventType.CorrelatedActivity,
            "anomalousactivity" => SecurityEventType.AnomalousActivity,
            "suspiciousactivity" => SecurityEventType.SuspiciousActivity,
            _ => SecurityEventType.Unknown
        };
    }
}

public enum SecurityEventType
{
    Unknown,
    AuthenticationSuccess,
    AuthenticationFailure,
    PrivilegeEscalation,
    AccountManagement,
    ProcessCreation,
    ServiceInstallation,
    RegistryModification,
    NetworkConnection,
    FileAccess,
    ScheduledTask,
    PowerShellExecution,
    DefenderAlert,
    SystemStartup,
    SystemShutdown,
    SecurityPolicyChange,
    // M4: New event types for correlation-based detection
    BurstActivity,
    CorrelatedActivity,
    AnomalousActivity,
    SuspiciousActivity
}

