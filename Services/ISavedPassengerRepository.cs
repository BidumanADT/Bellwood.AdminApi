using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface ISavedPassengerRepository
{
    Task<IReadOnlyList<SavedPassenger>> ListAsync(string userId, CancellationToken ct = default);
    Task<SavedPassenger?>               GetAsync(string userId, Guid id, CancellationToken ct = default);
    Task<SavedPassenger>                AddAsync(SavedPassenger passenger, CancellationToken ct = default);
    Task<SavedPassenger?>               UpdateAsync(SavedPassenger passenger, CancellationToken ct = default);
    Task<bool>                          DeleteAsync(string userId, Guid id, CancellationToken ct = default);
}
