using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TinyBaseSqlitePersister.Data.Contexts;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Unit of Work pattern to coordinate multiple repository operations
/// </summary>
public interface ISqliteUnitOfWork : IDisposable
{
    ISqliteStoreRepository Stores { get; }
    ISqliteTableRepository Tables { get; }
    ISqliteCellRepository Cells { get; }

    /// <summary>
    /// Save all changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a new transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}