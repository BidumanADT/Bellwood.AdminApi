using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Driver repository that stores drivers within their affiliate's JSON.
/// Drivers are nested under affiliates for hierarchical management.
/// </summary>
public sealed class FileDriverRepository : IDriverRepository
{
    private readonly IAffiliateRepository _affiliateRepo;

    public FileDriverRepository(IAffiliateRepository affiliateRepo)
    {
        _affiliateRepo = affiliateRepo;
    }

    public async Task<List<Driver>> GetByAffiliateIdAsync(string affiliateId, CancellationToken ct = default)
    {
        var affiliate = await _affiliateRepo.GetByIdAsync(affiliateId, ct);
        return affiliate?.Drivers ?? new List<Driver>();
    }

    public async Task<Driver?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var allAffiliates = await _affiliateRepo.GetAllAsync(ct);
        foreach (var aff in allAffiliates)
        {
            var driver = aff.Drivers.FirstOrDefault(d => d.Id == id);
            if (driver is not null) return driver;
        }
        return null;
    }

    public async Task AddAsync(Driver driver, CancellationToken ct = default)
    {
        var affiliate = await _affiliateRepo.GetByIdAsync(driver.AffiliateId, ct);
        if (affiliate is null)
            throw new InvalidOperationException($"Affiliate {driver.AffiliateId} not found");

        affiliate.Drivers.Add(driver);
        await _affiliateRepo.UpdateAsync(affiliate, ct);
    }

    public async Task UpdateAsync(Driver driver, CancellationToken ct = default)
    {
        var allAffiliates = await _affiliateRepo.GetAllAsync(ct);
        foreach (var aff in allAffiliates)
        {
            var existing = aff.Drivers.FirstOrDefault(d => d.Id == driver.Id);
            if (existing is not null)
            {
                aff.Drivers.Remove(existing);
                aff.Drivers.Add(driver);
                await _affiliateRepo.UpdateAsync(aff, ct);
                return;
            }
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var allAffiliates = await _affiliateRepo.GetAllAsync(ct);
        foreach (var aff in allAffiliates)
        {
            var toRemove = aff.Drivers.FirstOrDefault(d => d.Id == id);
            if (toRemove is not null)
            {
                aff.Drivers.Remove(toRemove);
                await _affiliateRepo.UpdateAsync(aff, ct);
                return;
            }
        }
    }
}
