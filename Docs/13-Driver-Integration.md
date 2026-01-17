# Driver Integration & Management

**Document Type**: Living Document - Feature Implementation  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document covers the complete **driver integration system** for the Bellwood AdminAPI, including affiliate management, driver assignment, and the DriverApp endpoint ecosystem.

**Key Features**:
- ?? **Affiliate Management** - Multi-tenant company structure
- ?? **Driver Profiles** - Linked to AuthServer via UserUid
- ?? **Assignment System** - Booking ? Driver linking
- ?? **DriverApp Endpoints** - Ride management & status updates
- ?? **Security** - UserUid validation & ownership verification

---

## ??? Architecture

### Entity Relationship

```
???????????????????????????????????
?         Affiliate               ?
?  (Company/Organization)         ?
?  - Chicago Limo Service         ?
?  - Suburban Chauffeurs          ?
???????????????????????????????????
             ? 1:N
             ?
???????????????????????????????????
?           Driver                ?
?  (Individual chauffeur)         ?
?  - Charlie Johnson              ?
?  - Sarah Lee                    ?
?  - UserUid: "driver-001" ???????????
???????????????????????????????????  ?
             ? 1:N                    ? Links to
             ?                        ? AuthServer
???????????????????????????????????  ?
?         Booking                 ?  ?
?  (Assigned rides)               ?  ?
?  - AssignedDriverId             ?  ?
?  - AssignedDriverUid ????????????  ?
?  - AssignedDriverName           ?  ?
???????????????????????????????????  ?
                                     ?
           ???????????????????????????
           ?
           ?
???????????????????????????????????
?       AuthServer User           ?
?  - Username: "charlie"          ?
?  - Role: "driver"               ?
?  - UserUid: "driver-001" ????????
?  - Password: (hashed)           ?
???????????????????????????????????
```

---

## ?? Data Models

### Affiliate Model

**File**: `Models/Affiliate.cs`

```csharp
public class Affiliate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    // Company information
    public string Name { get; set; } = "";
    public string? PointOfContact { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    
    // Address
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    
    // Nested drivers (populated from separate storage)
    public List<Driver>? Drivers { get; set; }
}
```

**Storage**: `App_Data/affiliates.json`

---

### Driver Model

**File**: `Models/Driver.cs`

```csharp
public class Driver
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AffiliateId { get; set; } = ""; // Foreign key
    
    // Driver information
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    
    // CRITICAL: Link to AuthServer user
    // This must match the "uid" claim in the driver's JWT token
    public string? UserUid { get; set; }
    
    // Optional metadata
    public string? LicenseNumber { get; set; }
    public string? VehicleInfo { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**Storage**: `App_Data/drivers.json`

**UserUid Requirement**:
- ? **Required** for driver assignment
- ? Links driver profile to AuthServer user account
- ? Must match `uid` claim in JWT for DriverApp authentication

---

### Driver Assignment Request

**File**: `Models/DriverAssignmentRequest.cs`

```csharp
public class DriverAssignmentRequest
{
    public string DriverId { get; set; } = ""; // Driver profile ID
}
```

---

## ?? The UserUid Link

### How It Works

```
???????????????????????????????????????????????????????????????
?                    AuthServer Creates User                  ?
?  POST /api/admin/users                                      ?
?  {                                                           ?
?    "username": "charlie",                                   ?
?    "password": "password",                                  ?
?    "role": "driver"                                         ?
?  }                                                           ?
?  Response:                                                   ?
?  {                                                           ?
?    "userId": "guid-123...",                                 ?
?    "uid": "driver-001" ? Unique identifier                 ?
?  }                                                           ?
???????????????????????????????????????????????????????????????
                  ?
                  ? Store UserUid
???????????????????????????????????????????????????????????????
?              AdminAPI Creates Driver Profile                ?
?  POST /affiliates/{affiliateId}/drivers                     ?
?  {                                                           ?
?    "name": "Charlie Johnson",                               ?
?    "phone": "312-555-0001",                                 ?
?    "userUid": "driver-001" ? Links to AuthServer           ?
?  }                                                           ?
???????????????????????????????????????????????????????????????
                  ?
                  ? Assign to Booking
???????????????????????????????????????????????????????????????
?              AdminPortal Assigns Driver                     ?
?  POST /bookings/{bookingId}/assign-driver                   ?
?  {                                                           ?
?    "driverId": "abc-def-123"                                ?
?  }                                                           ?
?  ? Stores: AssignedDriverUid = "driver-001"                ?
???????????????????????????????????????????????????????????????
                  ?
                  ? Driver Logs In
???????????????????????????????????????????????????????????????
?              DriverApp Authenticates                        ?
?  POST /api/auth/login                                       ?
?  {                                                           ?
?    "username": "charlie",                                   ?
?    "password": "password"                                   ?
?  }                                                           ?
?  Response JWT:                                              ?
?  {                                                           ?
?    "sub": "charlie",                                        ?
?    "uid": "driver-001", ? Used to filter rides             ?
?    "role": "driver"                                         ?
?  }                                                           ?
???????????????????????????????????????????????????????????????
                  ?
                  ? Get Assigned Rides
???????????????????????????????????????????????????????????????
?              DriverApp Fetches Rides                        ?
?  GET /driver/rides/today                                    ?
?  Authorization: Bearer {JWT with uid=driver-001}            ?
?                                                              ?
?  Server extracts uid from JWT:                              ?
?  var driverUid = context.User.FindFirst("uid")?.Value;      ?
?  // Result: "driver-001"                                    ?
?                                                              ?
?  Filters bookings:                                          ?
?  bookings.Where(b => b.AssignedDriverUid == "driver-001")  ?
?  // Returns only Charlie's rides!                           ?
???????????????????????????????????????????????????????????????
```

---

## ?? API Endpoints

### Affiliate Management

#### List All Affiliates

**Endpoint**: `GET /affiliates/list`  
**Auth**: Authenticated

**Response** (200 OK):
```json
[
  {
    "id": "abc123",
    "name": "Chicago Limo Service",
    "pointOfContact": "John Smith",
    "phone": "312-555-1234",
    "email": "dispatch@chicagolimo.com",
    "streetAddress": "123 Main St",
    "city": "Chicago",
    "state": "IL",
    "drivers": [
      {
        "id": "driver-abc",
        "affiliateId": "abc123",
        "name": "Charlie Johnson",
        "phone": "312-555-0001",
        "userUid": "driver-001",
        "isActive": true
      }
    ]
  }
]
```

**Implementation**:
```csharp
app.MapGet("/affiliates/list", async (
    IAffiliateRepository affiliateRepo, 
    IDriverRepository driverRepo) =>
{
    var affiliates = await affiliateRepo.GetAllAsync();
    
    // Populate drivers for each affiliate from separate storage
    foreach (var affiliate in affiliates)
    {
        affiliate.Drivers = await driverRepo.GetByAffiliateIdAsync(affiliate.Id);
    }
    
    return Results.Ok(affiliates);
})
.WithName("ListAffiliates")
.RequireAuthorization();
```

---

#### Create Affiliate

**Endpoint**: `POST /affiliates`  
**Auth**: Authenticated

**Request**:
```json
{
  "name": "Suburban Chauffeurs",
  "pointOfContact": "Emily Davis",
  "phone": "847-555-9876",
  "email": "emily@suburbanchauffeurs.com",
  "city": "Naperville",
  "state": "IL"
}
```

**Success Response** (201 Created):
```json
{
  "id": "def456",
  "name": "Suburban Chauffeurs",
  // ... full affiliate data
}
```

**Validation**:
- ? Name required
- ? Email required
- ? Auto-generated ID

---

#### Get Affiliate by ID

**Endpoint**: `GET /affiliates/{id}`  
**Auth**: Authenticated

**Response** (200 OK):
```json
{
  "id": "abc123",
  "name": "Chicago Limo Service",
  "drivers": [
    {
      "id": "driver-abc",
      "name": "Charlie Johnson",
      "userUid": "driver-001"
    }
  ]
}
```

**Response** (404 Not Found):
```json
{
  "error": "Affiliate not found"
}
```

---

#### Update Affiliate

**Endpoint**: `PUT /affiliates/{id}`  
**Auth**: Authenticated

**Request**:
```json
{
  "name": "Chicago Elite Limo Service",
  "phone": "312-555-9999",
  // ... updated fields
}
```

**Success Response** (200 OK):
```json
{
  "id": "abc123",
  "name": "Chicago Elite Limo Service",
  // ... full updated data
}
```

---

#### Delete Affiliate

**Endpoint**: `DELETE /affiliates/{id}`  
**Auth**: Authenticated

**Success Response** (200 OK):
```json
{
  "message": "Affiliate and associated drivers deleted",
  "id": "abc123"
}
```

**Cascade Delete**:
```csharp
// Delete all drivers belonging to this affiliate
await driverRepo.DeleteByAffiliateIdAsync(id);

// Then delete the affiliate
await affiliateRepo.DeleteAsync(id);
```

**Warning**: This is a destructive operation. All drivers under the affiliate are also deleted!

---

### Driver Management

#### Create Driver

**Endpoint**: `POST /affiliates/{affiliateId}/drivers`  
**Auth**: Authenticated

**Request**:
```json
{
  "name": "Charlie Johnson",
  "phone": "312-555-0001",
  "email": "charlie@chicagolimo.com",
  "userUid": "driver-001"
}
```

**Success Response** (201 Created):
```json
{
  "id": "driver-abc-123",
  "affiliateId": "abc123",
  "name": "Charlie Johnson",
  "phone": "312-555-0001",
  "userUid": "driver-001",
  "isActive": true
}
```

**Validation**:
- ? Name required
- ? Phone required
- ? UserUid must be unique (across all drivers)
- ? Affiliate must exist

**UserUid Uniqueness Check**:
```csharp
if (!string.IsNullOrWhiteSpace(driver.UserUid))
{
    var isUnique = await driverRepo.IsUserUidUniqueAsync(driver.UserUid);
    if (!isUnique)
        return Results.BadRequest(new { 
            error = $"UserUid '{driver.UserUid}' is already assigned to another driver" 
        });
}
```

---

#### List All Drivers

**Endpoint**: `GET /drivers/list`  
**Auth**: Authenticated

**Response** (200 OK):
```json
[
  {
    "id": "driver-abc",
    "affiliateId": "abc123",
    "name": "Charlie Johnson",
    "phone": "312-555-0001",
    "userUid": "driver-001",
    "isActive": true
  },
  {
    "id": "driver-def",
    "affiliateId": "abc123",
    "name": "Sarah Lee",
    "phone": "312-555-0002",
    "userUid": "driver-002",
    "isActive": true
  }
]
```

---

#### Get Driver by UserUid

**Endpoint**: `GET /drivers/by-uid/{userUid}`  
**Auth**: Authenticated

**Purpose**: Find driver profile by AuthServer UserUid

**Response** (200 OK):
```json
{
  "id": "driver-abc",
  "affiliateId": "abc123",
  "name": "Charlie Johnson",
  "phone": "312-555-0001",
  "userUid": "driver-001",
  "isActive": true
}
```

**Response** (404 Not Found):
```json
{
  "error": "No driver found with this UserUid"
}
```

**Use Cases**:
- DriverApp profile lookup during login
- AdminPortal verifying driver linkage

---

#### Get Driver by ID

**Endpoint**: `GET /drivers/{id}`  
**Auth**: Authenticated

**Response** (200 OK):
```json
{
  "id": "driver-abc",
  "affiliateId": "abc123",
  "name": "Charlie Johnson",
  // ... full driver data
}
```

---

#### Update Driver

**Endpoint**: `PUT /drivers/{id}`  
**Auth**: Authenticated

**Request**:
```json
{
  "name": "Charles 'Charlie' Johnson",
  "phone": "312-555-0001",
  "userUid": "driver-001",
  "isActive": true
}
```

**Success Response** (200 OK):
```json
{
  "id": "driver-abc",
  "name": "Charles 'Charlie' Johnson",
  // ... full updated data
}
```

**UserUid Uniqueness**:
```csharp
// Validate UserUid uniqueness if changed (exclude self)
if (!string.IsNullOrWhiteSpace(driver.UserUid))
{
    var isUnique = await repo.IsUserUidUniqueAsync(driver.UserUid, excludeDriverId: id);
    if (!isUnique)
        return Results.BadRequest(new { 
            error = $"UserUid '{driver.UserUid}' is already assigned to another driver" 
        });
}
```

---

#### Delete Driver

**Endpoint**: `DELETE /drivers/{id}`  
**Auth**: Authenticated

**Success Response** (200 OK):
```json
{
  "message": "Driver deleted",
  "id": "driver-abc"
}
```

**Note**: Does not cascade delete bookings. Existing assigned bookings retain driver information.

---

### Driver Assignment

#### Assign Driver to Booking

**Endpoint**: `POST /bookings/{bookingId}/assign-driver`  
**Auth**: `StaffOnly` (admin or dispatcher)

**Request**:
```json
{
  "driverId": "driver-abc-123"
}
```

**Success Response** (200 OK):
```json
{
  "bookingId": "booking-xyz",
  "assignedDriverId": "driver-abc-123",
  "assignedDriverName": "Charlie Johnson",
  "assignedDriverUid": "driver-001",
  "status": "Scheduled",
  "message": "Driver assigned successfully"
}
```

**Error Response** (400 Bad Request - No UserUid):
```json
{
  "error": "Cannot assign driver without a UserUid. Please link the driver to an AuthServer user first.",
  "driverId": "driver-abc-123",
  "driverName": "Charlie Johnson"
}
```

**Implementation**:
```csharp
app.MapPost("/bookings/{bookingId}/assign-driver", async (
    string bookingId,
    [FromBody] DriverAssignmentRequest request,
    IBookingRepository bookingRepo,
    IDriverRepository driverRepo,
    IAffiliateRepository affiliateRepo,
    IEmailSender email) =>
{
    // Validate booking exists
    var booking = await bookingRepo.GetAsync(bookingId);
    if (booking is null)
        return Results.NotFound(new { error = "Booking not found" });

    // Validate driver exists
    var driver = await driverRepo.GetByIdAsync(request.DriverId);
    if (driver is null)
        return Results.NotFound(new { error = "Driver not found" });

    // CRITICAL: Validate driver has UserUid for DriverApp authentication
    if (string.IsNullOrWhiteSpace(driver.UserUid))
    {
        return Results.BadRequest(new 
        { 
            error = "Cannot assign driver without a UserUid. Please link the driver to an AuthServer user first.",
            driverId = driver.Id,
            driverName = driver.Name
        });
    }

    // Get affiliate for email notification
    var affiliate = await affiliateRepo.GetByIdAsync(driver.AffiliateId);

    // Update booking with driver assignment
    await bookingRepo.UpdateDriverAssignmentAsync(
        bookingId,
        driver.Id,
        driver.UserUid, // ? This enables DriverApp access
        driver.Name);

    // Send email notification to affiliate
    await email.SendDriverAssignmentAsync(booking, driver, affiliate);

    return Results.Ok(new
    {
        bookingId,
        assignedDriverId = driver.Id,
        assignedDriverName = driver.Name,
        assignedDriverUid = driver.UserUid,
        status = "Scheduled",
        message = "Driver assigned successfully"
    });
})
.WithName("AssignDriver")
.RequireAuthorization("StaffOnly");
```

**What Happens**:
1. ? Booking status ? `Scheduled`
2. ? `CurrentRideStatus` ? `Scheduled`
3. ? Email sent to affiliate company
4. ? Driver can now see ride in DriverApp

---

### DriverApp Endpoints

#### Get Today's Rides

**Endpoint**: `GET /driver/rides/today`  
**Auth**: `DriverOnly`  
**Headers**: `X-Timezone-Id: {timezone}` (recommended)

**Response** (200 OK):
```json
[
  {
    "id": "ride-abc",
    "pickupDateTime": "2024-12-24T15:00:00Z", // Backward compatibility
    "pickupDateTimeOffset": "2024-12-24T09:00:00-06:00", // Timezone-aware
    "pickupLocation": "O'Hare Airport",
    "dropoffLocation": "Downtown Chicago",
    "passengerName": "Jane Doe",
    "passengerPhone": "312-555-1234",
    "status": "Scheduled"
  }
]
```

**Filtering Logic**:
```csharp
// Extract driver UID from JWT
var driverUid = context.User.FindFirst("uid")?.Value;

// Get driver's timezone from header
var driverTz = GetRequestTimeZone(context);
var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, driverTz);
var tomorrowLocal = nowLocal.AddHours(24);

// Filter rides
var driverRides = bookings
    .Where(b => b.AssignedDriverUid == driverUid
                && b.PickupDateTime >= nowLocal
                && b.PickupDateTime <= tomorrowLocal
                && b.CurrentRideStatus != RideStatus.Completed
                && b.CurrentRideStatus != RideStatus.Cancelled)
    .OrderBy(b => b.PickupDateTime);
```

---

#### Get Ride Detail

**Endpoint**: `GET /driver/rides/{id}`  
**Auth**: `DriverOnly`  
**Headers**: `X-Timezone-Id: {timezone}` (recommended)

**Response** (200 OK):
```json
{
  "id": "ride-abc",
  "pickupDateTime": "2024-12-24T15:00:00Z",
  "pickupDateTimeOffset": "2024-12-24T09:00:00-06:00",
  "pickupLocation": "O'Hare FBO",
  "pickupStyle": "MeetAndGreet",
  "pickupSignText": "DOE / Bellwood",
  "dropoffLocation": "Downtown Chicago",
  "passengerName": "Jane Doe",
  "passengerPhone": "312-555-1234",
  "passengerCount": 2,
  "checkedBags": 2,
  "carryOnBags": 1,
  "vehicleClass": "Sedan",
  "outboundFlight": "UA123",
  "additionalRequest": "Call on arrival",
  "status": "Scheduled"
}
```

**Ownership Verification**:
```csharp
// Verify driver owns this ride
if (booking.AssignedDriverUid != driverUid)
    return Results.Forbid();
```

---

#### Update Ride Status

**Endpoint**: `POST /driver/rides/{id}/status`  
**Auth**: `DriverOnly`

**Request**:
```json
{
  "newStatus": "OnRoute"
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "rideId": "ride-abc",
  "newStatus": "OnRoute",
  "bookingStatus": "Scheduled",
  "timestamp": "2024-12-23T15:30:00Z"
}
```

**FSM Validation**:
```csharp
var allowedTransitions = new Dictionary<RideStatus, RideStatus[]>
{
    [RideStatus.Scheduled] = new[] { RideStatus.OnRoute, RideStatus.Cancelled },
    [RideStatus.OnRoute] = new[] { RideStatus.Arrived, RideStatus.Cancelled },
    [RideStatus.Arrived] = new[] { RideStatus.PassengerOnboard, RideStatus.Cancelled },
    [RideStatus.PassengerOnboard] = new[] { RideStatus.Completed, RideStatus.Cancelled },
    [RideStatus.Completed] = Array.Empty<RideStatus>(),
    [RideStatus.Cancelled] = Array.Empty<RideStatus>()
};

// Validate transition
if (!allowedTransitions[currentStatus].Contains(request.NewStatus))
{
    return Results.BadRequest(new {
        error = $"Invalid status transition from {currentStatus} to {request.NewStatus}"
    });
}
```

**Side Effects**:
1. ? Updates `CurrentRideStatus`
2. ? Syncs `BookingStatus` (e.g., PassengerOnboard ? InProgress)
3. ? Broadcasts `RideStatusChanged` event via SignalR
4. ? Cleans up location data if `Completed` or `Cancelled`

---

## ?? Testing

### Seeding Test Data

**Script**: `Scripts/Seed-Affiliates.ps1`

```powershell
# Seed 2 affiliates with 3 drivers
.\Scripts\Seed-Affiliates.ps1

# Result:
# - Chicago Limo Service (2 drivers)
#   - Charlie Johnson (driver-001)
#   - Sarah Lee (driver-002)
# - Suburban Chauffeurs (1 driver)
#   - Robert Brown (driver-003)
```

**Manual Seed**:
```bash
# Create affiliate
curl -X POST https://localhost:5206/affiliates \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Limo Inc",
    "email": "test@limo.com"
  }'

# Create driver
curl -X POST https://localhost:5206/affiliates/{affiliateId}/drivers \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Driver",
    "phone": "555-1234",
    "userUid": "driver-test-001"
  }'
```

---

### Driver Assignment Workflow

**1. Create AuthServer User** (in AuthServer project):
```bash
curl -X POST https://localhost:5001/api/admin/users \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "newdriver",
    "password": "password",
    "role": "driver"
  }'

# Response: { "userId": "guid...", "uid": "driver-004" }
```

**2. Create Driver Profile** (in AdminAPI):
```bash
curl -X POST https://localhost:5206/affiliates/{affiliateId}/drivers \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "New Driver",
    "phone": "555-9999",
    "userUid": "driver-004"
  }'

# Response: { "id": "driver-xyz", "userUid": "driver-004" }
```

**3. Assign to Booking**:
```bash
curl -X POST https://localhost:5206/bookings/{bookingId}/assign-driver \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "driverId": "driver-xyz"
  }'

# Response: { "assignedDriverUid": "driver-004", "message": "Driver assigned successfully" }
```

**4. Driver Logs In** (DriverApp):
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "newdriver",
    "password": "password"
  }'

# Response: { "accessToken": "eyJ..." } // JWT with uid=driver-004
```

**5. Driver Gets Assigned Rides**:
```bash
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {driverToken}" \
  -H "X-Timezone-Id: America/Chicago"

# Response: [ { "id": "{bookingId}", ... } ]
```

---

## ?? Troubleshooting

### Issue 1: Cannot Assign Driver (No UserUid)

**Symptom**: API returns 400 Bad Request with message:
```json
{
  "error": "Cannot assign driver without a UserUid. Please link the driver to an AuthServer user first.",
  "driverId": "driver-abc",
  "driverName": "Charlie Johnson"
}
```

**Cause**: Driver profile created without `userUid` field

**Fix**: Update driver with UserUid:
```bash
curl -X PUT https://localhost:5206/drivers/{driverId} \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Charlie Johnson",
    "phone": "312-555-0001",
    "userUid": "driver-001"
  }'
```

---

### Issue 2: Driver Sees No Rides in DriverApp

**Symptom**: DriverApp shows empty list despite assigned rides

**Possible Causes**:

**1. UserUid Mismatch**:
```bash
# Check JWT token
# Decode token payload (base64url decode)
# Look for "uid" claim

# Check driver profile
curl -X GET https://localhost:5206/drivers/by-uid/{uid} \
  -H "Authorization: Bearer {adminToken}"

# Verify uid matches driver profile's userUid
```

**2. Booking Not Assigned**:
```bash
# Check booking
curl -X GET https://localhost:5206/bookings/{bookingId} \
  -H "Authorization: Bearer {adminToken}"

# Look for: "assignedDriverUid": "driver-001"
```

**3. Timezone Issue** (see `12-Timezone-Support.md`)

---

### Issue 3: Duplicate UserUid Error

**Symptom**: API returns 400 Bad Request:
```json
{
  "error": "UserUid 'driver-001' is already assigned to another driver"
}
```

**Cause**: Trying to assign same UserUid to multiple drivers

**Fix**: Use unique UserUid for each driver (AuthServer generates unique uids)

**Check Existing Drivers**:
```bash
curl -X GET https://localhost:5206/drivers/list \
  -H "Authorization: Bearer {adminToken}" \
  | jq '.[] | {id, name, userUid}'
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system integration
- `10-Real-Time-Tracking.md` - GPS tracking implementation
- `11-User-Access-Control.md` - RBAC & authorization
- `12-Timezone-Support.md` - Timezone handling
- `20-API-Reference.md` - Complete endpoint documentation
- `32-Troubleshooting.md` - Common issues & solutions

---

## ?? Future Enhancements

### Phase 3+ Roadmap

1. **Driver Availability Tracking**:
   ```csharp
   public class Driver
   {
       // Existing fields...
       
       // Phase 3: Availability
       public List<AvailabilityWindow>? Availability { get; set; }
       public bool IsCurrentlyAvailable { get; set; }
   }
   
   public class AvailabilityWindow
   {
       public DayOfWeek DayOfWeek { get; set; }
       public TimeSpan StartTime { get; set; }
       public TimeSpan EndTime { get; set; }
   }
   ```

2. **Auto-Assignment Algorithm**:
   - Match driver based on location, availability, vehicle class
   - Load balancing (distribute rides evenly)
   - Priority system (VIP drivers for premium rides)

3. **Driver Performance Metrics**:
   ```csharp
   public class DriverMetrics
   {
       public string DriverId { get; set; }
       public int TotalRides { get; set; }
       public int CompletedRides { get; set; }
       public int CancelledRides { get; set; }
       public double AverageRating { get; set; }
       public TimeSpan AveragePickupTime { get; set; }
   }
   ```

4. **Multi-Vehicle Support**:
   ```csharp
   public class Driver
   {
       // Existing fields...
       
       // Phase 3: Multiple vehicles
       public List<Vehicle>? Vehicles { get; set; }
   }
   
   public class Vehicle
   {
       public string Id { get; set; }
       public string Make { get; set; }
       public string Model { get; set; }
       public int Year { get; set; }
       public string LicensePlate { get; set; }
       public string VehicleClass { get; set; } // Sedan, SUV, S-Class, etc.
       public int Capacity { get; set; }
   }
   ```

5. **Affiliate Commission Tracking**:
   - Track revenue per affiliate
   - Automatic commission calculations
   - Monthly statements

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Next Steps**: See Phase 3 Roadmap above
