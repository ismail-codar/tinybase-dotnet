using Microsoft.EntityFrameworkCore;
using TinyBasePostgresPersister.Data.Contexts;
using TinyBasePostgresPersister.Models.Entities;

namespace TinyBasePostgresPersister.Data.Repositories;

/// <summary>
/// Repository implementation for Table entity operations
/// </summary>
public class TableRepository : Repository<Table>, ITableRepository
{
    public TableRepository(TinyBaseDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Table>> GetByStoreIdAsync(string storeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(t => t.StoreId == storeId).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Table>> GetManagedTablesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(t => t.IsManaged).ToListAsync(cancellationToken);
    }

    public async Task<Table?> GetByNameAndStoreIdAsync(string tableName, string storeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(t => t.Name == tableName && t.StoreId == storeId, cancellationToken);
    }

    public async Task<Table> UpdateSchemaAsync(string tableId, string schemaJson, CancellationToken cancellationToken = default)
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