using Microsoft.EntityFrameworkCore;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Repository implementation for SqliteTable entity operations
/// </summary>
public class SqliteTableRepository : Repository<SqliteTable>, ISqliteTableRepository
{
    public SqliteTableRepository(SqliteDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<SqliteTable>> GetByStoreIdAsync(string storeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(t => t.StoreId == storeId).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SqliteTable>> GetManagedTablesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(t => t.IsManaged).ToListAsync(cancellationToken);
    }

    public async Task<SqliteTable?> GetByNameAndStoreIdAsync(string tableName, string storeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(t => t.Name == tableName && t.StoreId == storeId, cancellationToken);
    }

    public async Task<SqliteTable> UpdateSchemaAsync(string tableId, string schemaJson, CancellationToken cancellationToken = default)
    {
        var table = await _dbSet.FindAsync(new object[] { tableId }, cancellationToken);
        if (table == null)
        {
            throw new InvalidOperationException($"Table with ID '{tableId}' not found.");
        }

        table.Schema = schemaJson;
        table.UpdatedAt = DateTime.UtcNow;
        
        return await UpdateAsync(table, cancellationToken);
    }

    public async Task<bool> ExistsByNameInStoreAsync(string tableName, string storeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(t => t.Name == tableName && t.StoreId == storeId, cancellationToken);
    }
}