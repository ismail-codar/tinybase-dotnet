using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using TinyBasePostgresPersister.Data.Contexts;
using TinyBasePostgresPersister.Data.Repositories;
using TinyBasePostgresPersister.Models.Configuration;
using TinyBasePostgresPersister.Services.Persisters;

namespace TinyBasePostgresPersister.Services;

/// <summary>
/// Factory service for creating PostgreSQL persister instances
/// </summary>
public interface IPostgresPersisterFactory
{
    /// <summary>
    /// Create a PostgreSQL persister for a TinyBase store
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="configOrTableName">Configuration or table name</param>
    /// <param name="onSqlCommand">SQL command callback</param>
    /// <param name="onIgnoredError">Error callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created persister instance</returns>
    Task<IPostgresPersister> CreatePostgresPersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory implementation for creating PostgreSQL persister instances
/// </summary>
public class PostgresPersisterFactory : IPostgresPersisterFactory
{
    private readonly TinyBaseDbContext _context;
    private readonly string _connectionString;

    public PostgresPersisterFactory(TinyBaseDbContext context, string connectionString)
    {
        _context = context;
        _connectionString = connectionString;
    }

    public async Task<IPostgresPersister> CreatePostgresPersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default)
    {
        // Parse configuration
        var config = await ParseConfigurationAsync(storeId, configOrTableName, cancellationToken);
        
        // Create database context unit of work
        var unitOfWork = new UnitOfWork(_context);
        
        // Create PostgreSQL connection
        var connection = new NpgsqlConnection(_connectionString);
        
        // Configure callbacks
        var persisterConfig = new PostgresPersisterConfig
        {
            StoreTableName = config.StoreTableName,
            ManagedTableNames = config.ManagedTableNames,
            IsJson = config.IsJson,
            TableConfigs = config.TableConfigs,
            OnSqlCommand = onSqlCommand,
            OnIgnoredError = onIgnoredError
        };

        // Create persister
        return new PostgresPersister(unitOfWork, connection, persisterConfig);
    }

    private async Task<PersisterConfig> ParseConfigurationAsync(
        string storeId, 
        string? configOrTableName, 
        CancellationToken cancellationToken)
    {
        var config = new PersisterConfig();

        if (string.IsNullOrEmpty(configOrTableName))
        {
            // Default configuration
            config.IsJson = false;
            config.ManagedTableNames = new List<string> { $"table_{storeId}" };
        }
        else if (configOrTableName.StartsWith("{"))
        {
            // JSON configuration
            try
            {
                var jsonConfig = JsonSerializer.Deserialize<PostgresPersisterConfig>(configOrTableName);
                if (jsonConfig != null)
                {
                    config.IsJson = jsonConfig.IsJson;
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