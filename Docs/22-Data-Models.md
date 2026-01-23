# Data Models & Entities

**Document Type**: Living Document - Technical Reference  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides complete entity and DTO (Data Transfer Object) documentation for the Bellwood AdminAPI, including field descriptions, relationships, and usage patterns.

**Storage**: JSON files in `App_Data/` directory

---

## ??? Core Entities

### QuoteRecord

**File**: `Models/QuoteRecord.cs`  
**Storage**: `App_Data/quotes.json`

**Purpose**: Represents a quote request submitted by a customer.

```csharp
public sealed class QuoteRecord
{
    public string Id { get; set; }                // Auto-generated GUID
    public DateTime CreatedUtc { get; set; }      // Submission timestamp (UTC)
    public QuoteStatus Status { get; set; }       // Quote workflow status
    
    // Phase 1: Ownership & Audit Trail
    public string? CreatedByUserId { get; set; }  // User who created (uid claim)
    public string? ModifiedByUserId { get; set; } // User who last modified
    public DateTime? ModifiedOnUtc { get; set; }  // Last modification timestamp
    
    // Flattened fields for list views
    public string BookerName { get; set; }        // Display name
    public string PassengerName { get; set; }     // Display name
    public string VehicleClass { get; set; }      // "Sedan", "SUV", etc.
    public string PickupLocation { get; set; }    // Short description
    public string? DropoffLocation { get; set; }  // Short description
    public DateTime PickupDateTime { get; set; }  // Requested pickup time
    
    // Full payload for detail view
    public QuoteDraft Draft { get; set; }         // Complete form data
}
```

**QuoteStatus Enum**:
```csharp
public enum QuoteStatus
{
    Submitted = 0,  // Initial state
    InReview = 1,   // Staff is reviewing
    Priced = 2,     // Price calculated
    Sent = 3,       // Quote sent to customer
    Closed = 4,     // Customer accepted/completed
    Rejected = 5    // Quote declined
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier (GUID, no hyphens) |
| `CreatedUtc` | DateTime | Yes | When quote was submitted (UTC) |
| `Status` | QuoteStatus | Yes | Current workflow status |
| `CreatedByUserId` | string? | No | User who created (Phase 1 ownership) |
| `ModifiedByUserId` | string? | No | User who last modified (audit trail) |
| `ModifiedOnUtc` | DateTime? | No | Last modification time (audit trail) |
| `BookerName` | string | Yes | Person requesting quote (flattened) |
| `PassengerName` | string | Yes | Person traveling (flattened) |
| `VehicleClass` | string | Yes | Requested vehicle type |
| `PickupLocation` | string | Yes | Pickup address/location |
| `DropoffLocation` | string? | No | Destination (null for "as directed") |
| `PickupDateTime` | DateTime | Yes | Requested pickup time |
| `Draft` | QuoteDraft | Yes | Complete form submission |

**Relationships**:
- None (quotes are standalone before conversion to bookings)

---

### BookingRecord

**File**: `Models/BookingRecord.cs`  
**Storage**: `App_Data/bookings.json`

**Purpose**: Represents a confirmed booking with lifecycle tracking.

```csharp
public sealed class BookingRecord
{
    public string Id { get; set; }                   // Auto-generated GUID
    public DateTime CreatedUtc { get; set; }         // Creation timestamp (UTC)
    public BookingStatus Status { get; set; }        // Public-facing status
    public DateTime? CancelledAt { get; set; }       // Cancellation timestamp
    
    // Phase 1: Ownership & Audit Trail
    public string? CreatedByUserId { get; set; }     // User who created
    public string? ModifiedByUserId { get; set; }    // User who last modified
    public DateTime? ModifiedOnUtc { get; set; }     // Last modification time
    
    // Driver assignment
    public string? AssignedDriverId { get; set; }    // Links to Driver.Id
    public string? AssignedDriverUid { get; set; }   // Links to AuthServer user
    public string? AssignedDriverName { get; set; }  // Cached display name
    public RideStatus? CurrentRideStatus { get; set; } // Driver-facing status
    
    // Flattened fields for list views
    public string BookerName { get; set; }           // Display name
    public string PassengerName { get; set; }        // Display name
    public string VehicleClass { get; set; }         // Vehicle type
    public string PickupLocation { get; set; }       // Short description
    public string? DropoffLocation { get; set; }     // Short description
    public DateTime PickupDateTime { get; set; }     // Scheduled pickup time
    
    // Full payload for detail view
    public QuoteDraft Draft { get; set; }            // Complete booking details
}
```

**BookingStatus Enum** (Public-Facing):
```csharp
public enum BookingStatus
{
    Requested = 0,   // Initial state from mobile app
    Confirmed = 1,   // Bellwood staff approved
    Scheduled = 2,   // Driver/vehicle assigned
    InProgress = 3,  // Ride started
    Completed = 4,   // Ride finished
    Cancelled = 5,   // User or staff cancelled
    NoShow = 6       // Passenger didn't show up
}
```

**RideStatus Enum** (Driver-Facing):
```csharp
public enum RideStatus
{
    Scheduled = 0,       // Booking assigned but driver hasn't moved
    OnRoute = 1,         // Driver heading to pickup
    Arrived = 2,         // Driver at pickup location
    PassengerOnboard = 3,// Passenger picked up, ride in progress
    Completed = 4,       // Ride finished successfully
    Cancelled = 5        // Ride cancelled
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier (GUID) |
| `CreatedUtc` | DateTime | Yes | Creation timestamp (UTC) |
| `Status` | BookingStatus | Yes | Public-facing workflow status |
| `CancelledAt` | DateTime? | No | When booking was cancelled |
| `CreatedByUserId` | string? | No | User who created (ownership) |
| `ModifiedByUserId` | string? | No | Last modifier (audit trail) |
| `ModifiedOnUtc` | DateTime? | No | Last modification time |
| `AssignedDriverId` | string? | No | Driver entity ID |
| `AssignedDriverUid` | string? | No | AuthServer user UID (critical for DriverApp) |
| `AssignedDriverName` | string? | No | Cached driver name |
| `CurrentRideStatus` | RideStatus? | No | Granular driver progress |
| `BookerName` | string | Yes | Person who booked |
| `PassengerName` | string | Yes | Person traveling |
| `VehicleClass` | string | Yes | Vehicle type requested |
| `PickupLocation` | string | Yes | Pickup address |
| `DropoffLocation` | string? | No | Destination |
| `PickupDateTime` | DateTime | Yes | Scheduled pickup time |
| `Draft` | QuoteDraft | Yes | Full booking details |

**Status Synchronization**:

| BookingStatus | CurrentRideStatus | Description |
|---------------|-------------------|-------------|
| `Requested` | null | Awaiting approval |
| `Confirmed` | null | Approved, not assigned |
| `Scheduled` | `Scheduled` | Driver assigned |
| `InProgress` | `OnRoute`, `Arrived`, `PassengerOnboard` | Ride active |
| `Completed` | `Completed` | Ride finished |
| `Cancelled` | `Cancelled` | Ride cancelled |
| `NoShow` | `Cancelled` | Passenger no-show |

**Relationships**:
- ? `Driver` (via `AssignedDriverId` and `AssignedDriverUid`)
- ? `Affiliate` (via `Driver.AffiliateId`)

---

### Affiliate

**File**: `Models/Affiliate.cs`  
**Storage**: `App_Data/affiliates.json`

**Purpose**: Partner company that provides drivers.

```csharp
public sealed class Affiliate
{
    public string Id { get; set; }            // Auto-generated GUID
    public string Name { get; set; }          // Company name
    public string? PointOfContact { get; set; } // Contact person
    public string Phone { get; set; }         // Main phone number
    public string Email { get; set; }         // Contact email
    public string? StreetAddress { get; set; } // Street address
    public string? City { get; set; }         // City
    public string? State { get; set; }        // State/province
    
    // Populated on-demand (not stored in JSON)
    public List<Driver> Drivers { get; set; } // Associated drivers
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier (GUID) |
| `Name` | string | Yes | Company/organization name |
| `PointOfContact` | string? | No | Primary contact person |
| `Phone` | string | Yes | Main phone number |
| `Email` | string | Yes | Contact email (for notifications) |
| `StreetAddress` | string? | No | Street address |
| `City` | string? | No | City |
| `State` | string? | No | State/province code |
| `Drivers` | List<Driver> | Yes | Drivers list (populated from separate storage) |

**Relationships**:
- ? `Driver` (one-to-many, stored separately)

**Note**: `Drivers` list is populated at runtime from `drivers.json`. This design supports thousands of drivers without bloating affiliate records.

---

### Driver

**File**: `Models/Driver.cs`  
**Storage**: `App_Data/drivers.json`

**Purpose**: Individual chauffeur linked to an affiliate and AuthServer user.

```csharp
public sealed class Driver
{
    public string Id { get; set; }            // Auto-generated GUID
    public string AffiliateId { get; set; }   // Foreign key to Affiliate
    public string Name { get; set; }          // Driver name
    public string Phone { get; set; }         // Contact phone
    public string? Email { get; set; }        // Email address
    public string? UserUid { get; set; }      // AuthServer link (critical!)
    public string? LicenseNumber { get; set; } // Driver's license
    public string? VehicleInfo { get; set; }  // Vehicle details
    public bool IsActive { get; set; }        // Active status
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier (GUID) |
| `AffiliateId` | string | Yes | Links to `Affiliate.Id` |
| `Name` | string | Yes | Driver's full name |
| `Phone` | string | Yes | Contact phone number |
| `Email` | string? | No | Email address |
| `UserUid` | string? | **Critical** | Links to AuthServer user (enables DriverApp login) |
| `LicenseNumber` | string? | No | Driver's license number |
| `VehicleInfo` | string? | No | Vehicle make/model/plate |
| `IsActive` | bool | Yes | Whether driver is active (default: true) |

**UserUid Requirement**:
- **Required** for driver assignment to bookings
- **Must match** `uid` claim in JWT token from AuthServer
- **Enables** DriverApp authentication and ride access

**Relationships**:
- ? `Affiliate` (via `AffiliateId`)
- ? `BookingRecord` (via `AssignedDriverUid`)

---

## ?? Shared Models

### QuoteDraft

**File**: `BellwoodGlobal.Mobile.Models/QuoteDraft.cs` (shared library)

**Purpose**: Complete form data submitted by PassengerApp or AdminPortal.

```csharp
public sealed class QuoteDraft
{
    // People
    public Person Booker { get; set; }              // Person requesting quote/booking
    public Person Passenger { get; set; }           // Person traveling
    
    // Trip details
    public string VehicleClass { get; set; }        // "Sedan", "SUV", "S-Class", "Sprinter"
    public DateTime PickupDateTime { get; set; }    // Requested pickup time
    public string PickupLocation { get; set; }      // Pickup address
    public PickupStyle PickupStyle { get; set; }    // Curbside or MeetAndGreet
    public string? PickupSignText { get; set; }     // Sign text for meet & greet
    public bool AsDirected { get; set; }            // True if no dropoff location
    public string? DropoffLocation { get; set; }    // Destination (null if AsDirected)
    public bool RoundTrip { get; set; }             // Round trip flag
    
    // Passenger details
    public int PassengerCount { get; set; }         // Number of passengers
    public int? CheckedBags { get; set; }           // Checked luggage count
    public int? CarryOnBags { get; set; }           // Carry-on luggage count
    
    // Flight information
    public string? OutboundFlight { get; set; }     // Flight number (if pickup from airport)
    public string? ReturnFlight { get; set; }       // Return flight (if round trip)
    
    // Additional information
    public string? AdditionalRequest { get; set; }  // Special requests/notes
}
```

**PickupStyle Enum**:
```csharp
public enum PickupStyle
{
    Curbside = 0,      // Driver waits curbside
    MeetAndGreet = 1   // Driver meets passenger with sign
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Booker` | Person | Yes | Person requesting the service |
| `Passenger` | Person | Yes | Person traveling (can be same as booker) |
| `VehicleClass` | string | Yes | Requested vehicle type |
| `PickupDateTime` | DateTime | Yes | Requested pickup time |
| `PickupLocation` | string | Yes | Pickup address/location |
| `PickupStyle` | PickupStyle | Yes | Curbside or meet & greet |
| `PickupSignText` | string? | No | Sign text (required if MeetAndGreet) |
| `AsDirected` | bool | Yes | No fixed destination |
| `DropoffLocation` | string? | No | Destination (null if AsDirected) |
| `RoundTrip` | bool | Yes | Round trip booking |
| `PassengerCount` | int | Yes | Number of passengers (1+) |
| `CheckedBags` | int? | No | Checked luggage count |
| `CarryOnBags` | int? | No | Carry-on luggage count |
| `OutboundFlight` | string? | No | Outbound flight number |
| `ReturnFlight` | string? | No | Return flight number |
| `AdditionalRequest` | string? | No | Special requests/notes |

---

### Person

**File**: `BellwoodGlobal.Mobile.Models/Person.cs`

**Purpose**: Represents a person (booker or passenger).

```csharp
public sealed class Person
{
    public string FirstName { get; set; }       // First name
    public string LastName { get; set; }        // Last name
    public string PhoneNumber { get; set; }     // Contact phone
    public string EmailAddress { get; set; }    // Email address
    
    public override string ToString() => $"{FirstName} {LastName}";
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `FirstName` | string | Yes | First name |
| `LastName` | string | Yes | Last name |
| `PhoneNumber` | string | Yes | Contact phone (formatted) |
| `EmailAddress` | string | Yes | Email address (lowercase recommended) |

---

## ?? Location Tracking Models

### LocationUpdate

**File**: `Models/LocationUpdate.cs`

**Purpose**: GPS coordinates submitted by DriverApp.

```csharp
public sealed class LocationUpdate
{
    public string RideId { get; set; }          // Booking ID
    public double Latitude { get; set; }        // GPS latitude (-90 to 90)
    public double Longitude { get; set; }       // GPS longitude (-180 to 180)
    public DateTime Timestamp { get; set; }     // When location was recorded (UTC)
    public double? Heading { get; set; }        // Direction of travel (0-360 degrees)
    public double? Speed { get; set; }          // Speed in meters/second
    public double? Accuracy { get; set; }       // GPS accuracy in meters
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `RideId` | string | Yes | Booking ID being tracked |
| `Latitude` | double | Yes | GPS latitude (decimal degrees) |
| `Longitude` | double | Yes | GPS longitude (decimal degrees) |
| `Timestamp` | DateTime | Yes | When location was recorded (UTC, auto-set) |
| `Heading` | double? | No | Direction of travel (0° = North, 90° = East) |
| `Speed` | double? | No | Speed in meters/second |
| `Accuracy` | double? | No | GPS accuracy radius in meters |

**Validation**:
- `Latitude`: -90 to 90
- `Longitude`: -180 to 180
- `Heading`: 0 to 360 (if provided)
- `Speed`: ? 0 (if provided)
- `Accuracy`: ? 0 (if provided)

**Storage**: In-memory only (1-hour TTL, see `ILocationService`)

---

### LocationEntry

**File**: `Services/ILocationService.cs`

**Purpose**: Location data with metadata for storage.

```csharp
public sealed class LocationEntry
{
    public required LocationUpdate Update { get; init; }  // GPS data
    public required string DriverUid { get; init; }       // Driver who sent update
    public required DateTime StoredAt { get; init; }      // When stored (UTC)
    
    public double AgeSeconds => (DateTime.UtcNow - StoredAt).TotalSeconds;
}
```

---

## ?? OAuth Models (Phase 2)

### OAuthClientCredentials

**File**: `Models/OAuthClientCredentials.cs`  
**Storage**: `App_Data/oauth-credentials.json` (encrypted)

**Purpose**: OAuth2 credentials for LimoAnywhere integration.

```csharp
public sealed class OAuthClientCredentials
{
    public string Id { get; set; } = "default";        // Always "default"
    public string ClientId { get; set; } = "";         // OAuth client ID
    public string ClientSecret { get; set; } = "";     // OAuth client secret (encrypted)
    public string? Description { get; set; }           // Optional description
    public DateTime? LastUpdatedUtc { get; set; }      // Last update timestamp
    public string? LastUpdatedBy { get; set; }         // Admin who updated
}
```

**Security**:
- `ClientSecret` is **encrypted** using ASP.NET Core Data Protection API
- Purpose string: `"Bellwood.OAuthCredentials.v1"`
- Never returned in full via API (masked as "abcd...wxyz")

---

## ?? Response DTOs

### Driver DTOs

**DriverRideListItemDto** (GET /driver/rides/today):

```csharp
public sealed class DriverRideListItemDto
{
    public string Id { get; set; }                          // Booking ID
    public DateTime PickupDateTime { get; set; }            // Original (backward compat)
    public DateTimeOffset PickupDateTimeOffset { get; set; } // Timezone-aware
    public string PickupLocation { get; set; }              // Pickup address
    public string? DropoffLocation { get; set; }            // Destination
    public string PassengerName { get; set; }               // Passenger name
    public string PassengerPhone { get; set; }              // Contact phone
    public RideStatus Status { get; set; }                  // Current ride status
}
```

**DriverRideDetailDto** (GET /driver/rides/{id}):

```csharp
public sealed class DriverRideDetailDto
{
    public string Id { get; set; }                          // Booking ID
    public DateTime PickupDateTime { get; set; }            // Original
    public DateTimeOffset PickupDateTimeOffset { get; set; } // Timezone-aware
    public string PickupLocation { get; set; }              // Full address
    public string PickupStyle { get; set; }                 // "Curbside" or "MeetAndGreet"
    public string? PickupSignText { get; set; }             // Sign text
    public string? DropoffLocation { get; set; }            // Destination
    public string PassengerName { get; set; }               // Passenger name
    public string PassengerPhone { get; set; }              // Contact phone
    public int PassengerCount { get; set; }                 // Number of passengers
    public int CheckedBags { get; set; }                    // Luggage count
    public int CarryOnBags { get; set; }                    // Carry-on count
    public string VehicleClass { get; set; }                // Vehicle type
    public string? OutboundFlight { get; set; }             // Flight number
    public string? AdditionalRequest { get; set; }          // Special requests
    public RideStatus Status { get; set; }                  // Current status
}
```

---

### Location DTOs

**LocationResponse** (GET /driver/location/{rideId}):

```csharp
public sealed class LocationResponse
{
    public string RideId { get; set; }           // Booking ID
    public double Latitude { get; set; }         // GPS latitude
    public double Longitude { get; set; }        // GPS longitude
    public DateTime Timestamp { get; set; }      // When recorded (UTC)
    public double? Heading { get; set; }         // Direction (degrees)
    public double? Speed { get; set; }           // Speed (m/s)
    public double? Accuracy { get; set; }        // GPS accuracy (meters)
    public double AgeSeconds { get; set; }       // How old the data is
    public string? DriverUid { get; set; }       // Driver's UserUid
    public string? DriverName { get; set; }      // Driver's name
}
```

**ActiveRideLocationDto** (GET /admin/locations):

```csharp
public sealed class ActiveRideLocationDto
{
    public string RideId { get; set; }           // Booking ID
    public string DriverUid { get; set; }        // Driver's UserUid
    public string? DriverName { get; set; }      // Driver's name
    public string? PassengerName { get; set; }   // Passenger name
    public string? PickupLocation { get; set; }  // Pickup address
    public string? DropoffLocation { get; set; } // Destination
    public RideStatus? CurrentStatus { get; set; } // Current ride status
    public double Latitude { get; set; }         // GPS latitude
    public double Longitude { get; set; }        // GPS longitude
    public DateTime Timestamp { get; set; }      // When recorded (UTC)
    public double? Heading { get; set; }         // Direction
    public double? Speed { get; set; }           // Speed (m/s)
    public double AgeSeconds { get; set; }       // Data age
}
```

---

### Billing DTOs (Phase 2)

**BookingDetailResponseDto** (GET /bookings/{id}):

```csharp
public sealed class BookingDetailResponseDto
{
    // Existing fields
    public string Id { get; set; }
    public string Status { get; set; }
    public string? CurrentRideStatus { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTimeOffset CreatedDateTimeOffset { get; set; }
    public string BookerName { get; set; }
    public string PassengerName { get; set; }
    public string VehicleClass { get; set; }
    public string PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime PickupDateTime { get; set; }
    public DateTimeOffset PickupDateTimeOffset { get; set; }
    public QuoteDraft Draft { get; set; }
    public string? AssignedDriverId { get; set; }
    public string? AssignedDriverUid { get; set; }
    public string AssignedDriverName { get; set; }
    
    // Phase 2: Billing fields (masked for dispatchers)
    public string? PaymentMethodId { get; set; }      // Stripe payment method ID
    public string? PaymentMethodLast4 { get; set; }   // Last 4 digits of card
    public decimal? PaymentAmount { get; set; }       // Amount charged
    public decimal? TotalAmount { get; set; }         // Total trip cost
    public decimal? TotalFare { get; set; }           // Base fare
}
```

**Note**: Billing fields are `null` for dispatchers (masked via reflection).

**QuoteDetailResponseDto** (GET /quotes/{id}):

```csharp
public sealed class QuoteDetailResponseDto
{
    // Existing fields
    public string Id { get; set; }
    public string Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string BookerName { get; set; }
    public string PassengerName { get; set; }
    public string VehicleClass { get; set; }
    public string PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime PickupDateTime { get; set; }
    public QuoteDraft Draft { get; set; }
    
    // Phase 2: Billing fields (masked for dispatchers)
    public decimal? EstimatedCost { get; set; }       // Estimated price
    public string? BillingNotes { get; set; }         // Internal notes
}
```

---

## ??? Data Relationships

### Entity Relationship Diagram

```
???????????????????
?    Affiliate    ?
?  (affiliates)   ?
???????????????????
         ? 1:N
         ?
???????????????????
?     Driver      ?
?   (drivers)     ?
???????????????????
         ? 1:N (via AssignedDriverUid)
         ?
???????????????????       ???????????????????
?  BookingRecord  ?       ?   QuoteRecord   ?
?   (bookings)    ?       ?    (quotes)     ?
???????????????????       ???????????????????
         ?                        ?
         ? contains               ? contains
         ?                        ?
???????????????????       ???????????????????
?   QuoteDraft    ?????????   QuoteDraft    ?
?   (embedded)    ?       ?   (embedded)    ?
???????????????????       ???????????????????
         ?
         ? contains
         ?
???????????????????
?     Person      ?
?  (Booker &      ?
?   Passenger)    ?
???????????????????
```

### Storage Layout

| File | Entity | Count Estimate | Index Strategy |
|------|--------|----------------|----------------|
| `quotes.json` | QuoteRecord | 1,000-10,000 | In-memory list (newest first) |
| `bookings.json` | BookingRecord | 10,000-100,000 | In-memory list (newest first) |
| `affiliates.json` | Affiliate | 10-100 | In-memory list |
| `drivers.json` | Driver | 100-1,000 | In-memory list, indexed by UserUid |
| `oauth-credentials.json` | OAuthClientCredentials | 1 (singleton) | Single record |

**Scalability**:
- Current design supports **~100K bookings** in memory
- For larger datasets, migrate to SQL Server or PostgreSQL
- Repositories use interface pattern for easy swapping

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system design
- `10-Real-Time-Tracking.md` - Location tracking implementation
- `11-User-Access-Control.md` - Ownership & authorization
- `12-Timezone-Support.md` - DateTime handling
- `13-Driver-Integration.md` - Driver management
- `20-API-Reference.md` - Complete endpoint documentation
- `23-Security-Model.md` - Security patterns

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Schema Version**: 2.0
