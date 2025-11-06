using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TinyBasePostgresPersister.Data.Contexts;

namespace TinyBasePostgresPersister.Data.Repositories;

/// <summary>
/// Unit of Work implementation to coordinate multiple repository operations
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly TinyBaseDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    private IStoreRepository? _stores;
    private ITableRepository? _tables;
    private ICellRepository? _cells;

    public UnitOfWork(TinyBaseDbContext context)
    {
        _context = context;
    }

    public IStoreRepository Stores => _stores ??= new StoreRepository(_context);
    public ITableRepository Tables => _tables ??= new TableRepository(_context);
    public ICellRepository Cells => _cells ??= new CellRepository(_context);

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