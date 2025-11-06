using TinyBasePostgresPersister.Models.Entities;

namespace TinyBasePostgresPersister.Data.Repositories;

/// <summary>
/// Repository interface for Cell entity operations
/// </summary>
public interface ICellRepository : IRepository<Cell>
{
    /// <summary>
    /// Get cells by table ID
    /// </summary>
    Task<IEnumerable<Cell>> GetByTableIdAsync(string tableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cells by table and row ID
    /// </summary>
    Task<IEnumerable<Cell>> GetByTableAndRowIdAsync(string tableId, string rowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cell by table, row, and column ID
    /// </summary>
    Task<Cell?> GetByTableRowColumnIdAsync(string tableId, string rowId, string columnId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update or insert cell (upsert operation)
    /// </summary>
    Task<Cell> UpsertAsync(string tableId, string rowId, string columnId, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete cells by table ID
    /// </summary>
    Task DeleteByTableIdAsync(string tableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete cells by table and row ID
    /// </summary>
    Task DeleteByTableAndRowIdAsync(string tableId, string rowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cell count for table
    /// </summary>
    Task<int> GetCellCountByTableIdAsync(string tableId, CancellationToken cancellationToken = default);
}