using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Models;

public sealed class QuoteDetailDto
{
    public string Id { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string Status { get; set; } = "Submitted";
    public QuoteDraft Draft { get; set; } = new();
}
