# Created On Time - Timezone Conversion Fix

**Date**: 2025-12-30  
**Status**: ? Fixed  
**Related To**: DATETIME_OFFSET_BUG_FIX.md  
**Impact**: Medium - Created On time displaying incorrectly (5 hours off for Eastern timezone)  

## Problem Summary

The "Created On" time in the Passenger App booking detail screen was displaying the time the booking was created in UTC instead of the user's local timezone. For a booking created at 8:41 AM Eastern time, it showed "Created 12/30/2025 1:41 PM" (which is 13:41 UTC).

## Screenshot Evidence

User created booking at **8:41 AM Eastern time**, but app showed:
- **Displayed**: "Created 12/30/2025 1:41 PM"
- **Expected**: "Created 12/30/2025 8:41 AM"
- **Difference**: +5 hours (UTC offset for Eastern timezone)

## Root Cause

The `CreatedUtc` field was being returned from the API as a raw `DateTime` with `Kind = Utc`:

```json
{
  "createdUtc": "2025-12-30T13:41:00Z",  // Z indicates UTC time
  ...
}
```

The mobile app was displaying this value without converting it to the user's local timezone, resulting in UTC time being shown instead of Eastern time.

## Solution

Added a new `CreatedDateTimeOffset` field to the booking endpoints that converts the UTC time to the user's local timezone (extracted from the `X-Timezone-Id` header):

```csharp
// Convert CreatedUtc to user's local timezone
var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(r.CreatedUtc, userTz);
var createdOffset = new DateTimeOffset(createdLocal, userTz.GetUtcOffset(createdLocal));

return new
{
    r.CreatedUtc, // Keep for backward compatibility
    CreatedDateTimeOffset = createdOffset, // Add timezone-aware version
    // ... other fields
};
```

## API Response Changes

### Before Fix
```json
{
  "id": "abc123",
  "createdUtc": "2025-12-30T13:41:00Z",
  ...
}
```

### After Fix
```json
{
  "id": "abc123",
  "createdUtc": "2025-12-30T13:41:00Z",          // Still present for backward compatibility
  "createdDateTimeOffset": "2025-12-30T08:41:00-05:00",  // NEW: Correct Eastern time!
  ...
}
```

## Affected Endpoints

1. **GET /bookings/list** - Passenger bookings list
2. **GET /bookings/{id}** - Passenger booking detail

## Mobile App Update Required

The Passenger App should be updated to use `CreatedDateTimeOffset` instead of `CreatedUtc` for displaying the creation time:

### Old Code (shows UTC time)
```csharp
// ? Wrong - displays UTC time
var createdTime = booking.CreatedUtc;
```

### New Code (shows local time)
```csharp
// ? Correct - displays local time
var createdTime = booking.CreatedDateTimeOffset;
```

## Testing

### Test Case 1: Eastern Timezone User

**Setup**:
- Device timezone: `America/New_York` (Eastern)
- Booking created at: 8:41 AM Eastern (13:41 UTC)

**Before Fix**:
```
Created On: 12/30/2025 1:41 PM  ? (showing UTC)
```

**After Fix**:
```
Created On: 12/30/2025 8:41 AM  ? (showing Eastern)
```

### Test Case 2: Central Timezone User

**Setup**:
- Device timezone: `America/Chicago` (Central)
- Booking created at: 7:41 AM Central (13:41 UTC)

**Before Fix**:
```
Created On: 12/30/2025 1:41 PM  ? (showing UTC)
```

**After Fix**:
```
Created On: 12/30/2025 7:41 AM  ? (showing Central)
```

## Backward Compatibility

- `CreatedUtc` field still returns UTC time (no breaking changes)
- `CreatedDateTimeOffset` field added as new field (additive change)
- Mobile apps that don't update will continue to show UTC time
- Mobile apps that update will show correct local time

## Related Fixes

This fix is part of the larger DateTime timezone conversion effort documented in:
- `DATETIME_OFFSET_BUG_FIX.md` - Main documentation
- Similar pattern used for `PickupDateTimeOffset` field

## Deployment Notes

- **API**: Deploy immediately (no breaking changes)
- **Mobile App**: Update to use `CreatedDateTimeOffset` field
- **Testing**: Verify creation time displays correctly in various timezones

---

**Resolution**: Added `CreatedDateTimeOffset` field that converts UTC time to user's local timezone.

**Next Steps**: Update Passenger App to use `CreatedDateTimeOffset` instead of `CreatedUtc`.
