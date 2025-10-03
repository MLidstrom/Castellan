using System.Collections.Concurrent;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Hubs;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// Security event store decorator that broadcasts events via SignalR
/// </summary>
public class SignalRSecurityEventStore : ISecurityEventStore
{
    private readonly ISecurityEventStore _innerStore;
    private readonly IScanProgressBroadcaster _broadcaster;
    private readonly ILogger<SignalRSecurityEventStore> _logger;

    public SignalRSecurityEventStore(
        ISecurityEventStore innerStore,
        IScanProgressBroadcaster broadcaster,
        ILogger<SignalRSecurityEventStore> logger)
    {
        _innerStore = innerStore;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public void AddSecurityEvent(SecurityEvent securityEvent)
    {
        // Add to the underlying store
        _innerStore.AddSecurityEvent(securityEvent);

        // Broadcast the new event via SignalR
        Task.Run(async () =>
        {
            try
            {
                // Create a sanitized version for broadcasting
                var broadcastEvent = new
                {
                    id = securityEvent.Id,
                    timestamp = securityEvent.OriginalEvent.Time,
                    eventType = securityEvent.EventType.ToString(),
                    riskLevel = securityEvent.RiskLevel,
                    confidence = securityEvent.Confidence,
                    summary = securityEvent.Summary,
                    eventId = securityEvent.OriginalEvent?.EventId,
                    machineName = securityEvent.OriginalEvent?.Host,
                    userName = securityEvent.OriginalEvent?.User,
                    hasCorrelation = securityEvent.IsCorrelationBased,
                    correlationContext = securityEvent.CorrelationContext,
                    mitreTechniques = securityEvent.MitreTechniques,
                    recommendedActions = securityEvent.RecommendedActions
                };

                await _broadcaster.BroadcastSecurityEvent(broadcastEvent);

                // If it has correlation, also broadcast as correlation alert
                if (securityEvent.IsCorrelationBased)
                {
                    var correlationAlert = new
                    {
                        id = securityEvent.Id,
                        timestamp = securityEvent.OriginalEvent.Time,
                        eventType = securityEvent.EventType.ToString(),
                        riskLevel = securityEvent.RiskLevel,
                        correlationIds = securityEvent.CorrelationIds,
                        correlationContext = securityEvent.CorrelationContext,
                        summary = $"[CORRELATED] {securityEvent.Summary}",
                        confidence = securityEvent.Confidence,
                        correlationScore = securityEvent.CorrelationScore
                    };

                    await _broadcaster.BroadcastCorrelationAlert(correlationAlert);
                }

                _logger.LogDebug("Broadcasted security event {Id} via SignalR", securityEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast security event {Id}", securityEvent.Id);
            }
        });
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page = 1, int pageSize = 10)
    {
        return _innerStore.GetSecurityEvents(page, pageSize);
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page, int pageSize, Dictionary<string, object> filters)
    {
        return _innerStore.GetSecurityEvents(page, pageSize, filters);
    }

    public SecurityEvent? GetSecurityEvent(string id)
    {
        return _innerStore.GetSecurityEvent(id);
    }

    public int GetTotalCount()
    {
        return _innerStore.GetTotalCount();
    }

    public int GetTotalCount(Dictionary<string, object> filters)
    {
        return _innerStore.GetTotalCount(filters);
    }

    public Dictionary<string, int> GetRiskLevelCounts()
    {
        return _innerStore.GetRiskLevelCounts();
    }

    public void Clear()
    {
        _innerStore.Clear();
    }
}