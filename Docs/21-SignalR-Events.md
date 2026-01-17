# SignalR Events & Hub Reference

**Document Type**: Living Document - Technical Reference  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides complete SignalR hub documentation for the Bellwood AdminAPI, including hub methods, events, groups, and client integration examples.

**Hub URL**: `wss://localhost:5206/hubs/location` (Development)  
**Production URL**: TBD

**Authentication**: Bearer JWT tokens (via query string or headers)

---

## ??? Hub Architecture

### LocationHub

**File**: `Hubs/LocationHub.cs`

**Purpose**: Real-time GPS tracking and ride status updates

**Features**:
- Auto-join `admin` group for staff users
- Manual subscription to specific rides (`ride_{id}`)
- Manual subscription to specific drivers (`driver_{uid}`)
- Automatic cleanup on disconnect

---

## ?? Authentication

### Connection with JWT

**JavaScript/TypeScript**:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location?access_token=" + jwtToken)
    .build();

await connection.start();
console.log("? Connected to LocationHub");
```

**C# (MAUI/Blazor)**:
```csharp
var hubConnection = new HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .Build();

await hubConnection.StartAsync();
Console.WriteLine("? Connected to LocationHub");
```

---

## ?? Hub Methods (Client ? Server)

### SubscribeToRide

**Description**: Subscribe to location updates for a specific ride

**Access**: Any authenticated user (passenger, admin, dispatcher)

**Client Invocation**:

**JavaScript**:
```javascript
await connection.invoke("SubscribeToRide", "ride-abc123");
console.log("?? Subscribed to ride-abc123");
```

**C#**:
```csharp
await hubConnection.InvokeAsync("SubscribeToRide", "ride-abc123");
Console.WriteLine("?? Subscribed to ride-abc123");
```

**Server Logic**:
```csharp
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
```

**Result**:
- Client joins `ride_{rideId}` group
- Client receives `SubscriptionConfirmed` event

---

### SubscribeToDriver

**Description**: Subscribe to all rides for a specific driver

**Access**: Admin or dispatcher only

**Client Invocation**:

**JavaScript**:
```javascript
await connection.invoke("SubscribeToDriver", "driver-001");
console.log("?? Subscribed to driver-001");
```

**C#**:
```csharp
await hubConnection.InvokeAsync("SubscribeToDriver", "driver-001");
Console.WriteLine("?? Subscribed to driver-001");
```

**Server Logic**:
```csharp
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
```

**Result**:
- Admin joins `driver_{driverUid}` group
- Receives updates for all rides assigned to this driver

**Error**: Throws `HubException` if user is not admin/dispatcher

---

### UnsubscribeFromRide

**Description**: Unsubscribe from ride updates

**Access**: Any authenticated user

**Client Invocation**:

**JavaScript**:
```javascript
await connection.invoke("UnsubscribeFromRide", "ride-abc123");
console.log("?? Unsubscribed from ride-abc123");
```

**C#**:
```csharp
await hubConnection.InvokeAsync("UnsubscribeFromRide", "ride-abc123");
Console.WriteLine("?? Unsubscribed from ride-abc123");
```

**Server Logic**:
```csharp
public async Task UnsubscribeFromRide(string rideId)
{
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ride_{rideId}");
    
    _logger.LogInformation("Client {ConnectionId} unsubscribed from ride {RideId}", 
        Context.ConnectionId, rideId);
}
```

---

### UnsubscribeFromDriver

**Description**: Unsubscribe from driver updates

**Access**: Admin or dispatcher only

**Client Invocation**:

**JavaScript**:
```javascript
await connection.invoke("UnsubscribeFromDriver", "driver-001");
console.log("?? Unsubscribed from driver-001");
```

**C#**:
```csharp
await hubConnection.InvokeAsync("UnsubscribeFromDriver", "driver-001");
Console.WriteLine("?? Unsubscribed from driver-001");
```

---

## ?? Hub Events (Server ? Client)

### LocationUpdate

**Description**: GPS location update from driver

**Broadcast To**:
- `ride_{rideId}` group (passengers tracking this ride)
- `driver_{driverUid}` group (admins tracking this driver)
- `admin` group (all admins)

**Frequency**: Every 5 seconds (via `LocationBroadcastService`)

**Payload**:
```typescript
interface LocationUpdateEvent {
  rideId: string;
  driverUid: string;
  driverName: string;
  latitude: number;
  longitude: number;
  heading?: number;      // Degrees (0-360)
  speed?: number;        // Meters/second
  accuracy?: number;     // Meters
  timestamp: string;     // ISO 8601 UTC
  ageSeconds: number;    // Age of location data
}
```

**Example Payload**:
```json
{
  "rideId": "ride-abc123",
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

**Client Handler**:

**JavaScript**:
```javascript
connection.on("LocationUpdate", (data) => {
    console.log(`?? Driver ${data.driverName} at (${data.latitude}, ${data.longitude})`);
    
    // Update map marker
    updateDriverMarker(
        data.rideId, 
        data.latitude, 
        data.longitude, 
        data.heading
    );
    
    // Update UI
    document.getElementById('driver-speed').textContent = 
        `${(data.speed * 2.237).toFixed(1)} mph`;
    document.getElementById('location-age').textContent = 
        `${data.ageSeconds.toFixed(0)}s ago`;
});
```

**C#**:
```csharp
hubConnection.On<LocationUpdateDto>("LocationUpdate", (data) =>
{
    Console.WriteLine($"?? Driver {data.DriverName} at ({data.Latitude}, {data.Longitude})");
    
    // Update UI (MAUI/Blazor)
    MainThread.BeginInvokeOnMainThread(() =>
    {
        UpdateDriverPosition(data);
    });
});
```

---

### RideStatusChanged

**Description**: Driver updated ride status

**Broadcast To**:
- `ride_{rideId}` group
- `driver_{driverUid}` group
- `admin` group

**Trigger**: When driver calls `POST /driver/rides/{id}/status`

**Payload**:
```typescript
interface RideStatusChangedEvent {
  rideId: string;
  driverUid: string;
  driverName: string;
  passengerName: string;
  newStatus: RideStatus;  // "Scheduled" | "OnRoute" | "Arrived" | "PassengerOnboard" | "Completed" | "Cancelled"
  timestamp: string;      // ISO 8601 UTC
}
```

**Example Payload**:
```json
{
  "rideId": "ride-abc123",
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson",
  "passengerName": "Jane Doe",
  "newStatus": "OnRoute",
  "timestamp": "2024-12-23T15:30:00Z"
}
```

**Client Handler**:

**JavaScript**:
```javascript
connection.on("RideStatusChanged", (data) => {
    console.log(`? Ride ${data.rideId} status: ${data.newStatus}`);
    
    // Update UI with user-friendly message
    const statusMessages = {
        "Scheduled": "Your ride is scheduled",
        "OnRoute": "Your driver is on the way!",
        "Arrived": "Your driver has arrived",
        "PassengerOnboard": "En route to destination",
        "Completed": "Ride completed. Thank you!",
        "Cancelled": "Ride cancelled"
    };
    
    showNotification(statusMessages[data.newStatus]);
    
    // Update status badge
    document.getElementById('ride-status').textContent = data.newStatus;
    document.getElementById('ride-status').className = `badge ${data.newStatus.toLowerCase()}`;
});
```

**C#**:
```csharp
hubConnection.On<RideStatusChangedDto>("RideStatusChanged", (data) =>
{
    Console.WriteLine($"? Status: {data.NewStatus}");
    
    MainThread.BeginInvokeOnMainThread(() =>
    {
        StatusLabel.Text = GetStatusMessage(data.NewStatus);
        
        // Show notification
        if (data.NewStatus == "Arrived")
        {
            ShowNotification("Your driver has arrived!");
        }
    });
});

string GetStatusMessage(string status) => status switch
{
    "Scheduled" => "Your ride is scheduled",
    "OnRoute" => "Your driver is on the way!",
    "Arrived" => "Your driver has arrived",
    "PassengerOnboard" => "En route to destination",
    "Completed" => "Ride completed. Thank you!",
    "Cancelled" => "Ride cancelled",
    _ => status
};
```

---

### TrackingStopped

**Description**: Ride completed or cancelled, tracking stopped

**Broadcast To**:
- `ride_{rideId}` group
- `driver_{driverUid}` group
- `admin` group

**Trigger**: When ride status changes to `Completed` or `Cancelled`

**Payload**:
```typescript
interface TrackingStoppedEvent {
  rideId: string;
  reason: string;     // "Ride completed" | "Ride cancelled"
  timestamp: string;  // ISO 8601 UTC
}
```

**Example Payload**:
```json
{
  "rideId": "ride-abc123",
  "reason": "Ride completed",
  "timestamp": "2024-12-23T16:00:00Z"
}
```

**Client Handler**:

**JavaScript**:
```javascript
connection.on("TrackingStopped", (data) => {
    console.log(`?? Tracking stopped: ${data.reason}`);
    
    // Remove driver marker from map
    removeDriverMarker(data.rideId);
    
    // Show completion message
    if (data.reason === "Ride completed") {
        showCompletionModal(`Ride completed at ${new Date(data.timestamp).toLocaleTimeString()}`);
    } else {
        showAlert(`Ride cancelled`);
    }
    
    // Unsubscribe from ride
    connection.invoke("UnsubscribeFromRide", data.rideId);
});
```

**C#**:
```csharp
hubConnection.On<TrackingStoppedDto>("TrackingStopped", (data) =>
{
    Console.WriteLine($"?? Stopped: {data.Reason}");
    
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        // Hide map
        MapContainer.IsVisible = false;
        
        // Show completion screen
        if (data.Reason == "Ride completed")
        {
            await Navigation.PushAsync(new RideCompletedPage());
        }
        else
        {
            await DisplayAlert("Ride Cancelled", data.Reason, "OK");
        }
        
        // Unsubscribe
        await hubConnection.InvokeAsync("UnsubscribeFromRide", data.RideId);
    });
});
```

---

### SubscriptionConfirmed

**Description**: Confirmation that client successfully subscribed to a ride

**Broadcast To**: Caller only (not a group broadcast)

**Trigger**: Immediately after `SubscribeToRide` invocation

**Payload**:
```typescript
interface SubscriptionConfirmedEvent {
  rideId: string;
  status: "subscribed";
}
```

**Example Payload**:
```json
{
  "rideId": "ride-abc123",
  "status": "subscribed"
}
```

**Client Handler**:

**JavaScript**:
```javascript
connection.on("SubscriptionConfirmed", (data) => {
    console.log(`? Confirmed subscription to ride ${data.rideId}`);
    
    // Update UI
    document.getElementById('tracking-status').textContent = 'Connected';
    document.getElementById('tracking-status').className = 'status connected';
});
```

**C#**:
```csharp
hubConnection.On<SubscriptionConfirmedDto>("SubscriptionConfirmed", (data) =>
{
    Console.WriteLine($"? Confirmed subscription to {data.RideId}");
    
    MainThread.BeginInvokeOnMainThread(() =>
    {
        TrackingStatusLabel.Text = "Connected";
        TrackingStatusLabel.TextColor = Colors.Green;
    });
});
```

---

## ?? SignalR Groups

| Group Name | Purpose | Auto-Join | Members | Events Received |
|------------|---------|-----------|---------|-----------------|
| `admin` | Monitor all active rides | Yes (for admin/dispatcher) | All staff users | `LocationUpdate`, `RideStatusChanged`, `TrackingStopped` |
| `ride_{rideId}` | Track specific ride | No (manual subscribe) | Passengers, admins | `LocationUpdate`, `RideStatusChanged`, `TrackingStopped` |
| `driver_{driverUid}` | Track specific driver | No (manual subscribe) | Admins only | `LocationUpdate`, `RideStatusChanged`, `TrackingStopped` |

### Auto-Join Logic

**Server-Side** (on connection):
```csharp
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
```

---

## ?? Connection Lifecycle

### Connection Flow

```
Client                                  SignalR Hub
  ?                                         ?
  ???? Connect (with JWT) ????????????????>?
  ?                                         ?
  ?<?????? OnConnectedAsync ?????????????????
  ?         (Auto-join admin group if staff)?
  ?                                         ?
  ???? SubscribeToRide("ride-abc") ???????>?
  ?                                         ?
  ?<?????? SubscriptionConfirmed ????????????
  ?                                         ?
  ?<?????? LocationUpdate (every 5s) ????????
  ?<?????? RideStatusChanged ????????????????
  ?                                         ?
  ???? UnsubscribeFromRide("ride-abc") ???>?
  ?                                         ?
  ???? Disconnect ?????????????????????????>?
  ?                                         ?
  ?<?????? OnDisconnectedAsync ??????????????
  ?         (Auto-removed from all groups)  ?
```

---

## ?? Client Integration Examples

### JavaScript/TypeScript (Web)

**Full Integration**:
```javascript
class TrackingService {
    constructor(jwtToken) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`https://localhost:5206/hubs/location?access_token=${jwtToken}`)
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();
        
        this.setupEventHandlers();
    }
    
    setupEventHandlers() {
        // Location updates
        this.connection.on("LocationUpdate", (data) => {
            this.onLocationUpdate(data);
        });
        
        // Status changes
        this.connection.on("RideStatusChanged", (data) => {
            this.onStatusChanged(data);
        });
        
        // Tracking stopped
        this.connection.on("TrackingStopped", (data) => {
            this.onTrackingStopped(data);
        });
        
        // Subscription confirmed
        this.connection.on("SubscriptionConfirmed", (data) => {
            console.log(`? Subscribed to ${data.rideId}`);
        });
        
        // Connection state changes
        this.connection.onreconnecting(() => {
            console.log("?? Reconnecting...");
        });
        
        this.connection.onreconnected(() => {
            console.log("? Reconnected!");
            // Re-subscribe to ride
            if (this.currentRideId) {
                this.subscribeToRide(this.currentRideId);
            }
        });
        
        this.connection.onclose(() => {
            console.log("?? Disconnected");
        });
    }
    
    async connect() {
        try {
            await this.connection.start();
            console.log("? Connected to SignalR");
        } catch (err) {
            console.error("? Connection failed:", err);
            throw err;
        }
    }
    
    async subscribeToRide(rideId) {
        this.currentRideId = rideId;
        await this.connection.invoke("SubscribeToRide", rideId);
    }
    
    async unsubscribeFromRide(rideId) {
        await this.connection.invoke("UnsubscribeFromRide", rideId);
        this.currentRideId = null;
    }
    
    async disconnect() {
        if (this.currentRideId) {
            await this.unsubscribeFromRide(this.currentRideId);
        }
        await this.connection.stop();
    }
    
    // Event handlers (override in subclass or assign)
    onLocationUpdate(data) {
        console.log("?? Location:", data);
    }
    
    onStatusChanged(data) {
        console.log("? Status:", data.newStatus);
    }
    
    onTrackingStopped(data) {
        console.log("?? Stopped:", data.reason);
    }
}

// Usage
const tracking = new TrackingService(jwtToken);
await tracking.connect();
await tracking.subscribeToRide("ride-abc123");

// Override handlers
tracking.onLocationUpdate = (data) => {
    updateMapMarker(data.latitude, data.longitude, data.heading);
};
```

---

### C# (MAUI/Blazor)

**Full Integration**:
```csharp
public class SignalRTrackingService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _jwtToken;
    private string? _currentRideId;
    
    public event Action<LocationUpdateDto>? OnLocationUpdate;
    public event Action<RideStatusChangedDto>? OnStatusChanged;
    public event Action<TrackingStoppedDto>? OnTrackingStopped;
    
    public SignalRTrackingService(string jwtToken)
    {
        _jwtToken = jwtToken;
    }
    
    public async Task ConnectAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("https://localhost:5206/hubs/location", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_jwtToken)!;
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) })
            .Build();
        
        SetupEventHandlers();
        
        await _hubConnection.StartAsync();
        Console.WriteLine("? Connected to SignalR");
    }
    
    private void SetupEventHandlers()
    {
        if (_hubConnection == null) return;
        
        // Location updates
        _hubConnection.On<LocationUpdateDto>("LocationUpdate", (data) =>
        {
            OnLocationUpdate?.Invoke(data);
        });
        
        // Status changes
        _hubConnection.On<RideStatusChangedDto>("RideStatusChanged", (data) =>
        {
            OnStatusChanged?.Invoke(data);
        });
        
        // Tracking stopped
        _hubConnection.On<TrackingStoppedDto>("TrackingStopped", (data) =>
        {
            OnTrackingStopped?.Invoke(data);
        });
        
        // Subscription confirmed
        _hubConnection.On<SubscriptionConfirmedDto>("SubscriptionConfirmed", (data) =>
        {
            Console.WriteLine($"? Subscribed to {data.RideId}");
        });
        
        // Connection state changes
        _hubConnection.Reconnecting += (error) =>
        {
            Console.WriteLine("?? Reconnecting...");
            return Task.CompletedTask;
        };
        
        _hubConnection.Reconnected += async (connectionId) =>
        {
            Console.WriteLine("? Reconnected!");
            // Re-subscribe to ride
            if (_currentRideId != null)
            {
                await SubscribeToRideAsync(_currentRideId);
            }
        };
        
        _hubConnection.Closed += (error) =>
        {
            Console.WriteLine("?? Disconnected");
            return Task.CompletedTask;
        };
    }
    
    public async Task SubscribeToRideAsync(string rideId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Not connected to SignalR");
        
        _currentRideId = rideId;
        await _hubConnection.InvokeAsync("SubscribeToRide", rideId);
    }
    
    public async Task UnsubscribeFromRideAsync(string rideId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("UnsubscribeFromRide", rideId);
        }
        _currentRideId = null;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_currentRideId != null)
        {
            await UnsubscribeFromRideAsync(_currentRideId);
        }
        
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
    }
}

// Usage in MAUI page
public partial class TrackingPage : ContentPage
{
    private SignalRTrackingService? _signalR;
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        var token = await SecureStorage.GetAsync("jwt_token");
        _signalR = new SignalRTrackingService(token);
        
        // Setup event handlers
        _signalR.OnLocationUpdate += (data) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateMapMarker(data.Latitude, data.Longitude, data.Heading);
            });
        };
        
        _signalR.OnStatusChanged += (data) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = data.NewStatus;
            });
        };
        
        _signalR.OnTrackingStopped += async (data) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Ride Complete", data.Reason, "OK");
                await Navigation.PopAsync();
            });
        };
        
        // Connect and subscribe
        await _signalR.ConnectAsync();
        await _signalR.SubscribeToRideAsync(RideId);
    }
    
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        
        if (_signalR != null)
        {
            await _signalR.DisposeAsync();
        }
    }
}
```

---

## ?? Troubleshooting

### Issue 1: 401 Unauthorized on Connection

**Symptom**: `connection.start()` fails with 401

**Cause**: JWT token not provided or invalid

**Fix**:
```javascript
// ? Wrong
.withUrl("https://localhost:5206/hubs/location")

// ? Correct
.withUrl("https://localhost:5206/hubs/location?access_token=" + token)
```

---

### Issue 2: No Events Received

**Symptom**: Connected but no `LocationUpdate` events

**Checklist**:
1. **Subscribed to group?**
   ```javascript
   await connection.invoke("SubscribeToRide", rideId);
   ```

2. **Ride active?**
   - Driver must have status `OnRoute`, `Arrived`, or `PassengerOnboard`
   - Check: `GET /bookings/{rideId}`

3. **Event handler registered?**
   ```javascript
   connection.on("LocationUpdate", (data) => { /* ... */ });
   ```

4. **LocationBroadcastService running?**
   - Check server console for: `"LocationBroadcastService started"`

---

### Issue 3: HubException on SubscribeToDriver

**Symptom**: "Only admins can subscribe to drivers"

**Cause**: User doesn't have `admin` or `dispatcher` role

**Fix**: Use `SubscribeToRide` instead (for non-admin users)

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system design
- `10-Real-Time-Tracking.md` - GPS tracking implementation
- `14-Passenger-Tracking.md` - Passenger endpoint integration
- `20-API-Reference.md` - REST API endpoints
- `22-Data-Models.md` - DTO schemas
- `32-Troubleshooting.md` - Common issues

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**SignalR Version**: ASP.NET Core 8.0
