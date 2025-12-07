using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface IAffiliateRepository
{
    /// <summary>
    /// Get all affiliates.
    /// </summary>
    Task<List<Affiliate>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Get an affiliate by ID.
    /// </summary>
    Task<Affiliate?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Add a new affiliate.
    /// </summary>
    Task AddAsync(Affiliate affiliate, CancellationToken ct = default);

    /// <summary>
    /// Update an existing affiliate.
    /// </summary>
    Task UpdateAsync(Affiliate affiliate, CancellationToken ct = default);

    /// <summary>
    /// Delete an affiliate by ID.
    /// Note: Callers should also delete associated drivers via IDriverRepository.DeleteByAffiliateIdAsync
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
