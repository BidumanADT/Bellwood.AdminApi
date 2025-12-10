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
    
    /// <summary>
    /// Direction of travel in degrees (0-360, where 0 is North).
    /// </summary>
    public double? Heading { get; set; }
    
    /// <summary>
    /// Current speed in meters per second.
    /// </summary>
    public double? Speed { get; set; }
    
    /// <summary>
    /// Location accuracy in meters.
    /// </summary>
    public double? Accuracy { get; set; }
}

/// <summary>
/// Response DTO for location queries (includes additional computed fields).
/// </summary>
public sealed class LocationResponse
{
    public string RideId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Heading { get; set; }
    public double? Speed { get; set; }
    public double? Accuracy { get; set; }
    
    /// <summary>
    /// Age of the location data in seconds.
    /// </summary>
    public double AgeSeconds { get; set; }
    
    /// <summary>
    /// Driver UID associated with this location (for admin views).
    /// </summary>
    public string? DriverUid { get; set; }
    
    /// <summary>
    /// Driver name for display (for admin views).
    /// </summary>
    public string? DriverName { get; set; }
}

/// <summary>
/// Active ride location info for admin dashboard.
/// </summary>
public sealed class ActiveRideLocationDto
{
    public string RideId { get; set; } = "";
    public string? DriverUid { get; set; }
    public string? DriverName { get; set; }
    public string? PassengerName { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public RideStatus? CurrentStatus { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Heading { get; set; }
    public double? Speed { get; set; }
    public double AgeSeconds { get; set; }
}
