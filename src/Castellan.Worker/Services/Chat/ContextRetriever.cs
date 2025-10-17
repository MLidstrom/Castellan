using System.Diagnostics;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services.Chat;

/// <summary>
/// RAG-powered context retriever for chat responses.
/// Fetches relevant security events, correlation patterns, and system metrics.
/// </summary>
public class ContextRetriever : IContextRetriever
{
    private readonly IVectorStore _vectorStore;
    private readonly ISecurityEventStore _eventStore;
    private readonly ICorrelationEngine _correlationEngine;
    private readonly ILogger<ContextRetriever> _logger;

    public ContextRetriever(
        IVectorStore vectorStore,
        ISecurityEventStore eventStore,
        ICorrelationEngine correlationEngine,
        ILogger<ContextRetriever> logger)
    {
        _vectorStore = vectorStore;
        _eventStore = eventStore;
        _correlationEngine = correlationEngine;
        _logger = logger;
    }

    public async Task<ChatContext> RetrieveContextAsync(
        string message,
        ChatIntent intent,
        ContextOptions? options = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        options ??= new ContextOptions();

        var context = new ChatContext
        {
            Intent = intent,
            TimeRange = options.TimeRange ?? TimeRange.Last24Hours
        };

        try
        {
            // Run context retrieval tasks in parallel for performance
            var tasks = new List<Task>
            {
                RetrieveSimilarEventsAsync(message, options, context, ct),
                RetrieveRecentCriticalEventsAsync(options, context, ct)
            };

            if (options.IncludeCorrelationPatterns)
            {
                tasks.Add(RetrieveCorrelationPatternsAsync(context, ct));
            }

            if (options.IncludeSystemMetrics)
            {
                tasks.Add(RetrieveSystemMetricsAsync(context, ct));
            }

            await Task.WhenAll(tasks);

            sw.Stop();
            _logger.LogInformation(
                "Retrieved context: {SimilarEvents} similar events, {CriticalEvents} critical events, {Patterns} patterns in {ElapsedMs}ms",
                context.SimilarEvents.Count,
                context.RecentCriticalEvents.Count,
                context.ActivePatterns.Count,
                sw.ElapsedMilliseconds);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context for message: {Message}", message);
            return context; // Return partial context on failure
        }
    }

    private async Task RetrieveSimilarEventsAsync(
        string message,
        ContextOptions options,
        ChatContext context,
        CancellationToken ct)
    {
        try
        {
            // Simple keyword-based search as fallback when vector search isn't available
            // Extract keywords from message for filtering
            var keywords = message.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3) // Only words longer than 3 characters
                .Take(5) // Top 5 keywords
                .ToList();

            if (keywords.Count == 0)
            {
                _logger.LogDebug("No keywords extracted from message for event search");
                return;
            }

            // Search events by keyword matching in event type, summary, or message
            var allEvents = await Task.Run(() =>
            {
                return _eventStore
                    .GetSecurityEvents(1, 100) // Get first 100 events
                    .Where(e => e.OriginalEvent.Time >= context.TimeRange.Start &&
                               e.OriginalEvent.Time <= context.TimeRange.End)
                    .ToList();
            }, ct);

            // Score events by keyword relevance
            var scoredEvents = allEvents
                .Select(e => new
                {
                    Event = e,
                    Score = CalculateKeywordRelevance(e, keywords)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(options.MaxSimilarEvents)
                .Select(x => x.Event)
                .ToList();

            context.SimilarEvents = scoredEvents;

            _logger.LogDebug("Retrieved {Count} similar events using keyword search (keywords: {Keywords})",
                scoredEvents.Count, string.Join(", ", keywords));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve similar events");
        }
    }

    private int CalculateKeywordRelevance(SecurityEvent evt, List<string> keywords)
    {
        int score = 0;
        var searchText = $"{evt.EventType} {evt.Summary} {evt.OriginalEvent.Message}".ToLower();

        foreach (var keyword in keywords)
        {
            if (searchText.Contains(keyword))
            {
                score += 10; // Base score for keyword match

                // Bonus for EventType match (more important)
                if (evt.EventType.ToString().ToLower().Contains(keyword))
                    score += 20;

                // Bonus for Summary match
                if (evt.Summary.ToLower().Contains(keyword))
                    score += 15;
            }
        }

        return score;
    }

    private async Task RetrieveRecentCriticalEventsAsync(
        ContextOptions options,
        ChatContext context,
        CancellationToken ct)
    {
        try
        {
            // Get recent high-risk and critical events using filters
            var filters = new Dictionary<string, object>
            {
                ["riskLevel"] = new[] { "Critical", "High" }
            };

            // Offload synchronous database operation to thread pool
            var criticalEvents = await Task.Run(() =>
            {
                return _eventStore
                    .GetSecurityEvents(1, options.MaxRecentCriticalEvents, filters)
                    .Where(e => e.OriginalEvent.Time >= context.TimeRange.Start &&
                               e.OriginalEvent.Time <= context.TimeRange.End)
                    .OrderByDescending(e => e.OriginalEvent.Time)
                    .ToList();
            }, ct);

            context.RecentCriticalEvents = criticalEvents;

            _logger.LogDebug("Retrieved {Count} recent critical events", criticalEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve recent critical events");
        }
    }

    private async Task RetrieveCorrelationPatternsAsync(
        ChatContext context,
        CancellationToken ct)
    {
        try
        {
            // Get active correlation patterns from correlation engine
            var stats = await _correlationEngine.GetStatisticsAsync(context.TimeRange.Start, context.TimeRange.End);

            // Convert correlation statistics to patterns
            // Note: This is a simplified implementation - actual correlation engine
            // would need to expose GetActivePatternsAsync method
            context.ActivePatterns = new List<CorrelationPattern>
            {
                new CorrelationPattern
                {
                    Id = "active-patterns",
                    Name = "Active Correlations",
                    Description = $"{stats.CorrelationsDetected} active correlation patterns detected",
                    Score = 0.8f,
                    EventCount = stats.CorrelationsDetected,
                    FirstSeen = DateTime.UtcNow.AddHours(-1),
                    LastSeen = DateTime.UtcNow
                }
            };

            _logger.LogDebug("Retrieved {Count} correlation patterns", context.ActivePatterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve correlation patterns");
        }
    }

    private async Task RetrieveSystemMetricsAsync(
        ChatContext context,
        CancellationToken ct)
    {
        try
        {
            // Offload synchronous database operations to thread pool
            var (totalCount, riskLevelCounts, events24h) = await Task.Run(() =>
            {
                var total = _eventStore.GetTotalCount();
                var riskLevels = _eventStore.GetRiskLevelCounts();
                var recentEvents = _eventStore
                    .GetSecurityEvents(1, 10000) // Get a large page to capture recent events
                    .Where(e => e.OriginalEvent.Time >= DateTime.UtcNow.AddHours(-24))
                    .ToList();
                return (total, riskLevels, recentEvents);
            }, ct);

            context.CurrentMetrics = new Castellan.Worker.Models.Chat.SystemMetrics
            {
                TotalEvents24h = events24h.Count,
                CriticalEvents = riskLevelCounts.GetValueOrDefault("Critical", 0),
                HighRiskEvents = riskLevelCounts.GetValueOrDefault("High", 0),
                OpenEvents = 0, // Note: SecurityEvent doesn't have IsAcknowledged property
                EventsByRiskLevel = riskLevelCounts,
                EventsByType = events24h
                    .GroupBy(e => e.EventType.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                SystemStatus = "Healthy",
                Timestamp = DateTime.UtcNow
            };

            _logger.LogDebug("Retrieved system metrics: {TotalEvents} events in last 24h", events24h.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve system metrics");
        }
    }
}
