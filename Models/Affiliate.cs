namespace Bellwood.AdminApi.Models;

/// <summary>
/// Affiliate company/organization that provides drivers.
/// </summary>
public sealed class Affiliate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? PointOfContact { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public List<Driver> Drivers { get; set; } = new();
}
