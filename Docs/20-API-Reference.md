# API Reference

**Document Type**: Living Document - Technical Reference  
**Last Updated**: February 8, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides complete API endpoint documentation for the Bellwood AdminAPI, including request/response schemas, authentication requirements, and practical examples.

**Base URL**: `https://localhost:5206` (Development)  
**Production URL**: TBD

**Authentication**: Bearer JWT tokens (obtained from AuthServer)

---

## ?? Endpoint Categories

1. [Health Check](#health-check)
2. [Quote Management](#quote-management)
3. [Booking Management](#booking-management)
4. [Affiliate Management](#affiliate-management)
5. [Driver Management](#driver-management)
6. [Driver Endpoints (DriverApp)](#driver-endpoints)
7. [Location Tracking](#location-tracking)
8. [Passenger Endpoints](#passenger-endpoints)
9. [Admin Endpoints](#admin-endpoints)
10. [User Management (Admin)](#user-management-admin)
11. [Audit Log Management (Admin)](#audit-log-management-admin)

---

## Health Check

### GET /health

**Description**: Health check endpoint (no authentication required)

**Request**:
```http
GET /health HTTP/1.1
Host: localhost:5206
```

**Response** (200 OK):
```json
{
  "status": "ok"
}
```

---

## Quote Management

### POST /quotes

**Description**: Submit a new quote request

**Auth**: Required (any authenticated user)

**Request**:
```http
POST /quotes HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "booker": {
    "firstName": "Alice",
    "lastName": "Morgan",
    "phoneNumber": "312-555-7777",
    "emailAddress": "alice.morgan@example.com"
  },
  "passenger": {
    "firstName": "Taylor",
    "lastName": "Reed",
    "phoneNumber": "773-555-1122",
    "emailAddress": "taylor.reed@example.com"
  },
  "vehicleClass": "Sedan",
  "pickupDateTime": "2024-12-24T09:00:00",
  "pickupLocation": "Langham Hotel, Chicago",
  "pickupStyle": "Curbside",
  "dropoffLocation": "O'Hare International Airport",
  "roundTrip": false,
  "passengerCount": 2,
  "checkedBags": 2,
  "carryOnBags": 1
}
```

**Response** (202 Accepted):
```json
{
  "id": "quote-abc123"
}
```

**Headers**:
```http
Location: /quotes/quote-abc123
```

**Side Effects**:
- Email sent to Bellwood staff
- Quote stored with `CreatedByUserId` (ownership tracking)

---

### GET /quotes/list

**Description**: List recent quotes (paginated)

**Auth**: `StaffOnly` (admin or dispatcher)

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `take` | integer | 50 | Number of quotes to return (max 200) |

**Request**:
```http
GET /quotes/list?take=20 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
[
  {
    "id": "quote-abc123",
    "createdUtc": "2024-12-23T15:30:00Z",
    "status": "Submitted",
    "bookerName": "Alice Morgan",
    "passengerName": "Taylor Reed",
    "vehicleClass": "Sedan",
    "pickupLocation": "Langham Hotel, Chicago",
    "dropoffLocation": "O'Hare International Airport",
    "pickupDateTime": "2024-12-24T09:00:00"
  }
]
```

**Filtering**:
- **Staff (admin/dispatcher)**: See all quotes
- **Bookers**: See only quotes they created (via `CreatedByUserId`)

---

### GET /quotes/{id}

**Description**: Get detailed quote by ID

**Auth**: `StaffOnly`

**Request**:
```http
GET /quotes/quote-abc123 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "id": "quote-abc123",
  "status": "Submitted",
  "createdUtc": "2024-12-23T15:30:00Z",
  "bookerName": "Alice Morgan",
  "passengerName": "Taylor Reed",
  "vehicleClass": "Sedan",
  "pickupLocation": "Langham Hotel, Chicago",
  "dropoffLocation": "O'Hare International Airport",
  "pickupDateTime": "2024-12-24T09:00:00",
  "draft": {
    "booker": {
      "firstName": "Alice",
      "lastName": "Morgan",
      "phoneNumber": "312-555-7777",
      "emailAddress": "alice.morgan@example.com"
    },
    "passenger": {
      "firstName": "Taylor",
      "lastName": "Reed",
      "phoneNumber": "773-555-1122",
      "emailAddress": "taylor.reed@example.com"
    },
    "vehicleClass": "Sedan",
    "pickupDateTime": "2024-12-24T09:00:00",
    "pickupLocation": "Langham Hotel, Chicago",
    "pickupStyle": "Curbside",
    "dropoffLocation": "O'Hare International Airport",
    "roundTrip": false,
    "passengerCount": 2,
    "checkedBags": 2,
    "carryOnBags": 1
  },
  "estimatedCost": null,
  "billingNotes": null
}
```

**Note**: `estimatedCost` and `billingNotes` are masked for dispatchers (null).

**Error Responses**:

- **404 Not Found**: Quote doesn't exist
- **403 Forbidden**: User doesn't have permission to view quote

---

### POST /quotes/{id}/acknowledge

**Description**: Dispatcher acknowledges receipt of quote (Phase Alpha)

**Auth**: `StaffOnly` (dispatcher or admin)

**Request**:
```http
POST /quotes/quote-abc123/acknowledge HTTP/1.1
Host: localhost:5206
Authorization: Bearer {dispatcherToken}
```

**Response** (200 OK):
```json
{
  "message": "Quote acknowledged successfully",
  "id": "quote-abc123",
  "status": "Acknowledged",
  "acknowledgedAt": "2026-01-27T14:30:00Z",
  "acknowledgedBy": "diana-user-guid"
}
```

**FSM Validation**:
- Can only acknowledge quotes with status `Submitted`
- Transition: `Submitted` ? `Acknowledged`

**Side Effects**:
- Quote status updated to `Acknowledged`
- `AcknowledgedAt` and `AcknowledgedByUserId` populated
- `ModifiedByUserId` and `ModifiedOnUtc` updated
- Audit log created

**Error Responses**:
- **404 Not Found**: Quote doesn't exist
- **400 Bad Request**: Quote status is not `Submitted`

---

### POST /quotes/{id}/respond

**Description**: Dispatcher sends price/ETA response to passenger (Phase Alpha)

**Auth**: `StaffOnly` (dispatcher or admin)

**Request**:
```http
POST /quotes/quote-abc123/respond HTTP/1.1
Host: localhost:5206
Authorization: Bearer {dispatcherToken}
Content-Type: application/json

{
  "estimatedPrice": 150.00,
  "estimatedPickupTime": "2026-02-01T14:00:00",
  "notes": "VIP service confirmed. Driver will meet you at arrivals."
}
```

**Response** (200 OK):
```json
{
  "message": "Quote response sent successfully",
  "id": "quote-abc123",
  "status": "Responded",
  "respondedAt": "2026-01-27T14:35:00Z",
  "respondedBy": "diana-user-guid",
  "estimatedPrice": 150.00,
  "estimatedPickupTime": "2026-02-01T14:00:00",
  "notes": "VIP service confirmed. Driver will meet you at arrivals."
}
```

**Validation**:
- `estimatedPrice` must be > 0
- `estimatedPickupTime` must be in the future (1-minute grace period for clock skew)
- `notes` is optional

**FSM Validation**:
- Can only respond to quotes with status `Acknowledged`
- Transition: `Acknowledged` ? `Responded`

**Side Effects**:
- Quote status updated to `Responded`
- `RespondedAt`, `RespondedByUserId`, `EstimatedPrice`, `EstimatedPickupTime`, `Notes` populated
- `ModifiedByUserId` and `ModifiedOnUtc` updated
- **Email sent to passenger** with quote response details
- Audit log created

**Error Responses**:
- **404 Not Found**: Quote doesn't exist
- **400 Bad Request**: 
  - Quote status is not `Acknowledged`
  - `estimatedPrice` ? 0
  - `estimatedPickupTime` is not in the future

---

### POST /quotes/{id}/accept

**Description**: Passenger accepts quote and creates booking (Phase Alpha)

**Auth**: Required (booker/owner only - staff cannot accept on behalf of passenger)

**Request**:
```http
POST /quotes/quote-abc123/accept HTTP/1.1
Host: localhost:5206
Authorization: Bearer {passengerToken}
```

**Response** (200 OK):
```json
{
  "message": "Quote accepted and booking created successfully",
  "quoteId": "quote-abc123",
  "quoteStatus": "Accepted",
  "bookingId": "booking-new-xyz",
  "bookingStatus": "Requested",
  "sourceQuoteId": "quote-abc123"
}
```

**FSM Validation**:
- Can only accept quotes with status `Responded`
- Transition: `Responded` ? `Accepted`

**Ownership Validation**:
- **Only the booker who created the quote can accept it**
- Staff (admin/dispatcher) cannot accept quotes on behalf of passengers
- User's `CreatedByUserId` must match quote's `CreatedByUserId`

**Side Effects**:
- Quote status updated to `Accepted`
- New booking created with:
  - Status: `Requested` (ready for staff confirmation)
  - `SourceQuoteId` linking back to original quote
  - `PickupDateTime` set to `EstimatedPickupTime` (if provided) or original `PickupDateTime`
  - `CreatedByUserId` set to current user
- **Email sent to Bellwood staff** notifying of quote acceptance
- Audit logs created (quote acceptance + booking creation)

**Error Responses**:
- **404 Not Found**: Quote doesn't exist
- **403 Forbidden**:
  - User doesn't own the quote
  - Staff user attempting to accept on behalf of passenger
- **400 Bad Request**: Quote status is not `Responded`

---

### POST /quotes/{id}/cancel

**Description**: Cancel a quote

**Auth**: Required (owner or staff)

**Request**:
```http
POST /quotes/quote-abc123/cancel HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "message": "Quote cancelled successfully",
  "id": "quote-abc123",
  "status": "Cancelled"
}
```

**Authorization**:
- **Bookers**: Can cancel their own quotes (via `CreatedByUserId`)
- **Staff**: Can cancel any quote

**Validation**:
- Cannot cancel quotes with status `Accepted` or `Cancelled`

**Side Effects**:
- Quote status updated to `Cancelled`
- `ModifiedByUserId` and `ModifiedOnUtc` updated
- Audit log created

**Error Responses**:
- **404 Not Found**: Quote doesn't exist
- **403 Forbidden**: User doesn't have permission to cancel
- **400 Bad Request**: Cannot cancel quote with current status

---

### POST /quotes/seed

**Description**: Seed sample quotes (DEV ONLY)

**Auth**: `AdminOnly`

**Request**:
```http
POST /quotes/seed HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "added": 5,
  "createdByUserId": "user-guid-123"
}
```

**What It Creates**: 5 sample quotes with various statuses:
- Submitted
- InReview
- Priced
- Rejected
- Closed

---

## Booking Management

### POST /bookings

**Description**: Submit a new booking request

**Auth**: Required (any authenticated user)

**Request**: Same schema as `POST /quotes`

**Response** (202 Accepted):
```json
{
  "id": "booking-xyz789"
}
```

**Side Effects**:
- Email sent to Bellwood staff
- Booking stored with `CreatedByUserId`

---

### GET /bookings/list

**Description**: List recent bookings (paginated)

**Auth**: `StaffOnly`

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `take` | integer | 50 | Number of bookings (max 200) |

**Request**:
```http
GET /bookings/list?take=20 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
X-Timezone-Id: America/Chicago
```

**Response** (200 OK):
```json
[
  {
    "id": "booking-xyz",
    "createdUtc": "2024-12-23T15:30:00Z",
    "createdDateTimeOffset": "2024-12-23T09:30:00-06:00",
    "status": "Scheduled",
    "currentRideStatus": "Scheduled",
    "bookerName": "Chris Bailey",
    "passengerName": "Jordan Chen",
    "vehicleClass": "Sedan",
    "pickupLocation": "Langham Hotel",
    "dropoffLocation": "Midway Airport",
    "pickupDateTime": "2024-12-24T15:00:00Z",
    "pickupDateTimeOffset": "2024-12-24T09:00:00-06:00",
    "assignedDriverId": "driver-abc",
    "assignedDriverUid": "driver-001",
    "assignedDriverName": "Charlie Johnson"
  }
]
```

**Timezone Handling**:
- `createdUtc` / `pickupDateTime`: Original UTC values
- `createdDateTimeOffset` / `pickupDateTimeOffset`: Converted to `X-Timezone-Id` (if provided)

**Filtering**:
- **Staff**: See all bookings
- **Drivers**: See only assigned bookings (via `AssignedDriverUid`)
- **Bookers**: See only their own bookings (via `CreatedByUserId`)

---

### GET /bookings/{id}

**Description**: Get detailed booking by ID

**Auth**: `StaffOnly`

**Request**:
```http
GET /bookings/booking-xyz HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
X-Timezone-Id: America/Chicago
```

**Response** (200 OK):
```json
{
  "id": "booking-xyz",
  "status": "Scheduled",
  "currentRideStatus": "Scheduled",
  "createdUtc": "2024-12-23T15:30:00Z",
  "createdDateTimeOffset": "2024-12-23T09:30:00-06:00",
  "bookerName": "Chris Bailey",
  "passengerName": "Jordan Chen",
  "vehicleClass": "Sedan",
  "pickupLocation": "Langham Hotel",
  "dropoffLocation": "Midway Airport",
  "pickupDateTime": "2024-12-24T15:00:00Z",
  "pickupDateTimeOffset": "2024-12-24T09:00:00-06:00",
  "draft": { /* full QuoteDraft object */ },
  "assignedDriverId": "driver-abc",
  "assignedDriverUid": "driver-001",
  "assignedDriverName": "Charlie Johnson",
  "paymentMethodId": null,
  "paymentMethodLast4": null,
  "paymentAmount": null,
  "totalAmount": null,
  "totalFare": null
}
```

**Billing Fields** (Phase 2):
- All billing fields are `null` (populated in Phase 3)
- **Dispatchers**: Billing fields masked (always null)
- **Admins**: See actual values (when populated)

---

### POST /bookings/{bookingId}/cancel

**Description**: Cancel a booking request

**Auth**: Required

**Authorization**:
- **Staff**: Can cancel any booking
- **Bookers**: Can only cancel their own bookings

**Request**:
```http
POST /bookings/booking-xyz/cancel HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "message": "Booking cancelled successfully",
  "id": "booking-xyz",
  "status": "Cancelled"
}
```

**Side Effects**:
- Booking status ? `Cancelled`
- Email sent to Bellwood staff
- Audit trail (`ModifiedByUserId`, `ModifiedOnUtc`)

**Error Responses**:
- **404 Not Found**: Booking doesn't exist
- **403 Forbidden**: User doesn't own booking (non-staff)
- **400 Bad Request**: Cannot cancel (status not `Requested` or `Confirmed`)

---

### POST /bookings/{bookingId}/assign-driver

**Description**: Assign driver to booking

**Auth**: `StaffOnly`

**Request**:
```http
POST /bookings/booking-xyz/assign-driver HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "driverId": "driver-abc-123"
}
```

**Response** (200 OK):
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

**Side Effects**:
- Booking status ? `Scheduled`
- `CurrentRideStatus` ? `Scheduled`
- Email sent to affiliate company

**Error Responses**:
- **404 Not Found**: Booking or driver doesn't exist
- **400 Bad Request**: Driver has no `UserUid` (cannot authenticate in DriverApp)

---

### POST /bookings/seed

**Description**: Seed sample bookings (DEV ONLY)

**Auth**: `AdminOnly`

**Request**:
```http
POST /bookings/seed HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "added": 8,
  "createdByUserId": "user-guid-123"
}
```

**What It Creates**: 8 sample bookings with various statuses:
- Requested
- Confirmed
- Scheduled (2 rides for Charlie)
- InProgress
- Completed
- Cancelled
- NoShow

---

## Affiliate Management

### GET /affiliates/list

**Description**: List all affiliates with nested drivers

**Auth**: Required

**Request**:
```http
GET /affiliates/list HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
[
  {
    "id": "affiliate-abc",
    "name": "Chicago Limo Service",
    "pointOfContact": "John Smith",
    "phone": "312-555-1234",
    "email": "dispatch@chicagolimo.com",
    "streetAddress": "123 Main St",
    "city": "Chicago",
    "state": "IL",
    "zipCode": "60601",
    "drivers": [
      {
        "id": "driver-123",
        "affiliateId": "affiliate-abc",
        "name": "Charlie Johnson",
        "phone": "312-555-0001",
        "email": "charlie@chicagolimo.com",
        "userUid": "driver-001",
        "isActive": true
      }
    ]
  }
]
```

---

### POST /affiliates

**Description**: Create a new affiliate

**Auth**: Required

**Request**:
```http
POST /affiliates HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Suburban Chauffeurs",
  "pointOfContact": "Emily Davis",
  "phone": "847-555-9876",
  "email": "emily@suburbanchauffeurs.com",
  "city": "Naperville",
  "state": "IL"
}
```

**Response** (201 Created):
```json
{
  "id": "affiliate-def",
  "name": "Suburban Chauffeurs",
  "pointOfContact": "Emily Davis",
  "phone": "847-555-9876",
  "email": "emily@suburbanchauffeurs.com",
  "city": "Naperville",
  "state": "IL",
  "drivers": []
}
```

**Validation**:
- `name` required
- `email` required
- Auto-generated `id`

---

### GET /affiliates/{id}

**Description**: Get affiliate by ID

**Auth**: Required

**Request**:
```http
GET /affiliates/affiliate-abc HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK): Same schema as list item

**Error Responses**:
- **404 Not Found**: Affiliate doesn't exist

---

### PUT /affiliates/{id}

**Description**: Update affiliate

**Auth**: Required

**Request**:
```http
PUT /affiliates/affiliate-abc HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Chicago Elite Limo Service",
  "phone": "312-555-9999",
  "email": "dispatch@chicagolimo.com"
}
```

**Response** (200 OK): Full updated affiliate object

---

### DELETE /affiliates/{id}

**Description**: Delete affiliate (cascade deletes drivers)

**Auth**: Required

**Request**:
```http
DELETE /affiliates/affiliate-abc HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "message": "Affiliate and associated drivers deleted",
  "id": "affiliate-abc"
}
```

**Warning**: This is destructive! All drivers under this affiliate are also deleted.

---

## Driver Management

### POST /affiliates/{affiliateId}/drivers

**Description**: Create driver under affiliate

**Auth**: Required

**Request**:
```http
POST /affiliates/affiliate-abc/drivers HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Charlie Johnson",
  "phone": "312-555-0001",
  "email": "charlie@chicagolimo.com",
  "userUid": "driver-001"
}
```

**Response** (201 Created):
```json
{
  "id": "driver-123",
  "affiliateId": "affiliate-abc",
  "name": "Charlie Johnson",
  "phone": "312-555-0001",
  "email": "charlie@chicagolimo.com",
  "userUid": "driver-001",
  "isActive": true
}
```

**Validation**:
- `name` required
- `phone` required
- `userUid` must be unique across all drivers
- `affiliateId` must exist

**Error Responses**:
- **400 Bad Request**: `UserUid` already assigned to another driver

---

### GET /drivers/list

**Description**: List all drivers

**Auth**: Required

**Request**:
```http
GET /drivers/list HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK): Array of driver objects

---

### GET /drivers/by-uid/{userUid}

**Description**: Find driver by AuthServer UserUid

**Auth**: Required

**Request**:
```http
GET /drivers/by-uid/driver-001 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK): Single driver object

**Error Responses**:
- **404 Not Found**: No driver with this UserUid

---

### GET /drivers/{id}

**Description**: Get driver by ID

**Auth**: Required

**Request**:
```http
GET /drivers/driver-123 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK): Single driver object

---

### PUT /drivers/{id}

**Description**: Update driver

**Auth**: Required

**Request**:
```http
PUT /drivers/driver-123 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Charles 'Charlie' Johnson",
  "phone": "312-555-0001",
  "userUid": "driver-001",
  "isActive": true
}
```

**Response** (200 OK): Full updated driver object

**Validation**: `userUid` must be unique (excluding self)

---

### DELETE /drivers/{id}

**Description**: Delete driver

**Auth**: Required

**Request**:
```http
DELETE /drivers/driver-123 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "message": "Driver deleted",
  "id": "driver-123"
}
```

**Note**: Existing bookings retain driver information (no cascade delete)

---

## Driver Endpoints

### GET /driver/rides/today

**Description**: Get driver's rides for next 24 hours

**Auth**: `DriverOnly`

**Headers**:
- `Authorization: Bearer {token}` (required)
- `X-Timezone-Id: {timezone}` (recommended)

**Request**:
```http
GET /driver/rides/today HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
X-Timezone-Id: Asia/Tokyo
```

**Response** (200 OK):
```json
[
  {
    "id": "ride-abc",
    "pickupDateTime": "2024-12-24T15:00:00Z",
    "pickupDateTimeOffset": "2024-12-25T00:00:00+09:00",
    "pickupLocation": "Langham Hotel",
    "dropoffLocation": "Midway Airport",
    "passengerName": "Jordan Chen",
    "passengerPhone": "312-555-6666",
    "status": "Scheduled"
  }
]
```

**Filtering**:
- Only rides assigned to this driver (via JWT `uid` claim)
- Pickup time within next 24 hours (in driver's timezone)
- Excludes `Completed` and `Cancelled` rides

---

### GET /driver/rides/{id}

**Description**: Get detailed ride information

**Auth**: `DriverOnly`

**Request**:
```http
GET /driver/rides/ride-abc HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
X-Timezone-Id: Asia/Tokyo
```

**Response** (200 OK):
```json
{
  "id": "ride-abc",
  "pickupDateTime": "2024-12-24T15:00:00Z",
  "pickupDateTimeOffset": "2024-12-25T00:00:00+09:00",
  "pickupLocation": "O'Hare FBO",
  "pickupStyle": "MeetAndGreet",
  "pickupSignText": "CHEN / Bellwood",
  "dropoffLocation": "Downtown Chicago",
  "passengerName": "Jordan Chen",
  "passengerPhone": "312-555-6666",
  "passengerCount": 1,
  "checkedBags": 1,
  "carryOnBags": 0,
  "vehicleClass": "Sedan",
  "outboundFlight": "UA123",
  "additionalRequest": "Call on arrival",
  "status": "Scheduled"
}
```

**Error Responses**:
- **404 Not Found**: Ride doesn't exist
- **403 Forbidden**: Driver doesn't own this ride

---

### POST /driver/rides/{id}/status

**Description**: Update ride status (FSM-validated)

**Auth**: `DriverOnly`

**Request**:
```http
POST /driver/rides/ride-abc/status HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "newStatus": "OnRoute"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "rideId": "ride-abc",
  "newStatus": "OnRoute",
  "bookingStatus": "Scheduled",
  "timestamp": "2024-12-23T15:30:00Z"
}
```

**Valid Transitions** (Finite State Machine):

| Current Status | Allowed Next Status |
|----------------|---------------------|
| `Scheduled` | `OnRoute`, `Cancelled` |
| `OnRoute` | `Arrived`, `Cancelled` |
| `Arrived` | `PassengerOnboard`, `Cancelled` |
| `PassengerOnboard` | `Completed`, `Cancelled` |
| `Completed` | *(terminal state)* |
| `Cancelled` | *(terminal state)* |

**Side Effects**:
- Updates `CurrentRideStatus`
- Syncs `BookingStatus` (e.g., `PassengerOnboard` ? `InProgress`)
- Broadcasts `RideStatusChanged` event via SignalR
- Cleans up location data if `Completed` or `Cancelled`

**Error Responses**:
- **400 Bad Request**: Invalid status transition

---

### POST /driver/location/update

**Description**: Submit GPS location update (rate-limited)

**Auth**: `DriverOnly`

**Rate Limit**: 10 seconds minimum between updates

**Request**:
```http
POST /driver/location/update HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
Content-Type: application/json

{
  "rideId": "ride-abc",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5
}
```

**Response** (200 OK):
```json
{
  "message": "Location updated",
  "rideId": "ride-abc",
  "timestamp": "2024-12-23T15:30:15Z"
}
```

**Error Responses**:
- **429 Too Many Requests**: Rate limited (< 10 seconds since last update)
- **400 Bad Request**: Ride not in active tracking status

---

## Location Tracking

### GET /driver/location/{rideId}

**Description**: Get latest location for a ride

**Auth**: Required

**Authorization**:
- **Driver**: Can see own assigned rides
- **Admin/Dispatcher**: Can see all rides
- **Backward Compatibility**: Authenticated users without role claims (temporary)

**Request**:
```http
GET /driver/location/ride-abc HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "rideId": "ride-abc",
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

**Error Responses**:
- **404 Not Found**: No recent location data (or ride doesn't exist)
- **403 Forbidden**: User doesn't have permission

---

### GET /admin/locations

**Description**: Get all active driver locations

**Auth**: `StaffOnly`

**Request**:
```http
GET /admin/locations HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "count": 3,
  "locations": [
    {
      "rideId": "ride-abc",
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

---

### GET /admin/locations/rides

**Description**: Batch query locations for specific ride IDs

**Auth**: `StaffOnly`

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `rideIds` | string | Yes | Comma-separated ride IDs |

**Request**:
```http
GET /admin/locations/rides?rideIds=ride-abc,ride-xyz,ride-123 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "requested": 3,
  "found": 2,
  "locations": [
    {
      "rideId": "ride-abc",
      // ... location data
    }
  ],
  "timestamp": "2024-12-23T15:30:15Z"
}
```

---

## Passenger Endpoints

### GET /passenger/rides/{rideId}/location

**Description**: Get location for passenger's own ride (email-based auth)

**Auth**: Required (JWT with `email` claim)

**Authorization**: User's email must match booking's booker OR passenger email

**Request**:
```http
GET /passenger/rides/ride-abc/location HTTP/1.1
Host: localhost:5206
Authorization: Bearer {token}
```

**Response - Tracking Active** (200 OK):
```json
{
  "rideId": "ride-abc",
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

**Response - Tracking Not Started** (200 OK):
```json
{
  "rideId": "ride-abc",
  "trackingActive": false,
  "message": "Driver has not started tracking yet",
  "currentStatus": "Scheduled"
}
```

**Error Responses**:
- **404 Not Found**: Ride doesn't exist
- **403 Forbidden**: Email doesn't match booker or passenger

---

## Admin Endpoints

### POST /dev/seed-affiliates

**Description**: Seed test affiliates and drivers (DEV ONLY)

**Auth**: `AdminOnly`

**Request**:
```http
POST /dev/seed-affiliates HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "affiliatesAdded": 2,
  "driversAdded": 3,
  "message": "Affiliates and drivers seeded successfully",
  "note": "Driver 'Charlie Johnson' has UserUid 'driver-001' matching AuthServer test user 'charlie'"
}
```

**What It Creates**:
- 2 affiliates:
  - Chicago Limo Service
  - Suburban Chauffeurs
- 3 drivers:
  - Charlie Johnson (driver-001)
  - Sarah Lee (driver-002)
  - Robert Brown (driver-003)

---

### GET /api/admin/oauth

**Description**: Get current OAuth credentials (secret masked)

**Auth**: `AdminOnly`

**Request**:
```http
GET /api/admin/oauth HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response - Configured** (200 OK):
```json
{
  "configured": true,
  "credentials": {
    "clientId": "bellwood-production",
    "clientSecretMasked": "abcd...wxyz",
    "lastUpdatedUtc": "2024-12-23T15:00:00Z",
    "lastUpdatedBy": "alice",
    "description": "Production LA credentials"
  }
}
```

**Response - Not Configured** (200 OK):
```json
{
  "configured": false,
  "message": "OAuth credentials not configured. Use PUT /api/admin/oauth to set them."
}
```

---

### PUT /api/admin/oauth

**Description**: Update OAuth credentials

**Auth**: `AdminOnly`

**Request**:
```http
PUT /api/admin/oauth HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "clientId": "bellwood-production",
  "clientSecret": "super-secret-key-12345",
  "description": "Production LA credentials"
}
```

**Response** (200 OK):
```json
{
  "message": "OAuth credentials updated successfully",
  "clientId": "bellwood-production",
  "clientSecretMasked": "supe...2345",
  "updatedBy": "alice",
  "updatedAt": "2024-12-23T15:30:00Z",
  "note": "Cache invalidated. New credentials will be used for all future API calls."
}
```

**Side Effects**:
- Credentials encrypted and stored in `App_Data/oauth-credentials.json`
- In-memory cache invalidated
- Audit trail updated

**Error Responses**:
- **400 Bad Request**: Missing `clientId` or `clientSecret`

---

## User Management (Admin)

### PUT /api/admin/users/{username}/role

**Description**: Update user role (proxy to AuthServer)

**Auth**: `AdminOnly`

**Request**:
```http
PUT /api/admin/users/{username}/role HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "role": "dispatcher"
}
```

**Response** (200 OK):
```json
{
  "message": "Successfully assigned role 'dispatcher' to user 'diana'.",
  "username": "diana",
  "previousRoles": ["booker"],
  "newRole": "dispatcher"
}
```

**Side Effects**:
- User's role updated in AuthServer
- Previous roles removed (mutually exclusive)
- **Audit log created** in AdminAPI
- User must re-login to get new role in JWT

**Valid Roles**:
- `admin` - Full system access
- `dispatcher` - Operational access (bookings, quotes, drivers)
- `booker` - Passenger/booker access
- `driver` - Driver app access

**Error Responses**:
- **400 Bad Request**: Invalid role or missing role field
- **404 Not Found**: User doesn't exist
- **500 Internal Server Error**: AuthServer communication failure

**Note**: This endpoint proxies to AuthServer at `https://localhost:5001`. Ensure AuthServer is running.

---

## Audit Log Management (Admin)

### GET /api/admin/audit-logs

**Description**: Query audit logs with filtering and pagination

**Auth**: `AdminOnly`

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `userId` | string | null | Filter by user ID |
| `entityType` | string | null | Filter by entity type (e.g., "Quote", "Booking") |
| `action` | string | null | Filter by action (e.g., "Quote.Created") |
| `startDate` | DateTime | null | Filter logs after this date (UTC) |
| `endDate` | DateTime | null | Filter logs before this date (UTC) |
| `take` | integer | 100 | Number of logs to return (max 1000) |
| `skip` | integer | 0 | Number of logs to skip (for pagination) |

**Request**:
```http
GET /api/admin/audit-logs?userId=user-123&entityType=Quote&take=50&skip=0 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "logs": [
    {
      "id": "audit-abc123",
      "timestamp": "2026-02-08T14:30:00Z",
      "userId": "user-123",
      "username": "alice",
      "action": "Quote.Created",
      "entityType": "Quote",
      "entityId": "quote-xyz",
      "result": "Success",
      "ipAddress": "192.168.1.100",
      "endpoint": "POST /quotes",
      "details": "{\"passengerName\":\"John Doe\",\"vehicleClass\":\"Sedan\"}",
      "errorMessage": null
    }
  ],
  "pagination": {
    "total": 150,
    "skip": 0,
    "take": 50,
    "returned": 50
  },
  "filters": {
    "userId": "user-123",
    "entityType": "Quote",
    "action": null,
    "startDate": null,
    "endDate": null
  }
}
```

---

### GET /api/admin/audit-logs/{id}

**Description**: Get a specific audit log entry by ID

**Auth**: `AdminOnly`

**Request**:
```http
GET /api/admin/audit-logs/audit-abc123 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "id": "audit-abc123",
  "timestamp": "2026-02-08T14:30:00Z",
  "userId": "user-123",
  "username": "alice",
  "action": "Quote.Created",
  "entityType": "Quote",
  "entityId": "quote-xyz",
  "result": "Success",
  "ipAddress": "192.168.1.100",
  "endpoint": "POST /quotes",
  "details": "{\"passengerName\":\"John Doe\",\"vehicleClass\":\"Sedan\"}",
  "errorMessage": null
}
```

**Side Effects**:
- Audit log created for viewing the audit log (meta-auditing)

**Error Responses**:
- **404 Not Found**: Audit log doesn't exist

---

### GET /api/admin/audit-logs/stats

**Description**: Get audit log statistics (count, oldest, newest)

**Auth**: `AdminOnly`

**Request**:
```http
GET /api/admin/audit-logs/stats HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "count": 1543,
  "oldestUtc": "2025-12-01T00:00:00Z",
  "newestUtc": "2026-02-08T15:30:00Z"
}
```

**Side Effects**:
- Audit log created for viewing stats

---

### DELETE /api/admin/audit-logs/cleanup

**Description**: Delete audit logs older than specified retention period

**Auth**: `AdminOnly`

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `retentionDays` | integer | 90 | Number of days to retain (1-365) |

**Request**:
```http
DELETE /api/admin/audit-logs/cleanup?retentionDays=90 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
{
  "message": "Audit log cleanup completed",
  "deletedCount": 324,
  "retentionDays": 90,
  "cutoffDate": "2025-11-10T15:30:00Z"
}
```

**Side Effects**:
- Audit logs older than cutoff date are permanently deleted
- System audit log created for cleanup action

**Validation**:
- `retentionDays` must be between 1 and 365
- Defaults to 90 days if invalid

---

### POST /api/admin/audit-logs/clear

**Description**: Clear all audit logs (requires safety confirmation)

**Auth**: `AdminOnly`

**?? WARNING**: This is a destructive operation that deletes ALL audit logs!

**Request**:
```http
POST /api/admin/audit-logs/clear HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "confirm": "CLEAR"
}
```

**Response** (200 OK):
```json
{
  "deletedCount": 1543,
  "clearedAtUtc": "2026-02-08T15:30:00Z",
  "clearedByUserId": "user-admin-123",
  "clearedByUsername": "alice",
  "message": "All audit logs have been cleared successfully"
}
```

**Safety Confirmation**:
- Request body **must** contain exactly `{"confirm": "CLEAR"}` (case-sensitive)
- Any other value will be rejected with 400 Bad Request

**Side Effects**:
- **ALL audit logs permanently deleted**
- One final audit log created AFTER clearing (recording the clear operation)
- Warning logged to application logs

**Error Responses**:
- **400 Bad Request**: Invalid or missing confirmation phrase

**Example Error Response**:
```json
{
  "error": "Confirmation phrase must be exactly 'CLEAR' (case-sensitive)"
}
```

**Use Cases**:
- Development/testing cleanup
- Compliance requirement to purge old data
- Starting fresh after testing

**Production Recommendations**:
1. **Export** logs before clearing (future feature)
2. **Backup** `App_Data/audit-logs.json` before clearing
3. **Document** reason for clearing in change log
4. **Notify** stakeholders before production clear

---

## ?? HTTP Status Codes

| Code | Meaning | Common Causes |
|------|---------|---------------|
| **200 OK** | Success | Request processed successfully |
| **201 Created** | Resource created | POST to create affiliate/driver |
| **202 Accepted** | Request accepted | Quote/booking submission |
| **400 Bad Request** | Invalid request | Missing fields, invalid status transition |
| **401 Unauthorized** | Authentication required | Missing or invalid JWT token |
| **403 Forbidden** | Permission denied | User lacks required role or ownership |
| **404 Not Found** | Resource not found | Invalid ID, no location data |
| **429 Too Many Requests** | Rate limited | Location updates < 10 seconds apart |

---

## ?? Authorization Policies

| Policy | Roles | Applied To |
|--------|-------|------------|
| `AdminOnly` | admin | Seed endpoints, OAuth management |
| `StaffOnly` | admin, dispatcher | Quotes, bookings, locations |
| `DriverOnly` | driver | Driver rides, status updates, location updates |
| `BookerOnly` | booker | (Future use) |
| *(Generic Auth)* | Any authenticated | Affiliate/driver management, bookings, quotes |

---

## ?? Testing with cURL

### Audit Log Management Examples

```bash
# Get admin token
TOKEN=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "alice", "password": "password"}' \
  | jq -r '.accessToken')

# Query audit logs
curl -X GET "https://localhost:5206/api/admin/audit-logs?entityType=Quote&take=20" \
  -H "Authorization: Bearer $TOKEN" \
  | jq

# Get specific audit log
curl -X GET "https://localhost:5206/api/admin/audit-logs/audit-abc123" \
  -H "Authorization: Bearer $TOKEN" \
  | jq

# Get audit log statistics
curl -X GET "https://localhost:5206/api/admin/audit-logs/stats" \
  -H "Authorization: Bearer $TOKEN" \
  | jq

# Clean up old logs (90 day retention)
curl -X DELETE "https://localhost:5206/api/admin/audit-logs/cleanup?retentionDays=90" \
  -H "Authorization: Bearer $TOKEN" \
  | jq

# Clear all logs (DANGER!)
curl -X POST "https://localhost:5206/api/admin/audit-logs/clear" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"confirm": "CLEAR"}' \
  | jq
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system design
- `10-Real-Time-Tracking.md` - GPS tracking details
- `11-User-Access-Control.md` - RBAC implementation
- `12-Timezone-Support.md` - Timezone handling
- `13-Driver-Integration.md` - Driver management
- `14-Passenger-Tracking.md` - Passenger endpoints
- `21-SignalR-Events.md` - Real-time event reference
- `22-Data-Models.md` - Complete entity schemas
- `23-Security-Model.md` - Security & authorization

---

**Last Updated**: February 8, 2026  
**Status**: ? Production Ready  
**API Version**: 2.0
