using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface IAffiliateRepository
{
    Task<List<Affiliate>> GetAllAsync(CancellationToken ct = default);
    Task<Affiliate?> GetByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Affiliate affiliate, CancellationToken ct = default);
    Task UpdateAsync(Affiliate affiliate, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
