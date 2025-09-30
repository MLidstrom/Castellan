using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

/// <summary>
/// SQLite-backed implementation of IYaraRuleStore.
/// Stores rules and matches in the main data/castellan.db database.
/// </summary>
public class DatabaseYaraRuleStore : IYaraRuleStore
{
    private readonly ILogger<DatabaseYaraRuleStore> _logger;
    private readonly string _dbPath;
    private readonly object _lock = new object();

    public DatabaseYaraRuleStore(
        ILogger<DatabaseYaraRuleStore> logger,
        DatabaseConfiguration dbConfig)
    {
        _logger = logger;
        _dbPath = dbConfig.Path;
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        EnsureSchema();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        lock (_lock)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS YaraRules (
  Id TEXT PRIMARY KEY,
  Name TEXT NOT NULL UNIQUE,
  Description TEXT,
  RuleContent TEXT NOT NULL,
  Category TEXT,
  Author TEXT,
  CreatedAt TEXT,
  UpdatedAt TEXT,
  IsEnabled INTEGER,
  Priority INTEGER,
  ThreatLevel TEXT,
  HitCount INTEGER,
  FalsePositiveCount INTEGER,
  AverageExecutionTimeMs REAL,
  MitreTechniques TEXT,
  Tags TEXT,
  Version INTEGER,
  PreviousVersion TEXT,
  IsValid INTEGER,
  ValidationError TEXT,
  LastValidated TEXT,
  Source TEXT,
  SourceUrl TEXT,
  TestSample TEXT,
  TestResult INTEGER
);
CREATE INDEX IF NOT EXISTS IX_YaraRules_Category ON YaraRules(Category);
CREATE INDEX IF NOT EXISTS IX_YaraRules_Enabled ON YaraRules(IsEnabled);
CREATE INDEX IF NOT EXISTS IX_YaraRules_Name ON YaraRules(Name);
CREATE INDEX IF NOT EXISTS IX_YaraRules_Priority ON YaraRules(Priority);
CREATE INDEX IF NOT EXISTS IX_YaraRules_UpdatedAt ON YaraRules(UpdatedAt);

CREATE TABLE IF NOT EXISTS YaraMatches (
  Id TEXT PRIMARY KEY,
  RuleId TEXT,
  RuleName TEXT,
  MatchTime TEXT,
  TargetFile TEXT,
  TargetHash TEXT,
  MatchedStrings TEXT,
  Metadata TEXT,
  ExecutionTimeMs REAL,
  SecurityEventId TEXT
);
CREATE INDEX IF NOT EXISTS IX_YaraMatches_RuleId ON YaraMatches(RuleId);
CREATE INDEX IF NOT EXISTS IX_YaraMatches_Time ON YaraMatches(MatchTime);
";
            cmd.ExecuteNonQuery();
        }
    }

    private static string Serialize(object obj) => JsonSerializer.Serialize(obj);
    private static List<string> DeserializeStringList(string? json)
        => string.IsNullOrWhiteSpace(json) ? new List<string>() : (JsonSerializer.Deserialize<List<string>>(json!) ?? new List<string>());
    private static Dictionary<string, string> DeserializeDict(string? json)
        => string.IsNullOrWhiteSpace(json) ? new Dictionary<string, string>() : (JsonSerializer.Deserialize<Dictionary<string, string>>(json!) ?? new Dictionary<string, string>());
    private static List<YaraMatchString> DeserializeMatchStrings(string? json)
        => string.IsNullOrWhiteSpace(json) ? new List<YaraMatchString>() : (JsonSerializer.Deserialize<List<YaraMatchString>>(json!) ?? new List<YaraMatchString>());

    public Task<IEnumerable<YaraRule>> GetAllRulesAsync()
    {
        return Task.FromResult(QueryRules("SELECT * FROM YaraRules ORDER BY Name"));
    }

    public Task<(IEnumerable<YaraRule> Rules, int TotalCount)> GetRulesPagedAsync(int page = 1, int limit = 25, string? category = null, string? tag = null, string? mitreTechnique = null, bool? enabled = null)
    {
        // Build WHERE clause and parameters based on filters
        var whereConditions = new List<string>();
        var parameters = new List<(string Name, object Value)>();

        if (!string.IsNullOrEmpty(category))
        {
            whereConditions.Add("Category = @Category");
            parameters.Add(("@Category", category));
        }

        if (!string.IsNullOrEmpty(tag))
        {
            whereConditions.Add("Tags LIKE @Tag");
            parameters.Add(("@Tag", "%" + tag + "%"));
        }

        if (!string.IsNullOrEmpty(mitreTechnique))
        {
            whereConditions.Add("MitreTechniques LIKE @MitreTechnique");
            parameters.Add(("@MitreTechnique", "%" + mitreTechnique + "%"));
        }

        if (enabled.HasValue)
        {
            whereConditions.Add("IsEnabled = @Enabled");
            parameters.Add(("@Enabled", enabled.Value ? 1 : 0));
        }

        string whereClause = whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";

        // Get total count for pagination
        string countSql = $"SELECT COUNT(*) FROM YaraRules {whereClause}";
        int totalCount = QueryCount(countSql, parameters.ToArray());

        // Get paginated results
        int offset = (page - 1) * limit;
        string dataSql = $"SELECT * FROM YaraRules {whereClause} ORDER BY Name LIMIT @Limit OFFSET @Offset";
        parameters.Add(("@Limit", limit));
        parameters.Add(("@Offset", offset));

        var rules = QueryRules(dataSql, parameters.ToArray());

        return Task.FromResult((rules, totalCount));
    }

    public Task<IEnumerable<YaraRule>> GetEnabledRulesAsync()
    {
        return Task.FromResult(QueryRules("SELECT * FROM YaraRules WHERE IsEnabled = 1 ORDER BY Name"));
    }

    public Task<YaraRule?> GetRuleByIdAsync(string id)
    {
        var rules = QueryRules("SELECT * FROM YaraRules WHERE Id = @Id", ("@Id", id));
        return Task.FromResult(rules.FirstOrDefault());
    }

    public Task<IEnumerable<YaraRule>> GetRulesByCategoryAsync(string category)
    {
        return Task.FromResult(QueryRules("SELECT * FROM YaraRules WHERE Category = @Category ORDER BY Name", ("@Category", category)));
    }

    public Task<IEnumerable<YaraRule>> GetRulesByMitreTechniqueAsync(string techniqueId)
    {
        // Mitre techniques stored as JSON array; simple LIKE match for now
        return Task.FromResult(QueryRules("SELECT * FROM YaraRules WHERE MitreTechniques LIKE @mt ORDER BY Name", ("@mt", "%" + techniqueId + "%")));
    }

    public Task<IEnumerable<YaraRule>> GetRulesByTagAsync(string tag)
    {
        return Task.FromResult(QueryRules("SELECT * FROM YaraRules WHERE Tags LIKE @tg ORDER BY Name", ("@tg", "%" + tag + "%")));
    }

    public Task<bool> RuleExistsAsync(string name)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM YaraRules WHERE Name = @Name LIMIT 1";
        cmd.Parameters.AddWithValue("@Name", name);
        var exists = cmd.ExecuteScalar() != null;
        return Task.FromResult(exists);
    }

    public Task<YaraRule> AddRuleAsync(YaraRule rule)
    {
        if (string.IsNullOrEmpty(rule.Id)) rule.Id = Guid.NewGuid().ToString();
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
        UpsertRule(rule);
        _logger.LogInformation("DB: Added YARA rule {Name} ({Id})", rule.Name, rule.Id);
        return Task.FromResult(rule);
    }

    public Task<YaraRule> UpdateRuleAsync(YaraRule rule)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        rule.Version = (rule.Version <= 0 ? 1 : rule.Version) + 1;
        UpsertRule(rule);
        _logger.LogInformation("DB: Updated YARA rule {Name} ({Id})", rule.Name, rule.Id);
        return Task.FromResult(rule);
    }

    public Task<bool> DeleteRuleAsync(string id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM YaraRules WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        var rows = cmd.ExecuteNonQuery();
        return Task.FromResult(rows > 0);
    }

    public Task UpdateRuleMetricsAsync(string ruleId, bool matched, double executionTimeMs)
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT HitCount, AverageExecutionTimeMs FROM YaraRules WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", ruleId);
            using var r = cmd.ExecuteReader();
            int hit = 0; double avg = 0;
            if (r.Read())
            {
                hit = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                avg = r.IsDBNull(1) ? 0 : r.GetDouble(1);
            }
            r.Close();
            if (matched) hit++;
            var totalExecutions = hit + (matched ? 0 : 1);
            var newAvg = totalExecutions > 0 ? ((avg * (totalExecutions - 1)) + executionTimeMs) / totalExecutions : executionTimeMs;
            cmd.Parameters.Clear();
            cmd.CommandText = "UPDATE YaraRules SET HitCount = @Hit, AverageExecutionTimeMs = @Avg WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Hit", hit);
            cmd.Parameters.AddWithValue("@Avg", newAvg);
            cmd.Parameters.AddWithValue("@Id", ruleId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return Task.CompletedTask;
    }

    public Task RecordFalsePositiveAsync(string ruleId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE YaraRules SET FalsePositiveCount = COALESCE(FalsePositiveCount,0) + 1 WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", ruleId);
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task<YaraMatch> SaveMatchAsync(YaraMatch match)
    {
        if (string.IsNullOrEmpty(match.Id)) match.Id = Guid.NewGuid().ToString();
        match.MatchTime = DateTime.UtcNow;
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO YaraMatches
(Id, RuleId, RuleName, MatchTime, TargetFile, TargetHash, MatchedStrings, Metadata, ExecutionTimeMs, SecurityEventId)
VALUES (@Id, @RuleId, @RuleName, @MatchTime, @TargetFile, @TargetHash, @MatchedStrings, @Metadata, @ExecMs, @SecId)";
        cmd.Parameters.AddWithValue("@Id", match.Id);
        cmd.Parameters.AddWithValue("@RuleId", match.RuleId ?? "");
        cmd.Parameters.AddWithValue("@RuleName", match.RuleName ?? "");
        cmd.Parameters.AddWithValue("@MatchTime", match.MatchTime.ToString("o"));
        cmd.Parameters.AddWithValue("@TargetFile", match.TargetFile ?? "");
        cmd.Parameters.AddWithValue("@TargetHash", match.TargetHash ?? "");
        cmd.Parameters.AddWithValue("@MatchedStrings", Serialize(match.MatchedStrings));
        cmd.Parameters.AddWithValue("@Metadata", Serialize(match.Metadata));
        cmd.Parameters.AddWithValue("@ExecMs", match.ExecutionTimeMs);
        cmd.Parameters.AddWithValue("@SecId", match.SecurityEventId ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
        _logger.LogInformation("DB: Saved YARA match for rule {RuleName}", match.RuleName);
        return Task.FromResult(match);
    }

    public Task<IEnumerable<YaraMatch>> GetRecentMatchesAsync(int count = 100)
    {
        return Task.FromResult(QueryMatches("SELECT * FROM YaraMatches ORDER BY datetime(MatchTime) DESC LIMIT @Count", ("@Count", count)));
    }

    public Task<IEnumerable<YaraMatch>> GetMatchesBySecurityEventAsync(string securityEventId)
    {
        return Task.FromResult(QueryMatches("SELECT * FROM YaraMatches WHERE SecurityEventId = @SecId ORDER BY datetime(MatchTime) DESC", ("@SecId", securityEventId)));
    }

    // Non-interface helper used by LegacyYaraRulesImporter
    public Task<int> BulkUpsertRulesAsync(List<YaraRule> rules)
    {
        var imported = 0;
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var r in rules)
        {
            if (string.IsNullOrEmpty(r.Id)) r.Id = Guid.NewGuid().ToString();
            UpsertRule(r, conn, tx);
            imported++;
        }
        tx.Commit();
        return Task.FromResult(imported);
    }

    private void UpsertRule(YaraRule r, SqliteConnection? existingConn = null, SqliteTransaction? tx = null)
    {
        using var conn = existingConn ?? OpenConnection();
        using var cmd = conn.CreateCommand();
        if (tx != null) cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO YaraRules
(Id, Name, Description, RuleContent, Category, Author, CreatedAt, UpdatedAt, IsEnabled, Priority, ThreatLevel, HitCount, FalsePositiveCount, AverageExecutionTimeMs, MitreTechniques, Tags, Version, PreviousVersion, IsValid, ValidationError, LastValidated, Source, SourceUrl, TestSample, TestResult)
VALUES (@Id, @Name, @Description, @RuleContent, @Category, @Author, @CreatedAt, @UpdatedAt, @IsEnabled, @Priority, @ThreatLevel, @HitCount, @FalsePositiveCount, @AvgMs, @Mitre, @Tags, @Version, @PrevVersion, @IsValid, @ValidationError, @LastValidated, @Source, @SourceUrl, @TestSample, @TestResult)
ON CONFLICT(Id) DO UPDATE SET
  Name = excluded.Name,
  Description = excluded.Description,
  RuleContent = excluded.RuleContent,
  Category = excluded.Category,
  Author = excluded.Author,
  UpdatedAt = excluded.UpdatedAt,
  IsEnabled = excluded.IsEnabled,
  Priority = excluded.Priority,
  ThreatLevel = excluded.ThreatLevel,
  HitCount = excluded.HitCount,
  FalsePositiveCount = excluded.FalsePositiveCount,
  AverageExecutionTimeMs = excluded.AverageExecutionTimeMs,
  MitreTechniques = excluded.MitreTechniques,
  Tags = excluded.Tags,
  Version = excluded.Version,
  PreviousVersion = excluded.PreviousVersion,
  IsValid = excluded.IsValid,
  ValidationError = excluded.ValidationError,
  LastValidated = excluded.LastValidated,
  Source = excluded.Source,
  SourceUrl = excluded.SourceUrl,
  TestSample = excluded.TestSample,
  TestResult = excluded.TestResult;
";
        cmd.Parameters.AddWithValue("@Id", r.Id);
        cmd.Parameters.AddWithValue("@Name", r.Name);
        cmd.Parameters.AddWithValue("@Description", r.Description ?? "");
        cmd.Parameters.AddWithValue("@RuleContent", r.RuleContent);
        cmd.Parameters.AddWithValue("@Category", r.Category ?? "General");
        cmd.Parameters.AddWithValue("@Author", r.Author ?? "System");
        cmd.Parameters.AddWithValue("@CreatedAt", r.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@UpdatedAt", r.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@IsEnabled", r.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@Priority", r.Priority);
        cmd.Parameters.AddWithValue("@ThreatLevel", r.ThreatLevel ?? "Medium");
        cmd.Parameters.AddWithValue("@HitCount", r.HitCount);
        cmd.Parameters.AddWithValue("@FalsePositiveCount", r.FalsePositiveCount);
        cmd.Parameters.AddWithValue("@AvgMs", r.AverageExecutionTimeMs);
        cmd.Parameters.AddWithValue("@Mitre", Serialize(r.MitreTechniques ?? new List<string>()));
        cmd.Parameters.AddWithValue("@Tags", Serialize(r.Tags ?? new List<string>()));
        cmd.Parameters.AddWithValue("@Version", r.Version);
        cmd.Parameters.AddWithValue("@PrevVersion", (object?)r.PreviousVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsValid", r.IsValid ? 1 : 0);
        cmd.Parameters.AddWithValue("@ValidationError", (object?)r.ValidationError ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastValidated", (object?)r.LastValidated?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Source", r.Source ?? "Custom");
        cmd.Parameters.AddWithValue("@SourceUrl", (object?)r.SourceUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TestSample", (object?)r.TestSample ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TestResult", r.TestResult.HasValue ? (r.TestResult.Value ? 1 : 0) : (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private int QueryCount(string sql, params (string Name, object Value)[] parameters)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private IEnumerable<YaraRule> QueryRules(string sql, params (string Name, object Value)[] parameters)
    {
        var list = new List<YaraRule>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var rule = new YaraRule
            {
                Id = r.GetString(r.GetOrdinal("Id")),
                Name = r.GetString(r.GetOrdinal("Name")),
                Description = r["Description"] as string ?? string.Empty,
                RuleContent = r.GetString(r.GetOrdinal("RuleContent")),
                Category = r["Category"] as string ?? "General",
                Author = r["Author"] as string ?? "System",
                CreatedAt = DateTime.TryParse(r["CreatedAt"] as string, out var ca) ? ca : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(r["UpdatedAt"] as string, out var ua) ? ua : DateTime.UtcNow,
                IsEnabled = Convert.ToInt32(r["IsEnabled"]) == 1,
                Priority = Convert.ToInt32(r["Priority"]),
                ThreatLevel = r["ThreatLevel"] as string ?? "Medium",
                HitCount = Convert.ToInt32(r["HitCount"]),
                FalsePositiveCount = Convert.ToInt32(r["FalsePositiveCount"]),
                AverageExecutionTimeMs = r["AverageExecutionTimeMs"] is double d ? d : Convert.ToDouble(r["AverageExecutionTimeMs"]) ,
                MitreTechniques = DeserializeStringList(r["MitreTechniques"] as string),
                Tags = DeserializeStringList(r["Tags"] as string),
                Version = Convert.ToInt32(r["Version"]),
                PreviousVersion = r["PreviousVersion"] as string,
                IsValid = Convert.ToInt32(r["IsValid"]) == 1,
                ValidationError = r["ValidationError"] as string,
                LastValidated = DateTime.TryParse(r["LastValidated"] as string, out var lv) ? lv : null,
                Source = r["Source"] as string ?? "Custom",
                SourceUrl = r["SourceUrl"] as string,
                TestSample = r["TestSample"] as string,
                TestResult = r["TestResult"] is DBNull ? null : (Convert.ToInt32(r["TestResult"]) == 1)
            };
            list.Add(rule);
        }
        return list;
    }

    private IEnumerable<YaraMatch> QueryMatches(string sql, params (string Name, object Value)[] parameters)
    {
        var list = new List<YaraMatch>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var m = new YaraMatch
            {
                Id = r.GetString(r.GetOrdinal("Id")),
                RuleId = r["RuleId"] as string ?? string.Empty,
                RuleName = r["RuleName"] as string ?? string.Empty,
                MatchTime = DateTime.TryParse(r["MatchTime"] as string, out var mt) ? mt : DateTime.UtcNow,
                TargetFile = r["TargetFile"] as string ?? string.Empty,
                TargetHash = r["TargetHash"] as string ?? string.Empty,
                MatchedStrings = DeserializeMatchStrings(r["MatchedStrings"] as string),
                Metadata = DeserializeDict(r["Metadata"] as string),
                ExecutionTimeMs = r["ExecutionTimeMs"] is double d ? d : 0,
                SecurityEventId = r["SecurityEventId"] as string
            };
            list.Add(m);
        }
        return list;
    }
}
