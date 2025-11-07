namespace Bellwood.AdminApi.Models;

/// <summary>
/// Booking status workflow (different from quotes).
/// </summary>
public enum BookingStatus
{
    Requested = 0,   // Initial state from mobile app
    Confirmed = 1,   // Bellwood staff approved
    Scheduled = 2,   // Driver/vehicle assigned
    InProgress = 3,  // Ride started
    Completed = 4,   // Ride finished
    Cancelled = 5,   // User or staff cancelled
    NoShow = 6       // Passenger didn't show up
}

/// <summary>
/// Server-side booking record with flattened fields for list views + full draft.
/// Mirrors QuoteRecord structure.
/// </summary>
public sealed class BookingRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public BookingStatus Status { get; set; } = BookingStatus.Requested;

    // Flattened fields for list views
    public string BookerName { get; set; } = "";
    public string PassengerName { get; set; } = "";
    public string VehicleClass { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string? DropoffLocation { get; set; }
    public DateTime PickupDateTime { get; set; }

    // Full payload for detail view
    public BellwoodGlobal.Mobile.Models.QuoteDraft Draft { get; set; } = new();
}