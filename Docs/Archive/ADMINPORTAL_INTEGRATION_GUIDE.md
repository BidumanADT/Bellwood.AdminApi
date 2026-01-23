# AdminPortal Integration Guide - Driver Status Updates & Real-Time Tracking

## Overview

This guide documents the required changes to the AdminPortal to properly receive and display real-time driver status updates and location tracking.

---

## ?? Current Issues

### Issue #1: Status Updates Not Displaying

**Problem**: When drivers update ride status (e.g., Scheduled ? OnRoute), the AdminPortal continues to show "Scheduled".

**Root Causes**:
1. ? AdminPortal doesn't subscribe to `RideStatusChanged` SignalR event
2. ? AdminPortal displays `booking.Status` instead of `booking.CurrentRideStatus`
3. ? `Status` field only changes for major transitions (InProgress, Completed, Cancelled)

**Impact**: Dispatchers cannot see real-time driver progress (OnRoute, Arrived, PassengerOnboard)

### Issue #2: Location Updates Failing

**Problem**: `GET /admin/locations` throws `System.Text.Json.JsonException` and disconnects SignalR.

**Root Causes**:
1. ? API returns `{ count, locations[], timestamp }` object
2. ? Portal expects raw `List<ActiveRideLocationDto>`
3. ? Portal's DTO missing `CurrentStatus` and `AgeSeconds` properties

**Impact**: No location tracking, no status updates, broken real-time features

---

## ? Required Fixes

### Fix #1: Update Response Deserialization

**File**: `Services/DriverTrackingService.cs` (or equivalent)

**Change From**:
```csharp
// ? OLD: Expects raw array
var locations = await response.Content.ReadFromJsonAsync<List<ActiveRideLocationDto>>();
```

**Change To**:
```csharp
// ? NEW: Handle API's envelope format
var envelope = await response.Content.ReadFromJsonAsync<LocationsResponse>();
var locations = envelope?.Locations ?? new List<ActiveRideLocationDto>();
```

**Add Response Wrapper**:
```csharp
public class LocationsResponse
{
    public int Count { get; set; }
    public List<ActiveRideLocationDto> Locations { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
```

### Fix #2: Update DTO to Match API

**File**: `Models/ActiveRideLocationDto.cs` (or equivalent)

**Add Missing Properties**:
```csharp
public class ActiveRideLocationDto
{
    // Existing properties
    public string RideId { get; set; } = "";
    public string DriverUid { get; set; } = "";
    public string DriverName { get; set; } = "";
    public string PassengerName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Heading { get; set; }
    public double? Speed { get; set; }
    public double? Accuracy { get; set; }
    
    // NEW PROPERTIES - Add these!
    public string CurrentStatus { get; set; } = "Scheduled";  // ? ADD THIS
    public double AgeSeconds { get; set; }                    // ? ADD THIS
    
    public string PickupLocation { get; set; } = "";
    public string DropoffLocation { get; set; } = "";
}
```

### Fix #3: Subscribe to RideStatusChanged Event

**File**: `Services/DriverTrackingService.cs` or `SignalRService.cs`

**Add Event Handler**:
```csharp
public async Task InitializeAsync()
{
    // Existing LocationUpdate handler
    _hubConnection.On<LocationUpdate>("LocationUpdate", OnLocationUpdate);
    
    // Existing TrackingStopped handler
    _hubConnection.On<TrackingStoppedEvent>("TrackingStopped", OnTrackingStopped);
    
    // NEW: Add RideStatusChanged handler
    _hubConnection.On<RideStatusChangedEvent>("RideStatusChanged", OnRideStatusChanged);
    
    await _hubConnection.StartAsync();
}

// NEW: Handler for status changes
private void OnRideStatusChanged(RideStatusChangedEvent evt)
{
    Console.WriteLine($"[SignalR] Ride {evt.RideId} status changed to {evt.NewStatus}");
    
    // Update the booking in your state/list
    UpdateBookingStatus(evt.RideId, evt.NewStatus);
    
    // Show toast notification to dispatcher
    ShowNotification($"{evt.DriverName} updated ride to {evt.NewStatus}");
    
    // If on booking detail page, refresh details
    if (CurrentRideId == evt.RideId)
    {
        RefreshRideDetails();
    }
}
```

**Add Event DTO**:
```csharp
public class RideStatusChangedEvent
{
    public string RideId { get; set; } = "";
    public string DriverUid { get; set; } = "";
    public string? DriverName { get; set; }
    public string? PassengerName { get; set; }
    public string NewStatus { get; set; } = "";  // "OnRoute", "Arrived", etc.
    public DateTime Timestamp { get; set; }
}
```

### Fix #4: Display CurrentRideStatus in UI

**File**: Booking list/detail pages

**Change From**:
```html
<!-- ? OLD: Shows booking status (always "Scheduled" for active rides) -->
<span class="status-badge">@booking.Status</span>
```

**Change To**:
```html
<!-- ? NEW: Shows driver's current status -->
<span class="status-badge status-@GetStatusClass(booking.CurrentRideStatus)">
    @(booking.CurrentRideStatus ?? booking.Status)
</span>
```

**Add Helper Method**:
```csharp
private string GetStatusClass(string? currentStatus)
{
    return currentStatus switch
    {
        "Scheduled" => "scheduled",
        "OnRoute" => "onroute",
        "Arrived" => "arrived",
        "PassengerOnboard" => "onboard",
        "Completed" => "completed",
        "Cancelled" => "cancelled",
        _ => "default"
    };
}
```

**Add CSS**:
```css
.status-badge {
    padding: 4px 12px;
    border-radius: 12px;
    font-size: 12px;
    font-weight: 600;
    text-transform: uppercase;
}

.status-scheduled { background: #e3f2fd; color: #1976d2; }
.status-onroute { background: #fff3e0; color: #f57c00; }
.status-arrived { background: #fce4ec; color: #c2185b; }
.status-onboard { background: #e8f5e9; color: #388e3c; }
.status-completed { background: #f1f8e9; color: #689f38; }
.status-cancelled { background: #ffebee; color: #d32f2f; }
```

---

## ?? SignalR Event Reference

### Event: RideStatusChanged

**When**: Driver updates ride status via DriverApp

**Groups Notified**:
- `ride_{rideId}` - Passengers tracking this specific ride
- `driver_{driverUid}` - Admin tracking this specific driver
- `admin` - All AdminPortal users (dispatchers)

**Payload**:
```json
{
  "rideId": "abc123",
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson",
  "passengerName": "Maria Garcia",
  "newStatus": "OnRoute",
  "timestamp": "2025-12-18T15:30:00Z"
}
```

**Status Values**:
- `Scheduled` - Driver has not started yet
- `OnRoute` - Driver is en route to pickup location
- `Arrived` - Driver has arrived at pickup location
- `PassengerOnboard` - Passenger is in the vehicle
- `Completed` - Ride finished successfully
- `Cancelled` - Ride was cancelled

### Event: LocationUpdate

**When**: Driver sends GPS update (every 15-30 seconds while ride active)

**Groups Notified**: Same as RideStatusChanged

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
  "timestamp": "2025-12-18T15:30:15Z"
}
```

### Event: TrackingStopped

**When**: Ride completes or is cancelled

**Payload**:
```json
{
  "rideId": "abc123",
  "reason": "Ride completed",
  "timestamp": "2025-12-18T16:00:00Z"
}
```

---

## ?? Updated API Response Contracts

### POST /driver/rides/{id}/status

**Old Response** (deprecated):
```json
{
  "message": "Status updated successfully",
  "rideId": "abc123",
  "newStatus": "OnRoute"
}
```

**New Response** (current):
```json
{
  "success": true,
  "rideId": "abc123",
  "newStatus": "OnRoute",
  "bookingStatus": "Scheduled",
  "timestamp": "2025-12-18T15:30:00Z"
}
```

**Fields**:
- `success` - Always true for successful updates
- `rideId` - The ride that was updated
- `newStatus` - Driver's current ride status (OnRoute, Arrived, etc.)
- `bookingStatus` - Public booking status (Scheduled, InProgress, Completed)
- `timestamp` - When the update occurred (UTC)

### GET /admin/locations

**Response Format**:
```json
{
  "count": 3,
  "locations": [
    {
      "rideId": "abc123",
      "driverUid": "driver-001",
      "driverName": "Charlie Johnson",
      "passengerName": "Maria Garcia",
      "latitude": 41.8781,
      "longitude": -87.6298,
      "heading": 45.5,
      "speed": 12.3,
      "accuracy": 8.5,
      "timestamp": "2025-12-18T15:30:00Z",
      "currentStatus": "OnRoute",
      "ageSeconds": 15.3,
      "pickupLocation": "O'Hare Airport",
      "dropoffLocation": "Downtown Chicago"
    }
  ],
  "timestamp": "2025-12-18T15:30:15Z"
}
```

---

## ?? Testing Checklist

### Test #1: Status Updates Appear in Real-Time

1. **Setup**:
   - Open AdminPortal in browser
   - Navigate to active bookings list
   - Have DriverApp logged in as Charlie

2. **Action**:
   - In DriverApp: Change ride status from Scheduled ? OnRoute

3. **Expected**:
   - ? AdminPortal shows "OnRoute" badge immediately (no refresh)
   - ? Toast notification appears: "Charlie Johnson updated ride to OnRoute"
   - ? Status badge changes color (orange for OnRoute)

4. **Verify**:
   - Check browser console: Should see `[SignalR] Ride abc123 status changed to OnRoute`
   - No errors in console

### Test #2: Location Updates Display

1. **Setup**:
   - AdminPortal open to driver tracking map
   - DriverApp sending location updates (ride must be OnRoute/Arrived/PassengerOnboard)

2. **Action**:
   - Driver moves (location updates every 15 seconds)

3. **Expected**:
   - ? Driver marker moves on map
   - ? Location timestamp updates
   - ? No deserialization errors

4. **Verify**:
   - Check `ActiveRideLocationDto` has `CurrentStatus` and `AgeSeconds` properties
   - Check console for successful location updates

### Test #3: Tracking Stops When Ride Completes

1. **Action**:
   - Driver completes ride (PassengerOnboard ? Completed)

2. **Expected**:
   - ? Status changes to "Completed"
   - ? Location tracking stops
   - ? Marker removed from map or grayed out
   - ? `TrackingStopped` event received

---

## ?? Deployment Steps

1. **Update DTO Models**:
   - Add `CurrentStatus` and `AgeSeconds` to `ActiveRideLocationDto`
   - Add `LocationsResponse` wrapper class
   - Add `RideStatusChangedEvent` class

2. **Update SignalR Subscription**:
   - Subscribe to `RideStatusChanged` event
   - Add event handler method
   - Wire up UI updates

3. **Update API Client**:
   - Change `GET /admin/locations` deserialization to use wrapper
   - Handle new response format

4. **Update UI**:
   - Display `CurrentRideStatus` instead of `Status`
   - Add status badge CSS
   - Add toast notification for status changes

5. **Test**:
   - Run all tests from Testing Checklist
   - Verify no console errors
   - Confirm real-time updates work

---

## ?? Status Field Reference

### Booking Status (`booking.Status`)

**Purpose**: Public-facing booking state  
**Audience**: Customers, accounting, reports  
**Values**: Requested, Confirmed, Scheduled, InProgress, Completed, Cancelled, NoShow

**When it changes**:
- **InProgress**: When `CurrentRideStatus` becomes `PassengerOnboard`
- **Completed**: When `CurrentRideStatus` becomes `Completed`
- **Cancelled**: When `CurrentRideStatus` becomes `Cancelled`

### Current Ride Status (`booking.CurrentRideStatus`)

**Purpose**: Real-time driver progress  
**Audience**: Dispatchers, drivers, passengers  
**Values**: Scheduled, OnRoute, Arrived, PassengerOnboard, Completed, Cancelled

**When it changes**: Whenever driver updates status via DriverApp

### Display Logic

```csharp
// For active rides, show CurrentRideStatus
// For unassigned or completed rides, show Status
string displayStatus = booking.CurrentRideStatus ?? booking.Status;
```

---

## ?? Debugging Tips

### SignalR Connection Issues

**Check**:
```javascript
// In browser console
console.log(connection.state); // Should be "Connected"
```

**If disconnected**:
1. Check JWT token expiration
2. Verify query string includes `access_token`
3. Check API logs for connection errors

### Status Not Updating

**Checklist**:
- [ ] `RideStatusChanged` event handler registered?
- [ ] Handler method actually called? (add `console.log`)
- [ ] UI bound to `CurrentRideStatus` not `Status`?
- [ ] SignalR connection in "Connected" state?

### Location Deserialization Errors

**Symptoms**:
```
System.Text.Json.JsonException: The JSON value could not be converted to List<ActiveRideLocationDto>
```

**Fix**:
- Use `LocationsResponse` wrapper class
- Deserialize to `envelope.Locations` not raw list

---

## ?? Related Documentation

- `DRIVER_STATUS_TIMEZONE_FIX_SUMMARY.md` - Status persistence implementation
- `REALTIME_TRACKING_BACKEND_SUMMARY.md` - Location tracking architecture
- `BELLWOOD_SYSTEM_INTEGRATION.md` - Overall system integration guide

---

## Summary

**Required Changes**:
1. ? Add `LocationsResponse` wrapper class
2. ? Update `ActiveRideLocationDto` with missing properties
3. ? Subscribe to `RideStatusChanged` SignalR event
4. ? Display `CurrentRideStatus` instead of `Status`
5. ? Add CSS for status badges

**Result**: Real-time driver status updates and location tracking work correctly in AdminPortal! ??
