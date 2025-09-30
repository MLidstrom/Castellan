namespace Castellan.Worker.Configuration;

/// <summary>
/// Database configuration containing resolved paths
/// </summary>
public class DatabaseConfiguration
{
    /// <summary>
    /// Fully resolved path to the database file
    /// </summary>
    public string Path { get; set; } = string.Empty;
}