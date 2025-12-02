using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface IDriverRepository
{
    Task<List<Driver>> GetByAffiliateIdAsync(string affiliateId, CancellationToken ct = default);
    Task<Driver?> GetByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Driver driver, CancellationToken ct = default);
    Task UpdateAsync(Driver driver, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
