using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Data.Repositories;
using TinyBaseSqlitePersister.Models.Configuration;
using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Services.Persisters;

/// <summary>
/// Event arguments for SQLite persister events
/// </summary>
public class SqlitePersisterEventArgs : EventArgs
{
    public string? Channel { get; set; }
    public string? Message { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

/// <summary>
/// Event arguments for database changes
/// </summary>
public class SqliteChangeEventArgs
{
    public string? Table { get; set; }
    public string? Schema { get; set; }
}

/// <summary>
/// Interface for the SQLite persister
/// </summary>
public interface ISqlitePersister : IDisposable
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
    event EventHandler<SqlitePersisterEventArgs> DataChanged;

    /// <summary>
    /// Event raised when table is created
    /// </summary>
    event EventHandler<SqlitePersisterEventArgs> TableCreated;

    /// <summary>
    /// Configuration hash for the persister
    /// </summary>
    string ConfigHash { get; }

    /// <summary>
    /// Whether the persister is currently listening
    /// </summary>
    bool IsListening { get; }
}