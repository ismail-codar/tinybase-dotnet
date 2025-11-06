using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Data.Repositories;
using TinyBaseSqlitePersister.Models.Configuration;
using TinyBaseSqlitePersister.Services.Persisters;

namespace TinyBaseSqlitePersister.Services;

/// <summary>
/// Factory service for creating SQLite persister instances
/// </summary>
public interface ISqlitePersisterFactory
{
    /// <summary>
    /// Create a SQLite persister for a TinyBase store
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="configOrTableName">Configuration or table name</param>
    /// <param name="onSqlCommand">SQL command callback</param>
    /// <param name="onIgnoredError">Error callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created persister instance</returns>
    Task<ISqlitePersister> CreateSqlitePersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a SQLite persister for a file-based database
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="databasePath">Path to SQLite database file</param>
    /// <param name="configOrTableName">Configuration or table name</param>
    /// <param name="onSqlCommand">SQL command callback</param>
    /// <param name="onIgnoredError">Error callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created persister instance</returns>
    Task<ISqlitePersister> CreateFileBasedSqlitePersisterAsync(
        string storeId,
        string databasePath,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory implementation for creating SQLite persister instances
/// </summary>
public class SqlitePersisterFactory : ISqlitePersisterFactory
{
    private readonly SqliteDbContext _context;
    private readonly string _connectionString;

    public SqlitePersisterFactory(SqliteDbContext context, string connectionString)
    {
        _context = context;
        _connectionString = connectionString;
    }

    public async Task<ISqlitePersister> CreateSqlitePersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default)
    {
        // Parse configuration
        var config = await ParseConfigurationAsync(storeId, configOrTableName, cancellationToken);
        
        // Create database context unit of work
        var unitOfWork = new SqliteUnitOfWork(_context);
        
        // Create SQLite connection
        var connection = new SqliteConnection(_connectionString);
        
        // Configure callbacks
        var persisterConfig = new SqlitePersisterConfig
        {
            StoreTableName = config.StoreTableName,
            ManagedTableNames = config.ManagedTableNames,
            IsJson = config.IsJson,
            AutoLoadIntervalSeconds = config.AutoLoadIntervalSeconds,
            TableConfigs = config.TableConfigs,
            OnSqlCommand = onSqlCommand,
            OnIgnoredError = onIgnoredError
        };

        // Create persister
        return new SqlitePersister(unitOfWork, connection, persisterConfig);
    }

    public async Task<ISqlitePersister> CreateFileBasedSqlitePersisterAsync(
        string storeId,
        string databasePath,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default)
    {
        // Create a connection string for the file-based database
        var connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Private;";
        
        // Ensure the directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Create a context for this specific database
        var optionsBuilder = new DbContextOptionsBuilder<SqliteDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        var context = new SqliteDbContext(optionsBuilder.Options);
        
        // Parse configuration
        var config = await ParseConfigurationAsync(storeId, configOrTableName, cancellationToken);
        
        // Create database context unit of work
        var unitOfWork = new SqliteUnitOfWork(context);
        
        // Create SQLite connection
        var connection = new SqliteConnection(connectionString);
        
        // Configure callbacks
        var persisterConfig = new SqlitePersisterConfig
        {
            StoreTableName = config.StoreTableName,
            ManagedTableNames = config.ManagedTableNames,
            IsJson = config.IsJson,
            AutoLoadIntervalSeconds = config.AutoLoadIntervalSeconds,
            TableConfigs = config.TableConfigs,
            OnSqlCommand = onSqlCommand,
            OnIgnoredError = onIgnoredError
        };

        // Create persister
        return new SqlitePersister(unitOfWork, connection, persisterConfig);
    }

    private async Task<SqlitePersisterConfig> ParseConfigurationAsync(
        string storeId, 
        string? configOrTableName, 
        CancellationToken cancellationToken)
    {
        var config = new SqlitePersisterConfig();

        if (string.IsNullOrEmpty(configOrTableName))
        {
            // Default configuration
            config.IsJson = false;
            config.AutoLoadIntervalSeconds = 5;
            config.ManagedTableNames = new List<string> { $"table_{storeId}" };
        }
        else if (configOrTableName.StartsWith("{"))
        {
            // JSON configuration
            try
            {
                var jsonConfig = JsonSerializer.Deserialize<SqlitePersisterConfig>(configOrTableName, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (jsonConfig != null)
                {
                    config.IsJson = jsonConfig.IsJson;
                    config.AutoLoadIntervalSeconds = jsonConfig.AutoLoadIntervalSeconds;
                    config.ManagedTableNames = new List<string>(jsonConfig.ManagedTableNames);
                    config.TableConfigs = new List<TableConfig>(jsonConfig.TableConfigs);
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON configuration: {ex.Message}", nameof(configOrTableName), ex);
            }
        }
        else
        {
            // Table name provided
            config.IsJson = false;
            config.ManagedTableNames = new List<string> { configOrTableName };
        }

        // Ensure we have at least one managed table name
        if (!config.ManagedTableNames.Any())
        {
            config.ManagedTableNames.Add($"default_{storeId}");
        }

        return config;
    }
}