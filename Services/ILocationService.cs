using System.Collections.Concurrent;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// In-memory storage for driver location updates with automatic expiration.
/// Designed to be easily swappable with distributed cache (e.g., Redis) for scaled deployments.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Store or update location for a ride. Enforces rate limiting.
    /// </summary>
    /// <param name="driverUid">The driver's unique identifier from AuthServer</param>
    /// <param name="update">The location update data</param>
    /// <returns>True if update was accepted, false if rate limited</returns>
    bool TryUpdateLocation(string driverUid, LocationUpdate update);

    /// <summary>
    /// Get the most recent location for a ride (if not expired).
    /// </summary>
    LocationUpdate? GetLatestLocation(string rideId);
    
    /// <summary>
    /// Get all active ride locations (for admin dashboard).
    /// Returns locations that haven't expired.
    /// </summary>
    IReadOnlyList<LocationEntry> GetAllActiveLocations();
    
    /// <summary>
    /// Get location entries for multiple rides at once.
    /// </summary>
    IReadOnlyList<LocationEntry> GetLocations(IEnumerable<string> rideIds);
    
    /// <summary>
    /// Remove location data for a ride (e.g., when ride completes/cancels).
    /// </summary>
    void RemoveLocation(string rideId);
    
    /// <summary>
    /// Event raised when a location is updated. Used for real-time broadcasting.
    /// </summary>
    event EventHandler<LocationUpdatedEventArgs>? LocationUpdated;
}

/// <summary>
/// Contains location data along with metadata for storage.
/// </summary>
public sealed class LocationEntry
{
    public required LocationUpdate Update { get; init; }
    public required string DriverUid { get; init; }
    public required DateTime StoredAt { get; init; }
    
    public double AgeSeconds => (DateTime.UtcNow - StoredAt).TotalSeconds;
}

/// <summary>
/// Event args for location update events.
/// </summary>
public sealed class LocationUpdatedEventArgs : EventArgs
{
    public required string RideId { get; init; }
    public required string DriverUid { get; init; }
    public required LocationUpdate Update { get; init; }
}

/// <summary>
/// In-memory implementation of ILocationService.
/// Suitable for single-instance deployments.
/// For multi-instance deployments, swap with Redis-backed implementation.
/// </summary>
public sealed class InMemoryLocationService : ILocationService
{
    private readonly ConcurrentDictionary<string, LocationEntry> _locations = new();
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(1);
    private readonly TimeSpan _minUpdateInterval = TimeSpan.FromSeconds(10); // Reduced to support 15s driver updates
    private readonly ILogger<InMemoryLocationService> _logger;
    
    public event EventHandler<LocationUpdatedEventArgs>? LocationUpdated;

    public InMemoryLocationService(ILogger<InMemoryLocationService> logger)
    {
        _logger = logger;
        
        // Start background cleanup task
        _ = Task.Run(CleanupExpiredEntriesAsync);
    }

    public bool TryUpdateLocation(string driverUid, LocationUpdate update)
    {
        var key = update.RideId;
        var now = DateTime.UtcNow;

        // Rate limiting: check if enough time has passed since last update
        if (_locations.TryGetValue(key, out var existing))
        {
            if (now - existing.StoredAt < _minUpdateInterval)
            {
                _logger.LogDebug("Rate limited location update for ride {RideId}", key);
                return false; // Too soon, reject update
            }
        }

        var entry = new LocationEntry
        {
            Update = update,
            DriverUid = driverUid,
            StoredAt = now
        };
        
        _locations[key] = entry;
        
        _logger.LogDebug("Location updated for ride {RideId}: ({Lat}, {Lon})", 
            key, update.Latitude, update.Longitude);
        
        // Raise event for real-time broadcasting
        LocationUpdated?.Invoke(this, new LocationUpdatedEventArgs
        {
            RideId = key,
            DriverUid = driverUid,
            Update = update
        });
        
        return true;
    }

    public LocationUpdate? GetLatestLocation(string rideId)
    {
        if (!_locations.TryGetValue(rideId, out var entry))
            return null;

        // Check if expired
        if (DateTime.UtcNow - entry.StoredAt > _expirationTime)
        {
            _locations.TryRemove(rideId, out _);
            return null;
        }

        return entry.Update;
    }
    
    public IReadOnlyList<LocationEntry> GetAllActiveLocations()
    {
        var now = DateTime.UtcNow;
        var activeLocations = new List<LocationEntry>();
        var expiredKeys = new List<string>();
        
        foreach (var kvp in _locations)
        {
            if (now - kvp.Value.StoredAt > _expirationTime)
            {
                expiredKeys.Add(kvp.Key);
            }
            else
            {
                activeLocations.Add(kvp.Value);
            }
        }
        
        // Clean up expired entries
        foreach (var key in expiredKeys)
        {
            _locations.TryRemove(key, out _);
        }
        
        return activeLocations;
    }
    
    public IReadOnlyList<LocationEntry> GetLocations(IEnumerable<string> rideIds)
    {
        var now = DateTime.UtcNow;
        var results = new List<LocationEntry>();
        
        foreach (var rideId in rideIds)
        {
            if (_locations.TryGetValue(rideId, out var entry))
            {
                if (now - entry.StoredAt <= _expirationTime)
                {
                    results.Add(entry);
                }
            }
        }
        
        return results;
    }
    
    public void RemoveLocation(string rideId)
    {
        if (_locations.TryRemove(rideId, out _))
        {
            _logger.LogDebug("Removed location data for ride {RideId}", rideId);
        }
    }
    
    private async Task CleanupExpiredEntriesAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();
            
            foreach (var kvp in _locations)
            {
                if (now - kvp.Value.StoredAt > _expirationTime)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredKeys)
            {
                _locations.TryRemove(key, out _);
            }
            
            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired location entries", expiredKeys.Count);
            }
        }
    }
}
