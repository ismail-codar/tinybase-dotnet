using TinyBasePostgresPersister.Models.Configuration;

namespace TinyBasePostgresPersister.Services.Persisters;

/// <summary>
/// Event arguments for persister events
/// </summary>
public class PersisterEventArgs : EventArgs
{
    public string? Channel { get; set; }
    public string? Message { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for the PostgreSQL persister
/// </summary>
public class PostgresPersisterConfig
{
    public string? StoreTableName { get; set; }
    public List<string> ManagedTableNames { get; set; } = new();
    public bool IsJson { get; set; }
    public List<TableConfig> TableConfigs { get; set; } = new();
    public Action<string, object[]>? OnSqlCommand { get; set; }
    public Action<Exception>? OnIgnoredError { get; set; }
}

/// <summary>
/// Interface for the PostgreSQL persister
/// </summary>
public interface IPostgresPersister : IDisposable
{
    /// <summary>
    /// Load data from database
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save data to database
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start listening for database changes
    /// </summary>
    Task StartListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop listening for database changes
    /// </summary>
    Task StopListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when data changes
    /// </summary>
    event EventHandler<PersisterEventArgs> DataChanged;

    /// <summary>
    /// Event raised when table is created
    /// </summary>
    event EventHandler<PersisterEventArgs> TableCreated;

    /// <summary>
    /// Configuration hash for the persister
    /// </summary>
    string ConfigHash { get; }

    /// <summary>
    /// Whether the persister is currently listening
    /// </summary>
    bool IsListening { get; }
}