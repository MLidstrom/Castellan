using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Configuration options for security event retention policies
/// </summary>
public class SecurityEventRetentionOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "SecurityEventRetention";

    /// <summary>
    /// Number of days to retain security events (default: 1 day)
    /// Valid range: 1-365 days
    /// </summary>
    [Range(1, 365, ErrorMessage = "Retention days must be between 1 and 365")]
    public int RetentionDays { get; set; } = 1;

    /// <summary>
    /// Number of hours to retain security events (alternative to RetentionDays)
    /// Valid range: 1-8760 hours (1 hour to 1 year)
    /// </summary>
    [Range(1, 8760, ErrorMessage = "Retention hours must be between 1 and 8760")]
    public int RetentionHours { get; set; } = 24;

    /// <summary>
    /// Whether to use tiered storage approach (hot/warm/cold)
    /// </summary>
    public bool EnableTieredStorage { get; set; } = false;

    /// <summary>
    /// Number of days to keep events in hot storage (fast access)
    /// Only used when EnableTieredStorage is true
    /// </summary>
    [Range(1, 30, ErrorMessage = "Hot storage days must be between 1 and 30")]
    public int HotStorageDays { get; set; } = 7;

    /// <summary>
    /// Number of days to keep events in warm storage (slower access, compressed)
    /// Only used when EnableTieredStorage is true
    /// </summary>
    [Range(1, 90, ErrorMessage = "Warm storage days must be between 1 and 90")]
    public int WarmStorageDays { get; set; } = 30;

    /// <summary>
    /// Maximum number of events to keep in memory (0 = unlimited)
    /// This prevents excessive memory usage
    /// </summary>
    [Range(0, 100000, ErrorMessage = "Max events in memory must be between 0 and 100000")]
    public int MaxEventsInMemory { get; set; } = 10000;

    /// <summary>
    /// Cleanup interval in minutes (how often to run retention cleanup)
    /// </summary>
    [Range(1, 1440, ErrorMessage = "Cleanup interval must be between 1 and 1440 minutes")]
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to compress old events to save disk space
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Age in days after which events should be compressed
    /// Only used when EnableCompression is true
    /// </summary>
    [Range(1, 30, ErrorMessage = "Compression threshold days must be between 1 and 30")]
    public int CompressionThresholdDays { get; set; } = 7;

    /// <summary>
    /// Gets the retention period as a TimeSpan
    /// Uses RetentionDays if > 0, otherwise falls back to RetentionHours
    /// </summary>
    public TimeSpan GetRetentionPeriod()
    {
        return RetentionDays > 0 
            ? TimeSpan.FromDays(RetentionDays) 
            : TimeSpan.FromHours(RetentionHours);
    }

    /// <summary>
    /// Gets the hot storage period as a TimeSpan
    /// </summary>
    public TimeSpan GetHotStoragePeriod()
    {
        return TimeSpan.FromDays(HotStorageDays);
    }

    /// <summary>
    /// Gets the warm storage period as a TimeSpan
    /// </summary>
    public TimeSpan GetWarmStoragePeriod()
    {
        return TimeSpan.FromDays(WarmStorageDays);
    }

    /// <summary>
    /// Gets the compression threshold as a TimeSpan
    /// </summary>
    public TimeSpan GetCompressionThreshold()
    {
        return TimeSpan.FromDays(CompressionThresholdDays);
    }

    /// <summary>
    /// Gets the cleanup interval as a TimeSpan
    /// </summary>
    public TimeSpan GetCleanupInterval()
    {
        return TimeSpan.FromMinutes(CleanupIntervalMinutes);
    }

    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    public bool IsValid()
    {
        // Ensure retention period is reasonable
        if (RetentionDays <= 0 && RetentionHours <= 0)
            return false;

        // Ensure tiered storage makes sense if enabled
        if (EnableTieredStorage)
        {
            if (HotStorageDays >= WarmStorageDays)
                return false;
            
            var totalRetentionDays = GetRetentionPeriod().TotalDays;
            if (WarmStorageDays > totalRetentionDays)
                return false;
        }

        // Ensure compression threshold is reasonable
        if (EnableCompression && CompressionThresholdDays > GetRetentionPeriod().TotalDays)
            return false;

        return true;
    }
}
