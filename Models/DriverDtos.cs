using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Models;

/// <summary>
/// Minimal ride info for driver's list view.
/// </summary>
public sealed class DriverRideListItemDto
{
    public string Id { get; set; } = "";
    public DateTime PickupDateTime { get; set; }
    public string PickupLocation { get; set; } = "";
    public string? DropoffLocation { get; set; }
    public string PassengerName { get; set; } = "";
    public string PassengerPhone { get; set; } = "";
    public RideStatus Status { get; set; }
}

/// <summary>
/// Detailed ride info for driver's detail view.
/// </summary>
public sealed class DriverRideDetailDto
{
    public string Id { get; set; } = "";
    public DateTime PickupDateTime { get; set; }
    public string PickupLocation { get; set; } = "";
    public string PickupStyle { get; set; } = "Curbside";
    public string? PickupSignText { get; set; }
    public string? DropoffLocation { get; set; }
    public string PassengerName { get; set; } = "";
    public string PassengerPhone { get; set; } = "";
    public int PassengerCount { get; set; }
    public int CheckedBags { get; set; }
    public int CarryOnBags { get; set; }
    public string VehicleClass { get; set; } = "";
    public FlightInfo? OutboundFlight { get; set; }
    public string? AdditionalRequest { get; set; }
    public RideStatus Status { get; set; }
}

/// <summary>
/// Request to update ride status.
/// </summary>
public sealed class RideStatusUpdateRequest
{
    public RideStatus NewStatus { get; set; }
}

/// <summary>
/// Location update from driver.
/// </summary>
public sealed class LocationUpdate
{
    public string RideId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
