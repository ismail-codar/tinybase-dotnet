using System.Text.Json;
using System.Text.RegularExpressions;
using TinyBaseSqlitePersister.Models.Configuration;

namespace TinyBaseSqlitePersister.Services;

/// <summary>
/// Configuration utilities for SQLite persister
/// </summary>
public static class SqliteConfigurationUtilities
{
    private const string TRUE = "TRUE";
    private const string SELECT = "SELECT ";
    private const string WHERE = " WHERE ";
    private static readonly Regex TableNamePattern = new(@"^([a-zA-Z_][a-zA-Z0-9_]*)$");

    /// <summary>
    /// Parse configuration structures for SQLite
    /// </summary>
    public static (bool isJson, SqlitePersisterConfig? rawConfig, SqlitePersisterConfig defaultedConfig, HashSet<string> managedTableNamesSet) 
        GetConfigStructures(string? configOrTableName = null)
    {
        var isJson = false;
        SqlitePersisterConfig? rawConfig = null;

        if (!string.IsNullOrEmpty(configOrTableName))
        {
            if (configOrTableName.StartsWith("{"))
            {
                // JSON configuration
                isJson = true;
                try
                {
                    rawConfig = JsonSerializer.Deserialize<SqlitePersisterConfig>(configOrTableName, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"Invalid JSON configuration: {ex.Message}", nameof(configOrTableName), ex);
                }
            }
            else
            {
                // Simple table name
                rawConfig = new SqlitePersisterConfig
                {
                    IsJson = false,
                    ManagedTableNames = new List<string> { configOrTableName }
                };
            }
        }

        // Create defaulted configuration
        var defaultedConfig = new SqlitePersisterConfig
        {
            IsJson = rawConfig?.IsJson ?? false,
            StoreTableName = rawConfig?.StoreTableName ?? "tinybase_store",
            ManagedTableNames = new List<string>(rawConfig?.ManagedTableNames ?? new List<string> { "default_table" }),
            TableConfigs = new List<TableConfig>(rawConfig?.TableConfigs ?? new List<TableConfig>()),
            AutoLoadIntervalSeconds = rawConfig?.AutoLoadIntervalSeconds ?? 5
        };

        // Ensure at least one managed table name
        if (!defaultedConfig.ManagedTableNames.Any())
        {
            defaultedConfig.ManagedTableNames.Add("default_table");
        }

        var managedTableNamesSet = new HashSet<string>(defaultedConfig.ManagedTableNames);

        return (isJson, rawConfig, defaultedConfig, managedTableNamesSet);
    }

    /// <summary>
    /// Validate table name for SQLite
    /// </summary>
    public static bool IsValidTableName(string tableName)
    {
        return !string.IsNullOrEmpty(tableName) && TableNamePattern.IsMatch(tableName);
    }

    /// <summary>
    /// Escape SQLite identifier
    /// </summary>
    public static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        return $"[{identifier.Replace("]", "]]")}]";
    }

    /// <summary>
    /// Escape SQLite identifiers
    /// </summary>
    public static string[] EscapeIdentifiers(params string[] identifiers)
    {
        return identifiers.Select(EscapeIdentifier).ToArray();
    }

    /// <summary>
    /// Generate placeholders for SQL parameters
    /// </summary>
    public static string GetPlaceholders(int count)
    {
        if (count <= 0) return string.Empty;
        return string.Join(",", Enumerable.Range(1, count).Select(i => $"?{i}"));
    }

    /// <summary>
    /// Get SQLite database schema information
    /// </summary>
    public static string GetTableSchemaQuery(IEnumerable<string> tableNames)
    {
        var tableArray = tableNames.ToArray();
        var placeholders = GetPlaceholders(tableArray.Length);
        
        return $"{SELECT} t.name as table_name, c.name as column_name FROM sqlite_master t, pragma_table_info(t.name) c {WHERE} t.type IN ('table','view') AND t.name IN ({placeholders}) ORDER BY t.name, c.ordinal_position";
    }

    /// <summary>
    /// Create table creation SQL for SQLite
    /// </summary>
    public static string CreateTableSql(string tableName, string? additionalColumns = null)
    {
        var escapedTableName = EscapeIdentifier(tableName);
        var baseColumns = "(\"id\" TEXT PRIMARY KEY)";
        
        if (!string.IsNullOrEmpty(additionalColumns))
        {
            return $"CREATE TABLE IF NOT EXISTS {escapedTableName} {baseColumns}, {additionalColumns})";
        }
        
        return $"CREATE TABLE IF NOT EXISTS {escapedTableName} {baseColumns}";
    }

    /// <summary>
    /// Generate hash for configuration
    /// </summary>
    public static string GenerateConfigHash(SqlitePersisterConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Get SQLite version query
    /// </summary>
    public static string GetVersionQuery()
    {
        return @"
            SELECT 
                CAST(user_version AS INTEGER) as data_version,
                CAST(schema_version AS INTEGER) as schema_version,
                total_changes() as total_changes
            FROM pragma_user_version, pragma_schema_version";
    }

    /// <summary>
    /// Get list of managed tables query
    /// </summary>
    public static string GetManagedTablesQuery(IEnumerable<string> tableNames)
    {
        var tableArray = tableNames.ToArray();
        var placeholders = GetPlaceholders(tableArray.Length);
        
        return $"{SELECT} name as table_name FROM sqlite_master {WHERE} type = 'table' AND name IN ({placeholders})";
    }

    /// <summary>
    /// Convert C# boolean to SQLite boolean (0 or 1)
    /// </summary>
    public static object ConvertToSqliteBoolean(bool value)
    {
        return value ? 1 : 0;
    }

    /// <summary>
    /// Convert any value to SQLite-compatible format
    /// </summary>
    public static object ConvertToSqliteValue(object value)
    {
        return value switch
        {
            true => 1,
            false => 0,
            null => DBNull.Value,
            _ => value
        };
    }

    /// <summary>
    /// Create connection string for SQLite file
    /// </summary>
    public static string CreateFileConnectionString(string filePath, bool readOnly = false)
    {
        var mode = readOnly ? "Mode=ReadOnly" : "Mode=ReadWriteCreate";
        return $"Data Source={filePath};{mode};Cache=Private;";
    }

    /// <summary>
    /// Create connection string for in-memory SQLite
    /// </summary>
    public static string CreateInMemoryConnectionString()
    {
        return "Data Source=:memory:;Mode=Memory;Cache=Shared;";
    }

    /// <summary>
    /// Ensure database file directory exists
    /// </summary>
    public static void EnsureDatabaseDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}