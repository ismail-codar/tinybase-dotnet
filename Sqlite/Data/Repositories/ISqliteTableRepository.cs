using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Repository interface for SqliteTable entity operations
/// </summary>
public interface ISqliteTableRepository : IRepository<SqliteTable>
{
    /// <summary>
    /// Get tables by store ID
    /// </summary>
    Task<IEnumerable<SqliteTable>> GetByStoreIdAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get managed tables
    /// </summary>
    Task<IEnumerable<SqliteTable>> GetManagedTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get table by name and store ID
    /// </summary>
    Task<SqliteTable?> GetByNameAndStoreIdAsync(string tableName, string storeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update table schema information
    /// </summary>
    Task<SqliteTable> UpdateSchemaAsync(string tableId, string schemaJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if table exists by name in store
    /// </summary>
    Task<bool> ExistsByNameInStoreAsync(string tableName, string storeId, CancellationToken cancellationToken = default);
}