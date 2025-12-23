using Microsoft.AspNetCore.SignalR;
using Bellwood.AdminApi.Hubs;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Background service that bridges location updates from ILocationService to SignalR hub.
/// This decouples the location storage from the real-time broadcasting.
/// </summary>
public sealed class LocationBroadcastService : BackgroundService
{
    private readonly ILocationService _locationService;
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly IBookingRepository _bookingRepo;
    private readonly ILogger<LocationBroadcastService> _logger;
    
    // Cache driver names to avoid repeated lookups
    private readonly Dictionary<string, string> _driverNameCache = new();
    private readonly object _cacheLock = new();

    public LocationBroadcastService(
        ILocationService locationService,
        IHubContext<LocationHub> hubContext,
        IBookingRepository bookingRepo,
        ILogger<LocationBroadcastService> logger)
    {
        _locationService = locationService;
        _hubContext = hubContext;
        _bookingRepo = bookingRepo;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to location updates
        _locationService.LocationUpdated += OnLocationUpdated;
        
        _logger.LogInformation("LocationBroadcastService started - listening for location updates");
        
        // Keep the service running
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _locationService.LocationUpdated -= OnLocationUpdated;
        _logger.LogInformation("LocationBroadcastService stopped");
        return base.StopAsync(cancellationToken);
    }

    private async void OnLocationUpdated(object? sender, LocationUpdatedEventArgs e)
    {
        try
        {
            // Get driver name from cache or lookup
            var driverName = await GetDriverNameAsync(e.RideId, e.DriverUid);
            
            // Broadcast via SignalR
            await _hubContext.BroadcastLocationUpdateAsync(
                e.RideId,
                e.DriverUid,
                e.Update,
                driverName);
            
            _logger.LogDebug("Broadcasted location update for ride {RideId} via SignalR", e.RideId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast location update for ride {RideId}", e.RideId);
        }
    }

    private async Task<string?> GetDriverNameAsync(string rideId, string driverUid)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_driverNameCache.TryGetValue(driverUid, out var cachedName))
                return cachedName;
        }
        
        // Lookup from booking
        try
        {
            var booking = await _bookingRepo.GetAsync(rideId);
            if (booking?.AssignedDriverName != null)
            {
                lock (_cacheLock)
                {
                    _driverNameCache[driverUid] = booking.AssignedDriverName;
                }
                return booking.AssignedDriverName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to lookup driver name for ride {RideId}", rideId);
        }
        
        return null;
    }
}
