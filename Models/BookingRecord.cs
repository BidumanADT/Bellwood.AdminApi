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
/// Driver-facing ride status (granular driver actions).
/// </summary>
public enum RideStatus
{
    Scheduled = 0,       // Booking assigned but driver hasn't moved yet
    OnRoute = 1,         // Driver heading to pickup
    Arrived = 2,         // Driver at pickup location
    PassengerOnboard = 3,// Passenger picked up, ride in progress
    Completed = 4,       // Ride finished successfully
    Cancelled = 5        // Ride cancelled
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
    public DateTime? CancelledAt { get; set; }

    // =====================================================================
    // OWNERSHIP & AUDIT FIELDS (Phase 1 - User Data Access Enforcement)
    // =====================================================================
    
    /// <summary>
    /// The user ID (uid claim from JWT) of the user who created this booking.
    /// Nullable for backward compatibility with existing records.
    /// Used to enforce per-user data isolation for bookers/passengers.
    /// </summary>
    public string? CreatedByUserId { get; set; }
    
    /// <summary>
    /// The user ID of the last user who modified this booking.
    /// Populated on updates for audit trail purposes.
    /// </summary>
    public string? ModifiedByUserId { get; set; }
    
    /// <summary>
    /// Timestamp of the last modification to this booking.
    /// Populated on updates for audit trail purposes.
    /// </summary>
    public DateTime? ModifiedOnUtc { get; set; }

    // Driver assignment (both IDs for different purposes)
    public string? AssignedDriverId { get; set; }      // Links to Driver entity
    public string? AssignedDriverUid { get; set; }     // Links to AuthServer UID for driver app
    public string? AssignedDriverName { get; set; }    // Cached display name
    public RideStatus? CurrentRideStatus { get; set; }

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