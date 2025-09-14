using System.Text.Json;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace Castellan.Worker.Services;

/// <summary>
/// One-time importer for legacy file-based YARA rules (data/yara/rules.json) into SQLite via EF.
/// Safe to run multiple times; it upserts by rule Name.
/// </summary>
public static class LegacyYaraRulesImporter
{
    public static async Task<int> ImportAsync(IServiceProvider services, ILogger? logger = null, CancellationToken ct = default)
    {
        var imported = 0;
        try
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CastellanDbContext>();
            var store = scope.ServiceProvider.GetRequiredService<DatabaseYaraRuleStore>();
            var dbCount = await context.YaraRules.CountAsync(ct);

            // Locate legacy rules.json in the legacy file location
            var rulesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "yara");
            var rulesPath = Path.Combine(rulesDir, "rules.json");
            if (!File.Exists(rulesPath))
            {
                logger?.LogInformation("No legacy YARA rules file found at {Path}. Skipping import.", rulesPath);
                return 0;
            }

            // Read and deserialize
            var json = await File.ReadAllTextAsync(rulesPath, ct);
            var rules = JsonSerializer.Deserialize<List<YaraRule>>(json) ?? new List<YaraRule>();
            if (rules.Count == 0)
            {
                logger?.LogInformation("Legacy YARA rules file is empty. Skipping import.");
                return 0;
            }

            logger?.LogInformation("Found {Count} legacy YARA rules. Existing DB has {DbCount} rules.", rules.Count, dbCount);

            // Normalize incoming rules (clear IDs so EF assigns, preserve Name as unique key for upsert)
            foreach (var r in rules)
            {
                // If ID is present from file, leave it; DatabaseYaraRuleStore upsert matches on Name
                r.UpdatedAt = DateTime.UtcNow;
                if (r.CreatedAt == default)
                    r.CreatedAt = DateTime.UtcNow;
                if (string.IsNullOrEmpty(r.Author)) r.Author = "Imported";
                if (string.IsNullOrEmpty(r.Source)) r.Source = "Import";
                if (r.Tags == null) r.Tags = new List<string>();
                if (r.MitreTechniques == null) r.MitreTechniques = new List<string>();
                r.IsEnabled = true;
                r.IsValid = true; // assume valid; scanner will revalidate on compile
            }

            imported = await store.BulkUpsertRulesAsync(rules);

            // Update system configuration
            var cfg = await context.SystemConfiguration.FirstOrDefaultAsync(c => c.Key == "LastYaraRulesUpdate", ct);
            if (cfg == null)
            {
                context.SystemConfiguration.Add(new SystemConfiguration
                {
                    Key = "LastYaraRulesUpdate",
                    Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    Description = "Last date YARA rules were imported from legacy file",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                cfg.Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                cfg.UpdatedAt = DateTime.UtcNow;
            }
            await context.SaveChangesAsync(ct);

            logger?.LogInformation("Legacy YARA import complete: {Imported} rules processed.", imported);
            return imported;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to import legacy YARA rules");
            throw;
        }
    }
}
