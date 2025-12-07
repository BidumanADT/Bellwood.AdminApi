using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface IDriverRepository
{
    /// <summary>
    /// Get all drivers across all affiliates.
    /// </summary>
    Task<List<Driver>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Get drivers belonging to a specific affiliate.
    /// </summary>
    Task<List<Driver>> GetByAffiliateIdAsync(string affiliateId, CancellationToken ct = default);

    /// <summary>
    /// Get a driver by their unique ID.
    /// </summary>
    Task<Driver?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Get a driver by their AuthServer UserUid (for driver app authentication).
    /// </summary>
    Task<Driver?> GetByUserUidAsync(string userUid, CancellationToken ct = default);

    /// <summary>
    /// Check if a UserUid is unique (not already assigned to another driver).
    /// </summary>
    /// <param name="userUid">The UserUid to check</param>
    /// <param name="excludeDriverId">Optional driver ID to exclude (for updates)</param>
    Task<bool> IsUserUidUniqueAsync(string userUid, string? excludeDriverId = null, CancellationToken ct = default);

    /// <summary>
    /// Add a new driver.
    /// </summary>
    Task AddAsync(Driver driver, CancellationToken ct = default);

    /// <summary>
    /// Update an existing driver.
    /// </summary>
    Task UpdateAsync(Driver driver, CancellationToken ct = default);

    /// <summary>
    /// Delete a driver by ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Delete all drivers belonging to an affiliate (for cascade delete).
    /// </summary>
    Task DeleteByAffiliateIdAsync(string affiliateId, CancellationToken ct = default);
}
