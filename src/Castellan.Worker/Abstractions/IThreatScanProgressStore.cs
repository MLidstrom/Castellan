using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

public interface IThreatScanProgressStore
{
    void SetProgress(string scanId, ThreatScanProgress progress);
    ThreatScanProgress? GetProgress(string scanId);
    void RemoveProgress(string scanId);
    ThreatScanProgress? GetCurrentProgress();
}