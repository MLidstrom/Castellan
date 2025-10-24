namespace Castellan.Worker.Configuration;

public class SignalROptions
{
    public const string SectionName = "SignalR";

    /// <summary>
    /// Retry intervals for SignalR reconnection attempts (in milliseconds)
    /// Frontend will retry connecting using these intervals in sequence
    /// After exhausting these intervals, it will continue using the last interval indefinitely
    /// </summary>
    public List<int> RetryIntervalsMs { get; set; } = new() { 0, 2000, 5000 };
}
