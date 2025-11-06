using TinyBasePostgresPersister.Models.Entities;

namespace TinyBasePostgresPersister.Data.Repositories;

/// <summary>
/// Repository interface for Store entity operations
/// </summary>
public interface IStoreRepository : IRepository<Store>
{
    /// <summary>
    /// Get store by config hash
    /// </summary>
    Task<Store?> GetByConfigHashAsync(string configHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get stores that are persisted
    /// </summary>
    Task<IEnumerable<Store>> GetPersistedStoresAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update store configuration
    /// </summary>
    Task<Store> UpdateConfigurationAsync(string storeId, string configuration, string configHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete store and all its related data
    /// </summary>
    Task DeleteStoreWithDataAsync(string storeId, CancellationToken cancellationToken = default);
}