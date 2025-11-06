using Microsoft.EntityFrameworkCore;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Repository implementation for SqliteStore entity operations
/// </summary>
public class SqliteStoreRepository : Repository<SqliteStore>, ISqliteStoreRepository
{
    public SqliteStoreRepository(SqliteDbContext context) : base(context)
    {
    }

    public async Task<SqliteStore?> GetByConfigHashAsync(string configHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.ConfigHash == configHash, cancellationToken);
    }

    public async Task<IEnumerable<SqliteStore>> GetPersistedStoresAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(s => s.IsPersisted).ToListAsync(cancellationToken);
    }

    public async Task<SqliteStore> UpdateConfigurationAsync(string storeId, string configuration, string configHash, CancellationToken cancellationToken = default)
    {
        var store = await _dbSet.FindAsync(new object[] { storeId }, cancellationToken);
        if (store == null)
        {
            throw new InvalidOperationException($"Store with ID '{storeId}' not found.");
        }

        store.Configuration = configuration;
        store.ConfigHash = configHash;
        store.UpdatedAt = DateTime.UtcNow;
        
        return await UpdateAsync(store, cancellationToken);
    }

    public async Task DeleteStoreWithDataAsync(string storeId, CancellationToken cancellationToken = default)
    {
        // Get store with related data
        var store = await _dbSet
            .Include(s => s.Tables)
                .ThenInclude(t => t.Cells)
            .FirstOrDefaultAsync(s => s.Id == storeId, cancellationToken);

        if (store != null)
        {
            // Delete all cells, tables, and store
            foreach (var table in store.Tables)
            {
                _context.Cells.RemoveRange(table.Cells);
            }
            _context.Tables.RemoveRange(store.Tables);
            await DeleteAsync(store, cancellationToken);
        }
    }
}