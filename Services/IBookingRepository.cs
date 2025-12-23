using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services
{
    public interface IBookingRepository
    {
        Task<BookingRecord> AddAsync(BookingRecord rec, CancellationToken ct = default);
        Task<BookingRecord?> GetAsync(string id, CancellationToken ct = default);
        Task<IReadOnlyList<BookingRecord>> ListAsync(int take = 50, CancellationToken ct = default);
        Task UpdateStatusAsync(string id, BookingStatus status, CancellationToken ct = default);
        Task UpdateDriverAssignmentAsync(string bookingId, string? driverId, string? driverUid, string? driverName, CancellationToken ct = default);
        
        /// <summary>
        /// Update both the ride status (driver-facing) and booking status (public-facing).
        /// This ensures CurrentRideStatus is persisted to storage.
        /// </summary>
        Task UpdateRideStatusAsync(string id, RideStatus rideStatus, BookingStatus bookingStatus, CancellationToken ct = default);
    }
}