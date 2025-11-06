using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TinyBasePostgresPersister.Data.Contexts;
using TinyBasePostgresPersister.Data.Repositories;
using TinyBasePostgresPersister.Models.Configuration;
using TinyBasePostgresPersister.Models.Entities;

namespace TinyBasePostgresPersister.Services.Persisters;

/// <summary>
/// PostgreSQL persister implementation recreating the TypeScript functionality
/// </summary>
public class PostgresPersister : IPostgresPersister
{
    private const string TABLE_CREATED = "c";
    private const string DATA_CHANGED = "d";
    private const string TINYBASE_PREFIX = "TINYBASE";
    private const string EVENT_REGEX = @"^([cd]:)(.+)";

    private readonly IUnitOfWork _unitOfWork;
    private readonly NpgsqlConnection _connection;
    private readonly PostgresPersisterConfig _config;
    private readonly string _channelName;
    private readonly string _configHash;
    private readonly bool _isJson;
    private bool _isListening;
    private readonly List<string> _managedTableNames;
    private readonly List<string> _createdFunctionNames = new();

    public event EventHandler<PersisterEventArgs>? DataChanged;
    public event EventHandler<PersisterEventArgs>? TableCreated;

    public string ConfigHash { get; }
    public bool IsListening => _isListening;

    public PostgresPersister(
        IUnitOfWork unitOfWork, 
        NpgsqlConnection connection, 
        PostgresPersisterConfig config)
    {
        _unitOfWork = unitOfWork;
        _connection = connection;
        _config = config;

        // Generate configuration hash
        _configHash = GenerateConfigHash(config);
        _channelName = $"{TINYBASE_PREFIX}_{_configHash}";

        // Determine persistence mode
        _isJson = config.IsJson;
        _managedTableNames = new List<string>(config.ManagedTableNames);

        ConfigHash = _configHash;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadStoreDataAsync(cancellationToken);
        await LoadTableDataAsync(cancellationToken);
        await LoadCellDataAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyDataChanged(cancellationToken);
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening) return;

        try
        {
            // Reserve a command connection
            await _connection.OpenAsync(cancellationToken);
            
            // Create notification channel
            var command = new NpgsqlCommand($"LISTEN {_channelName}", _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            // Set up notification handler
            _connection.Notification += OnNotificationReceived;
            _isListening = true;

            // Set up database triggers
            await SetupDatabaseTriggersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _config.OnIgnoredError?.Invoke(ex);
            throw;
        }
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        if (!_isListening) return;

        try
        {
            _connection.Notification -= OnNotificationReceived;
            
            // Remove database triggers
            await RemoveDatabaseTriggersAsync(cancellationToken);
            
            // Stop listening
            var command = new NpgsqlCommand($"UNLISTEN {_channelName}", _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            await _connection.CloseAsync();
            _isListening = false;
        }
        catch (Exception ex)
        {
            _config.OnIgnoredError?.Invoke(ex);
        }
    }

    private async Task LoadStoreDataAsync(CancellationToken cancellationToken = default)
    {
        // Load store data from the database
        var stores = await _unitOfWork.Stores.GetAllAsync(cancellationToken);
        // TODO: Load data into the store (implementation depends on store structure)
    }

    private async Task LoadTableDataAsync(CancellationToken cancellationToken = default)
    {
        // Load table data from the database
        var tables = await _unitOfWork.Tables.GetAllAsync(cancellationToken);
        // TODO: Load data into the tables (implementation depends on store structure)
    }

    private async Task LoadCellDataAsync(CancellationToken cancellationToken = default)
    {
        // Load cell data from the database
        var cells = await _unitOfWork.Cells.GetAllAsync(cancellationToken);
        // TODO: Load data into the cells (implementation depends on store structure)
    }

    private async Task SetupDatabaseTriggersAsync(CancellationToken cancellationToken = default)
    {
        // Create functions for table creation events
        var tableCreatedFunctionName = await CreateTableCreatedFunctionAsync(cancellationToken);
        _createdFunctionNames.Add(tableCreatedFunctionName);

        // Create trigger for DDL events
        await CreateTableCreatedTriggerAsync(tableCreatedFunctionName, cancellationToken);

        // Create data change function
        var dataChangedFunctionName = await CreateDataChangedFunctionAsync(cancellationToken);
        _createdFunctionNames.Add(dataChangedFunctionName);

        // Create triggers for data changes on managed tables
        foreach (var tableName in _managedTableNames)
        {
            await EnsureTableExistsAsync(tableName, cancellationToken);
            await CreateDataChangedTriggersAsync(tableName, dataChangedFunctionName, cancellationToken);
        }
    }

    private async Task RemoveDatabaseTriggersAsync(CancellationToken cancellationToken = default)
    {
        // Drop all created functions
        if (_createdFunctionNames.Any())
        {
            var functionList = string.Join(",", _createdFunctionNames);
            var command = new NpgsqlCommand($"DROP FUNCTION IF EXISTS {functionList} CASCADE", _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _createdFunctionNames.Clear();
        }
    }

    private async Task<string> CreateTableCreatedFunctionAsync(CancellationToken cancellationToken = default)
    {
        var functionName = EscapeIdentifier($"{TINYBASE_PREFIX}_{TABLE_CREATED}_{_configHash}");
        var sql = $@"
            CREATE OR REPLACE FUNCTION {functionName}()
            RETURNS event_trigger AS $$
            DECLARE
                row record;
            BEGIN
                FOR row IN SELECT object_identity FROM pg_event_trigger_ddl_commands() 
                WHERE command_tag = 'CREATE TABLE' LOOP 
                    PERFORM pg_notify('{_channelName}', 'c:' || SPLIT_PART(row.object_identity, '.', 2));
                END LOOP;
            END;
            $$ LANGUAGE plpgsql;";

        var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return functionName;
    }

    private async Task CreateTableCreatedTriggerAsync(string functionName, CancellationToken cancellationToken = default)
    {
        var triggerName = EscapeIdentifier($"{TINYBASE_PREFIX}_{TABLE_CREATED}_{_configHash}");
        var sql = $@"
            CREATE EVENT TRIGGER {triggerName}
            ON ddl_command_end
            WHEN TAG IN ('CREATE_TABLE')
            EXECUTE FUNCTION {functionName}();";

        var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string> CreateDataChangedFunctionAsync(CancellationToken cancellationToken = default)
    {
        var functionName = EscapeIdentifier($"{TINYBASE_PREFIX}_{DATA_CHANGED}_{_configHash}");
        var sql = $@"
            CREATE OR REPLACE FUNCTION {functionName}()
            RETURNS trigger AS $$
            BEGIN
                PERFORM pg_notify('{_channelName}', 'd:' || TG_TABLE_NAME);
                RETURN NULL;
            END;
            $$ LANGUAGE plpgsql;";

        var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return functionName;
    }

    private async Task CreateDataChangedTriggersAsync(string tableName, string functionName, CancellationToken cancellationToken = default)
    {
        var escapedTableName = EscapeIdentifier(tableName);
        
        // Create triggers for INSERT, UPDATE, DELETE
        var operations = new[] { "INSERT", "UPDATE", "DELETE" };
        
        foreach (var operation in operations)
        {
            var triggerName = EscapeIdentifier($"{TINYBASE_PREFIX}_{DATA_CHANGED}_{_configHash}_{tableName}_{operation}");
            var sql = $@"
                CREATE OR REPLACE TRIGGER {triggerName}
                AFTER {operation} ON {escapedTableName}
                FOR EACH ROW
                EXECUTE FUNCTION {functionName}();";

            var command = new NpgsqlCommand(sql, _connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureTableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (_isJson) return; // JSON mode doesn't create table structures

        var escapedTableName = EscapeIdentifier(tableName);
        var sql = $@"CREATE TABLE IF NOT EXISTS {escapedTableName} (""_id"" text PRIMARY KEY)";
        
        var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task NotifyDataChanged(CancellationToken cancellationToken = default)
    {
        var message = $"'{_channelName}', 'd:notification'";
        var sql = $"SELECT pg_notify({message})";
        
        var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void OnNotificationReceived(object? sender, NpgsqlNotificationEventArgs e)
    {
        try
        {
            var message = e.Payload;
            if (message.StartsWith($"{TABLE_CREATED}:"))
            {
                var tableName = message.Substring(2);
                var args = new PersisterEventArgs
                {
                    Channel = e.Channel,
                    Message = message,
                    EventType = TABLE_CREATED,
                    TableName = tableName
                };
                TableCreated?.Invoke(this, args);
            }
            else if (message.StartsWith($"{DATA_CHANGED}:"))
            {
                var tableName = message.Substring(2);
                var args = new PersisterEventArgs
                {
                    Channel = e.Channel,
                    Message = message,
                    EventType = DATA_CHANGED,
                    TableName = tableName
                };
                DataChanged?.Invoke(this, args);
            }
        }
        catch (Exception ex)
        {
            _config.OnIgnoredError?.Invoke(ex);
        }
    }

    private string GenerateConfigHash(PostgresPersisterConfig config)
    {
        var configJson = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(configJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private string EscapeIdentifier(string identifier)
    {
        // Simple identifier escaping - in production, use proper PostgreSQL escaping
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    public void Dispose()
    {
        _isListening = false;
        _connection?.Dispose();
    }
}