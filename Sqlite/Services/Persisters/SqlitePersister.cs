using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Data.Repositories;
using TinyBaseSqlitePersister.Models.Configuration;
using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Services.Persisters;

/// <summary>
/// SQLite persister implementation adapted from TypeScript functionality
/// </summary>
public class SqlitePersister : ISqlitePersister
{
    private const string CHANGE_EVENT = "change";
    private const string TINYBASE_PREFIX = "TINYBASE";
    
    private readonly ISqliteUnitOfWork _unitOfWork;
    private readonly SqliteConnection _connection;
    private readonly SqlitePersisterConfig _config;
    private readonly string _configHash;
    private readonly bool _isJson;
    private readonly List<string> _managedTableNames;
    private readonly int _autoLoadIntervalSeconds;
    
    private Timer? _pollingTimer;
    private SqliteVersionInfo? _lastVersionInfo;
    private bool _isListening;
    
    public event EventHandler<SqlitePersisterEventArgs>? DataChanged;
    public event EventHandler<SqlitePersisterEventArgs>? TableCreated;

    public string ConfigHash { get; }
    public bool IsListening => _isListening;

    public SqlitePersister(
        ISqliteUnitOfWork unitOfWork, 
        SqliteConnection connection, 
        SqlitePersisterConfig config)
    {
        _unitOfWork = unitOfWork;
        _connection = connection;
        _config = config;

        // Generate configuration hash
        _configHash = GenerateConfigHash(config);
        
        // Determine persistence mode
        _isJson = config.IsJson;
        _managedTableNames = new List<string>(config.ManagedTableNames);
        _autoLoadIntervalSeconds = config.AutoLoadIntervalSeconds;

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
            await _connection.OpenAsync(cancellationToken);
            
            // Initialize version info
            _lastVersionInfo = await GetVersionInfoAsync(cancellationToken);
            
            // Note: SQLite doesn't have built-in change notifications like PostgreSQL
            // Change events will be detected through polling only
            
            // Start polling timer if interval is set
            if (_autoLoadIntervalSeconds > 0)
            {
                _pollingTimer = new Timer(async _ => await PollForChanges(), null, 
                    TimeSpan.FromSeconds(_autoLoadIntervalSeconds), 
                    TimeSpan.FromSeconds(_autoLoadIntervalSeconds));
            }
            
            _isListening = true;
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
            // Stop polling timer
            _pollingTimer?.Dispose();
            _pollingTimer = null;
            
            // Note: No change event handler to remove for SQLite
            _connection.Close();
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

    private async Task PollForChanges()
    {
        if (!_isListening) return;

        try
        {
            using var command = new SqliteCommand(@"
                SELECT 
                    CAST(user_version AS INTEGER) as data_version,
                    CAST(schema_version AS INTEGER) as schema_version,
                    total_changes() as total_changes
                FROM pragma_user_version, pragma_schema_version", _connection);

            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var currentVersion = new SqliteVersionInfo
                {
                    DataVersion = reader.GetInt32(0),
                    SchemaVersion = reader.GetInt32(1),
                    TotalChanges = reader.GetInt32(2)
                };

                if (HasVersionChanged(currentVersion))
                {
                    var args = new SqlitePersisterEventArgs
                    {
                        Message = "version_change",
                        EventType = "version_change",
                        TableName = "all_tables"
                    };
                    DataChanged?.Invoke(this, args);
                }
                
                _lastVersionInfo = currentVersion;
            }
        }
        catch (Exception ex)
        {
            _config.OnIgnoredError?.Invoke(ex);
        }
    }

    private bool HasVersionChanged(SqliteVersionInfo currentVersion)
    {
        if (_lastVersionInfo == null) return false;
        
        return currentVersion.DataVersion != _lastVersionInfo.DataVersion ||
               currentVersion.SchemaVersion != _lastVersionInfo.SchemaVersion ||
               currentVersion.TotalChanges != _lastVersionInfo.TotalChanges;
    }

    private async Task<SqliteVersionInfo> GetVersionInfoAsync(CancellationToken cancellationToken = default)
    {
        using var command = new SqliteCommand(@"
            SELECT 
                CAST(user_version AS INTEGER) as data_version,
                CAST(schema_version AS INTEGER) as schema_version,
                total_changes() as total_changes
            FROM pragma_user_version, pragma_schema_version", _connection);

        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new SqliteVersionInfo
            {
                DataVersion = reader.GetInt32(0),
                SchemaVersion = reader.GetInt32(1),
                TotalChanges = reader.GetInt32(2)
            };
        }

        return new SqliteVersionInfo { DataVersion = 0, SchemaVersion = 0, TotalChanges = 0 };
    }

    

    private async Task NotifyDataChanged(CancellationToken cancellationToken = default)
    {
        // SQLite doesn't have a NOTIFY equivalent, so we can just trigger the change event
        var args = new SqlitePersisterEventArgs
        {
            Message = "Manual save notification",
            EventType = "manual_save",
            TableName = "all_tables"
        };
        DataChanged?.Invoke(this, args);
    }

    private string GenerateConfigHash(SqlitePersisterConfig config)
    {
        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(configJson));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        _isListening = false;
        _pollingTimer?.Dispose();
        _connection?.Dispose();
    }
}