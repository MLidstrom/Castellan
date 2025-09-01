using System.Text.Json;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace Castellan.Worker.Services;

/// <summary>
/// Service for importing MITRE ATT&CK techniques from official STIX data
/// </summary>
public class MitreAttackImportService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<MitreAttackImportService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MitreAttackImportService(
        CastellanDbContext context, 
        ILogger<MitreAttackImportService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Downloads and imports all MITRE ATT&CK techniques
    /// </summary>
    public async Task<ImportResult> ImportAllTechniquesAsync()
    {
        try
        {
            _logger.LogInformation("Starting MITRE ATT&CK techniques import...");

            // Download the latest STIX data
            var stixData = await DownloadMitreStixDataAsync();
            
            // Parse and extract techniques
            var techniques = ParseStixTechniques(stixData);
            
            // Import to database
            var result = await ImportTechniquesToDatabaseAsync(techniques);
            
            _logger.LogInformation("MITRE ATT&CK import completed. {TechniquesImported} techniques imported, {TechniquesUpdated} updated, {Errors} errors", 
                result.TechniquesImported, result.TechniquesUpdated, result.Errors.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import MITRE ATT&CK techniques");
            throw;
        }
    }

    /// <summary>
    /// Downloads the official MITRE ATT&CK STIX data
    /// </summary>
    private async Task<string> DownloadMitreStixDataAsync()
    {
        const string mitreStixUrl = "https://raw.githubusercontent.com/mitre/cti/master/enterprise-attack/enterprise-attack.json";
        
        _logger.LogInformation("Downloading MITRE ATT&CK STIX data from {Url}", mitreStixUrl);
        
        var response = await _httpClient.GetAsync(mitreStixUrl);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Downloaded {Size} bytes of STIX data", content.Length);
        
        return content;
    }

    /// <summary>
    /// Parses STIX JSON data and extracts MITRE techniques
    /// </summary>
    private List<MitreTechnique> ParseStixTechniques(string stixJson)
    {
        _logger.LogInformation("Parsing STIX data to extract techniques...");
        
        var techniques = new List<MitreTechnique>();
        
        try
        {
            using var doc = JsonDocument.Parse(stixJson);
            var objects = doc.RootElement.GetProperty("objects");
            
            foreach (var obj in objects.EnumerateArray())
            {
                if (!obj.TryGetProperty("type", out var typeElement) || 
                    typeElement.GetString() != "attack-pattern")
                    continue;
                
                var technique = ParseTechniqueFromStixObject(obj);
                if (technique != null)
                {
                    techniques.Add(technique);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing STIX data");
            throw;
        }
        
        _logger.LogInformation("Extracted {Count} techniques from STIX data", techniques.Count);
        return techniques;
    }

    /// <summary>
    /// Parses a single STIX attack-pattern object into a MitreTechnique
    /// </summary>
    private MitreTechnique? ParseTechniqueFromStixObject(JsonElement obj)
    {
        try
        {
            // Extract external references to find the MITRE technique ID
            if (!obj.TryGetProperty("external_references", out var externalRefs))
                return null;
            
            string? techniqueId = null;
            foreach (var extRef in externalRefs.EnumerateArray())
            {
                if (extRef.TryGetProperty("source_name", out var sourceName) &&
                    sourceName.GetString() == "mitre-attack" &&
                    extRef.TryGetProperty("external_id", out var externalId))
                {
                    techniqueId = externalId.GetString();
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(techniqueId))
                return null;
            
            // Extract basic properties
            var name = obj.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
            var description = obj.TryGetProperty("description", out var descElement) ? descElement.GetString() : "";
            
            // Extract kill chain phases (tactics)
            var tactics = new List<string>();
            if (obj.TryGetProperty("kill_chain_phases", out var killChainPhases))
            {
                foreach (var phase in killChainPhases.EnumerateArray())
                {
                    if (phase.TryGetProperty("phase_name", out var phaseName))
                    {
                        var tacticName = phaseName.GetString();
                        if (!string.IsNullOrEmpty(tacticName))
                        {
                            tactics.Add(FormatTacticName(tacticName));
                        }
                    }
                }
            }

            // If no tactics found through kill_chain_phases, check if it's in a different structure
            if (tactics.Count == 0 && obj.TryGetProperty("x_mitre_tactics", out var mitreTactics))
            {
                foreach (var tactic in mitreTactics.EnumerateArray())
                {
                    var tacticName = tactic.GetString();
                    if (!string.IsNullOrEmpty(tacticName))
                    {
                        tactics.Add(FormatTacticName(tacticName));
                    }
                }
            }
            
            // Extract platforms
            var platforms = new List<string>();
            if (obj.TryGetProperty("x_mitre_platforms", out var platformsElement))
            {
                foreach (var platform in platformsElement.EnumerateArray())
                {
                    platforms.Add(platform.GetString() ?? "");
                }
            }
            
            // Extract data sources
            var dataSources = new List<string>();
            if (obj.TryGetProperty("x_mitre_data_sources", out var dataSourcesElement))
            {
                foreach (var dataSource in dataSourcesElement.EnumerateArray())
                {
                    dataSources.Add(dataSource.GetString() ?? "");
                }
            }
            
            var technique = new MitreTechnique
            {
                TechniqueId = techniqueId,
                Name = name ?? "Unknown",
                Description = description,
                Tactic = tactics.Count > 0 ? string.Join(", ", tactics) : null,
                Platform = platforms.Count > 0 ? string.Join(", ", platforms) : null,
                DataSources = JsonSerializer.Serialize(dataSources),
                Mitigations = "[]", // Will be populated separately if needed
                Examples = "[]",    // Will be populated separately if needed
                CreatedAt = DateTime.UtcNow
            };

            // Log techniques that are missing key data for debugging
            if (string.IsNullOrEmpty(technique.Tactic) || string.IsNullOrEmpty(technique.Platform))
            {
                _logger.LogDebug("Technique {TechniqueId} missing data - Tactic: '{Tactic}', Platform: '{Platform}'", 
                    techniqueId, technique.Tactic ?? "null", technique.Platform ?? "null");
            }

            return technique;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse STIX object");
            return null;
        }
    }

    /// <summary>
    /// Formats tactic names for consistent display
    /// </summary>
    private static string FormatTacticName(string? tacticName)
    {
        if (string.IsNullOrEmpty(tacticName))
            return "";
        
        // Convert from kebab-case to Title Case
        return string.Join(" ", tacticName.Split('-')
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    /// <summary>
    /// Imports techniques to the database
    /// </summary>
    private async Task<ImportResult> ImportTechniquesToDatabaseAsync(List<MitreTechnique> techniques)
    {
        var result = new ImportResult();
        
        _logger.LogInformation("Importing {Count} techniques to database...", techniques.Count);
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            foreach (var technique in techniques)
            {
                try
                {
                    var existing = await _context.MitreTechniques
                        .FirstOrDefaultAsync(t => t.TechniqueId == technique.TechniqueId);
                    
                    if (existing == null)
                    {
                        _context.MitreTechniques.Add(technique);
                        result.TechniquesImported++;
                    }
                    else
                    {
                        // Update existing technique
                        existing.Name = technique.Name;
                        existing.Description = technique.Description;
                        existing.Tactic = technique.Tactic;
                        existing.Platform = technique.Platform;
                        existing.DataSources = technique.DataSources;
                        existing.Mitigations = technique.Mitigations;
                        existing.Examples = technique.Examples;
                        
                        result.TechniquesUpdated++;
                    }
                    
                    // Save periodically to avoid memory issues
                    if ((result.TechniquesImported + result.TechniquesUpdated) % 50 == 0)
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogDebug("Saved batch of techniques. Progress: {Progress}/{Total}", 
                            result.TechniquesImported + result.TechniquesUpdated, techniques.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import technique {TechniqueId}: {TechniqueName}", 
                        technique.TechniqueId, technique.Name);
                    result.Errors.Add($"Failed to import {technique.TechniqueId}: {ex.Message}");
                }
            }
            
            // Final save
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Update configuration to track import date
            await UpdateImportConfiguration();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed during MITRE import");
            throw;
        }
        
        return result;
    }

    /// <summary>
    /// Updates system configuration to track import date
    /// </summary>
    private async Task UpdateImportConfiguration()
    {
        var configService = new SystemConfigurationService(_context, 
            _logger.GetType().Assembly.CreateInstance(typeof(ILogger<SystemConfigurationService>).Name) as ILogger<SystemConfigurationService> 
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemConfigurationService>.Instance);
        
        await configService.SetConfigurationValueAsync(
            "LastMitreImport", 
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            "Last successful MITRE ATT&CK data import");
        
        await configService.SetConfigurationValueAsync(
            "MitreImportVersion",
            DateTime.UtcNow.ToString("yyyyMMdd"),
            "Version of MITRE ATT&CK data imported");
    }

    /// <summary>
    /// Gets the count of techniques currently in the database
    /// </summary>
    public async Task<int> GetTechniqueCountAsync()
    {
        return await _context.MitreTechniques.CountAsync();
    }

    /// <summary>
    /// Checks if techniques need to be imported (database is empty or old)
    /// </summary>
    public async Task<bool> ShouldImportTechniquesAsync()
    {
        var count = await GetTechniqueCountAsync();
        
        // Always import if database is empty
        if (count == 0)
        {
            _logger.LogInformation("Database is empty, MITRE import required");
            return true;
        }
        
        // If we have very few techniques (just seed data), import full dataset
        if (count < 50)
        {
            _logger.LogInformation("Database has only {Count} techniques (likely just seed data), importing full dataset", count);
            return true;
        }
        
        // Check if data is older than configured refresh interval
        var refreshIntervalDays = _configuration.GetValue<int>("Mitre:RefreshIntervalDays", 30);
        var configService = new SystemConfigurationService(_context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemConfigurationService>.Instance);
        
        var lastImport = await configService.GetLastMitreUpdateAsync();
        if (lastImport == null)
        {
            _logger.LogInformation("No previous import date found, importing MITRE data");
            return true;
        }
        
        var daysSinceImport = (DateTime.UtcNow - lastImport.Value).TotalDays;
        if (daysSinceImport > refreshIntervalDays)
        {
            _logger.LogInformation("MITRE data is {Days} days old (refresh interval: {RefreshDays} days), importing updates", 
                (int)daysSinceImport, refreshIntervalDays);
            return true;
        }
        
        _logger.LogDebug("MITRE data is current ({Days} days old, refresh interval: {RefreshDays} days)", 
            (int)daysSinceImport, refreshIntervalDays);
        return false;
    }
}

/// <summary>
/// Result of MITRE ATT&CK import operation
/// </summary>
public class ImportResult
{
    public int TechniquesImported { get; set; }
    public int TechniquesUpdated { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public int TotalProcessed => TechniquesImported + TechniquesUpdated;
    public bool HasErrors => Errors.Count > 0;
}
