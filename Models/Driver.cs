namespace Bellwood.AdminApi.Models;

/// <summary>
/// Individual driver associated with an affiliate.
/// </summary>
public sealed class Driver
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AffiliateId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>
    /// Optional: Link to AuthServer identity for driver app login.
    /// </summary>
    public string? UserUid { get; set; }
}
