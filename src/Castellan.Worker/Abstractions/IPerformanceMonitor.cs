using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for performance monitoring service
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Record pipeline processing metrics
    /// </summary>
    /// <param name="processingTimeMs">Processing time in milliseconds</param>
    /// <param name="eventsProcessed">Number of events processed</param>
    /// <param name="queueDepth">Current queue depth</param>
    /// <param name="error">Processing error if any</param>
    void RecordPipelineMetrics(double processingTimeMs, int eventsProcessed, int queueDepth, string? error = null);

    /// <summary>
    /// Record event collection metrics
    /// </summary>
    /// <param name="channel">Event channel name</param>
    /// <param name="eventsCollected">Number of events collected</param>
    /// <param name="collectionTimeMs">Collection time in milliseconds</param>
    /// <param name="duplicatesFiltered">Number of duplicates filtered</param>
    /// <param name="error">Collection error if any</param>
    void RecordEventCollectionMetrics(string channel, int eventsCollected, double collectionTimeMs, int duplicatesFiltered = 0, string? error = null);

    /// <summary>
    /// Record vector store metrics
    /// </summary>
    /// <param name="embeddingTimeMs">Embedding time in milliseconds</param>
    /// <param name="upsertTimeMs">Upsert time in milliseconds</param>
    /// <param name="searchTimeMs">Search time in milliseconds</param>
    /// <param name="vectorsProcessed">Number of vectors processed</param>
    /// <param name="error">Vector store error if any</param>
    void RecordVectorStoreMetrics(double embeddingTimeMs = 0, double upsertTimeMs = 0, double searchTimeMs = 0, int vectorsProcessed = 0, string? error = null);

    /// <summary>
    /// Record security detection metrics
    /// </summary>
    /// <param name="eventType">Security event type</param>
    /// <param name="riskLevel">Risk level</param>
    /// <param name="confidence">Confidence score</param>
    /// <param name="isDeterministic">Whether detection was deterministic</param>
    /// <param name="isCorrelationBased">Whether detection was correlation-based</param>
    void RecordSecurityDetection(string eventType, string riskLevel, int confidence, bool isDeterministic, bool isCorrelationBased = false);

    /// <summary>
    /// Record LLM analysis metrics
    /// </summary>
    /// <param name="provider">LLM provider</param>
    /// <param name="model">Model name</param>
    /// <param name="responseTimeMs">Response time in milliseconds</param>
    /// <param name="tokens">Number of tokens</param>
    /// <param name="success">Whether request was successful</param>
    void RecordLlmMetrics(string provider, string model, double responseTimeMs, int tokens = 0, bool success = true);

    /// <summary>
    /// Record notification metrics
    /// </summary>
    /// <param name="notificationType">Type of notification (Desktop)</param>
    /// <param name="riskLevel">Risk level of notification</param>
    /// <param name="deliveryTimeMs">Delivery time in milliseconds</param>
    /// <param name="success">Whether notification was successful</param>
    void RecordNotificationMetrics(string notificationType, string riskLevel, double deliveryTimeMs, bool success = true);

    /// <summary>
    /// Record vector cleanup metrics
    /// </summary>
    /// <param name="vectorsRemoved">Number of vectors removed</param>
    void RecordVectorCleanup(int vectorsRemoved);

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    /// <returns>Current performance metrics</returns>
    PerformanceMetrics GetCurrentMetrics();

    /// <summary>
    /// Get performance metrics for a specific time period
    /// </summary>
    /// <param name="from">Start time</param>
    /// <param name="to">End time</param>
    /// <returns>Aggregated metrics for the time period</returns>
    Task<PerformanceMetrics> GetMetricsAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Reset all metrics
    /// </summary>
    void ResetMetrics();

    /// <summary>
    /// Export metrics to file
    /// </summary>
    /// <param name="filePath">File path to export to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportMetricsAsync(string filePath, CancellationToken cancellationToken = default);
}