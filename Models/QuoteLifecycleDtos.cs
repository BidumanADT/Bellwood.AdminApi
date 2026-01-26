namespace Bellwood.AdminApi.Models;

/// <summary>
/// Phase Alpha: Request DTO for dispatcher responding to a quote.
/// Contains manually entered price estimate and pickup time (placeholder until LimoAnywhere integration).
/// </summary>
public sealed class QuoteResponseRequest
{
    /// <summary>
    /// Estimated price in USD (manual entry for alpha test).
    /// Must be greater than 0.
    /// </summary>
    public decimal EstimatedPrice { get; set; }
    
    /// <summary>
    /// Estimated pickup time (may differ from requested time).
    /// Must be in the future.
    /// </summary>
    public DateTime EstimatedPickupTime { get; set; }
    
    /// <summary>
    /// Optional notes from dispatcher to passenger.
    /// Example: "Traffic expected", "VIP service confirmed", etc.
    /// </summary>
    public string? Notes { get; set; }
}
