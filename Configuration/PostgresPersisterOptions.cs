namespace TinyBasePostgresPersister.Configuration;

/// <summary>
/// Configuration options for PostgreSQL persister
/// </summary>
public class PostgresPersisterOptions
{
    /// <summary>
    /// PostgreSQL connection string
    /// </summary>
    public string ConnectionString { get; set; } = "Host=localhost;Database=tinybase;Username=postgres;Password=password";

    /// <summary>
    /// Default table name for persister
    /// </summary>
    public string DefaultTableName { get; set; } = "tinybase_store";

    /// <summary>
    /// Whether to create database schema automatically
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Enable detailed SQL logging
    /// </summary>
    public bool EnableSqlLogging { get; set; } = false;

    /// <summary>
    /// Database provider (PostgreSQL)
    /// </summary>
    public string DatabaseProvider { get; set; } = "PostgreSQL";
}