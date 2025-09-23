using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;

namespace Castellan.Worker.Services;

public class DatabaseSchemaUpdateService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<DatabaseSchemaUpdateService> _logger;

    public DatabaseSchemaUpdateService(CastellanDbContext context, ILogger<DatabaseSchemaUpdateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureTablesExistAsync()
    {
        try
        {
            var connectionString = _context.Database.GetConnectionString();
            
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // Check if SavedSearches table exists
            var savedSearchesExists = await TableExistsAsync(connection, "SavedSearches");
            if (!savedSearchesExists)
            {
                _logger.LogInformation("Creating SavedSearches table...");
                await CreateSavedSearchesTableAsync(connection);
                _logger.LogInformation("SavedSearches table created successfully");
            }
            else
            {
                // Check if table has correct schema, recreate if not
                var hasCorrectSchema = await SavedSearchesHasCorrectSchemaAsync(connection);
                if (!hasCorrectSchema)
                {
                    _logger.LogInformation("SavedSearches table has incorrect schema, recreating...");
                    using var dropCommand = new SqliteCommand("DROP TABLE IF EXISTS SavedSearches;", connection);
                    await dropCommand.ExecuteNonQueryAsync();
                    await CreateSavedSearchesTableAsync(connection);
                    _logger.LogInformation("SavedSearches table recreated with correct schema");
                }
            }

            // Check if SearchHistory table exists
            var searchHistoryExists = await TableExistsAsync(connection, "SearchHistory");
            if (!searchHistoryExists)
            {
                _logger.LogInformation("Creating SearchHistory table...");
                await CreateSearchHistoryTableAsync(connection);
                _logger.LogInformation("SearchHistory table created successfully");
            }

            // Check if ThreatScanHistory table exists
            var threatScanHistoryExists = await TableExistsAsync(connection, "ThreatScanHistory");
            if (!threatScanHistoryExists)
            {
                _logger.LogInformation("Creating ThreatScanHistory table...");
                await CreateThreatScanHistoryTableAsync(connection);
                _logger.LogInformation("ThreatScanHistory table created successfully");
            }

            // Ensure correlation fields exist in SecurityEvents table
            await EnsureCorrelationFieldsExistAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure search tables exist");
            throw;
        }
    }

    private async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
    {
        var query = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
        using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@tableName", tableName);
        
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private async Task<bool> SavedSearchesHasCorrectSchemaAsync(SqliteConnection connection)
    {
        try
        {
            // Check if the table has the expected columns
            var query = "PRAGMA table_info(SavedSearches)";
            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var columns = new HashSet<string>();
            while (await reader.ReadAsync())
            {
                columns.Add(reader["name"].ToString()!);
            }
            
            // Check for required columns from the entity model
            var requiredColumns = new[] { "Id", "UserId", "Name", "SearchFilters", "IsPublic", "CreatedAt", "UpdatedAt", "UseCount" };
            return requiredColumns.All(col => columns.Contains(col));
        }
        catch
        {
            return false;
        }
    }

    private async Task CreateSavedSearchesTableAsync(SqliteConnection connection)
    {
        var sql = @"
            CREATE TABLE ""SavedSearches"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SavedSearches"" PRIMARY KEY AUTOINCREMENT,
                ""UserId"" TEXT NOT NULL,
                ""Name"" TEXT NOT NULL,
                ""Description"" TEXT NULL,
                ""SearchFilters"" TEXT NOT NULL,
                ""IsPublic"" INTEGER NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT NOT NULL,
                ""LastUsedAt"" TEXT NULL,
                ""UseCount"" INTEGER NOT NULL,
                ""Tags"" TEXT NULL
            );

            CREATE INDEX ""IX_SavedSearches_UserId"" ON ""SavedSearches"" (""UserId"");
            CREATE INDEX ""IX_SavedSearches_Name"" ON ""SavedSearches"" (""Name"");
            CREATE INDEX ""IX_SavedSearches_LastUsedAt"" ON ""SavedSearches"" (""LastUsedAt"");
            CREATE INDEX ""IX_SavedSearches_CreatedAt"" ON ""SavedSearches"" (""CreatedAt"");
            CREATE UNIQUE INDEX ""IX_SavedSearches_UserName"" ON ""SavedSearches"" (""UserId"", ""Name"");
        ";
        
        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateSearchHistoryTableAsync(SqliteConnection connection)
    {
        var sql = @"
            CREATE TABLE ""SearchHistory"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SearchHistory"" PRIMARY KEY AUTOINCREMENT,
                ""UserId"" TEXT NOT NULL,
                ""SearchFilters"" TEXT NOT NULL,
                ""SearchHash"" TEXT NOT NULL,
                ""ResultCount"" INTEGER NOT NULL,
                ""ExecutionTimeMs"" INTEGER NOT NULL,
                ""CreatedAt"" TEXT NOT NULL
            );

            CREATE INDEX ""IX_SearchHistory_UserId"" ON ""SearchHistory"" (""UserId"");
            CREATE INDEX ""IX_SearchHistory_SearchHash"" ON ""SearchHistory"" (""SearchHash"");
            CREATE INDEX ""IX_SearchHistory_CreatedAt"" ON ""SearchHistory"" (""CreatedAt"");
            CREATE INDEX ""IX_SearchHistory_UserHash"" ON ""SearchHistory"" (""UserId"", ""SearchHash"");
            CREATE INDEX ""IX_SearchHistory_UserTime"" ON ""SearchHistory"" (""UserId"", ""CreatedAt"");
        ";
        
        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateThreatScanHistoryTableAsync(SqliteConnection connection)
    {
        var sql = @"
            CREATE TABLE ""ThreatScanHistory"" (
                ""Id"" TEXT NOT NULL CONSTRAINT ""PK_ThreatScanHistory"" PRIMARY KEY,
                ""ScanType"" TEXT NOT NULL,
                ""Status"" TEXT NOT NULL,
                ""StartTime"" TEXT NOT NULL,
                ""EndTime"" TEXT NULL,
                ""Duration"" REAL NOT NULL DEFAULT 0,
                ""FilesScanned"" INTEGER NOT NULL DEFAULT 0,
                ""DirectoriesScanned"" INTEGER NOT NULL DEFAULT 0,
                ""BytesScanned"" INTEGER NOT NULL DEFAULT 0,
                ""ThreatsFound"" INTEGER NOT NULL DEFAULT 0,
                ""MalwareDetected"" INTEGER NOT NULL DEFAULT 0,
                ""BackdoorsDetected"" INTEGER NOT NULL DEFAULT 0,
                ""SuspiciousFiles"" INTEGER NOT NULL DEFAULT 0,
                ""RiskLevel"" TEXT NOT NULL DEFAULT 'Low',
                ""Summary"" TEXT NULL,
                ""ErrorMessage"" TEXT NULL,
                ""ScanPath"" TEXT NULL,
                ""CreatedAt"" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX ""IX_ThreatScanHistory_StartTime"" ON ""ThreatScanHistory"" (""StartTime"");
            CREATE INDEX ""IX_ThreatScanHistory_Status"" ON ""ThreatScanHistory"" (""Status"");
            CREATE INDEX ""IX_ThreatScanHistory_ScanType"" ON ""ThreatScanHistory"" (""ScanType"");
            CREATE INDEX ""IX_ThreatScanHistory_CreatedAt"" ON ""ThreatScanHistory"" (""CreatedAt"");
        ";

        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureCorrelationFieldsExistAsync(SqliteConnection connection)
    {
        try
        {
            // Check if SecurityEvents table has correlation fields
            var hasCorrelationFields = await SecurityEventsHasCorrelationFieldsAsync(connection);
            if (!hasCorrelationFields)
            {
                _logger.LogInformation("Adding correlation fields to SecurityEvents table...");
                await AddCorrelationFieldsToSecurityEventsAsync(connection);
                _logger.LogInformation("Correlation fields added to SecurityEvents table successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure correlation fields exist");
            throw;
        }
    }

    private async Task<bool> SecurityEventsHasCorrelationFieldsAsync(SqliteConnection connection)
    {
        try
        {
            // Check if the table has the correlation columns
            var query = "PRAGMA table_info(SecurityEvents)";
            using var command = new SqliteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var columns = new HashSet<string>();
            while (await reader.ReadAsync())
            {
                columns.Add(reader["name"].ToString()!);
            }

            // Check for correlation fields
            return columns.Contains("CorrelationIds") && columns.Contains("CorrelationContext");
        }
        catch
        {
            return false;
        }
    }

    private async Task AddCorrelationFieldsToSecurityEventsAsync(SqliteConnection connection)
    {
        var sql = @"
            ALTER TABLE ""SecurityEvents"" ADD COLUMN ""CorrelationIds"" TEXT NULL;
            ALTER TABLE ""SecurityEvents"" ADD COLUMN ""CorrelationContext"" TEXT NULL;
        ";

        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}