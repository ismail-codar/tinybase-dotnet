using Microsoft.EntityFrameworkCore;
using TinyBaseSqlitePersister.Data.Contexts;
using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Repository implementation for SqliteCell entity operations
/// </summary>
public class SqliteCellRepository : Repository<SqliteCell>, ISqliteCellRepository
{
    public SqliteCellRepository(SqliteDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<SqliteCell>> GetByTableIdAsync(string tableId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(c => c.TableId == tableId).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SqliteCell>> GetByTableAndRowIdAsync(string tableId, string rowId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(c => c.TableId == tableId && c.RowId == rowId).ToListAsync(cancellationToken);
    }

    public async Task<SqliteCell?> GetByTableRowColumnIdAsync(string tableId, string rowId, string columnId, CancellationToken cancellationToken = default)
    {
        var cellId = $"{tableId}_{rowId}_{columnId}";
        return await _dbSet.FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);
    }

    public async Task<SqliteCell> UpsertAsync(string tableId, string rowId, string columnId, string value, CancellationToken cancellationToken = default)
    {
        var cellId = $"{tableId}_{rowId}_{columnId}";
        var existingCell = await _dbSet.FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        if (existingCell != null)
        {
            existingCell.Value = value;
            existingCell.UpdatedAt = DateTime.UtcNow;
            return await UpdateAsync(existingCell, cancellationToken);
        }
        else
        {
            var newCell = new SqliteCell
            {
                TableId = tableId,
                RowId = rowId,
                ColumnId = columnId,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            return await AddAsync(newCell, cancellationToken);
        }
    }

    public async Task DeleteByTableIdAsync(string tableId, CancellationToken cancellationToken = default)
    {
        var cells = await _dbSet.Where(c => c.TableId == tableId).ToListAsync(cancellationToken);
        await DeleteRangeAsync(cells, cancellationToken);
    }

    public async Task DeleteByTableAndRowIdAsync(string tableId, string rowId, CancellationToken cancellationToken = default)
    {
        var cells = await _dbSet.Where(c => c.TableId == tableId && c.RowId == rowId).ToListAsync(cancellationToken);
        await DeleteRangeAsync(cells, cancellationToken);
    }

    public async Task<int> GetCellCountByTableIdAsync(string tableId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(c => c.TableId == tableId, cancellationToken);
    }
}