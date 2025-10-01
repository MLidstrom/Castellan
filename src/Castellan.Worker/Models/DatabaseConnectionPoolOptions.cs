namespace Castellan.Worker.Models;

public class DatabaseConnectionPoolOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "SQLite"; // "SQLite" or "PostgreSQL"
    public int MaxPoolSize { get; set; } = 100;
    public int MinPoolSize { get; set; } = 5;
    public TimeSpan ConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(30);
    public bool EnableStatistics { get; set; } = true;
    public DatabaseConnectionPoolHealthCheckOptions HealthCheck { get; set; } = new();
    public SQLiteOptimizationOptions? SQLiteOptimizations { get; set; } = new();
    public PostgreSQLOptimizationOptions? PostgreSQLOptimizations { get; set; } = new();
}

public class DatabaseConnectionPoolHealthCheckOptions
{
    public bool Enabled { get; set; } = true;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}

public class SQLiteOptimizationOptions
{
    public string JournalMode { get; set; } = "WAL";
    public int CacheSize { get; set; } = 10000;
    public int BusyTimeout { get; set; } = 5000;
    public string Synchronous { get; set; } = "NORMAL";
    public string TempStore { get; set; } = "MEMORY";
}

public class PostgreSQLOptimizationOptions
{
    public bool Pooling { get; set; } = true;
    public int MinPoolSize { get; set; } = 10;
    public int MaxPoolSize { get; set; } = 200;
    public int ConnectionLifetime { get; set; } = 1800;
    public int ConnectionIdleLifetime { get; set; } = 300;
    public int CommandTimeout { get; set; } = 30;
    public int Timeout { get; set; } = 15;
    public int KeepAlive { get; set; } = 30;
    public string ApplicationName { get; set; } = "Castellan";
    public bool IncludeErrorDetail { get; set; } = false;
}