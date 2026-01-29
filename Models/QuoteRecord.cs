namespace Bellwood.AdminApi.Models;

// Phase Alpha: Extended quote lifecycle status
public enum QuoteStatus 
{ 
    Pending,        // Initial passenger request (renamed from Submitted)
    InReview,       // Legacy status (deprecated - use Acknowledged)
    Acknowledged,   // Dispatcher acknowledged receipt (Phase Alpha)
    Priced,         // Legacy status (deprecated - use Responded)
    Sent,           // Legacy status (deprecated - use Responded)
    Responded,      // Dispatcher provided price/ETA (Phase Alpha)
    Accepted,       // Passenger accepted quote (Phase Alpha)
    Closed,         // Legacy status (deprecated)
    Cancelled,      // Quote cancelled (Phase Alpha)
    Rejected        // Admin rejected quote
}

public sealed class QuoteRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public QuoteStatus Status { get; set; } = QuoteStatus.Pending;  // Changed default

    // =====================================================================
    // OWNERSHIP & AUDIT FIELDS (Phase 1 - User Data Access Enforcement)
    // =====================================================================
    
    /// <summary>
    /// The user ID (uid claim from JWT) of the user who created this quote.
    /// Nullable for backward compatibility with existing records.
    /// Used to enforce per-user data isolation for bookers/passengers.
    /// </summary>
    public string? CreatedByUserId { get; set; }
    
    /// <summary>
    /// The user ID of the last user who modified this quote.
    /// Populated on updates for audit trail purposes.
    /// </summary>
    public string? ModifiedByUserId { get; set; }
    
    /// <summary>
    /// Timestamp of the last modification to this quote.
    /// Populated on updates for audit trail purposes.
    /// </summary>
    public DateTime? ModifiedOnUtc { get; set; }

    // =====================================================================
    // PHASE ALPHA: QUOTE LIFECYCLE TRACKING FIELDS
    // =====================================================================
    
    /// <summary>
    /// Timestamp when dispatcher acknowledged receipt of quote.
    /// Populated when status changes to Acknowledged.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
    
    /// <summary>
    /// User ID of dispatcher who acknowledged the quote.
    /// Links to AuthServer user identity.
    /// </summary>
    public string? AcknowledgedByUserId { get; set; }
    
    /// <summary>
    /// Timestamp when dispatcher sent price/ETA response.
    /// Populated when status changes to Responded.
    /// </summary>
    public DateTime? RespondedAt { get; set; }
    
    /// <summary>
    /// User ID of dispatcher who sent the response.
    /// Links to AuthServer user identity.
    /// </summary>
    public string? RespondedByUserId { get; set; }
    
    /// <summary>
    /// Estimated price provided by dispatcher (manual entry for alpha test).
    /// Placeholder until LimoAnywhere integration in Phase 3.
    /// </summary>
    public decimal? EstimatedPrice { get; set; }
    
    /// <summary>
    /// Estimated pickup time provided by dispatcher.
    /// May differ from requested pickup time.
    /// </summary>
    public DateTime? EstimatedPickupTime { get; set; }
    
    /// <summary>
    /// Optional notes from dispatcher to passenger.
    /// E.g., "Traffic delay expected", "VIP service confirmed", etc.
    /// </summary>
    public string? Notes { get; set; }

    // minimal fields for list
    public string BookerName { get; set; } = "";
    public string PassengerName { get; set; } = "";
    public string VehicleClass { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string? DropoffLocation { get; set; } 
    public DateTime PickupDateTime { get; set; }

    // full payload for detail view
    public BellwoodGlobal.Mobile.Models.QuoteDraft Draft { get; set; } = new();
}
