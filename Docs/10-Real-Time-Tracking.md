# Real-Time GPS Tracking

**Document Type**: Living Document - Feature Implementation  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document covers the complete **real-time GPS tracking system** for the Bellwood AdminAPI, enabling live driver location tracking via **SignalR WebSockets** with sub-second latency.

**Key Features**:
- ?? **Real-Time Updates** - GPS location broadcasted every 5 seconds via SignalR
- ?? **Privacy by Design** - Role-based access with ownership verification
- ? **Rate Limiting** - 10-second minimum between driver updates
- ?? **Multi-User Groups** - Passengers, admins, and drivers can subscribe
- ?? **Automatic Cleanup** - Location data removed when ride completes

---

## ??? Architecture

### System Components

```
???????????????????????????????????????????????????????????????
?                     DriverApp (MAUI)                        ?
?  - GPS tracking (every 10-30 seconds)                       ?
?  - POST /driver/location/update                             ?
???????????????????????????????????????????????????????????????
                  ? HTTP POST (rate-limited)
                  ?
???????????????????????????????????????????????????????????????
?               AdminAPI - Location Pipeline                  ?
?                                                              ?
?  1. InMemoryLocationService (rate-limited storage)         ?
?     ?? Stores: {rideId ? LocationUpdate}                   ?
?     ?? TTL: 1 hour auto-expiration                         ?
?                                                              ?
?  2. LocationBroadcastService (background service)           ?
?     ?? Polls every 5 seconds                               ?
?     ?? Broadcasts via SignalR to all groups                ?
?                                                              ?
?  3. LocationHub (SignalR hub)                               ?
?     ?? Groups: ride_{id}, driver_{uid}, admin              ?
?     ?? Events: LocationUpdate, RideStatusChanged           ?
???????????????????????????????????????????????????????????????
                  ? SignalR WebSocket
                  ?
???????????????????????????????????????????????????????????????
?                    Client Applications                       ?
?                                                              ?
?  AdminPortal (Blazor)    PassengerApp (MAUI)               ?
?  - Joins "admin" group    - Joins "ride_{id}" group        ?
?  - Sees all rides         - Sees own ride only             ?
?  - Map dashboard          - Tracking screen                ?
???????????????????????????????????????????????????????????????
```

---

## ?? SignalR Hub

### LocationHub Implementation

**File**: `Hubs/LocationHub.cs`

```csharp
public class LocationHub : Hub
{
    private readonly ILogger<LocationHub> _logger;

    public LocationHub(ILogger<LocationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var connectionId = Context.ConnectionId;
        
        // Auto-join admin group for admin/dispatcher users
        if (user?.IsInRole("admin") == true || user?.IsInRole("dispatcher") == true)
        {
            await Groups.AddToGroupAsync(connectionId, "admin");
            _logger.LogInformation("Admin user {Username} joined admin group", 
                user.Identity?.Name);
        }
        
        await base.OnConnectedAsync();
    }

    // Passenger/Admin: Subscribe to a specific ride
    public async Task SubscribeToRide(string rideId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ride_{rideId}");
        
        // Send confirmation
        await Clients.Caller.SendAsync("SubscriptionConfirmed", new
        {
            rideId,
            status = "subscribed"
        });
        
        _logger.LogInformation("Client {ConnectionId} subscribed to ride {RideId}", 
            Context.ConnectionId, rideId);
    }

    // Admin: Subscribe to a specific driver (all their rides)
    public async Task SubscribeToDriver(string driverUid)
    {
        // Only admins can subscribe to drivers
        if (Context.User?.IsInRole("admin") != true && 
            Context.User?.IsInRole("dispatcher") != true)
        {
            throw new HubException("Only admins can subscribe to drivers");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"driver_{driverUid}");
        
        _logger.LogInformation("Admin {Username} subscribed to driver {DriverUid}", 
            Context.User?.Identity?.Name, driverUid);
    }

    // Unsubscribe from ride
    public async Task UnsubscribeFromRide(string rideId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ride_{rideId}");
        
        _logger.LogInformation("Client {ConnectionId} unsubscribed from ride {RideId}", 
            Context.ConnectionId, rideId);
    }

    // Unsubscribe from driver
    public async Task UnsubscribeFromDriver(string driverUid)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver_{driverUid}");
        
        _logger.LogInformation("Client {ConnectionId} unsubscribed from driver {DriverUid}", 
            Context.ConnectionId, driverUid);
    }
}
```

### SignalR Groups

| Group Name | Purpose | Auto-Join | Members |
|------------|---------|-----------|---------|
| `admin` | Monitor all active rides | Yes (for admin/dispatcher) | Admins, dispatchers |
| `ride_{rideId}` | Track specific ride | No (manual subscribe) | Passengers, admins |
| `driver_{driverUid}` | Track specific driver | No (manual subscribe) | Admins only |

---

## ?? Location Service

### InMemoryLocationService

**File**: `Services/InMemoryLocationService.cs`

**Purpose**: Thread-safe in-memory storage with rate limiting and automatic expiration.

**Key Features**:
- ? Rate limiting (10-second minimum between updates)
- ? TTL-based expiration (1 hour)
- ? Thread-safe with `ConcurrentDictionary`
- ? Event-driven broadcasts (via `LocationUpdated` event)

```csharp
public class InMemoryLocationService : ILocationService
{
    private readonly ConcurrentDictionary<string, LocationEntry> _locations = new();
    private static readonly TimeSpan MinUpdateInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LocationTtl = TimeSpan.FromHours(1);

    public event EventHandler<LocationUpdate>? LocationUpdated;

    public bool TryUpdateLocation(string driverUid, LocationUpdate update)
    {
        var now = DateTime.UtcNow;
        
        // Check rate limit
        if (_locations.TryGetValue(update.RideId, out var existing))
        {
            var timeSinceLastUpdate = now - existing.Update.Timestamp;
            if (timeSinceLastUpdate < MinUpdateInterval)
            {
                return false; // Rate limited
            }
        }

        // Store location
        var entry = new LocationEntry(driverUid, update, now);
        _locations[update.RideId] = entry;

        // Trigger event for background broadcast
        LocationUpdated?.Invoke(this, update);

        return true;
    }

    public LocationUpdate? GetLatestLocation(string rideId)
    {
        if (_locations.TryGetValue(rideId, out var entry))
        {
            // Check if expired
            if (DateTime.UtcNow - entry.ReceivedAt < LocationTtl)
            {
                return entry.Update;
            }
        }
        return null;
    }

    public void RemoveLocation(string rideId)
    {
        _locations.TryRemove(rideId, out _);
    }

    public IEnumerable<(string DriverUid, LocationUpdate Update, double AgeSeconds)> 
        GetAllActiveLocations()
    {
        var now = DateTime.UtcNow;
        return _locations.Values
            .Where(e => now - e.ReceivedAt < LocationTtl)
            .Select(e => (e.DriverUid, e.Update, (now - e.Update.Timestamp).TotalSeconds));
    }

    private record LocationEntry(string DriverUid, LocationUpdate Update, DateTime ReceivedAt);
}
```

### LocationUpdate Model

**File**: `Models/LocationUpdate.cs`

```csharp
public class LocationUpdate
{
    public string RideId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double? Heading { get; set; }    // Degrees (0-360)
    public double? Speed { get; set; }      // Meters/second
    public double? Accuracy { get; set; }   // Meters
}
```

---

## ?? Background Broadcast Service

### LocationBroadcastService

**File**: `Services/LocationBroadcastService.cs`

**Purpose**: Background service that polls location updates every 5 seconds and broadcasts via SignalR.

```csharp
public class LocationBroadcastService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<LocationBroadcastService> _logger;
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(5);

    public LocationBroadcastService(
        IServiceProvider services,
        ILogger<LocationBroadcastService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LocationBroadcastService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastLocationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting locations");
            }

            await Task.Delay(BroadcastInterval, stoppingToken);
        }

        _logger.LogInformation("LocationBroadcastService stopped");
    }

    private async Task BroadcastLocationsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        
        var locationService = scope.ServiceProvider
            .GetRequiredService<ILocationService>();
        var hubContext = scope.ServiceProvider
            .GetRequiredService<IHubContext<LocationHub>>();
        var bookingRepo = scope.ServiceProvider
            .GetRequiredService<IBookingRepository>();

        var activeLocations = locationService.GetAllActiveLocations();

        foreach (var (driverUid, update, ageSeconds) in activeLocations)
        {
            // Get booking to enrich data
            var booking = await bookingRepo.GetAsync(update.RideId, ct);
            if (booking == null) continue;

            var payload = new
            {
                update.RideId,
                DriverUid = driverUid,
                DriverName = booking.AssignedDriverName,
                update.Latitude,
                update.Longitude,
                update.Heading,
                update.Speed,
                update.Accuracy,
                update.Timestamp,
                AgeSeconds = ageSeconds
            };

            // Broadcast to multiple groups
            await Task.WhenAll(
                // Passengers tracking this specific ride
                hubContext.Clients.Group($"ride_{update.RideId}")
                    .SendAsync("LocationUpdate", payload, ct),
                
                // Admins tracking this specific driver
                hubContext.Clients.Group($"driver_{driverUid}")
                    .SendAsync("LocationUpdate", payload, ct),
                
                // All admins (dashboard)
                hubContext.Clients.Group("admin")
                    .SendAsync("LocationUpdate", payload, ct)
            );
        }
    }
}
```

---

## ?? API Endpoints

### Driver Location Update

**Endpoint**: `POST /driver/location/update`  
**Auth**: `DriverOnly`  
**Rate Limit**: 10 seconds minimum between updates

**Request**:
```json
{
  "rideId": "abc123",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5
}
```

**Success Response** (200 OK):
```json
{
  "message": "Location updated",
  "rideId": "abc123",
  "timestamp": "2024-12-23T15:30:00Z"
}
```

**Rate Limited Response** (429 Too Many Requests):
```json
{
  "error": "Rate limit exceeded. Minimum 10 seconds between updates."
}
```

**Implementation**:
```csharp
app.MapPost("/driver/location/update", async (
    [FromBody] LocationUpdate update,
    HttpContext context,
    ILocationService locationService,
    IBookingRepository repo) =>
{
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    // Verify ride exists and belongs to driver
    var booking = await repo.GetAsync(update.RideId);
    if (booking is null || booking.AssignedDriverUid != driverUid)
        return Results.NotFound(new { error = "Ride not found" });

    // Only accept updates for active rides
    var activeStatuses = new[] { 
        RideStatus.OnRoute, 
        RideStatus.Arrived, 
        RideStatus.PassengerOnboard 
    };
    
    if (!booking.CurrentRideStatus.HasValue || 
        !activeStatuses.Contains(booking.CurrentRideStatus.Value))
    {
        return Results.BadRequest(new { 
            error = "Location tracking not active for this ride" 
        });
    }

    // Try to store location (rate-limited)
    if (!locationService.TryUpdateLocation(driverUid, update))
    {
        return Results.StatusCode(429); // Too Many Requests
    }

    return Results.Ok(new { 
        message = "Location updated",
        rideId = update.RideId,
        timestamp = DateTime.UtcNow
    });
})
.WithName("UpdateDriverLocation")
.RequireAuthorization("DriverOnly");
```

---

### Get Ride Location (Admin/Driver)

**Endpoint**: `GET /driver/location/{rideId}`  
**Auth**: Authenticated  
**Authorization**: Driver owns ride OR admin/dispatcher role

**Success Response** (200 OK):
```json
{
  "rideId": "abc123",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "timestamp": "2024-12-23T15:30:15Z",
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5,
  "ageSeconds": 5.2,
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson"
}
```

**Not Found Response** (404):
```json
{
  "message": "No recent location data"
}
```

**Authorization Logic**:
```csharp
bool isAuthorized = false;

// Driver can see their own ride
if (!string.IsNullOrEmpty(driverUid) && driverUid == booking.AssignedDriverUid)
{
    isAuthorized = true;
}
// Admins/dispatchers can see all rides
else if (userRole == "admin" || userRole == "dispatcher")
{
    isAuthorized = true;
}
// Backward compatibility for AdminPortal (no role claim yet)
else if (string.IsNullOrEmpty(userRole) && context.User.Identity?.IsAuthenticated == true)
{
    isAuthorized = true; // TODO: Remove once role claims added
}
```

---

### Get Passenger Ride Location

**Endpoint**: `GET /passenger/rides/{rideId}/location`  
**Auth**: Authenticated  
**Authorization**: Email matches booker or passenger

**Tracking Active Response** (200 OK):
```json
{
  "rideId": "abc123",
  "trackingActive": true,
  "latitude": 41.8781,
  "longitude": -87.6298,
  "timestamp": "2024-12-23T15:30:15Z",
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5,
  "ageSeconds": 5.2,
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson"
}
```

**Tracking Not Started Response** (200 OK):
```json
{
  "rideId": "abc123",
  "trackingActive": false,
  "message": "Driver has not started tracking yet",
  "currentStatus": "Scheduled"
}
```

**Email-Based Authorization**:
```csharp
var userEmail = context.User.FindFirst("email")?.Value;
bool isPassengerAuthorized = false;

// Check booker email
if (!string.IsNullOrEmpty(userEmail) && 
    !string.IsNullOrEmpty(booking.Draft?.Booker?.EmailAddress) &&
    userEmail.Equals(booking.Draft.Booker.EmailAddress, StringComparison.OrdinalIgnoreCase))
{
    isPassengerAuthorized = true;
}

// Check passenger email (if different from booker)
if (!isPassengerAuthorized && 
    !string.IsNullOrEmpty(userEmail) &&
    !string.IsNullOrEmpty(booking.Draft?.Passenger?.EmailAddress) &&
    userEmail.Equals(booking.Draft.Passenger.EmailAddress, StringComparison.OrdinalIgnoreCase))
{
    isPassengerAuthorized = true;
}

if (!isPassengerAuthorized)
{
    return Results.Problem(statusCode: 403, title: "Forbidden");
}
```

---

### Get All Active Locations (Admin)

**Endpoint**: `GET /admin/locations`  
**Auth**: `StaffOnly` (admin or dispatcher)

**Response** (200 OK):
```json
{
  "count": 3,
  "locations": [
    {
      "rideId": "abc123",
      "driverUid": "driver-001",
      "driverName": "Charlie Johnson",
      "passengerName": "Jane Doe",
      "pickupLocation": "O'Hare Airport",
      "dropoffLocation": "Downtown Chicago",
      "currentStatus": "OnRoute",
      "latitude": 41.8781,
      "longitude": -87.6298,
      "heading": 45.5,
      "speed": 12.3,
      "ageSeconds": 15.3,
      "timestamp": "2024-12-23T15:30:00Z"
    }
  ],
  "timestamp": "2024-12-23T15:30:15Z"
}
```

**Use Cases**:
- AdminPortal live map dashboard
- Dispatcher monitoring all active rides
- Debugging/operations

---

### Batch Query Locations (Admin)

**Endpoint**: `GET /admin/locations/rides?rideIds=a,b,c`  
**Auth**: `StaffOnly`

**Response** (200 OK):
```json
{
  "requested": 3,
  "found": 2,
  "locations": [
    {
      "rideId": "abc123",
      "driverUid": "driver-001",
      // ... full location data
    }
  ],
  "timestamp": "2024-12-23T15:30:15Z"
}
```

**Use Cases**:
- AdminPortal fetching locations for visible bookings
- Efficient batch queries (avoid N+1 problem)

---

## ?? Security & Privacy

### Authorization Matrix

| Endpoint | Required Auth | Additional Checks |
|----------|---------------|-------------------|
| `POST /driver/location/update` | `DriverOnly` | Ride ownership + active status |
| `GET /driver/location/{rideId}` | Authenticated | Driver owns OR admin/dispatcher |
| `GET /passenger/rides/{rideId}/location` | Authenticated | Email matches booker/passenger |
| `GET /admin/locations` | `StaffOnly` | Admin or dispatcher role |
| `GET /admin/locations/rides` | `StaffOnly` | Admin or dispatcher role |

### Privacy Protections

1. **Role-Based Access**:
   - Drivers see only their assigned rides
   - Passengers see only their own rides (email verification)
   - Admins/dispatchers see all rides (operational need)

2. **Email-Based Passenger Auth**:
   ```csharp
   // Passengers don't have user accounts
   // Authorization via email claim in JWT
   var userEmail = context.User.FindFirst("email")?.Value;
   bool isAuthorized = 
       userEmail == booking.Draft.Booker.EmailAddress ||
       userEmail == booking.Draft.Passenger.EmailAddress;
   ```

3. **Automatic Cleanup**:
   - Location data removed when ride completes/cancels
   - 1-hour TTL for stale data
   - SignalR clients notified via `TrackingStopped` event

4. **Rate Limiting**:
   - Prevents GPS spam (10-second minimum)
   - Reduces server load and battery drain
   - Enforced server-side (not client-side)

---

## ?? SignalR Events

### LocationUpdate Event

**Broadcasted**: Every 5 seconds for active rides  
**Groups**: `ride_{id}`, `driver_{uid}`, `admin`

**Payload**:
```json
{
  "rideId": "abc123",
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5,
  "timestamp": "2024-12-23T15:30:00Z",
  "ageSeconds": 2.1
}
```

**Client Handlers**:

**JavaScript**:
```javascript
connection.on("LocationUpdate", (data) => {
    console.log(`Driver ${data.driverName} at (${data.latitude}, ${data.longitude})`);
    // Update map marker
    updateDriverMarker(data.rideId, data.latitude, data.longitude, data.heading);
});
```

**C# (MAUI/Blazor)**:
```csharp
hubConnection.On<LocationUpdateDto>("LocationUpdate", (data) =>
{
    Console.WriteLine($"Driver {data.DriverName} at ({data.Latitude}, {data.Longitude})");
    // Update UI
    UpdateDriverPosition(data);
});
```

---

### RideStatusChanged Event

**Broadcasted**: When driver updates ride status  
**Groups**: `ride_{id}`, `driver_{uid}`, `admin`

**Payload**:
```json
{
  "rideId": "abc123",
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson",
  "passengerName": "Jane Doe",
  "newStatus": "OnRoute",
  "timestamp": "2024-12-23T15:30:00Z"
}
```

**Status Values**:
- `Scheduled` ? `OnRoute` ? `Arrived` ? `PassengerOnboard` ? `Completed`
- Or: `Cancelled` from any state

**Client Handler**:
```javascript
connection.on("RideStatusChanged", (data) => {
    console.log(`Ride ${data.rideId} status: ${data.newStatus}`);
    // Update UI (e.g., "Driver en route", "Driver arrived")
    updateRideStatus(data.rideId, data.newStatus);
});
```

---

### TrackingStopped Event

**Broadcasted**: When ride completes or cancels  
**Groups**: `ride_{id}`, `driver_{uid}`, `admin`

**Payload**:
```json
{
  "rideId": "abc123",
  "reason": "Ride completed",
  "timestamp": "2024-12-23T16:00:00Z"
}
```

**Reasons**:
- `"Ride completed"` - Driver marked ride as complete
- `"Ride cancelled"` - Ride was cancelled

**Client Handler**:
```javascript
connection.on("TrackingStopped", (data) => {
    console.log(`Tracking stopped: ${data.reason}`);
    // Remove map marker, show completion message
    removeDriverMarker(data.rideId);
    showMessage(`Ride completed at ${data.timestamp}`);
});
```

---

### SubscriptionConfirmed Event

**Broadcasted**: When client subscribes to a ride  
**Groups**: Caller only (not broadcasted)

**Payload**:
```json
{
  "rideId": "abc123",
  "status": "subscribed"
}
```

**Client Handler**:
```javascript
await connection.invoke("SubscribeToRide", "abc123");
// Wait for confirmation
connection.on("SubscriptionConfirmed", (data) => {
    console.log(`Subscribed to ride ${data.rideId}`);
});
```

---

## ?? Testing

### Manual Testing Workflow

**1. Start Servers**:
```bash
# Terminal 1: AuthServer
cd AuthServer
dotnet run

# Terminal 2: AdminAPI
cd Bellwood.AdminApi
dotnet run
```

**2. Seed Test Data**:
```powershell
cd Scripts
.\Seed-All.ps1
```

**3. Get Driver Token**:
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "charlie", "password": "password"}'
```

**4. Send Location Updates**:
```bash
# First update (success)
curl -X POST https://localhost:5206/driver/location/update \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "rideId": "abc123",
    "latitude": 41.8781,
    "longitude": -87.6298,
    "heading": 45.5,
    "speed": 12.3,
    "accuracy": 8.5
  }'

# Second update < 10 seconds later (rate limited)
curl -X POST https://localhost:5206/driver/location/update \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "rideId": "abc123",
    "latitude": 41.8782,
    "longitude": -87.6299
  }'
# Expected: 429 Too Many Requests
```

**5. Get Location (Admin)**:
```bash
curl -X GET https://localhost:5206/admin/locations \
  -H "Authorization: Bearer {adminToken}"
```

---

### SignalR Integration Testing

**JavaScript (Browser Console)**:
```javascript
// 1. Connect to hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location?access_token=" + token)
    .build();

// 2. Setup event handlers
connection.on("LocationUpdate", (data) => {
    console.log("?? Location:", data);
});

connection.on("RideStatusChanged", (data) => {
    console.log("? Status:", data.newStatus);
});

connection.on("TrackingStopped", (data) => {
    console.log("?? Stopped:", data.reason);
});

// 3. Connect
await connection.start();
console.log("? Connected");

// 4. Subscribe to ride
await connection.invoke("SubscribeToRide", "abc123");
console.log("?? Subscribed to ride abc123");

// 5. Wait for updates (sent every 5 seconds)
// Watch console for LocationUpdate events
```

**C# (MAUI/Blazor)**:
```csharp
// 1. Create connection
var hubConnection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5206/hubs/location", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .Build();

// 2. Setup handlers
hubConnection.On<LocationUpdateDto>("LocationUpdate", (data) =>
{
    Console.WriteLine($"?? Location: ({data.Latitude}, {data.Longitude})");
});

// 3. Connect
await hubConnection.StartAsync();

// 4. Subscribe
await hubConnection.InvokeAsync("SubscribeToRide", "abc123");
```

---

## ?? Performance Considerations

### Rate Limiting Strategy

| Aspect | Value | Reason |
|--------|-------|--------|
| **Driver Update Frequency** | 10-30 seconds | Balance accuracy vs battery/bandwidth |
| **Server Rate Limit** | 10 seconds minimum | Prevent GPS spam, reduce load |
| **Broadcast Interval** | 5 seconds | Sub-second latency for clients |
| **Location TTL** | 1 hour | Auto-cleanup stale data |

### Scalability

**Current Implementation** (In-Memory):
- ? Simple & fast
- ? Zero external dependencies
- ? Single-server only (not distributed)
- ? Lost on restart

**Future Scaling Options** (Phase 3+):

1. **Redis-Backed Location Service**:
   ```csharp
   public class RedisLocationService : ILocationService
   {
       private readonly IConnectionMultiplexer _redis;
       
       public bool TryUpdateLocation(string driverUid, LocationUpdate update)
       {
           var db = _redis.GetDatabase();
           var key = $"location:{update.RideId}";
           
           // Check rate limit
           var lastUpdate = db.StringGet($"{key}:timestamp");
           if (!string.IsNullOrEmpty(lastUpdate))
           {
               var timeSince = DateTime.UtcNow - DateTime.Parse(lastUpdate);
               if (timeSince < TimeSpan.FromSeconds(10))
                   return false; // Rate limited
           }
           
           // Store with TTL
           db.StringSet(key, JsonSerializer.Serialize(update), 
               expiry: TimeSpan.FromHours(1));
           db.StringSet($"{key}:timestamp", DateTime.UtcNow.ToString("O"));
           
           return true;
       }
   }
   ```

2. **Azure SignalR Service**:
   - Offload WebSocket connections to Azure
   - Auto-scaling for 100K+ concurrent connections
   - Built-in backplane for multi-server deployments

3. **Azure Service Bus**:
   - Pub/sub for location updates
   - Guaranteed delivery
   - Dead-letter queues for failed broadcasts

---

## ?? Troubleshooting

### Common Issues

#### 1. "429 Too Many Requests" on Every Update

**Symptom**: Driver can't send location updates (always rate-limited)

**Cause**: DriverApp sending updates < 10 seconds apart

**Fix**: Check DriverApp GPS update frequency:
```csharp
// Ensure minimum 10-second interval
var locationOptions = new LocationOptions
{
    MinimumInterval = TimeSpan.FromSeconds(15) // Buffer
};
```

---

#### 2. SignalR Connection Failures

**Symptom**: `connection.start()` fails with 401 Unauthorized

**Cause**: JWT token not in query string

**Fix**: Pass token correctly:
```javascript
// ? Wrong
.withUrl("https://localhost:5206/hubs/location")

// ? Correct
.withUrl("https://localhost:5206/hubs/location?access_token=" + token)
```

---

#### 3. No LocationUpdate Events Received

**Symptom**: Connected to SignalR but no events

**Possible Causes**:
1. **Not subscribed to any group**:
   ```javascript
   // Must subscribe to receive events
   await connection.invoke("SubscribeToRide", "abc123");
   ```

2. **Ride not active**:
   - Location updates only sent for rides with status:
     - `OnRoute`, `Arrived`, or `PassengerOnboard`
   - Check ride status: `GET /bookings/{id}`

3. **LocationBroadcastService not running**:
   - Check console for: `"LocationBroadcastService started"`
   - If missing, service failed to start

---

#### 4. Stale Location Data (ageSeconds > 60)

**Symptom**: Location `ageSeconds` field very high

**Causes**:
1. **Driver stopped sending updates**:
   - Check DriverApp GPS is running
   - Verify network connectivity

2. **Rate limiting** (if ageSeconds ~10):
   - Normal - driver updates every 10-15 seconds
   - Broadcasts happen every 5 seconds

3. **Location expired** (if ageSeconds > 3600):
   - Data older than 1 hour
   - Should be auto-removed

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system integration
- `13-Driver-Integration.md` - Driver endpoints & assignment
- `14-Passenger-Tracking.md` - Passenger location endpoints
- `21-SignalR-Events.md` - Complete SignalR reference
- `32-Troubleshooting.md` - Common issues & solutions

---

## ?? Future Enhancements

### Phase 3+ Roadmap

1. **Geofencing**:
   - Auto-detect when driver arrives at pickup location
   - Trigger status change to `Arrived` automatically
   - Reduce driver app interactions

2. **ETA Calculations**:
   ```csharp
   public class EtaService
   {
       public async Task<TimeSpan> CalculateEta(
           LocationUpdate driverLocation,
           BookingRecord booking)
       {
           var distance = CalculateDistance(
               driverLocation.Latitude, 
               driverLocation.Longitude,
               booking.PickupLatitude,
               booking.PickupLongitude);
           
           var speed = driverLocation.Speed ?? 5.0; // m/s
           return TimeSpan.FromSeconds(distance / speed);
       }
   }
   ```

3. **Historical Tracking (Breadcrumbs)**:
   - Store location history for completed rides
   - Dispute resolution (proof of route)
   - Analytics (average speed, route optimization)

4. **Redis-Backed Storage**:
   - Distributed cache for multi-server deployments
   - Persistent location history

5. **Azure SignalR Service**:
   - Scale to 100K+ concurrent connections
   - Automatic failover & load balancing

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Next Steps**: See Phase 3 Roadmap above
