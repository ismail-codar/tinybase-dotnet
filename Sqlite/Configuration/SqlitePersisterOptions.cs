namespace TinyBaseSqlitePersister.Configuration;

/// <summary>
/// Configuration options for SQLite persister
/// </summary>
public class SqlitePersisterOptions
{
    /// <summary>
    /// SQLite connection string
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=tinybase.db;Mode=ReadWriteCreate;Cache=Private;";

    /// <summary>
    /// Default table name for persister
    /// </summary>
    public string DefaultTableName { get; set; } = "tinybase_store";

    /// <summary>
    /// Auto-load interval in seconds (0 = disabled)
    /// </summary>
    public int AutoLoadIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Enable detailed SQL logging
    /// </summary>
    public bool EnableSqlLogging { get; set; } = false;

    /// <summary>
    /// Database file path (for file-based databases)
    /// </summary>
    public string? DatabaseFilePath { get; set; }

    /// <summary>
    /// Whether to use in-memory database
    /// </summary>
    public bool UseInMemory { get; set; } = false;

    /// <summary>
    /// Database provider (SQLite)
    /// </summary>
    public string DatabaseProvider { get; set; } = "SQLite";
}