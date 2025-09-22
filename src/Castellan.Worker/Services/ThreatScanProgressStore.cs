using System.Collections.Concurrent;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public class ThreatScanProgressStore : IThreatScanProgressStore
{
    private readonly ConcurrentDictionary<string, ThreatScanProgress> _progressStore = new();
    private volatile string? _currentScanId;

    public void SetProgress(string scanId, ThreatScanProgress progress)
    {
        _progressStore[scanId] = progress;
        _currentScanId = scanId;
    }

    public ThreatScanProgress? GetProgress(string scanId)
    {
        return _progressStore.TryGetValue(scanId, out var progress) ? progress : null;
    }

    public void RemoveProgress(string scanId)
    {
        _progressStore.TryRemove(scanId, out _);
        if (_currentScanId == scanId)
        {
            _currentScanId = null;
        }
    }

    public ThreatScanProgress? GetCurrentProgress()
    {
        var currentId = _currentScanId;
        return currentId != null ? GetProgress(currentId) : null;
    }
}