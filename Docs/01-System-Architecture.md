# System Architecture & Integration

**Document Type**: Living Document  
**Last Updated**: January 14, 2026  
**Status**: ? Current

---

## ?? Overview

The Bellwood Global platform consists of five interconnected components that work together to manage bookings, driver assignments, and ride execution:

| Component | Purpose | Technology |
|-----------|---------|------------|
| **AuthServer** | JWT authentication & user management | ASP.NET Core 8 + Identity + SQLite |
| **AdminAPI** | Core business logic & data storage | ASP.NET Core 8 Minimal APIs + JSON files |
| **AdminPortal** | Staff web interface | Blazor Server |
| **PassengerApp** | Customer mobile app | .NET MAUI |
| **DriverApp** | Driver mobile app | .NET MAUI |

---

## ?? The Critical Link: UserUid

The entire driver assignment system hinges on a single field: **`UserUid`**

This field creates the bridge between:
- **AuthServer**: User identity (stored as `uid` claim)
- **AdminAPI**: Driver record (`Driver.UserUid`)
- **Bookings**: Assignment link (`BookingRecord.AssignedDriverUid`)

```
????????????????????????????????????????????????????????????????
?                          UserUid Flow                         ?
????????????????????????????????????????????????????????????????
?                                                               ?
?  AuthServer              AdminAPI               DriverApp     ?
?  ??????????              ????????               ?????????    ?
?                                                               ?
?  User: charlie          Driver:                JWT Token:     ?
?  uid claim: "driver-001" UserUid: "driver-001" uid: "driver-001" ?
?       ?                      ?                      ?         ?
?       ?                      ?                      ?         ?
?       ?                 Booking:                    ?         ?
?       ?                 AssignedDriverUid:          ?         ?
?       ?                 "driver-001"                ?         ?
?       ?                      ?                      ?         ?
?       ???????????????????????????????????????????????         ?
?                              ?                                ?
?                  MATCH! Driver sees ride                      ?
?                                                               ?
????????????????????????????????????????????????????????????????
```

---

## ?? Component Details

### 1. AuthServer (BellwoodAuthServer)

**Role**: Issues JWT tokens with identity claims for all applications

**Key Endpoints**:
| Endpoint | Purpose |
|----------|---------|
| `POST /login` | JSON login for mobile apps |
| `POST /connect/token` | OAuth2-style token endpoint |
| `POST /api/admin/users/drivers` | Create driver user with uid |
| `PUT /api/admin/users/{username}/uid` | Update user's uid claim |
| `GET /api/admin/users/by-uid/{userUid}` | Find user by uid |

**JWT Structure**:
```json
{
  "sub": "charlie",
  "uid": "driver-001",
  "userId": "bfdb90a8-4e2b-4d97-bfb4-20eae23b6808",
  "role": "driver",
  "email": "charlie@example.com",
  "exp": 1234567890
}
```

**Critical Configuration**:
- Signing Key: `super-long-jwt-signing-secret-1234` (must match AdminAPI)
- Token Expiration: 1 hour
- Custom `uid` claim: Overrides default Identity ID when set

---

### 2. AdminAPI (Bellwood.AdminApi)

**Role**: Central business logic, data storage, and API for all clients

**Key Endpoints**:

#### Booking Management
| Endpoint | Purpose |
|----------|---------|
| `GET /bookings/list` | List bookings (includes ownership filtering) |
| `GET /bookings/{id}` | Get booking detail (ownership verified) |
| `POST /bookings/{id}/assign-driver` | Assign driver (requires driver to have `UserUid`) |
| `POST /bookings/{id}/cancel` | Cancel booking (ownership verified) |

#### Driver Management
| Endpoint | Purpose |
|----------|---------|
| `POST /affiliates/{id}/drivers` | Create driver (accepts `UserUid`) |
| `PUT /drivers/{id}` | Update driver (validates `UserUid` uniqueness) |
| `GET /drivers/list` | List all drivers |
| `GET /drivers/by-uid/{userUid}` | Find driver by AuthServer UID |

#### Driver App Endpoints
| Endpoint | Authorization | Purpose |
|----------|---------------|---------|
| `GET /driver/rides/today` | DriverOnly | Get rides for authenticated driver |
| `GET /driver/rides/{id}` | DriverOnly | Get ride detail (ownership verified) |
| `POST /driver/rides/{id}/status` | DriverOnly | Update ride status |
| `POST /driver/location/update` | DriverOnly | Submit GPS location update |

**Data Models**:

```csharp
// Driver entity
public class Driver
{
    public string Id { get; set; }           // GUID - internal reference
    public string AffiliateId { get; set; }  // Links to affiliate
    public string Name { get; set; }
    public string Phone { get; set; }
    public string? UserUid { get; set; }     // Links to AuthServer identity
}

// Booking record
public class BookingRecord
{
    public string Id { get; set; }
    public string? AssignedDriverId { get; set; }    // Links to Driver.Id
    public string? AssignedDriverUid { get; set; }   // Links to AuthServer uid
    public string? AssignedDriverName { get; set; }
    public string? CreatedByUserId { get; set; }     // Phase 1: Ownership tracking
    // ... other fields
}
```

**Storage**:
- `App_Data/drivers.json` - Dedicated driver storage (scalable)
- `App_Data/affiliates.json` - Affiliate data (drivers not nested)
- `App_Data/bookings.json` - Booking records with ownership
- `App_Data/quotes.json` - Quote records with ownership

---

### 3. AdminPortal (Bellwood.AdminPortal)

**Role**: Web interface for Bellwood staff to manage affiliates, drivers, and bookings

**Key Features**:

#### Driver Creation (`AffiliateDetail.razor`)
- Input field for `UserUid` with helper text
- Validates and sends `userUid` to AdminAPI
- Shows "Linked" / "Not linked" badges for drivers

#### Driver Assignment (`BookingDetail.razor`)
- Lists available drivers with link status badges
- Shows warning when assigning unlinked driver
- Displays `AssignedDriverUid` for debugging

**Models**:
```csharp
public class DriverDto
{
    public string Id { get; set; }
    public string AffiliateId { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string? UserUid { get; set; }  // For AuthServer linking
}
```

---

### 4. PassengerApp (BellwoodGlobal.Mobile)

**Role**: Customer-facing mobile app for booking rides and tracking

**Relevant Features**:
- Views booking list and details
- Sees assigned driver name (privacy-conscious - no contact info)
- Can track driver location for own bookings
- DEBUG mode shows `AssignedDriverUid` for troubleshooting

**Models**:
```csharp
public class BookingListItem
{
    // ... other fields
    public string? AssignedDriverId { get; set; }
    public string? AssignedDriverUid { get; set; }  // For debug visibility
    public string? AssignedDriverName { get; set; }
    public string? CurrentRideStatus { get; set; }  // Real-time driver status
}
```

---

### 5. DriverApp (BellwoodGlobal.DriverApp)

**Role**: Driver-facing mobile app for viewing and executing assigned rides

**Authentication Flow**:
1. Driver logs in with credentials (e.g., `charlie` / `password`)
2. AuthServer returns JWT with `uid` claim (e.g., `driver-001`)
3. App stores JWT for API calls
4. App sends `X-Timezone-Id` header with all requests

**API Calls**:
1. `GET /driver/rides/today` - Fetches rides where `AssignedDriverUid` matches JWT `uid`
2. `GET /driver/rides/{id}` - Gets ride details (ownership verified by API)
3. `POST /driver/rides/{id}/status` - Updates ride status
4. `POST /driver/location/update` - Sends GPS coordinates

**Why It Works**:
- JWT contains `uid` claim matching driver's `UserUid`
- API filters bookings by `AssignedDriverUid`
- Ownership verified on every request

---

## ?? Complete System Flows

### Flow 1: Creating a Driver with AuthServer Link

```
???????????????????                              ?????????????????
?   AdminPortal   ?                              ?    AdminAPI   ?
?                 ?  POST /affiliates/{id}/      ?               ?
? 1. Enter driver ???????????drivers?????????????? 2. Validate   ?
?    details      ?  { name, phone,              ?    UserUid    ?
?    + UserUid    ?    userUid: "driver-001" }   ?    uniqueness ?
?                 ?                              ?               ?
?                 ????????????????????????????????? 3. Store in   ?
? 4. Show success ?  { id, name, phone,          ?    drivers.json?
?    + link badge ?    userUid: "driver-001" }   ?               ?
???????????????????                              ?????????????????
         ?
         ? (Separate step - may be automated in future)
         ?
???????????????????
?   AuthServer    ?
?                 ?
? POST /api/admin/?
? users/drivers   ?
? { username,     ?
?   password,     ?
?   userUid:      ?
?   "driver-001" }?
?                 ?
? Creates user    ?
? with uid claim  ?
???????????????????
```

### Flow 2: Assigning Driver to Booking

```
???????????????????                              ?????????????????
?   AdminPortal   ?                              ?    AdminAPI   ?
?                 ?                              ?               ?
? 1. Select       ?  POST /bookings/{id}/        ? 2. Lookup     ?
?    driver from  ???????assign-driver????????????    driver     ?
?    list         ?  { driverId: "abc123" }      ?               ?
?                 ?                              ? 3. Check      ?
?                 ?                              ?    UserUid    ?
?                 ?                              ?    exists     ?
?                 ?                              ?               ?
?                 ?                              ? 4. If no      ?
?                 ????????????????????????????????    UserUid:   ?
? Show error:     ?  { error: "Cannot assign     ?    return 400 ?
? "Driver not     ?    driver without UserUid" } ?               ?
?  linked"        ?                              ?               ?
?                 ?                              ? 5. If has     ?
?                 ?                              ?    UserUid:   ?
?                 ????????????????????????????????    Update     ?
? Show success    ?  { assignedDriverUid:        ?    booking    ?
?                 ?    "driver-001" }            ?               ?
???????????????????                              ?????????????????
```

### Flow 3: Driver Viewing Assigned Rides

```
???????????????????       POST /login            ?????????????????
?    DriverApp    ? ???????????????????????????????   AuthServer  ?
?                 ?   { charlie, password }      ?               ?
? 1. Login        ?                              ? 2. Validate   ?
?                 ????????????????????????????????    credentials?
? 3. Store JWT    ?   { accessToken: JWT with   ?               ?
?                 ?     uid="driver-001" }       ? 3. Issue JWT  ?
?                 ?                              ?    with uid   ?
?                 ?                              ?    claim      ?
???????????????????                              ?????????????????
         ?
         ? GET /driver/rides/today
         ? Authorization: Bearer <jwt>
         ?
???????????????????
?    AdminAPI     ?
?                 ?
? 4. Extract uid  ?
?    from JWT     ?
?    ("driver-001")?
?                 ?
? 5. Query:       ?
?    SELECT *     ?
?    FROM bookings?
?    WHERE        ?
?    AssignedDriver?
?    Uid =        ?
?    "driver-001" ?
?                 ?
? 6. Return rides ?
???????????????????
         ?
         ?
???????????????????
?    DriverApp    ?
?                 ?
? 7. Display      ?
?    assigned     ?
?    rides        ?
???????????????????
```

### Flow 4: Passenger Viewing Booking (Phase 1 - Ownership)

```
???????????????????       GET /bookings/list      ?????????????????
?  PassengerApp   ? ???????????????????????????????    AdminAPI   ?
?                 ?   Authorization: Bearer        ?               ?
?                 ?   X-Timezone-Id: America/NY    ? 1. Extract    ?
?                 ?                                ?    userId     ?
?                 ?                                ?    from JWT   ?
?                 ?                                ?               ?
?                 ?                                ? 2. Filter:    ?
?                 ?                                ?    WHERE      ?
?                 ?                                ?    CreatedBy  ?
?                 ?                                ?    UserId =   ?
?                 ?                                ?    <user>     ?
?                 ?                                ?               ?
?                 ???????????????????????????????? 3. Return     ?
?                 ?   [ bookings owned by user ]   ?    filtered   ?
?                 ?                                ?    list       ?
?                 ?                                ?               ?
? Display:        ?                                ?               ?
? - Only OWN      ?                                ?               ?
?   bookings      ?                                ?               ?
???????????????????                                ?????????????????
```

---

## ?? Data Alignment Checklist

### AuthServer ? AdminAPI

| AuthServer | AdminAPI | Purpose |
|------------|----------|---------|
| User `uid` claim | `Driver.UserUid` | Identity linking |
| JWT signing key | JWT validation key | Token verification |
| `role: driver` claim | `DriverOnly` policy | Authorization |
| `userId` claim | `CreatedByUserId` field | Ownership tracking |

### AdminAPI ? AdminPortal

| AdminAPI Response | AdminPortal Model | Purpose |
|-------------------|-------------------|---------|
| `Driver.UserUid` | `DriverDto.UserUid` | Display link status |
| `Booking.AssignedDriverId` | `BookingInfo.AssignedDriverId` | Driver reference |
| `Booking.AssignedDriverUid` | `BookingInfo.AssignedDriverUid` | Debug visibility |
| `Booking.CreatedByUserId` | Not displayed | Ownership enforcement |

### AdminAPI ? PassengerApp

| AdminAPI Response | PassengerApp Model | Purpose |
|-------------------|-------------------|---------|
| `Booking.AssignedDriverId` | `BookingListItem.AssignedDriverId` | Driver reference |
| `Booking.AssignedDriverUid` | `BookingListItem.AssignedDriverUid` | Debug visibility |
| `Booking.AssignedDriverName` | `BookingListItem.AssignedDriverName` | Display to user |
| `Booking.CreatedByUserId` | Not exposed | Ownership filtering |

### AdminAPI ? DriverApp

| AdminAPI | DriverApp | Purpose |
|----------|-----------|---------|
| Filters by `AssignedDriverUid` | Sends JWT with `uid` | Ride ownership |
| `DriverOnly` policy | JWT with `role: driver` | Authorization |
| Timezone conversion | Sends `X-Timezone-Id` header | Correct pickup times |

---

## ?? Scalability Design

### GUID-Based Identifiers

All entities use GUID-based IDs instead of sequential codes:

| Entity | ID Format | Example |
|--------|-----------|---------|
| Affiliate | GUID (32 hex) | `a1b2c3d4e5f67890...` |
| Driver | GUID (32 hex) | `f0e1d2c3b4a59687...` |
| Booking | GUID (32 hex) | `0123456789abcdef...` |
| Quote | GUID (32 hex) | `0123456789abcdef...` |
| UserUid | GUID or custom | `driver-001` (test) or GUID (prod) |

### Separate Storage

Drivers are stored in dedicated `drivers.json` file:
- Supports thousands of drivers
- Efficient lookup by `UserUid`
- No nested data in affiliates
- Thread-safe with `SemaphoreSlim`

### Uniqueness Enforcement

| Field | Scope | Enforcement |
|-------|-------|-------------|
| `Driver.Id` | Global | Auto-generated GUID |
| `Driver.UserUid` | Global | Validated on create/update |
| `AuthServer uid` | Global | One user per uid claim |
| `Booking.Id` | Global | Auto-generated GUID |
| `Quote.Id` | Global | Auto-generated GUID |

---

## ?? Configuration Requirements

### Shared JWT Configuration

All components must use the same JWT signing key:

```json
// AuthServer appsettings.json
{
  "Jwt": {
    "Key": "super-long-jwt-signing-secret-1234"
  }
}

// AdminAPI appsettings.json
{
  "Jwt": {
    "Key": "super-long-jwt-signing-secret-1234"
  }
}
```

### API URLs

| Component | Default URL | Used By |
|-----------|-------------|---------|
| AuthServer | `https://localhost:5001` | All apps for login |
| AdminAPI | `https://localhost:5206` | Portal, PassengerApp, DriverApp |

### Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment | `Development` |
| `ASPNETCORE_URLS` | Listening URLs | `https://localhost:5206` |
| `Jwt__Key` | JWT signing key | (see config) |

---

## ?? Test Data

### Seeded AuthServer Users

| Username | Password | Role | UID Claim | UserID |
|----------|----------|------|-----------|--------|
| alice | password | admin | (GUID) | `bfdb90a8-4e2b-...` |
| chris | password | booker | (GUID) | `fbaf1dc3-9c0a-...` |
| charlie | password | driver | `driver-001` | (GUID) |

### Seeded AdminAPI Drivers (via `/dev/seed-affiliates`)

| Name | Phone | UserUid | Affiliate |
|------|-------|---------|-----------|
| Charlie Johnson | 312-555-0001 | `driver-001` | Chicago Limo Service |
| Sarah Lee | 312-555-0002 | `driver-002` | Chicago Limo Service |
| Robert Brown | 847-555-1000 | `driver-003` | Suburban Chauffeurs |

### Test Scenario

1. Login as `charlie` (password: `password`) in DriverApp
2. JWT will contain `uid: "driver-001"`
3. Any bookings assigned to driver "Charlie Johnson" will appear
4. Because `Driver.UserUid = "driver-001"` matches JWT `uid`

---

## ?? Summary

The Bellwood system achieves seamless integration through:

1. ? **AuthServer** user identity (`uid` claim in JWT)
2. ? **AdminAPI** driver records (`Driver.UserUid`)
3. ? **Bookings** (`BookingRecord.AssignedDriverUid`)
4. ? **Ownership** (`BookingRecord.CreatedByUserId`) - Phase 1

**Key Validations**:
- `UserUid` uniqueness enforced on driver create/update
- Driver assignment requires `UserUid` (400 error if missing)
- JWT `uid` claim matches booking filter criteria
- JWT `userId` claim enforces ownership filtering

**Result**: When a driver logs in, they see exactly the rides assigned to them, and the system prevents silent failures by requiring the identity link before assignment. Passengers see only their own bookings, and admins see everything.

---

**Last Updated**: January 14, 2026  
**Related Documents**:
- `11-User-Access-Control.md` - Phase 1 ownership & RBAC
- `13-Driver-Integration.md` - Driver endpoints & assignment
- `20-API-Reference.md` - Complete endpoint documentation
- `23-Security-Model.md` - JWT & authorization details
