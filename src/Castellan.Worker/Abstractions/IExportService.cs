using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for exporting security events in various formats
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export security events to CSV format
    /// </summary>
    /// <param name="events">Events to export</param>
    /// <param name="includeRawData">Include raw event data in export</param>
    /// <returns>CSV content as byte array</returns>
    Task<byte[]> ExportToCsvAsync(IEnumerable<SecurityEvent> events, bool includeRawData = false);
    
    /// <summary>
    /// Export security events to JSON format
    /// </summary>
    /// <param name="events">Events to export</param>
    /// <param name="includeRawData">Include raw event data in export</param>
    /// <returns>JSON content as byte array</returns>
    Task<byte[]> ExportToJsonAsync(IEnumerable<SecurityEvent> events, bool includeRawData = false);
    
    /// <summary>
    /// Export security events to PDF format
    /// </summary>
    /// <param name="events">Events to export</param>
    /// <param name="includeSummary">Include executive summary</param>
    /// <param name="includeRecommendations">Include recommendations section</param>
    /// <returns>PDF content as byte array</returns>
    Task<byte[]> ExportToPdfAsync(IEnumerable<SecurityEvent> events, bool includeSummary = true, bool includeRecommendations = true);
    
    /// <summary>
    /// Get available export formats
    /// </summary>
    /// <returns>List of supported formats</returns>
    Task<IEnumerable<ExportFormat>> GetSupportedFormatsAsync();
}

/// <summary>
/// Represents an export format
/// </summary>
public class ExportFormat
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public bool SupportsRawData { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Export request parameters
/// </summary>
public class ExportRequest
{
    public string Format { get; set; } = string.Empty;
    public Dictionary<string, object> Filters { get; set; } = new();
    public bool IncludeRawData { get; set; }
    public bool IncludeSummary { get; set; } = true;
    public bool IncludeRecommendations { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string[] RiskLevels { get; set; } = Array.Empty<string>();
    public string[] EventTypes { get; set; } = Array.Empty<string>();
}
