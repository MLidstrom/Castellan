namespace Castellan.Worker.Configuration;

public class CorrelationOptions
{
    public const string SectionName = "Correlation";
    
    /// <summary>
    /// Minimum correlation score required to create a correlation-based security event
    /// </summary>
    public double MinCorrelationScore { get; set; } = 0.7;
    
    /// <summary>
    /// Minimum burst score required to create a burst-based security event
    /// </summary>
    public double MinBurstScore { get; set; } = 0.7;
    
    /// <summary>
    /// Minimum anomaly score required to create an anomaly-based security event
    /// </summary>
    public double MinAnomalyScore { get; set; } = 0.7;
    
    /// <summary>
    /// Minimum total score (correlation + burst + anomaly) required to create a correlation-based event
    /// </summary>
    public double MinTotalScore { get; set; } = 1.5;
    
    /// <summary>
    /// Enable creation of low-score correlation events (set to false to filter out low-confidence events)
    /// </summary>
    public bool EnableLowScoreEvents { get; set; } = false;
}

