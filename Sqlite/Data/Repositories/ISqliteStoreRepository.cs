using TinyBaseSqlitePersister.Models.Entities;

namespace TinyBaseSqlitePersister.Data.Repositories;

/// <summary>
/// Repository interface for SqliteStore entity operations
/// </summary>
public interface ISqliteStoreRepository : IRepository<SqliteStore>
{
    /// <summary>
    /// Get store by config hash
    /// </summary>
    Task<SqliteStore?> GetByConfigHashAsync(string configHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stores that are persisted
    /// </summary>
    Task<IEnumerable<SqliteStore>> GetPersistedStoresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update store configuration
    /// </summary>
    Task<SqliteStore> UpdateConfigurationAsync(string storeId, string configuration, string configHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete store and all its related data
    /// </summary>
    Task DeleteStoreWithDataAsync(string storeId, CancellationToken cancellationToken = default);
}