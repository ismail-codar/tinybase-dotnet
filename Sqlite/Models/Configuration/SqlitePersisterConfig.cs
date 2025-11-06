namespace TinyBaseSqlitePersister.Models.Configuration;

/// <summary>
/// Configuration for SQLite persister
/// </summary>
public class SqlitePersisterConfig
{
    /// <summary>
    /// Table name for storing persister data
    /// </summary>
    public string? StoreTableName { get; set; }

    /// <summary>
    /// List of managed table names
    /// </summary>
    public List<string> ManagedTableNames { get; set; } = new();

    /// <summary>
    /// Whether to use JSON persistence mode
    /// </summary>
    public bool IsJson { get; set; }

    /// <summary>
    /// Auto-load interval in seconds (0 = disabled)
    /// </summary>
    public int AutoLoadIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Table configuration for tabular mode
    /// </summary>
    public List<TableConfig> TableConfigs { get; set; } = new();
}

/// <summary>
/// Configuration for a specific table
/// </summary>
public class TableConfig
{
    /// <summary>
    /// Table name
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Column names in the table
    /// </summary>
    public List<string> ColumnNames { get; set; } = new();

    /// <summary>
    /// Column types for each column
    /// </summary>
    public List<string> ColumnTypes { get; set; } = new();
}

/// <summary>
/// SQLite-specific version information
/// </summary>
public class SqliteVersionInfo
{
    /// <summary>
    /// Data version
    /// </summary>
    public int DataVersion { get; set; }

    /// <summary>
    /// Schema version
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Total changes count
    /// </summary>
    public int TotalChanges { get; set; }
}