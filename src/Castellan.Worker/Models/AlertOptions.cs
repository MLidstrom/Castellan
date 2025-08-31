namespace Castellan.Worker.Models;

public sealed class AlertOptions
{
    public string MinRiskLevel { get; set; } = "medium";
    public bool EnableConsoleAlerts { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;

    public static int GetRiskLevelValue(string riskLevel) => riskLevel.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0
    };

    public bool ShouldAlert(string riskLevel)
    {
        var minLevel = GetRiskLevelValue(MinRiskLevel);
        var eventLevel = GetRiskLevelValue(riskLevel);
        return eventLevel >= minLevel;
    }
}

