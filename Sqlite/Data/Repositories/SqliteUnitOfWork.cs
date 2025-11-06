using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TinyBaseSqlitePersister.Data.Contexts;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Unit of Work implementation to coordinate multiple repository operations
/// </summary>
public class SqliteUnitOfWork : ISqliteUnitOfWork
{
    private readonly SqliteDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    private ISqliteStoreRepository? _stores;
    private ISqliteTableRepository? _tables;
    private ISqliteCellRepository? _cells;

    public SqliteUnitOfWork(SqliteDbContext context)
    {
        _context = context;
    }

    public ISqliteStoreRepository Stores => _stores ??= new SqliteStoreRepository(_context);
    public ISqliteTableRepository Tables => _tables ??= new SqliteTableRepository(_context);
    public ISqliteCellRepository Cells => _cells ??= new SqliteCellRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction?.CommitAsync(cancellationToken)!;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _currentTransaction?.Dispose();
            _context.Dispose();
        }
        _disposed = true;
    }
}