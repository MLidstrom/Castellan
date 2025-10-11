using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

public interface ISecurityEventStore
{
    // Async method (preferred for performance)
    Task AddSecurityEventAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);

    // Sync method (kept for backward compatibility)
    void AddSecurityEvent(SecurityEvent securityEvent);

    IEnumerable<SecurityEvent> GetSecurityEvents(int page = 1, int pageSize = 10);
    IEnumerable<SecurityEvent> GetSecurityEvents(int page, int pageSize, Dictionary<string, object> filters);
    SecurityEvent? GetSecurityEvent(string id);
    int GetTotalCount();
    int GetTotalCount(Dictionary<string, object> filters);
    Dictionary<string, int> GetRiskLevelCounts();
    Dictionary<string, int> GetRiskLevelCounts(Dictionary<string, object> filters);
    void Clear();
}
