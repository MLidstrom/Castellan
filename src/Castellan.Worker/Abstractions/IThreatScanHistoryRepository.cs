using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

public interface IThreatScanHistoryRepository
{
    Task<string> CreateScanAsync(ThreatScanResult scanResult);
    Task UpdateScanAsync(ThreatScanResult scanResult);
    Task<ThreatScanResult?> GetScanAsync(string id);
    Task<IEnumerable<ThreatScanResult>> GetScanHistoryAsync(int page, int pageSize);
    Task<int> GetScanCountAsync();
    Task DeleteOldScansAsync(DateTime olderThan);
}