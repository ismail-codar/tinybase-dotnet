using TinyBasePostgresPersister.Models.Entities;

namespace TinyBasePostgresPersister.Data.Repositories;

/// <summary>
/// Repository interface for Table entity operations
/// </summary>
public interface ITableRepository : IRepository<Table>
{
    /// <summary>
    /// Get tables by store ID
    /// </summary>
    Task<IEnumerable<Table>> GetByStoreIdAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get managed tables
    /// </summary>
    Task<IEnumerable<Table>> GetManagedTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get table by name and store ID
    /// </summary>
    Task<Table?> GetByNameAndStoreIdAsync(string tableName, string storeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get table schema information
    /// </summary>
    Task<Table> UpdateSchemaAsync(string tableId, string schemaJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if table exists by name in store
    /// </summary>
    Task<bool> ExistsByNameInStoreAsync(string tableName, string storeId, CancellationToken cancellationToken = default);
}