namespace Bellwood.AdminApi.Models;

public enum QuoteStatus { Submitted, InReview, Priced, Sent, Closed, Rejected }

public sealed class QuoteRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public QuoteStatus Status { get; set; } = QuoteStatus.Submitted;

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
