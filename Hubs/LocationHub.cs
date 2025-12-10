using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Hubs;

/// <summary>
/// SignalR hub for real-time driver location updates.
/// Passengers connect to receive location updates for their active rides.
/// Admins can monitor all active rides.
/// </summary>
[Authorize]
public sealed class LocationHub : Hub
{
    private readonly ILogger<LocationHub> _logger;
    
    // Group naming conventions:
    // - "ride_{rideId}" - passengers tracking a specific ride
    // - "admin" - dispatchers/admins monitoring all rides
    // - "driver_{driverUid}" - admin tracking a specific driver
    
    public LocationHub(ILogger<LocationHub> logger)
    {
        _logger = logger;
    }
    
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        var role = Context.User?.FindFirst("role")?.Value;
        
        _logger.LogInformation("Client connected: {ConnectionId}, User: {User}, Role: {Role}", 
            Context.ConnectionId, userId, role ?? "none");
        
        // Auto-join admin group if user has admin role
        if (role == "admin" || role == "dispatcher")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
            _logger.LogInformation("User {User} added to admin group", userId);
        }
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        _logger.LogInformation("Client disconnected: {ConnectionId}, User: {User}", 
            Context.ConnectionId, userId);
        
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// Subscribe to location updates for a specific ride.
    /// Passengers call this to start receiving updates for their ride.
    /// </summary>
    public async Task SubscribeToRide(string rideId)
    {
        if (string.IsNullOrWhiteSpace(rideId))
        {
            _logger.LogWarning("SubscribeToRide called with empty rideId");
            return;
        }
        
        var groupName = $"ride_{rideId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Connection {ConnectionId} subscribed to {Group}", 
            Context.ConnectionId, groupName);
        
        // Acknowledge subscription
        await Clients.Caller.SendAsync("SubscriptionConfirmed", new { rideId, status = "subscribed" });
    }
    
    /// <summary>
    /// Unsubscribe from location updates for a specific ride.
    /// </summary>
    public async Task UnsubscribeFromRide(string rideId)
    {
        if (string.IsNullOrWhiteSpace(rideId))
            return;
        
        var groupName = $"ride_{rideId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from {Group}", 
            Context.ConnectionId, groupName);
    }
    
    /// <summary>
    /// Subscribe to updates for a specific driver (admin use).
    /// </summary>
    public async Task SubscribeToDriver(string driverUid)
    {
        // Only allow admins to subscribe to individual drivers
        var role = Context.User?.FindFirst("role")?.Value;
        if (role != "admin" && role != "dispatcher")
        {
            _logger.LogWarning("Non-admin attempted to subscribe to driver: {ConnectionId}", 
                Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", new { message = "Unauthorized" });
            return;
        }
        
        if (string.IsNullOrWhiteSpace(driverUid))
            return;
        
        var groupName = $"driver_{driverUid}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Admin connection {ConnectionId} subscribed to driver {DriverUid}", 
            Context.ConnectionId, driverUid);
    }
    
    /// <summary>
    /// Unsubscribe from a specific driver's updates.
    /// </summary>
    public async Task UnsubscribeFromDriver(string driverUid)
    {
        if (string.IsNullOrWhiteSpace(driverUid))
            return;
        
        var groupName = $"driver_{driverUid}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}

/// <summary>
/// Static methods for broadcasting location updates from outside the hub.
/// </summary>
public static class LocationHubExtensions
{
    /// <summary>
    /// Broadcast a location update to all interested parties.
    /// </summary>
    public static async Task BroadcastLocationUpdateAsync(
        this IHubContext<LocationHub> hubContext,
        string rideId,
        string driverUid,
        LocationUpdate update,
        string? driverName = null)
    {
        var payload = new
        {
            rideId,
            driverUid,
            driverName,
            latitude = update.Latitude,
            longitude = update.Longitude,
            heading = update.Heading,
            speed = update.Speed,
            accuracy = update.Accuracy,
            timestamp = update.Timestamp
        };
        
        // Send to passengers tracking this specific ride
        await hubContext.Clients.Group($"ride_{rideId}").SendAsync("LocationUpdate", payload);
        
        // Send to admins tracking this specific driver
        await hubContext.Clients.Group($"driver_{driverUid}").SendAsync("LocationUpdate", payload);
        
        // Send to all admins
        await hubContext.Clients.Group("admin").SendAsync("LocationUpdate", payload);
    }
    
    /// <summary>
    /// Notify clients that tracking has stopped for a ride.
    /// </summary>
    public static async Task NotifyTrackingStoppedAsync(
        this IHubContext<LocationHub> hubContext,
        string rideId,
        string reason)
    {
        var payload = new { rideId, reason, timestamp = DateTime.UtcNow };
        
        await hubContext.Clients.Group($"ride_{rideId}").SendAsync("TrackingStopped", payload);
        await hubContext.Clients.Group("admin").SendAsync("TrackingStopped", payload);
    }
}
