namespace Castellan.Worker.Models.ThreatIntelligence;

/// <summary>
/// Risk level classification for threat intelligence results
/// </summary>
public enum ThreatRiskLevel
{
    /// <summary>
    /// No threat detected or very low risk
    /// </summary>
    Low = 0,

    /// <summary>
    /// Potentially suspicious but low confidence
    /// </summary>
    Medium = 1,

    /// <summary>
    /// Likely threat with moderate confidence
    /// </summary>
    High = 2,

    /// <summary>
    /// High-confidence threat requiring immediate attention
    /// </summary>
    Critical = 3
}
