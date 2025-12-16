# Real-Time Driver Tracking Backend Implementation Summary

## Overview

This document summarizes the backend implementation for real-time driver tracking in the Bellwood AdminAPI. The changes enable passengers and administrators to receive live driver location updates via SignalR WebSockets, with fallback support for HTTP polling. The implementation is designed for single-instance deployment but structured for easy migration to distributed caching (e.g., Redis) when scaling.

## Files Created

### 1. `Hubs/LocationHub.cs`
**New: SignalR Hub for real-time location updates**

A SignalR hub that manages client connections and group subscriptions for real-time location broadcasting.

Key features:
- **Group-based routing**: Passengers subscribe to `ride_{rideId}` groups, admins auto-join `admin` group
- **JWT authentication**: Uses the same JWT tokens as REST endpoints via query string
- **Role-based access**: Admin-only methods for tracking individual drivers

Hub methods:
| Method | Description | Access |
|--------|-------------|--------|
| `SubscribeToRide(rideId)` | Subscribe to updates for a specific ride | All authenticated |
| `UnsubscribeFromRide(rideId)` | Stop receiving updates for a ride | All authenticated |
| `SubscribeToDriver(driverUid)` | Track a specific driver | Admin/Dispatcher only |
| `UnsubscribeFromDriver(driverUid)` | Stop tracking a driver | Admin/Dispatcher only |

Client events received:
- `LocationUpdate` - New location data with coordinates, heading, speed
- `TrackingStopped` - Notification when ride completes/cancels
- `SubscriptionConfirmed` - Acknowledgment of successful subscription

### 2. `Services/LocationBroadcastService.cs`
**New: Background service bridging location storage to SignalR**

A hosted service that listens to `ILocationService.LocationUpdated` events and broadcasts them via SignalR to all subscribed clients.

Features:
- Decouples storage from broadcasting for better reliability
- Caches driver names to reduce database lookups
- Broadcasts to ride groups, driver groups, and admin group simultaneously

## Files Modified

### 3. `Models/DriverDtos.cs`
**Enhancement: Extended LocationUpdate model with rich GPS data**

New properties added to `LocationUpdate`:
| Property | Type | Purpose |
|----------|------|---------|
| `Heading` | double? | Direction of travel (0-360°, 0 = North) |
| `Speed` | double? | Current speed in meters/second |
| `Accuracy` | double? | Location accuracy in meters |

New DTOs added:

**`LocationResponse`** - For GET endpoint responses:
- All LocationUpdate fields plus `AgeSeconds`, `DriverUid`, `DriverName`

**`ActiveRideLocationDto`** - For admin dashboard views:
- Full location data plus ride context (passenger name, pickup/dropoff, status)

### 4. `Services/ILocationService.cs`
**Enhancement: Extended interface with batch queries and events**

New interface members:
| Member | Type | Purpose |
|--------|------|---------|
| `GetAllActiveLocations()` | Method | Returns all non-expired locations (admin dashboard) |
| `GetLocations(rideIds)` | Method | Batch query for multiple rides |
| `RemoveLocation(rideId)` | Method | Clean up when ride ends |
| `LocationUpdated` | Event | Fired on every location update for broadcasting |

New types:
- `LocationEntry` - Wraps `LocationUpdate` with metadata (DriverUid, StoredAt)
- `LocationUpdatedEventArgs` - Event args containing update details

Implementation improvements:
- Rate limiting reduced from 15s to 10s (supports driver's 15s proximity mode)
- Background task for periodic expired entry cleanup
- Event-driven architecture for real-time broadcasting
- Logging for debugging and monitoring

### 5. `Program.cs`
**Enhancement: SignalR integration and new admin endpoints**

Service registrations added:
```csharp
builder.Services.AddSignalR(options => 
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddHostedService<LocationBroadcastService>();
```

JWT configuration updated to support SignalR WebSocket authentication:
```csharp
OnMessageReceived = context =>
{
    var accessToken = context.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(accessToken) && 
        path.StartsWithSegments("/hubs/location"))
    {
        context.Token = accessToken;
    }
    return Task.CompletedTask;
}
```

Hub endpoint mapped:
```csharp
app.MapHub<LocationHub>("/hubs/location");
```

New/updated endpoints:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/hubs/location` | WebSocket | SignalR hub for real-time updates |
| `/admin/locations` | GET | All active driver locations |
| `/admin/locations/rides?rideIds=a,b,c` | GET | Batch query specific rides |
| `/driver/location/{rideId}` | GET | Enhanced with heading/speed/accuracy |
| `/driver/location/update` | POST | Now accepts heading/speed/accuracy |
| `/driver/rides/{id}/status` | POST | Now cleans up location & notifies clients on complete/cancel |

## How It Works

### Real-Time Update Flow

```
Driver App (GPS @ 15-30s intervals)
       ?
POST /driver/location/update
{
  "rideId": "abc123",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "timestamp": "2024-01-15T10:30:00Z",
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5
}
       ?
InMemoryLocationService
  - Validates rate limit (10s min interval)
  - Stores in ConcurrentDictionary
  - Raises LocationUpdated event
       ?
LocationBroadcastService (BackgroundService)
  - Receives LocationUpdated event
  - Looks up driver name (cached)
  - Calls SignalR hub context
       ?
SignalR Hub broadcasts to:
  - "ride_{rideId}" group ? Passenger App
  - "driver_{driverUid}" group ? Admin tracking specific driver
  - "admin" group ? All dispatchers/admins
       ?
Clients receive "LocationUpdate" event
```

### Connection Flow (Passenger App)

```
1. Passenger opens ride tracking screen
       ?
2. Connect to WebSocket: /hubs/location?access_token={jwt}
       ?
3. Call hub method: SubscribeToRide("abc123")
       ?
4. Receive "SubscriptionConfirmed" event
       ?
5. Receive "LocationUpdate" events as driver moves
       ?
6. When ride ends, receive "TrackingStopped" event
       ?
7. Disconnect or call UnsubscribeFromRide
```

### Polling Fallback

For clients that can't maintain WebSocket connections:

```
Passenger App (polling @ 15s)
       ?
GET /driver/location/{rideId}
       ?
Returns LocationResponse:
{
  "rideId": "abc123",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5,
  "timestamp": "2024-01-15T10:30:00Z",
  "ageSeconds": 5.2,
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson"
}
```

### Ride Lifecycle Integration

When ride status changes to Completed or Cancelled:
1. Location data is removed from storage
2. SignalR clients receive "TrackingStopped" event with reason
3. Prevents stale data from lingering

## Security

- **Authentication**: All endpoints require valid JWT
- **SignalR Auth**: Token passed via `?access_token=` query parameter
- **Driver Ownership**: Drivers can only update their assigned rides
- **Admin Groups**: Only admin/dispatcher roles can subscribe to individual drivers
- **Rate Limiting**: 10-second minimum between location updates per ride
- **Auto-Expiration**: Location data expires after 1 hour

## Future Extensibility

The implementation is designed for easy extension:

1. **Distributed Cache**: Replace `InMemoryLocationService` with Redis-backed implementation
   - Same `ILocationService` interface
   - Event publishing via Redis Pub/Sub

2. **Azure SignalR Service**: Scale WebSocket connections
   - Add `Microsoft.Azure.SignalR` package
   - Configure connection string
   - No code changes required

3. **ETA Calculations**: Speed data enables server-side ETA refinement
   - Calculate distance to destination
   - Estimate arrival based on current speed

4. **Geofencing**: Infrastructure supports geofence triggers
   - Compare location to predefined zones
   - Trigger notifications on entry/exit

5. **Route History**: Add persistence layer for breadcrumbs
   - Store each location update to database/blob
   - Enable post-ride route visualization

## Configuration

No additional configuration required for basic operation. Optional settings:

| Setting | Default | Purpose |
|---------|---------|---------|
| `Jwt:Key` | fallback | JWT signing key (must match AuthServer) |

## Testing Recommendations

1. **SignalR Connection**: Use SignalR client or browser console to test WebSocket
2. **Rate Limiting**: Verify 10-second minimum is enforced
3. **Event Broadcasting**: Subscribe multiple clients, verify all receive updates
4. **Ride Completion**: Confirm "TrackingStopped" event fires and data cleaned up
5. **Admin Endpoints**: Test batch queries with multiple ride IDs
6. **Token Expiry**: Verify SignalR reconnection when JWT expires
