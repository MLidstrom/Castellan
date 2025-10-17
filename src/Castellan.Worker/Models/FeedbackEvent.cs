using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Represents analyst feedback on an AI prediction for continuous learning.
/// Stores corrections, confidence ratings, and metadata for model improvement.
/// </summary>
public class FeedbackEvent
{
    /// <summary>
    /// Unique identifier for the feedback record.
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the security event this feedback is for.
    /// </summary>
    [Required]
    public string SecurityEventId { get; set; } = "";

    /// <summary>
    /// Analyst user ID who provided the feedback.
    /// </summary>
    [Required]
    public string AnalystUserId { get; set; } = "";

    /// <summary>
    /// Timestamp when feedback was provided.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original AI prediction values.
    /// </summary>
    public PredictionSnapshot OriginalPrediction { get; set; } = new();

    /// <summary>
    /// Analyst-corrected values.
    /// </summary>
    public PredictionSnapshot CorrectedPrediction { get; set; } = new();

    /// <summary>
    /// Analyst's rating of the original prediction quality (1-5 scale).
    /// 1 = Completely wrong, 5 = Perfect
    /// </summary>
    [Range(1, 5)]
    public int PredictionQuality { get; set; }

    /// <summary>
    /// Optional analyst comment explaining the correction.
    /// </summary>
    public string? AnalystComment { get; set; }

    /// <summary>
    /// Type of feedback: Correction, Confirmation, FalsePositive, FalseNegative
    /// </summary>
    [Required]
    public FeedbackType FeedbackType { get; set; }

    /// <summary>
    /// Whether this feedback has been processed by the learning service.
    /// </summary>
    public bool Processed { get; set; }

    /// <summary>
    /// When this feedback was processed for model updates.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Metadata for tracking feedback source and context.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Snapshot of prediction values (original or corrected).
/// </summary>
public class PredictionSnapshot
{
    public string EventType { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public float Confidence { get; set; }
    public string[] MitreTechniques { get; set; } = Array.Empty<string>();
    public string Summary { get; set; } = "";
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Type of feedback provided by the analyst.
/// </summary>
public enum FeedbackType
{
    /// <summary>
    /// Analyst corrected one or more prediction fields.
    /// </summary>
    Correction,

    /// <summary>
    /// Analyst confirmed the prediction was accurate.
    /// </summary>
    Confirmation,

    /// <summary>
    /// AI flagged as threat but it was benign (false positive).
    /// </summary>
    FalsePositive,

    /// <summary>
    /// AI missed a threat that analyst identified (false negative).
    /// </summary>
    FalseNegative
}
