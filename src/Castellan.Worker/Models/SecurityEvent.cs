using System.Text.Json;

namespace Castellan.Worker.Models;

/// <summary>
/// Represents a security event detected by the system.
/// This is the main business logic model for security events.
/// </summary>
public class SecurityEvent
{
    public string Id { get; set; } = string.Empty;
    public LogEvent OriginalEvent { get; init; } = null!;
    public SecurityEventType EventType { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public int Confidence { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string[] MitreTechniques { get; init; } = Array.Empty<string>();
    public string[] RecommendedActions { get; init; } = Array.Empty<string>();
    public bool IsDeterministic { get; init; }
    public bool IsCorrelationBased { get; init; }
    public bool IsEnhanced { get; init; }
    public double CorrelationScore { get; init; }
    public double BurstScore { get; init; }
    public double AnomalyScore { get; init; }
    public string? EnrichmentData { get; init; }

    public SecurityEvent() { }

    /// <summary>
    /// Creates a deterministic security event from rule-based detection.
    /// </summary>
    public static SecurityEvent CreateDeterministic(
        LogEvent originalEvent,
        SecurityEventType eventType,
        string riskLevel,
        int confidence,
        string summary,
        string[] mitreTechniques,
        string[] recommendedActions)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            OriginalEvent = originalEvent,
            EventType = eventType,
            RiskLevel = riskLevel,
            Confidence = confidence,
            Summary = summary,
            MitreTechniques = mitreTechniques,
            RecommendedActions = recommendedActions,
            IsDeterministic = true,
            IsCorrelationBased = false,
            IsEnhanced = false,
            CorrelationScore = 0.0,
            BurstScore = 0.0,
            AnomalyScore = 0.0
        };
    }

    /// <summary>
    /// Creates a correlation-based security event.
    /// </summary>
    public static SecurityEvent CreateCorrelationBased(
        LogEvent originalEvent,
        SecurityEventType eventType,
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
            Id = Guid.NewGuid().ToString(),
            OriginalEvent = originalEvent,
            EventType = eventType,
            RiskLevel = riskLevel,
            Confidence = confidence,
            Summary = summary,
            MitreTechniques = mitreTechniques,
            RecommendedActions = recommendedActions,
            IsDeterministic = false,
            IsCorrelationBased = true,
            IsEnhanced = false,
            CorrelationScore = correlationScore,
            BurstScore = burstScore,
            AnomalyScore = anomalyScore
        };
    }

    /// <summary>
    /// Creates an enhanced security event with additional correlation data.
    /// </summary>
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
            Id = Guid.NewGuid().ToString(),
            OriginalEvent = originalEvent,
            EventType = eventType,
            RiskLevel = riskLevel,
            Confidence = confidence,
            Summary = summary,
            MitreTechniques = mitreTechniques,
            RecommendedActions = recommendedActions,
            IsDeterministic = isDeterministic,
            IsCorrelationBased = correlationScore > 0 || burstScore > 0 || anomalyScore > 0,
            IsEnhanced = true,
            CorrelationScore = correlationScore,
            BurstScore = burstScore,
            AnomalyScore = anomalyScore
        };
    }

    /// <summary>
    /// Creates a security event from LLM response JSON.
    /// </summary>
    public static SecurityEvent CreateFromLlmResponse(
        LogEvent originalEvent,
        string llmResponseJson)
    {
        try
        {
            var response = JsonSerializer.Deserialize<LlmSecurityEventResponse>(llmResponseJson);
            if (response == null)
            {
                throw new ArgumentException("Invalid LLM response JSON", nameof(llmResponseJson));
            }

            // Parse event type
            var eventType = Enum.TryParse<SecurityEventType>(response.EventType, true, out var parsedType) 
                ? parsedType 
                : SecurityEventType.Unknown;

            return new SecurityEvent
            {
                Id = Guid.NewGuid().ToString(),
                OriginalEvent = originalEvent,
                EventType = eventType,
                RiskLevel = response.RiskLevel ?? "low",
                Confidence = response.Confidence ?? 50,
                Summary = response.Summary ?? "Security event detected by LLM",
                MitreTechniques = response.MitreTechniques ?? Array.Empty<string>(),
                RecommendedActions = response.RecommendedActions ?? Array.Empty<string>(),
                IsDeterministic = false,
                IsCorrelationBased = false,
                IsEnhanced = false,
                CorrelationScore = 0.0,
                BurstScore = 0.0,
                AnomalyScore = 0.0
            };
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Failed to parse LLM response JSON: {ex.Message}", nameof(llmResponseJson), ex);
        }
    }

    /// <summary>
    /// Converts this SecurityEvent to a SecurityEventEntity for database storage.
    /// </summary>
    public SecurityEventEntity ToEntity()
    {
        return new SecurityEventEntity
        {
            EventId = Id,
            EventType = EventType.ToString(),
            Severity = RiskLevel switch
            {
                "critical" => "Critical",
                "high" => "High",
                "medium" => "Medium",
                "low" => "Low",
                _ => "Unknown"
            },
            RiskLevel = RiskLevel,
            Source = OriginalEvent.Channel,
            Message = OriginalEvent.Message,
            Summary = Summary,
            EventData = JsonSerializer.Serialize(new
            {
                OriginalEvent.RawJson,
                OriginalEvent.UniqueId,
                OriginalEvent.Level
            }),
            Timestamp = OriginalEvent.Time.DateTime,
            MitreTechniques = JsonSerializer.Serialize(MitreTechniques),
            RecommendedActions = JsonSerializer.Serialize(RecommendedActions),
            Confidence = Confidence,
            CorrelationScore = CorrelationScore,
            BurstScore = BurstScore,
            AnomalyScore = AnomalyScore,
            IsDeterministic = IsDeterministic,
            IsCorrelationBased = IsCorrelationBased,
            IsEnhanced = IsEnhanced,
            EnrichmentData = EnrichmentData,
            CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Represents the structure of an LLM response for security event detection.
/// </summary>
public class LlmSecurityEventResponse
{
    public string? EventType { get; set; }
    public string? RiskLevel { get; set; }
    public int? Confidence { get; set; }
    public string? Summary { get; set; }
    public string[]? MitreTechniques { get; set; }
    public string[]? RecommendedActions { get; set; }
}
