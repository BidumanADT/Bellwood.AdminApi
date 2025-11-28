using System.Collections.Concurrent;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// In-memory storage for driver location updates with automatic expiration.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Store or update location for a ride. Enforces rate limiting.
    /// </summary>
    bool TryUpdateLocation(string driverUid, LocationUpdate update);

    /// <summary>
    /// Get the most recent location for a ride (if not expired).
    /// </summary>
    LocationUpdate? GetLatestLocation(string rideId);
}

public sealed class InMemoryLocationService : ILocationService
{
    private readonly ConcurrentDictionary<string, (LocationUpdate Update, DateTime LastUpdateTime)> _locations = new();
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(1);
    private readonly TimeSpan _minUpdateInterval = TimeSpan.FromSeconds(15);

    public bool TryUpdateLocation(string driverUid, LocationUpdate update)
    {
        var key = update.RideId;
        var now = DateTime.UtcNow;

        // Rate limiting: check if enough time has passed since last update
        if (_locations.TryGetValue(key, out var existing))
        {
            if (now - existing.LastUpdateTime < _minUpdateInterval)
                return false; // Too soon, reject update
        }

        _locations[key] = (update, now);
        return true;
    }

    public LocationUpdate? GetLatestLocation(string rideId)
    {
        if (!_locations.TryGetValue(rideId, out var entry))
            return null;

        // Check if expired
        if (DateTime.UtcNow - entry.LastUpdateTime > _expirationTime)
        {
            _locations.TryRemove(rideId, out _);
            return null;
        }

        return entry.Update;
    }
}
