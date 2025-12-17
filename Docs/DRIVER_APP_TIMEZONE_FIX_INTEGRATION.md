# DriverApp Integration Guide - Timezone Fix

## Overview

The AdminAPI now returns pickup times with explicit timezone information to prevent the 6-hour shift issue. DriverApp needs a simple one-line change to use the new property.

---

## The Problem (Before Fix)

```
Stored in DB:    Dec 16 @ 10:15 PM Central
Sent to App:     "2025-12-16T22:15:00Z" (interpreted as UTC)
Displayed:       Dec 17 @ 4:15 AM (6-hour shift!)
```

## The Solution (After Fix)

```
Stored in DB:    Dec 16 @ 10:15 PM Central
Sent to App:     "2025-12-16T22:15:00-06:00" (explicit offset)
Displayed:       Dec 16 @ 10:15 PM (correct!)
```

---

## Required Changes

### 1. Update Model (RideDto or equivalent)

**Change From**:
```csharp
public class RideDto
{
    public string Id { get; set; }
    public DateTime PickupDateTime { get; set; }  // ? OLD
    public string PickupLocation { get; set; }
    // ... other properties
}
```

**Change To**:
```csharp
public class RideDto
{
    public string Id { get; set; }
    public DateTimeOffset PickupDateTime { get; set; }  // ? NEW - Just change the type!
    public string PickupLocation { get; set; }
    // ... other properties
}
```

**That's it!** Just change `DateTime` to `DateTimeOffset`.

### 2. Update Bindings (XAML)

**No Changes Needed!** StringFormat works the same:

```xml
<Label Text="{Binding PickupDateTime, StringFormat='{0:MMM dd @ h:mm tt}'}" />
```

Output: `Dec 16 @ 10:15 PM` ?

### 3. Update Code-Behind (if manually formatting)

**Change From**:
```csharp
var formattedTime = ride.PickupDateTime.ToString("MMM dd @ h:mm tt");
```

**Change To**:
```csharp
// Same code! DateTimeOffset.ToString() works identically
var formattedTime = ride.PickupDateTime.ToString("MMM dd @ h:mm tt");
```

---

## Testing

### Before Change
```
Pickup Time:  Dec 17 @ 4:15 AM ? (6 hours too late)
```

### After Change
```
Pickup Time:  Dec 16 @ 10:15 PM ? (correct!)
```

### Test Cases

1. **Central Time Driver**:
   - Stored: Dec 16 @ 10:15 PM Central
   - Header: `X-Timezone-Id: America/Chicago`
   - Displayed: Dec 16 @ 10:15 PM ?

2. **Tokyo Driver** (Cross-Timezone):
   - Same stored time
   - Header: `X-Timezone-Id: Asia/Tokyo`
   - Displayed: Dec 17 @ 12:15 PM JST ?

---

## Backward Compatibility

**Good News**: The API returns **both** properties during transition:

```json
{
  "id": "abc123",
  "pickupDateTime": "2025-12-16T22:15:00Z",
  "pickupDateTimeOffset": "2025-12-16T22:15:00-06:00",
  "pickupLocation": "O'Hare Airport"
}
```

**Old DriverApp**: Uses `pickupDateTime` (still works, but wrong time)  
**New DriverApp**: Uses `pickupDateTimeOffset` (correct time!)

**Migration Path**:
1. Update DriverApp to use `DateTimeOffset`
2. Test thoroughly
3. Deploy
4. API will eventually remove old `pickupDateTime` property

---

## JSON Deserialization

System.Text.Json automatically handles `DateTimeOffset`:

**API Response**:
```json
{
  "pickupDateTimeOffset": "2025-12-16T22:15:00-06:00"
}
```

**Deserialized As**:
```csharp
DateTimeOffset value = DateTimeOffset.Parse("2025-12-16T22:15:00-06:00");
// Offset: -06:00 (Central Time)
// DateTime: 2025-12-16 22:15:00
// Displays correctly regardless of device timezone!
```

---

## Common Issues

### Issue: "Cannot convert DateTime to DateTimeOffset"

**Solution**: Change property type from `DateTime` to `DateTimeOffset` in your DTO/model.

### Issue: "Still seeing wrong time"

**Checklist**:
- [ ] Changed property type to `DateTimeOffset`? ?
- [ ] Using new property name (`PickupDateTimeOffset`)? ?
- [ ] Sending `X-Timezone-Id` header in API requests? ?
- [ ] Cleared app cache / reinstalled? ?

### Issue: "Null reference exception"

**Cause**: Old API responses don't include new property yet.

**Solution**: Ensure AdminAPI is updated and running the latest version.

---

## Summary

**One-Line Fix**:
```csharp
// Change this:
public DateTime PickupDateTime { get; set; }

// To this:
public DateTimeOffset PickupDateTime { get; set; }
```

**Result**: Pickup times display correctly, no more 6-hour shift! ?
