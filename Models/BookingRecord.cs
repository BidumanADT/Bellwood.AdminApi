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

    // =====================================================================
    // PHASE ALPHA: QUOTE-TO-BOOKING LINK
    // =====================================================================
    
    /// <summary>
    /// ID of the quote that originated this booking.
    /// Populated when passenger accepts a quote (POST /quotes/{id}/accept).
    /// Null for direct bookings (not created from quotes).
    /// </summary>
    public string? SourceQuoteId { get; set; }

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

    // =====================================================================
    // PAYMENT & BILLING FIELDS (Phase 3C - Sensitive Data Protection)
    // =====================================================================
    
    /// <summary>
    /// Stripe payment method ID (encrypted).
    /// Example: "pm_1234567890abcdef" (encrypted with Data Protection API)
    /// Phase 3+: Populated when payment is processed.
    /// </summary>
    public string? PaymentMethodIdEncrypted { get; set; }
    
    /// <summary>
    /// Last 4 digits of payment method (unencrypted for display).
    /// Example: "4242" for Visa ending in 4242
    /// Phase 3+: Populated when payment is processed.
    /// </summary>
    public string? PaymentMethodLast4 { get; set; }
    
    /// <summary>
    /// Payment method type (unencrypted).
    /// Example: "card", "bank_account"
    /// Phase 3+: Populated when payment is processed.
    /// </summary>
    public string? PaymentMethodType { get; set; }
    
    /// <summary>
    /// Total amount charged in cents (unencrypted).
    /// Example: 12500 = $125.00
    /// Phase 3+: Populated when payment is processed.
    /// </summary>
    public int? TotalAmountCents { get; set; }
    
    /// <summary>
    /// Total fare/price in cents (unencrypted).
    /// Example: 10000 = $100.00 (before fees/taxes)
    /// Phase 3+: Populated when pricing is calculated.
    /// </summary>
    public int? TotalFareCents { get; set; }
    
    /// <summary>
    /// Currency code (ISO 4217).
    /// Example: "USD", "CAD", "EUR"
    /// Phase 3+: Populated when payment is processed.
    /// </summary>
    public string? CurrencyCode { get; set; }
    
    /// <summary>
    /// Billing notes (encrypted if contains sensitive info).
    /// Example: Invoice number, special billing instructions
    /// Phase 3+: Optional field for internal use.
    /// </summary>
    public string? BillingNotesEncrypted { get; set; }
}