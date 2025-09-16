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

    public async Task EnsureSearchTablesExistAsync()
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
}