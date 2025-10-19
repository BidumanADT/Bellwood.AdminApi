namespace Bellwood.AdminApi.Models;

public enum QuoteStatus { Submitted, InReview, Priced, Sent, Closed, Rejected }

public sealed class QuoteRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public QuoteStatus Status { get; set; } = QuoteStatus.Submitted;

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
