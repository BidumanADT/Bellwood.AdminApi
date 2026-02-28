using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Services;

public sealed class BookerProfileService
{
    private readonly IBookerRepository _repo;

    public BookerProfileService(IBookerRepository repo)
    {
        _repo = repo;
    }

    public Task<BookerProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        => _repo.GetByUserIdAsync(userId, ct);

    public Task<IReadOnlyList<BookerProfile>> ListAsync(int take = 100, CancellationToken ct = default)
        => _repo.ListAsync(take, ct);

    public Task<BookerProfile> UpsertAsync(BookerProfile profile, CancellationToken ct = default)
        => _repo.UpsertAsync(profile, ct);

    public async Task<BookerProfile> SyncFromDraftAsync(string userId, Passenger booker, CancellationToken ct = default)
    {
        var profile = new BookerProfile
        {
            UserId = userId,
            FirstName = booker.FirstName.Trim(),
            LastName = booker.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(booker.PhoneNumber) ? null : booker.PhoneNumber.Trim(),
            EmailAddress = string.IsNullOrWhiteSpace(booker.EmailAddress) ? null : booker.EmailAddress.Trim()
        };

        return await _repo.UpsertAsync(profile, ct);
    }
}
