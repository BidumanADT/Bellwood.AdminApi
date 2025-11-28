# Driver API Endpoints - Implementation Summary

## ? Implementation Complete

All driver endpoints have been successfully implemented in the AdminAPI with JWT authentication, role-based authorization, and finite-state-machine validation.

---

## ?? **Changes Made**

### **1. Data Models**

#### `Models/BookingRecord.cs` - Updated
- Added `AssignedDriverUid` (string?) - Matches JWT `uid` claim
- Added `CurrentRideStatus` (RideStatus?) - Driver-facing status
- Created `RideStatus` enum: `Scheduled`, `OnRoute`, `Arrived`, `PassengerOnboard`, `Completed`, `Cancelled`

#### `Models/DriverDtos.cs` - New File
```csharp
public sealed class DriverRideListItemDto { /* minimal info for list */ }
public sealed class DriverRideDetailDto { /* full details for single ride */ }
public sealed class RideStatusUpdateRequest { public RideStatus NewStatus { get; set; } }
public sealed class LocationUpdate { /* lat/long + timestamp */ }
```

### **2. Services**

#### `Services/ILocationService.cs` - New File
- `InMemoryLocationService` - In-memory location storage with:
  - **Rate limiting**: Min 15 seconds between updates
  - **Auto-expiration**: 1 hour TTL
  - Thread-safe with `ConcurrentDictionary`

### **3. Authorization**

#### `Program.cs` - Service Registration
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DriverOnly", policy =>
        policy.RequireClaim("role", "driver"));
});
```

---

## ?? **API Endpoints**

### **Authentication Requirements**
All endpoints require:
- Valid JWT with `role=driver` claim
- `uid` claim identifying the driver
- `Authorization: Bearer {token}` header

---

### **GET /driver/rides/today**
Returns driver's assigned rides for the next 24 hours.

**Authorization**: `DriverOnly` policy

**Response** (200 OK):
```json
[
  {
    "id": "abc123",
    "pickupDateTime": "2024-01-15T14:30:00Z",
    "pickupLocation": "O'Hare FBO",
    "dropoffLocation": "Downtown Chicago",
    "passengerName": "Taylor Reed",
    "passengerPhone": "773-555-1122",
    "status": "Scheduled"
  }
]
```

**Filters**:
- `PickupDateTime` between now and +24 hours
- `AssignedDriverUid == {authenticated driver}`
- Excludes `Completed` and `Cancelled` rides

---

### **GET /driver/rides/{id}**
Returns detailed information about a specific ride.

**Authorization**: `DriverOnly` policy + ownership validation

**Response** (200 OK):
```json
{
  "id": "abc123",
  "pickupDateTime": "2024-01-15T14:30:00Z",
  "pickupLocation": "O'Hare FBO",
  "pickupStyle": "MeetAndGreet",
  "pickupSignText": "REED / Bellwood",
  "dropoffLocation": "Downtown Chicago",
  "passengerName": "Taylor Reed",
  "passengerPhone": "773-555-1122",
  "passengerCount": 2,
  "checkedBags": 2,
  "carryOnBags": 2,
  "vehicleClass": "SUV",
  "outboundFlight": {
    "flightNumber": "UA1234",
    "tailNumber": null
  },
  "additionalRequest": "Extra water bottles",
  "status": "Scheduled"
}
```

**Error Responses**:
- `401 Unauthorized` - Invalid/missing JWT
- `403 Forbidden` - Driver doesn't own this ride
- `404 Not Found` - Ride doesn't exist

---

### **POST /driver/rides/{id}/status**
Updates the ride status with FSM validation.

**Authorization**: `DriverOnly` policy + ownership validation

**Request Body**:
```json
{
  "newStatus": "OnRoute"
}
```

**Valid Status Transitions**:
```
Scheduled ? OnRoute, Cancelled
OnRoute ? Arrived, Cancelled
Arrived ? PassengerOnboard, Cancelled
PassengerOnboard ? Completed, Cancelled
Completed ? (none)
Cancelled ? (none)
```

**Response** (200 OK):
```json
{
  "message": "Status updated successfully",
  "rideId": "abc123",
  "newStatus": "OnRoute"
}
```

**Error Responses**:
- `400 Bad Request` - Invalid state transition
- `403 Forbidden` - Driver doesn't own this ride
- `404 Not Found` - Ride doesn't exist

**Side Effects**:
- `PassengerOnboard` ? sets `BookingStatus = InProgress`
- `Completed` ? sets `BookingStatus = Completed`
- `Cancelled` ? sets `BookingStatus = Cancelled`

---

### **POST /driver/location/update**
Receives periodic GPS coordinates while ride is active.

**Authorization**: `DriverOnly` policy + ownership validation

**Request Body**:
```json
{
  "rideId": "abc123",
  "latitude": 41.9742,
  "longitude": -87.9073,
  "timestamp": "2024-01-15T14:35:12Z"
}
```

**Response** (200 OK):
```json
{
  "message": "Location updated"
}
```

**Rate Limiting**:
- Minimum 15 seconds between updates per ride
- Returns `429 Too Many Requests` if too frequent

**Validation**:
- Ride must exist and belong to driver
- Ride status must be `OnRoute`, `Arrived`, or `PassengerOnboard`
- Returns `400 Bad Request` if ride not active

**Storage**:
- In-memory only (not persisted to file)
- Automatic expiration after 1 hour
- Thread-safe implementation

---

### **GET /driver/location/{rideId}**
Returns the most recent location update for a ride.

**Authorization**: Any authenticated user (passenger/admin/driver)

**Response** (200 OK):
```json
{
  "rideId": "abc123",
  "latitude": 41.9742,
  "longitude": -87.9073,
  "timestamp": "2024-01-15T14:35:12Z",
  "ageSeconds": 8.4
}
```

**Error Responses**:
- `404 Not Found` - No recent location data (expired or never updated)

---

## ?? **Testing with Seed Data**

The `/bookings/seed` endpoint now creates rides with driver assignments:

```csharp
AssignedDriverUid = "driver-001"  // Test driver UID
CurrentRideStatus = RideStatus.Scheduled
```

To test as a driver:
1. Get a JWT with `role=driver` and `uid=driver-001` from AuthServer
2. Call `POST /bookings/seed` to create test rides
3. Call `GET /driver/rides/today` to see assigned rides
4. Update status: `POST /driver/rides/{id}/status` with `{ "newStatus": "OnRoute" }`
5. Send location: `POST /driver/location/update` with coordinates

---

## ?? **Security Features**

1. **JWT Validation**: All endpoints require valid JWT with `role=driver`
2. **Ownership Verification**: Drivers can only access their own rides (`AssignedDriverUid == uid`)
3. **FSM Enforcement**: Invalid status transitions are rejected
4. **Rate Limiting**: Location updates throttled to prevent abuse
5. **Active Ride Check**: Location tracking only works for `OnRoute`/`Arrived`/`PassengerOnboard`
6. **Privacy**: Location data expires after 1 hour, not persisted to disk

---

## ?? **Mobile App Integration Guide**

### **1. JWT Token**
Your JWT must include:
```json
{
  "sub": "auth0|12345",
  "uid": "driver-001",
  "role": "driver",
  "exp": 1705334400
}
```

### **2. HTTP Client Setup**
```csharp
public class DriverApiClient
{
    private readonly HttpClient _http;
    
    private void AddAuthHeader(HttpRequestMessage req, string token)
    {
        req.Headers.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }
    
    public async Task<List<DriverRideListItemDto>> GetTodaysRidesAsync(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/driver/rides/today");
        AddAuthHeader(req, token);
        
        var response = await _http.SendAsync(req);
        response.EnsureSuccessStatusCode();
        
        return await response.Content
            .ReadFromJsonAsync<List<DriverRideListItemDto>>();
    }
}
```

### **3. Location Update Loop**
```csharp
private async Task StartLocationTracking(string rideId, string token)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    
    while (await timer.WaitForNextTickAsync())
    {
        var location = await _geolocator.GetCurrentPositionAsync();
        
        var update = new LocationUpdate
        {
            RideId = rideId,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Timestamp = DateTime.UtcNow
        };
        
        await SendLocationUpdateAsync(update, token);
    }
}
```

---

## ?? **Status Workflow Example**

```
1. Driver opens app ? sees ride (status: Scheduled)
2. Driver clicks "Start Trip" ? POST /status { newStatus: "OnRoute" }
3. App starts GPS tracking (every 30s)
4. Driver arrives ? POST /status { newStatus: "Arrived" }
5. Passenger gets in ? POST /status { newStatus: "PassengerOnboard" }
6. Driver completes drop-off ? POST /status { newStatus: "Completed" }
7. App stops GPS tracking
```

---

## ?? **Endpoint Summary**

| Endpoint | Method | Auth | Purpose |
|----------|--------|------|---------|
| `/driver/rides/today` | GET | DriverOnly | List next 24h rides |
| `/driver/rides/{id}` | GET | DriverOnly | Get ride details |
| `/driver/rides/{id}/status` | POST | DriverOnly | Update ride status |
| `/driver/location/update` | POST | DriverOnly | Send GPS coordinates |
| `/driver/location/{rideId}` | GET | Authenticated | Get latest location |

---

## ?? **Known Limitations**

1. **In-Memory Storage**: Location data doesn't survive server restart
2. **No Persistence**: Location history not saved for analytics
3. **Simple Rate Limit**: Per-ride 15s minimum, no per-driver limit
4. **UTC Times**: No timezone conversion (displays UTC to driver)
5. **No Pagination**: `/rides/today` returns all rides (up to 200)

---

## ?? **Next Steps for Mobile App**

1. ? Implement `IDriverApi` interface with these 5 endpoints
2. ? Add JWT token retrieval from AuthServer
3. ? Create `AuthHttpHandler` to inject `Authorization` header
4. ? Implement location tracking service (30s interval)
5. ? Build UI for ride list and detail pages
6. ? Add status update buttons with confirmation dialogs
7. ? Test with seed data (`driver-001` UID)

---

## ?? **Support**

For questions about the API implementation, contact the AdminAPI team or refer to the Swagger documentation at `/swagger` when running in development mode.
