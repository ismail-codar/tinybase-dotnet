using System.Text.Json;
using System.Text.RegularExpressions;
using TinyBasePostgresPersister.Models.Configuration;

namespace TinyBasePostgresPersister.Services;

/// <summary>
/// Configuration utilities for parsing and validating persister configurations
/// </summary>
public static class ConfigurationUtilities
{
    private const string TABLE_NAME_PLACEHOLDER = "___TABLE_NAME___";
    private const string TRUE = "TRUE";
    private const string SELECT = "SELECT ";
    private const string WHERE = " WHERE ";
    private static readonly Regex TableNamePattern = new(@"^([a-zA-Z_][a-zA-Z0-9_]*)$");

    /// <summary>
    /// Parse configuration structures similar to TypeScript getConfigStructures
    /// </summary>
    public static (bool isJson, PersisterConfig? rawConfig, PersisterConfig defaultedConfig, HashSet<string> managedTableNamesSet) 
        GetConfigStructures(string? configOrTableName = null)
    {
        var isJson = false;
        PersisterConfig? rawConfig = null;

        if (!string.IsNullOrEmpty(configOrTableName))
        {
            if (configOrTableName.StartsWith("{"))
            {
                // JSON configuration
                isJson = true;
                try
                {
                    rawConfig = JsonSerializer.Deserialize<PersisterConfig>(configOrTableName, new JsonSerializerOptions
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
                rawConfig = new PersisterConfig
                {
                    IsJson = false,
                    ManagedTableNames = new List<string> { configOrTableName }
                };
            }
        }

        // Create defaulted configuration
        var defaultedConfig = new PersisterConfig
        {
            IsJson = rawConfig?.IsJson ?? false,
            StoreTableName = rawConfig?.StoreTableName ?? "tinybase_store",
            ManagedTableNames = new List<string>(rawConfig?.ManagedTableNames ?? new List<string> { "default_table" }),
            TableConfigs = new List<TableConfig>(rawConfig?.TableConfigs ?? new List<TableConfig>())
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
    /// Validate table name for PostgreSQL
    /// </summary>
    public static bool IsValidTableName(string tableName)
    {
        return !string.IsNullOrEmpty(tableName) && TableNamePattern.IsMatch(tableName);
    }

    /// <summary>
    /// Escape PostgreSQL identifier
    /// </summary>
    public static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;

        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    /// <summary>
    /// Escape PostgreSQL identifiers
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
        return string.Join(",", Enumerable.Range(1, count).Select(i => $"${i}"));
    }

    /// <summary>
    /// Replace table name placeholder in SQL
    /// </summary>
    public static string ReplaceTableName(string sql, string tableName, string newValue)
    {
        return sql.Replace(tableName, newValue);
    }

    /// <summary>
    /// Get database schema information
    /// </summary>
    public static string GetTableSchemaQuery(IEnumerable<string> tableNames)
    {
        var tableArray = tableNames.ToArray();
        var placeholders = GetPlaceholders(tableArray.Length);
        
        return $"{SELECT} table_name, column_name FROM information_schema.columns {WHERE} table_schema='public' AND table_name IN({placeholders})";
    }

    /// <summary>
    /// Create table creation SQL
    /// </summary>
    public static string CreateTableSql(string tableName, string? additionalColumns = null)
    {
        var escapedTableName = EscapeIdentifier(tableName);
        var baseColumns = "(\"_id\" text PRIMARY KEY)";
        
        if (!string.IsNullOrEmpty(additionalColumns))
        {
            return $"CREATE TABLE IF NOT EXISTS {escapedTableName} {baseColumns}, {additionalColumns})";
        }
        
        return $"CREATE TABLE IF NOT EXISTS {escapedTableName} {baseColumns}";
    }

    /// <summary>
    /// Generate hash for configuration
    /// </summary>
    public static string GenerateConfigHash(PersisterConfig config)
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
    /// Apply when condition for triggers
    /// </summary>
    public static string ApplyWhenCondition(string whenCondition, string tableName, bool isNew = true)
    {
        if (string.IsNullOrEmpty(whenCondition) || whenCondition == TRUE)
            return TRUE;

        var columnName = isNew ? "NEW" : "OLD";
        return whenCondition.Replace(TABLE_NAME_PLACEHOLDER, columnName);
    }
}