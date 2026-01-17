# Passenger Location Tracking

**Document Type**: Living Document - Feature Implementation  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document covers the **passenger-safe location tracking** system for the Bellwood AdminAPI, enabling passengers to track their own rides via the PassengerApp with **email-based authorization**.

**Key Features**:
- ?? **Email-Based Auth** - Passengers authorize via email (no user accounts needed)
- ?? **Passenger-Safe Endpoint** - Dedicated endpoint with privacy protections
- ?? **No Cross-Tracking** - Passengers can ONLY see their own rides
- ? **trackingActive Flag** - Clear indication of GPS status
- ?? **Real-Time Updates** - Same SignalR infrastructure as admin/drivers

---

## ??? Architecture

### Passenger Tracking Flow

```
???????????????????????????????????????????????
?          PassengerApp (MAUI)                ?
?  - User logs in with email                 ?
?  - JWT contains email claim                ?
???????????????????????????????????????????????
                  ? 1. Authenticate
                  ?
???????????????????????????????????????????????
?             AuthServer                      ?
?  - Issues JWT with email claim             ?
?  - No userId/uid (passenger not stored)    ?
?  JWT: { "email": "jane@example.com" }      ?
???????????????????????????????????????????????
                  ? 2. Request location
                  ?
???????????????????????????????????????????????
?      AdminAPI - Passenger Endpoint          ?
?  GET /passenger/rides/{rideId}/location     ?
?                                              ?
?  1. Extract email from JWT                  ?
?  2. Fetch booking by rideId                 ?
?  3. Verify email matches booker OR passenger?
?  4. Return location if tracking active      ?
???????????????????????????????????????????????
                  ? 3. Response
                  ?
???????????????????????????????????????????????
?          PassengerApp Display               ?
?  if (trackingActive):                       ?
?    - Show driver on map                     ?
?    - Display ETA, speed, heading            ?
?  else:                                       ?
?    - Show "Driver hasn't started yet"       ?
???????????????????????????????????????????????
```

---

## ?? Email-Based Authorization

### Why Email Auth?

**Problem**: Passengers don't have user accounts in the Bellwood system.

**Solution**: Use email address as identity.

**How It Works**:
1. PassengerApp user logs in with email (in AuthServer)
2. AuthServer issues JWT with `email` claim
3. AdminAPI verifies email matches booking's booker or passenger email
4. If match ? Allow access, else ? 403 Forbidden

---

### Authorization Logic

**File**: `Program.cs` (Passenger Location Endpoint)

```csharp
app.MapGet("/passenger/rides/{rideId}/location", async (
    string rideId,
    HttpContext context,
    ILocationService locationService,
    IBookingRepository repo) =>
{
    // 1. Get booking
    var booking = await repo.GetAsync(rideId);
    if (booking is null)
        return Results.NotFound(new { error = "Ride not found" });

    // 2. PASSENGER AUTHORIZATION: Verify caller owns this booking
    var userEmail = context.User.FindFirst("email")?.Value;
    
    bool isPassengerAuthorized = false;
    
    // 3. Check booker email
    if (!string.IsNullOrEmpty(userEmail) && 
        !string.IsNullOrEmpty(booking.Draft?.Booker?.EmailAddress) &&
        userEmail.Equals(booking.Draft.Booker.EmailAddress, StringComparison.OrdinalIgnoreCase))
    {
        isPassengerAuthorized = true;
    }
    
    // 4. Check passenger email (if different from booker)
    if (!isPassengerAuthorized && 
        !string.IsNullOrEmpty(userEmail) &&
        !string.IsNullOrEmpty(booking.Draft?.Passenger?.EmailAddress) &&
        userEmail.Equals(booking.Draft.Passenger.EmailAddress, StringComparison.OrdinalIgnoreCase))
    {
        isPassengerAuthorized = true;
    }
    
    // 5. Deny if no match
    if (!isPassengerAuthorized)
    {
        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You can only view location for your own bookings");
    }

    // 6. Get location data
    var location = locationService.GetLatestLocation(rideId);
    if (location is null)
    {
        // Return "not started" response instead of 404
        return Results.Ok(new
        {
            rideId,
            trackingActive = false,
            message = "Driver has not started tracking yet",
            currentStatus = booking.CurrentRideStatus?.ToString() ?? "Scheduled"
        });
    }

    // 7. Return location with trackingActive flag
    return Results.Ok(new
    {
        rideId = location.RideId,
        trackingActive = true, // ? CRITICAL: PassengerApp expects this!
        latitude = location.Latitude,
        longitude = location.Longitude,
        timestamp = location.Timestamp,
        heading = location.Heading,
        speed = location.Speed,
        accuracy = location.Accuracy,
        ageSeconds = (DateTime.UtcNow - location.Timestamp).TotalSeconds,
        driverUid = booking.AssignedDriverUid,
        driverName = booking.AssignedDriverName
    });
})
.WithName("GetPassengerRideLocation")
.RequireAuthorization(); // Authenticated passengers only
```

---

### Email Matching Scenarios

| Scenario | Booker Email | Passenger Email | User JWT Email | Access |
|----------|--------------|-----------------|----------------|--------|
| **Self-Booking** | jane@example.com | jane@example.com | jane@example.com | ? Allow |
| **Booking for Someone Else** | alice@example.com | bob@example.com | alice@example.com | ? Allow (booker) |
| **Booking for Someone Else** | alice@example.com | bob@example.com | bob@example.com | ? Allow (passenger) |
| **Unauthorized** | alice@example.com | bob@example.com | charlie@example.com | ? Deny (403) |

---

## ?? API Endpoint

### Get Passenger Ride Location

**Endpoint**: `GET /passenger/rides/{rideId}/location`  
**Auth**: Authenticated (JWT with `email` claim)  
**Authorization**: Email must match booker or passenger

---

### Response: Tracking Active

**Success Response** (200 OK):
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

**Field Descriptions**:

| Field | Type | Description |
|-------|------|-------------|
| `rideId` | string | Booking ID |
| `trackingActive` | boolean | **true** if driver is actively tracking |
| `latitude` | number | GPS latitude (-90 to 90) |
| `longitude` | number | GPS longitude (-180 to 180) |
| `timestamp` | string (ISO 8601) | When location was recorded (UTC) |
| `heading` | number? | Direction of travel (degrees, 0-360) |
| `speed` | number? | Speed in meters/second |
| `accuracy` | number? | GPS accuracy in meters |
| `ageSeconds` | number | How old the location is (seconds) |
| `driverUid` | string | Driver's unique ID |
| `driverName` | string | Driver's display name |

---

### Response: Tracking Not Started

**Success Response** (200 OK):
```json
{
  "rideId": "abc123",
  "trackingActive": false,
  "message": "Driver has not started tracking yet",
  "currentStatus": "Scheduled"
}
```

**When This Happens**:
- Driver hasn't started GPS tracking yet
- Ride status is `Scheduled` (not `OnRoute`, `Arrived`, or `PassengerOnboard`)
- Driver hasn't sent any location updates

**PassengerApp Behavior**:
- Show loading spinner or placeholder
- Display message: "Waiting for driver to start..."
- Poll endpoint every 10-15 seconds until `trackingActive: true`

---

### Error Responses

#### 401 Unauthorized

**Cause**: No JWT token or invalid token

**Response**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

**Fix**: Ensure `Authorization: Bearer {token}` header is sent

---

#### 403 Forbidden

**Cause**: Email doesn't match booker or passenger

**Response**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "detail": "You can only view location for your own bookings",
  "status": 403
}
```

**Fix**: User is trying to track someone else's ride (security working correctly)

---

#### 404 Not Found

**Cause**: Ride ID doesn't exist

**Response**:
```json
{
  "error": "Ride not found"
}
```

**Fix**: Verify ride ID is correct

---

## ?? PassengerApp Integration

### Authentication

**1. User Login** (PassengerApp ? AuthServer):

```csharp
public class AuthService
{
    private readonly HttpClient _httpClient;
    
    public async Task<string> LoginAsync(string email, string password)
    {
        var request = new
        {
            email,
            password
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            "https://localhost:5001/api/auth/passenger/login", 
            request);
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result.AccessToken; // JWT with email claim
    }
}
```

**2. Store Token**:

```csharp
// Store in SecureStorage (MAUI)
await SecureStorage.SetAsync("jwt_token", token);
```

**3. Add to HTTP Client**:

```csharp
public class LocationService
{
    private readonly HttpClient _httpClient;
    
    public LocationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task SetAuthTokenAsync(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }
}
```

---

### Fetching Location

**Poll Location Periodically**:

```csharp
public class TrackingViewModel : INotifyPropertyChanged
{
    private readonly LocationService _locationService;
    private System.Timers.Timer _pollingTimer;
    
    public bool TrackingActive { get; set; }
    public LocationDto? CurrentLocation { get; set; }
    public string StatusMessage { get; set; } = "";
    
    public async Task StartTrackingAsync(string rideId)
    {
        // Poll every 10 seconds
        _pollingTimer = new System.Timers.Timer(10_000);
        _pollingTimer.Elapsed += async (s, e) => await UpdateLocationAsync(rideId);
        _pollingTimer.Start();
        
        // Initial fetch
        await UpdateLocationAsync(rideId);
    }
    
    private async Task UpdateLocationAsync(string rideId)
    {
        try
        {
            var response = await _locationService.GetPassengerRideLocationAsync(rideId);
            
            if (response.TrackingActive)
            {
                // Driver is tracking - show on map
                TrackingActive = true;
                CurrentLocation = response;
                StatusMessage = $"Driver {response.DriverName} is en route";
                
                // Update map marker
                UpdateMapMarker(response.Latitude, response.Longitude, response.Heading);
            }
            else
            {
                // Driver hasn't started - show placeholder
                TrackingActive = false;
                StatusMessage = response.Message ?? "Waiting for driver to start...";
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            StatusMessage = "Access denied. This isn't your ride.";
            _pollingTimer?.Stop();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
    
    public void StopTracking()
    {
        _pollingTimer?.Stop();
        _pollingTimer?.Dispose();
    }
}
```

---

### Displaying on Map

**Microsoft.Maui.Controls.Maps**:

```csharp
public class TrackingMapPage : ContentPage
{
    private Map _map;
    private Pin? _driverPin;
    
    public TrackingMapPage()
    {
        _map = new Map
        {
            IsShowingUser = true // Show passenger's location
        };
        
        Content = _map;
    }
    
    public void UpdateDriverLocation(double lat, double lon, double? heading)
    {
        if (_driverPin == null)
        {
            // Create driver pin
            _driverPin = new Pin
            {
                Label = "Your Driver",
                Type = PinType.Place,
                Location = new Location(lat, lon)
            };
            _map.Pins.Add(_driverPin);
        }
        else
        {
            // Update existing pin
            _driverPin.Location = new Location(lat, lon);
        }
        
        // Center map on driver (with padding for passenger)
        _map.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(lat, lon),
            Distance.FromMiles(1)));
        
        // TODO: Rotate pin icon based on heading
    }
    
    public void ShowWaitingMessage(string message)
    {
        // Remove driver pin
        if (_driverPin != null)
        {
            _map.Pins.Remove(_driverPin);
            _driverPin = null;
        }
        
        // Show loading overlay
        DisplayAlert("Waiting", message, "OK");
    }
}
```

---

### SignalR Real-Time Updates

**Subscribe to Ride Updates**:

```csharp
public class SignalRService
{
    private HubConnection? _hubConnection;
    
    public async Task ConnectAsync(string jwtToken)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("https://localhost:5206/hubs/location", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(jwtToken)!;
            })
            .Build();
        
        // Handle location updates
        _hubConnection.On<LocationUpdateDto>("LocationUpdate", (data) =>
        {
            Console.WriteLine($"?? Driver at ({data.Latitude}, {data.Longitude})");
            // Update UI
            MessagingCenter.Send(this, "LocationUpdate", data);
        });
        
        // Handle status changes
        _hubConnection.On<RideStatusChangedDto>("RideStatusChanged", (data) =>
        {
            Console.WriteLine($"? Status: {data.NewStatus}");
            MessagingCenter.Send(this, "StatusChanged", data);
        });
        
        // Handle tracking stopped
        _hubConnection.On<TrackingStoppedDto>("TrackingStopped", (data) =>
        {
            Console.WriteLine($"?? Tracking stopped: {data.Reason}");
            MessagingCenter.Send(this, "TrackingStopped", data);
        });
        
        await _hubConnection.StartAsync();
    }
    
    public async Task SubscribeToRideAsync(string rideId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SubscribeToRide", rideId);
            Console.WriteLine($"?? Subscribed to ride {rideId}");
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }
    }
}
```

---

## ?? Privacy & Security

### What Passengers CAN See

? **Location of their OWN driver** (if tracking active)  
? **Driver's name** (for identification)  
? **Ride status** (Scheduled, OnRoute, Arrived, etc.)  
? **ETA** (future enhancement)

### What Passengers CANNOT See

? **Other passengers' rides**  
? **Driver's personal information** (beyond name)  
? **Historical location data** (only real-time)  
? **Admin dashboard data**

### Security Measures

1. **Email Verification**:
   - JWT must contain valid `email` claim
   - Email must match booking's booker OR passenger
   - Case-insensitive comparison

2. **No Cross-Tracking**:
   - Passenger A cannot track Passenger B's ride
   - Enforced at endpoint level (403 Forbidden)

3. **No Ride Enumeration**:
   - Must know exact ride ID (no list endpoint)
   - Cannot guess or brute-force ride IDs

4. **Automatic Cleanup**:
   - Location data removed when ride completes
   - SignalR broadcasts `TrackingStopped` event

---

## ?? Testing

### Manual Testing Workflow

**1. Create Test Booking**:

```bash
# Create booking as passenger
curl -X POST https://localhost:5206/bookings \
  -H "Authorization: Bearer {passengerToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "booker": {
      "firstName": "Jane",
      "lastName": "Doe",
      "email": "jane@example.com",
      "phoneNumber": "312-555-1234"
    },
    "passenger": {
      "firstName": "Jane",
      "lastName": "Doe",
      "email": "jane@example.com",
      "phoneNumber": "312-555-1234"
    },
    "pickupDateTime": "2024-12-24T09:00:00",
    "pickupLocation": "Airport",
    "dropoffLocation": "Hotel",
    "vehicleClass": "Sedan"
  }'

# Response: { "id": "ride-abc-123" }
```

**2. Assign Driver (Admin)**:

```bash
curl -X POST https://localhost:5206/bookings/ride-abc-123/assign-driver \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "driverId": "driver-xyz"
  }'
```

**3. Get Location (Before Tracking Starts)**:

```bash
curl -X GET https://localhost:5206/passenger/rides/ride-abc-123/location \
  -H "Authorization: Bearer {passengerToken}"

# Expected Response:
# {
#   "rideId": "ride-abc-123",
#   "trackingActive": false,
#   "message": "Driver has not started tracking yet",
#   "currentStatus": "Scheduled"
# }
```

**4. Driver Starts Tracking**:

```bash
# Driver updates status to OnRoute
curl -X POST https://localhost:5206/driver/rides/ride-abc-123/status \
  -H "Authorization: Bearer {driverToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "newStatus": "OnRoute"
  }'

# Driver sends first location update
curl -X POST https://localhost:5206/driver/location/update \
  -H "Authorization: Bearer {driverToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "rideId": "ride-abc-123",
    "latitude": 41.8781,
    "longitude": -87.6298,
    "heading": 45.5,
    "speed": 12.3
  }'
```

**5. Get Location (After Tracking Starts)**:

```bash
curl -X GET https://localhost:5206/passenger/rides/ride-abc-123/location \
  -H "Authorization: Bearer {passengerToken}"

# Expected Response:
# {
#   "rideId": "ride-abc-123",
#   "trackingActive": true,
#   "latitude": 41.8781,
#   "longitude": -87.6298,
#   "timestamp": "2024-12-23T15:30:00Z",
#   "heading": 45.5,
#   "speed": 12.3,
#   "driverName": "Charlie Johnson"
# }
```

**6. Test Unauthorized Access**:

```bash
# Try to access with different user's token
curl -X GET https://localhost:5206/passenger/rides/ride-abc-123/location \
  -H "Authorization: Bearer {otherPassengerToken}"

# Expected Response: 403 Forbidden
# {
#   "title": "Forbidden",
#   "detail": "You can only view location for your own bookings",
#   "status": 403
# }
```

---

### Unit Tests

**Testing Email Authorization**:

```csharp
[Fact]
public async Task GetPassengerRideLocation_WithMatchingBookerEmail_ReturnsLocation()
{
    // Arrange
    var booking = new BookingRecord
    {
        Id = "ride-123",
        Draft = new QuoteDraft
        {
            Booker = new Person { EmailAddress = "jane@example.com" },
            Passenger = new Person { EmailAddress = "jane@example.com" }
        }
    };
    
    var user = CreateUserWithEmailClaim("jane@example.com");
    
    // Act
    var result = await GetPassengerRideLocation("ride-123", user);
    
    // Assert
    Assert.IsType<OkObjectResult>(result);
}

[Fact]
public async Task GetPassengerRideLocation_WithNonMatchingEmail_ReturnsForbidden()
{
    // Arrange
    var booking = new BookingRecord
    {
        Id = "ride-123",
        Draft = new QuoteDraft
        {
            Booker = new Person { EmailAddress = "jane@example.com" },
            Passenger = new Person { EmailAddress = "jane@example.com" }
        }
    };
    
    var user = CreateUserWithEmailClaim("charlie@example.com");
    
    // Act
    var result = await GetPassengerRideLocation("ride-123", user);
    
    // Assert
    Assert.IsType<ForbidResult>(result);
}
```

---

## ?? Troubleshooting

### Issue 1: Always Returns "trackingActive: false"

**Symptom**: Passenger endpoint always shows tracking not started

**Possible Causes**:

**1. Driver Hasn't Started Tracking**:
```bash
# Check ride status
curl -X GET https://localhost:5206/bookings/{rideId} \
  -H "Authorization: Bearer {adminToken}"

# Look for: "currentRideStatus": "Scheduled"
# If not "OnRoute", "Arrived", or "PassengerOnboard", tracking isn't active
```

**2. Location Data Not Stored**:
```bash
# Check admin locations endpoint
curl -X GET https://localhost:5206/admin/locations \
  -H "Authorization: Bearer {adminToken}"

# Look for ride in response
# If missing, driver updates not reaching server
```

---

### Issue 2: 403 Forbidden (Email Mismatch)

**Symptom**: Passenger gets 403 when accessing their own ride

**Diagnosis**:
```bash
# Decode JWT token
# Check "email" claim matches booking

# Get booking
curl -X GET https://localhost:5206/bookings/{rideId} \
  -H "Authorization: Bearer {adminToken}"

# Check: booking.Draft.Booker.EmailAddress
# Check: booking.Draft.Passenger.EmailAddress

# Verify JWT email matches one of these (case-insensitive)
```

**Common Causes**:
- JWT has different email than booking
- Typo in email (e.g., `john@example.com` vs `jon@example.com`)
- JWT doesn't contain `email` claim

---

### Issue 3: SignalR Events Not Received

**Symptom**: PassengerApp connected but no `LocationUpdate` events

**Possible Causes**:

**1. Not Subscribed to Ride**:
```csharp
// Must call after connecting
await hubConnection.InvokeAsync("SubscribeToRide", rideId);
```

**2. Driver Not Tracking**:
- Driver must have status `OnRoute`, `Arrived`, or `PassengerOnboard`
- Driver must send location updates via `POST /driver/location/update`

**3. Wrong Group**:
```csharp
// Passenger must subscribe to "ride_{rideId}" group
// NOT "driver_{driverUid}" (admin-only)
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system integration
- `10-Real-Time-Tracking.md` - GPS tracking infrastructure
- `11-User-Access-Control.md` - RBAC & authorization
- `21-SignalR-Events.md` - SignalR event reference
- `23-Security-Model.md` - Complete security documentation
- `32-Troubleshooting.md` - Common issues & solutions

---

## ?? Future Enhancements

### Phase 3+ Roadmap

1. **ETA Calculation**:
   ```csharp
   public class EtaService
   {
       public async Task<TimeSpan> CalculateEtaAsync(
           double driverLat, 
           double driverLon,
           double pickupLat,
           double pickupLon,
           double currentSpeed)
       {
           var distance = CalculateDistance(driverLat, driverLon, pickupLat, pickupLon);
           var averageSpeed = currentSpeed > 0 ? currentSpeed : 10.0; // m/s default
           return TimeSpan.FromSeconds(distance / averageSpeed);
       }
   }
   ```

2. **Passenger Notifications**:
   - Push notification when driver is 10 minutes away
   - SMS/email when driver arrives
   - Ride completion summary

3. **Ride History**:
   ```csharp
   // GET /passenger/rides/history
   // Returns past rides for this passenger (by email)
   ```

4. **Share Tracking Link**:
   ```csharp
   // Generate temporary tracking link (no auth required)
   // POST /passenger/rides/{rideId}/share
   // Returns: "https://bellwood.com/track?token=xyz..."
   // Link expires after ride completes
   ```

5. **Multi-Stop Support**:
   - Track multiple waypoints
   - Show progress through stops
   - ETA per stop

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Next Steps**: See Phase 3 Roadmap above
