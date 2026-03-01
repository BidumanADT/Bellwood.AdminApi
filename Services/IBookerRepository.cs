using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface IBookerRepository
{
    Task<BookerProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<BookerProfile>> ListAsync(int take = 100, CancellationToken ct = default);
    Task<BookerProfile> UpsertAsync(BookerProfile profile, CancellationToken ct = default);
}
