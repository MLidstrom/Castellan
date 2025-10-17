using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for collecting and managing analyst feedback on AI predictions.
/// Enables continuous learning by capturing corrections and confidence ratings.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Records analyst feedback on a security event prediction.
    /// </summary>
    /// <param name="feedback">The feedback event containing corrections</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The persisted feedback event with generated ID</returns>
    Task<FeedbackEvent> RecordFeedbackAsync(FeedbackEvent feedback, CancellationToken ct = default);

    /// <summary>
    /// Retrieves feedback for a specific security event.
    /// </summary>
    /// <param name="securityEventId">The security event ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Feedback events for the security event, or empty list if none</returns>
    Task<List<FeedbackEvent>> GetFeedbackForEventAsync(string securityEventId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all unprocessed feedback for model training.
    /// </summary>
    /// <param name="limit">Maximum number of feedback events to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of unprocessed feedback events</returns>
    Task<List<FeedbackEvent>> GetUnprocessedFeedbackAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Marks feedback as processed after being used for model updates.
    /// </summary>
    /// <param name="feedbackIds">IDs of feedback events to mark as processed</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkFeedbackProcessedAsync(IEnumerable<string> feedbackIds, CancellationToken ct = default);

    /// <summary>
    /// Gets feedback statistics for monitoring and reporting.
    /// </summary>
    /// <param name="since">Start time for statistics window</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Aggregated feedback statistics</returns>
    Task<FeedbackStatistics> GetFeedbackStatisticsAsync(DateTime since, CancellationToken ct = default);
}

/// <summary>
/// Aggregated statistics about analyst feedback.
/// </summary>
public class FeedbackStatistics
{
    public int TotalFeedback { get; set; }
    public int Corrections { get; set; }
    public int Confirmations { get; set; }
    public int FalsePositives { get; set; }
    public int FalseNegatives { get; set; }
    public double AveragePredictionQuality { get; set; }
    public int UnprocessedCount { get; set; }
    public Dictionary<string, int> FeedbackByAnalyst { get; set; } = new();
    public Dictionary<string, int> CorrectionsByEventType { get; set; } = new();
}
