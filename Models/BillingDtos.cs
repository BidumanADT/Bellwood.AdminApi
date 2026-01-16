namespace Bellwood.AdminApi.Models;

/// <summary>
/// Phase 2: Booking detail response DTO with billing fields.
/// Billing fields are masked for dispatchers using UserAuthorizationHelper.MaskBillingFields().
/// </summary>
public class BookingDetailResponseDto
{
    // Core booking information
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string? CurrentRideStatus { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTimeOffset? CreatedDateTimeOffset { get; set; }
    
    // Booking details
    public string BookerName { get; set; } = "";
    public string PassengerName { get; set; } = "";
    public string VehicleClass { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string DropoffLocation { get; set; } = "";
    public DateTime PickupDateTime { get; set; }
    public DateTimeOffset? PickupDateTimeOffset { get; set; }
    
    // Driver assignment
    public string? AssignedDriverId { get; set; }
    public string? AssignedDriverUid { get; set; }
    public string? AssignedDriverName { get; set; }
    
    // Full draft data (for compatibility)
    public object? Draft { get; set; }
    
    // Phase 2: Billing fields (masked for dispatchers, null until payment integration)
    // These fields are placeholders for future payment integration (Phase 3+)
    public string? PaymentMethodId { get; set; }
    public string? PaymentMethodLast4 { get; set; }
    public decimal? PaymentAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? TotalFare { get; set; }
}

/// <summary>
/// Phase 2: Quote detail response DTO with billing fields.
/// Billing fields are masked for dispatchers using UserAuthorizationHelper.MaskBillingFields().
/// </summary>
public class QuoteDetailResponseDto
{
    // Core quote information
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    
    // Quote details
    public string BookerName { get; set; } = "";
    public string PassengerName { get; set; } = "";
    public string VehicleClass { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string DropoffLocation { get; set; } = "";
    public DateTime PickupDateTime { get; set; }
    
    // Full draft data (for compatibility)
    public object? Draft { get; set; }
    
    // Phase 2: Billing fields (masked for dispatchers, null until payment integration)
    // These fields are placeholders for future payment integration (Phase 3+)
    public decimal? EstimatedCost { get; set; }
    public string? BillingNotes { get; set; }
}
