using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Service for filtering out benign sequential security event patterns
/// </summary>
public class EventIgnorePatternService
{
    private readonly IOptionsMonitor<IgnorePatternOptions> _options;
    private readonly ILogger<EventIgnorePatternService> _logger;
    private readonly ConcurrentQueue<(SecurityEvent Event, DateTime Timestamp)> _recentEvents = new();

    public EventIgnorePatternService(
        IOptionsMonitor<IgnorePatternOptions> options,
        ILogger<EventIgnorePatternService> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a security event completes a sequential ignore pattern
    /// </summary>
    public bool ShouldIgnoreEvent(SecurityEvent securityEvent)
    {
        var options = _options.CurrentValue;

        _logger.LogDebug("EventIgnorePatternService.ShouldIgnoreEvent called for event {EventId}, Type={EventType}, Techniques={Techniques}, Enabled={Enabled}",
            securityEvent.Id, securityEvent.EventType, string.Join(",", securityEvent.MitreTechniques), options.Enabled);

        if (!options.Enabled)
        {
            _logger.LogDebug("Ignore patterns disabled, returning false");
            return false;
        }

        // Check if we should filter ALL local events
        if (options.FilterAllLocalEvents && options.LocalMachines != null && options.LocalMachines.Count > 0)
        {
            var sourceMachine = securityEvent.OriginalEvent.Host;
            if (options.LocalMachines.Any(lm => lm.Equals(sourceMachine, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("Filtering ALL events from local machine: {Machine}", sourceMachine);
                return true; // Filter out ALL events from this local machine
            }
        }

        // Add current event to recent events
        var now = DateTime.UtcNow;
        _recentEvents.Enqueue((securityEvent, now));

        // Cleanup old events outside the time window
        CleanupOldEvents(now, options.SequenceTimeWindowSeconds);

        // Limit queue size
        while (_recentEvents.Count > options.MaxRecentEvents)
        {
            _recentEvents.TryDequeue(out _);
        }

        // Check if current event completes any sequential pattern
        foreach (var pattern in options.Patterns)
        {
            if (MatchesSequentialPattern(securityEvent, pattern, now, options.SequenceTimeWindowSeconds))
            {
                _logger.LogDebug("Event {EventId} matches sequential ignore pattern: {Reason}",
                    securityEvent.Id, pattern.Reason);
                return true;
            }
        }

        return false;
    }

    private void CleanupOldEvents(DateTime now, int timeWindowSeconds)
    {
        var cutoff = now.AddSeconds(-timeWindowSeconds);

        while (_recentEvents.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            _recentEvents.TryDequeue(out _);
        }
    }

    private bool MatchesSequentialPattern(
        SecurityEvent currentEvent,
        SequentialIgnorePattern pattern,
        DateTime now,
        int timeWindowSeconds)
    {
        if (pattern.Sequence == null || pattern.Sequence.Count == 0)
        {
            return false;
        }

        // Get recent events within time window
        var cutoff = now.AddSeconds(-timeWindowSeconds);
        var recentEventsList = _recentEvents
            .Where(e => e.Timestamp >= cutoff)
            .OrderBy(e => e.Timestamp)
            .ToList();

        // If IgnoreAllEventsInSequence is true, check if current event matches ANY step
        if (pattern.IgnoreAllEventsInSequence)
        {
            for (int stepIndex = 0; stepIndex < pattern.Sequence.Count; stepIndex++)
            {
                if (MatchesEventStep(currentEvent, pattern.Sequence[stepIndex]))
                {
                    // Current event matches this step, check if full pattern exists
                    if (CheckPatternAtPosition(recentEventsList, pattern, stepIndex))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        else
        {
            // Original behavior: only filter the LAST event in sequence
            // The current event should match the LAST step in the sequence
            var lastStep = pattern.Sequence[pattern.Sequence.Count - 1];
            if (!MatchesEventStep(currentEvent, lastStep))
            {
                return false;
            }

            // Now check if we can find the previous steps in order
            int patternIndex = pattern.Sequence.Count - 2; // Start from second-to-last step
            int eventIndex = recentEventsList.Count - 2; // Skip the current event (last in list)

            while (patternIndex >= 0 && eventIndex >= 0)
            {
                if (MatchesEventStep(recentEventsList[eventIndex].Event, pattern.Sequence[patternIndex]))
                {
                    patternIndex--; // Move to previous pattern step
                }
                eventIndex--; // Move to previous event
            }

            // Pattern matches if we found all steps
            return patternIndex < 0;
        }
    }

    private bool CheckPatternAtPosition(
        List<(SecurityEvent Event, DateTime Timestamp)> recentEventsList,
        SequentialIgnorePattern pattern,
        int currentStepIndex)
    {
        // Current event is the last in the list and matches pattern.Sequence[currentStepIndex]
        // Check if we can find all other steps before and after in order

        int eventIndex = recentEventsList.Count - 1;

        // Check steps BEFORE current position
        int patternIndex = currentStepIndex - 1;
        int checkEventIndex = eventIndex - 1;

        while (patternIndex >= 0 && checkEventIndex >= 0)
        {
            if (MatchesEventStep(recentEventsList[checkEventIndex].Event, pattern.Sequence[patternIndex]))
            {
                patternIndex--;
            }
            checkEventIndex--;
        }

        // If we didn't find all previous steps, pattern doesn't match
        if (patternIndex >= 0)
        {
            return false;
        }

        // Check steps AFTER current position (which should have already occurred)
        // Since current event is the latest, there should be NO steps after
        // If currentStepIndex is not the last step, pattern can't match yet
        return currentStepIndex == pattern.Sequence.Count - 1 || pattern.IgnoreAllEventsInSequence;
    }

    private bool MatchesEventStep(SecurityEvent securityEvent, EventStep step)
    {
        // Check event type match
        if (!string.IsNullOrEmpty(step.EventType) &&
            !securityEvent.EventType.ToString().Equals(step.EventType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check source machine match
        if (step.SourceMachines != null && step.SourceMachines.Count > 0)
        {
            var sourceMachine = securityEvent.OriginalEvent.Host;
            var matches = step.SourceMachines.Any(sm => sm.Equals(sourceMachine, StringComparison.OrdinalIgnoreCase));
            _logger.LogDebug("SourceMachine check: actual={Actual}, expected={Expected}, matches={Matches}",
                sourceMachine, string.Join(",", step.SourceMachines), matches);
            if (!matches)
            {
                return false;
            }
        }

        // Check account name match
        if (step.AccountNames != null && step.AccountNames.Count > 0)
        {
            var accountName = ExtractAccountName(securityEvent.OriginalEvent.Message);
            var matches = !string.IsNullOrEmpty(accountName) &&
                step.AccountNames.Any(an => accountName.Contains(an, StringComparison.OrdinalIgnoreCase));
            _logger.LogDebug("AccountName check: actual={Actual}, expected={Expected}, matches={Matches}",
                accountName, string.Join(",", step.AccountNames), matches);
            if (!matches)
            {
                return false;
            }
        }

        // Check logon type match
        if (step.LogonTypes != null && step.LogonTypes.Count > 0)
        {
            var logonType = ExtractLogonType(securityEvent.OriginalEvent.Message);
            var matches = logonType != null && step.LogonTypes.Contains(logonType.Value);
            _logger.LogDebug("LogonType check: actual={Actual}, expected={Expected}, matches={Matches}",
                logonType, string.Join(",", step.LogonTypes), matches);
            if (!matches)
            {
                return false;
            }
        }

        // Check source IP match
        if (step.SourceIPs != null && step.SourceIPs.Count > 0)
        {
            var sourceIP = ExtractSourceIP(securityEvent.OriginalEvent.Message);
            if (string.IsNullOrEmpty(sourceIP) ||
                !step.SourceIPs.Any(ip => ip.Equals(sourceIP, StringComparison.OrdinalIgnoreCase) || sourceIP == "-"))
            {
                return false;
            }
        }

        // Check MITRE technique match
        if (step.MitreTechniques != null && step.MitreTechniques.Count > 0)
        {
            if (step.RequireAllTechniques)
            {
                // All step techniques must be present in the event
                if (!step.MitreTechniques.All(pt =>
                    securityEvent.MitreTechniques.Any(et => et.Equals(pt, StringComparison.OrdinalIgnoreCase))))
                {
                    return false;
                }
            }
            else
            {
                // Any step technique match is sufficient
                if (!step.MitreTechniques.Any(pt =>
                    securityEvent.MitreTechniques.Any(et => et.Equals(pt, StringComparison.OrdinalIgnoreCase))))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private string? ExtractAccountName(string message)
    {
        // Extract account name from "New Logon" section of Windows Event Log message
        // This is the account that was logged on (not the subject who requested it)
        // Format: "New Logon:\r\n\tSecurity ID:\t\t...\r\n\tAccount Name:\t\tSYSTEM"
        var newLogonMatch = System.Text.RegularExpressions.Regex.Match(
            message,
            @"New Logon:.*?Account Name:\s+([^\r\n]+)",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (newLogonMatch.Success)
        {
            return newLogonMatch.Groups[1].Value.Trim();
        }

        // Fallback: try to match any Account Name field
        var match = System.Text.RegularExpressions.Regex.Match(message, @"Account Name:\s+([^\r\n]+)");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private int? ExtractLogonType(string message)
    {
        // Extract logon type from Windows Event Log message
        // Format: "Logon Type:\t\t5" or "Logon Type:		5"
        var match = System.Text.RegularExpressions.Regex.Match(message, @"Logon Type:\s+(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var logonType) ? logonType : null;
    }

    private string? ExtractSourceIP(string message)
    {
        // Extract source network address from Windows Event Log message
        // Format: "Source Network Address:\t\t192.168.1.100" or "Source Network Address:		-"
        var match = System.Text.RegularExpressions.Regex.Match(message, @"Source Network Address:\s+([^\r\n]+)");
        var ip = match.Success ? match.Groups[1].Value.Trim() : null;
        return string.IsNullOrEmpty(ip) || ip == "-" ? null : ip;
    }
}
