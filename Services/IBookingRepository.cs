using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services
{
    public interface IBookingRepository
    {
        Task<BookingRecord> AddAsync(BookingRecord rec, CancellationToken ct = default);
        Task<BookingRecord?> GetAsync(string id, CancellationToken ct = default);
        Task<IReadOnlyList<BookingRecord>> ListAsync(int take = 50, CancellationToken ct = default);
        Task UpdateStatusAsync(string id, BookingStatus status, CancellationToken ct = default);
    }
}