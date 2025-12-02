namespace Bellwood.AdminApi.Models;

/// <summary>
/// Request to assign a driver to a booking.
/// </summary>
public sealed class DriverAssignmentRequest
{
    public string DriverId { get; set; } = "";
}
