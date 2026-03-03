using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface ISavedLocationRepository
{
    Task<IReadOnlyList<SavedLocation>> ListAsync(string userId, CancellationToken ct = default);
    Task<SavedLocation?>               GetAsync(string userId, Guid id, CancellationToken ct = default);
    Task<SavedLocation>                AddAsync(SavedLocation location, CancellationToken ct = default);
    Task<SavedLocation?>               UpdateAsync(SavedLocation location, CancellationToken ct = default);
    Task<bool>                         DeleteAsync(string userId, Guid id, CancellationToken ct = default);
}
