namespace TinyBasePostgresPersister.Models.Configuration;

/// <summary>
/// Configuration for PostgreSQL persister
/// </summary>
public class PersisterConfig
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

    /// <summary>
    /// When condition for triggers
    /// </summary>
    public string? WhenCondition { get; set; }
}

/// <summary>
/// Defaulted table configuration
/// </summary>
public class DefaultedTableConfig
{
    public string TableName { get; set; } = string.Empty;
    public List<string> ColumnNames { get; set; } = new();
    public List<string> ColumnTypes { get; set; } = new();
    public string? WhenCondition { get; set; }
    public bool IsManaged { get; set; }
}