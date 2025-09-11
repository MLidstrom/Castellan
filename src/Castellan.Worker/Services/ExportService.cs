using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

/// <summary>
/// Implementation of security event export service supporting CSV, JSON, and PDF formats
/// </summary>
public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;
    
    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }
    
    public async Task<byte[]> ExportToCsvAsync(IEnumerable<SecurityEvent> events, bool includeRawData = false)
    {
        _logger.LogInformation("Exporting {Count} security events to CSV format", events.Count());
        
        var csvBuilder = new StringBuilder();
        
        // CSV Headers
        var headers = new List<string>
        {
            "Id", "Timestamp", "EventType", "RiskLevel", "Confidence", "Summary",
            "Machine", "User", "Source", "EventId", "Level", "MitreTechniques",
            "RecommendedActions", "IsDeterministic", "IsCorrelationBased", "IsEnhanced",
            "CorrelationScore", "BurstScore", "AnomalyScore"
        };
        
        if (includeRawData)
        {
            headers.AddRange(new[] { "RawMessage", "UniqueId", "RawJson" });
        }
        
        csvBuilder.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));
        
        // CSV Data
        foreach (var securityEvent in events)
        {
            var row = new List<object?>
            {
                securityEvent.Id,
                securityEvent.OriginalEvent.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                securityEvent.EventType.ToString(),
                securityEvent.RiskLevel,
                securityEvent.Confidence,
                securityEvent.Summary,
                securityEvent.OriginalEvent.Host,
                securityEvent.OriginalEvent.User,
                securityEvent.OriginalEvent.Channel,
                securityEvent.OriginalEvent.EventId,
                securityEvent.OriginalEvent.Level,
                string.Join("; ", securityEvent.MitreTechniques),
                string.Join("; ", securityEvent.RecommendedActions),
                securityEvent.IsDeterministic,
                securityEvent.IsCorrelationBased,
                securityEvent.IsEnhanced,
                securityEvent.CorrelationScore.ToString("F3", CultureInfo.InvariantCulture),
                securityEvent.BurstScore.ToString("F3", CultureInfo.InvariantCulture),
                securityEvent.AnomalyScore.ToString("F3", CultureInfo.InvariantCulture)
            };
            
            if (includeRawData)
            {
                row.AddRange(new object?[]
                {
                    securityEvent.OriginalEvent.Message,
                    securityEvent.OriginalEvent.UniqueId,
                    securityEvent.OriginalEvent.RawJson
                });
            }
            
            csvBuilder.AppendLine(string.Join(",", row.Select(r => EscapeCsvValue(r?.ToString() ?? string.Empty))));
        }
        
        return Encoding.UTF8.GetBytes(csvBuilder.ToString());
    }
    
    public async Task<byte[]> ExportToJsonAsync(IEnumerable<SecurityEvent> events, bool includeRawData = false)
    {
        _logger.LogInformation("Exporting {Count} security events to JSON format", events.Count());
        
        var exportData = new
        {
            metadata = new
            {
                exportedAt = DateTime.UtcNow,
                totalEvents = events.Count(),
                includesRawData = includeRawData,
                exportedBy = "Castellan Security Platform",
                version = "v0.4.0"
            },
            events = events.Select(e => CreateExportEventObject(e, includeRawData))
        };
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(exportData, options);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public async Task<byte[]> ExportToPdfAsync(IEnumerable<SecurityEvent> events, bool includeSummary = true, bool includeRecommendations = true)
    {
        _logger.LogInformation("Exporting {Count} security events to PDF format", events.Count());
        
        // For now, we'll create a simple HTML-to-PDF approach
        // In production, you'd use a proper PDF library like iTextSharp or PuppeteerSharp
        var htmlBuilder = new StringBuilder();
        
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html>");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("    <meta charset='utf-8'>");
        htmlBuilder.AppendLine("    <title>Castellan Security Events Export</title>");
        htmlBuilder.AppendLine("    <style>");
        htmlBuilder.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        htmlBuilder.AppendLine("        .header { border-bottom: 2px solid #333; padding-bottom: 10px; margin-bottom: 20px; }");
        htmlBuilder.AppendLine("        .summary { background-color: #f5f5f5; padding: 15px; margin-bottom: 20px; border-radius: 5px; }");
        htmlBuilder.AppendLine("        .event { border: 1px solid #ddd; margin-bottom: 15px; padding: 15px; border-radius: 5px; }");
        htmlBuilder.AppendLine("        .event-header { font-weight: bold; font-size: 1.1em; margin-bottom: 10px; }");
        htmlBuilder.AppendLine("        .risk-critical { border-left: 5px solid #dc3545; }");
        htmlBuilder.AppendLine("        .risk-high { border-left: 5px solid #fd7e14; }");
        htmlBuilder.AppendLine("        .risk-medium { border-left: 5px solid #ffc107; }");
        htmlBuilder.AppendLine("        .risk-low { border-left: 5px solid #28a745; }");
        htmlBuilder.AppendLine("        .risk-unknown { border-left: 5px solid #6c757d; }");
        htmlBuilder.AppendLine("        .metadata { font-size: 0.9em; color: #666; }");
        htmlBuilder.AppendLine("        .recommendations { background-color: #e7f3ff; padding: 10px; margin-top: 10px; border-radius: 3px; }");
        htmlBuilder.AppendLine("        .mitre-techniques { font-family: monospace; background-color: #f8f9fa; padding: 5px; border-radius: 3px; }");
        htmlBuilder.AppendLine("    </style>");
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        
        // Header
        htmlBuilder.AppendLine("    <div class='header'>");
        htmlBuilder.AppendLine("        <h1>Castellan Security Events Report</h1>");
        htmlBuilder.AppendLine($"        <p>Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        htmlBuilder.AppendLine("    </div>");
        
        // Summary
        if (includeSummary)
        {
            var totalEvents = events.Count();
            var riskLevelCounts = events.GroupBy(e => e.RiskLevel).ToDictionary(g => g.Key, g => g.Count());
            var eventTypeCounts = events.GroupBy(e => e.EventType).ToDictionary(g => g.Key, g => g.Count());
            
            htmlBuilder.AppendLine("    <div class='summary'>");
            htmlBuilder.AppendLine("        <h2>Executive Summary</h2>");
            htmlBuilder.AppendLine($"        <p><strong>Total Events:</strong> {totalEvents}</p>");
            htmlBuilder.AppendLine("        <h3>Risk Level Distribution</h3>");
            htmlBuilder.AppendLine("        <ul>");
            foreach (var kvp in riskLevelCounts.OrderByDescending(x => GetRiskLevelPriority(x.Key)))
            {
                htmlBuilder.AppendLine($"            <li><strong>{kvp.Key.ToUpper()}:</strong> {kvp.Value} events</li>");
            }
            htmlBuilder.AppendLine("        </ul>");
            htmlBuilder.AppendLine("        <h3>Event Type Distribution</h3>");
            htmlBuilder.AppendLine("        <ul>");
            foreach (var kvp in eventTypeCounts.OrderByDescending(x => x.Value))
            {
                htmlBuilder.AppendLine($"            <li><strong>{kvp.Key}:</strong> {kvp.Value} events</li>");
            }
            htmlBuilder.AppendLine("        </ul>");
            htmlBuilder.AppendLine("    </div>");
        }
        
        // Events
        htmlBuilder.AppendLine("    <h2>Security Events</h2>");
        foreach (var securityEvent in events.OrderByDescending(e => e.OriginalEvent.Time))
        {
            var riskClass = $"risk-{securityEvent.RiskLevel.ToLower()}";
            htmlBuilder.AppendLine($"    <div class='event {riskClass}'>");
            htmlBuilder.AppendLine($"        <div class='event-header'>{securityEvent.EventType} - {securityEvent.RiskLevel.ToUpper()} Risk</div>");
            htmlBuilder.AppendLine($"        <p><strong>Summary:</strong> {securityEvent.Summary}</p>");
            htmlBuilder.AppendLine("        <div class='metadata'>");
            htmlBuilder.AppendLine($"            <p><strong>Timestamp:</strong> {securityEvent.OriginalEvent.Time:yyyy-MM-dd HH:mm:ss}</p>");
            htmlBuilder.AppendLine($"            <p><strong>Machine:</strong> {securityEvent.OriginalEvent.Host} | <strong>User:</strong> {securityEvent.OriginalEvent.User}</p>");
            htmlBuilder.AppendLine($"            <p><strong>Source:</strong> {securityEvent.OriginalEvent.Channel} | <strong>Event ID:</strong> {securityEvent.OriginalEvent.EventId}</p>");
            htmlBuilder.AppendLine($"            <p><strong>Confidence:</strong> {securityEvent.Confidence}%</p>");
            if (securityEvent.MitreTechniques.Any())
            {
                htmlBuilder.AppendLine($"            <p><strong>MITRE Techniques:</strong> <span class='mitre-techniques'>{string.Join(", ", securityEvent.MitreTechniques)}</span></p>");
            }
            htmlBuilder.AppendLine("        </div>");
            
            if (includeRecommendations && securityEvent.RecommendedActions.Any())
            {
                htmlBuilder.AppendLine("        <div class='recommendations'>");
                htmlBuilder.AppendLine("            <strong>Recommended Actions:</strong>");
                htmlBuilder.AppendLine("            <ul>");
                foreach (var action in securityEvent.RecommendedActions)
                {
                    htmlBuilder.AppendLine($"                <li>{action}</li>");
                }
                htmlBuilder.AppendLine("            </ul>");
                htmlBuilder.AppendLine("        </div>");
            }
            htmlBuilder.AppendLine("    </div>");
        }
        
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");
        
        // For now, return HTML as UTF-8 bytes
        // In production, convert HTML to PDF using a proper library
        return Encoding.UTF8.GetBytes(htmlBuilder.ToString());
    }
    
    public async Task<IEnumerable<ExportFormat>> GetSupportedFormatsAsync()
    {
        return new List<ExportFormat>
        {
            new()
            {
                Id = "csv",
                Name = "CSV (Comma Separated Values)",
                Extension = "csv",
                MimeType = "text/csv",
                SupportsRawData = true,
                Description = "Tabular format suitable for spreadsheets and data analysis"
            },
            new()
            {
                Id = "json",
                Name = "JSON (JavaScript Object Notation)",
                Extension = "json",
                MimeType = "application/json",
                SupportsRawData = true,
                Description = "Structured format ideal for API integration and data exchange"
            },
            new()
            {
                Id = "pdf",
                Name = "PDF (Portable Document Format)",
                Extension = "pdf",
                MimeType = "application/pdf",
                SupportsRawData = false,
                Description = "Formatted report suitable for documentation and sharing"
            }
        };
    }
    
    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
            
        // Escape commas, quotes, and newlines
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }
    
    private static object CreateExportEventObject(SecurityEvent securityEvent, bool includeRawData)
    {
        var exportEvent = new
        {
            id = securityEvent.Id,
            timestamp = securityEvent.OriginalEvent.Time,
            eventType = securityEvent.EventType.ToString(),
            riskLevel = securityEvent.RiskLevel,
            confidence = securityEvent.Confidence,
            summary = securityEvent.Summary,
            mitreTechniques = securityEvent.MitreTechniques,
            recommendedActions = securityEvent.RecommendedActions,
            isDeterministic = securityEvent.IsDeterministic,
            isCorrelationBased = securityEvent.IsCorrelationBased,
            isEnhanced = securityEvent.IsEnhanced,
            scores = new
            {
                correlation = securityEvent.CorrelationScore,
                burst = securityEvent.BurstScore,
                anomaly = securityEvent.AnomalyScore
            },
            source = new
            {
                machine = securityEvent.OriginalEvent.Host,
                user = securityEvent.OriginalEvent.User,
                channel = securityEvent.OriginalEvent.Channel,
                eventId = securityEvent.OriginalEvent.EventId,
                level = securityEvent.OriginalEvent.Level
            }
        };
        
        if (includeRawData)
        {
            return new
            {
                exportEvent.id,
                exportEvent.timestamp,
                exportEvent.eventType,
                exportEvent.riskLevel,
                exportEvent.confidence,
                exportEvent.summary,
                exportEvent.mitreTechniques,
                exportEvent.recommendedActions,
                exportEvent.isDeterministic,
                exportEvent.isCorrelationBased,
                exportEvent.isEnhanced,
                exportEvent.scores,
                exportEvent.source,
                rawData = new
                {
                    message = securityEvent.OriginalEvent.Message,
                    uniqueId = securityEvent.OriginalEvent.UniqueId,
                    rawJson = securityEvent.OriginalEvent.RawJson
                }
            };
        }
        
        return exportEvent;
    }
    
    private static int GetRiskLevelPriority(string riskLevel)
    {
        return riskLevel.ToLower() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0
        };
    }
}
