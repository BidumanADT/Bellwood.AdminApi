# Worldwide Timezone Support

**Document Type**: Living Document - Feature Implementation  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document covers the **worldwide timezone support** system for the Bellwood AdminAPI, enabling operations across **400+ timezones** with automatic detection and conversion.

**Key Features**:
- ?? **Worldwide Support** - All IANA and Windows timezone IDs supported
- ?? **Automatic Detection** - Client timezone sent via HTTP header
- ?? **Correct Display** - DateTimeOffset for timezone-aware dates
- ?? **DateTime.Kind Handling** - Detects UTC vs Unspecified dates
- ? **Backward Compatible** - Fallback to Central Time if header missing

---

## ?? How It Works

### Client ? Server Flow

```
???????????????????????????????????????????????
?      Mobile App (DriverApp/PassengerApp)    ?
?  - Detects device timezone: TimeZoneInfo.Local.Id ?
?  - Sends "X-Timezone-Id: Asia/Tokyo" header ?
???????????????????????????????????????????????
                  ? HTTP GET /driver/rides/today
                  ? Header: X-Timezone-Id: Asia/Tokyo
                  ?
???????????????????????????????????????????????
?              AdminAPI Server                ?
?  1. GetRequestTimeZone(context)             ?
?     ?? Read X-Timezone-Id header            ?
?     ?? Load timezone: Asia/Tokyo            ?
?                                              ?
?  2. Filter rides in driver's local time     ?
?     ?? nowLocal = UTC ? Asia/Tokyo          ?
?     ?? tomorrowLocal = nowLocal + 24h       ?
?                                              ?
?  3. Convert pickup times to driver timezone ?
?     ?? Handle DateTime.Kind (UTC vs Unspecified) ?
?     ?? Return DateTimeOffset with correct offset ?
???????????????????????????????????????????????
                  ? JSON Response
                  ? PickupDateTimeOffset: "2024-12-24T09:00:00+09:00"
                  ?
???????????????????????????????????????????????
?              Mobile App Display              ?
?  - Shows: "Dec 24, 9:00 AM JST"            ?
?  - Automatic local formatting               ?
???????????????????????????????????????????????
```

---

## ?? Implementation

### GetRequestTimeZone Helper

**File**: `Program.cs`

```csharp
/// <summary>
/// Get timezone from request header or fallback to Central (backward compatibility).
/// Mobile apps should send X-Timezone-Id header (e.g., "America/New_York", "Europe/London").
/// </summary>
static TimeZoneInfo GetRequestTimeZone(HttpContext context)
{
    // Try to get timezone from header
    var timezoneHeader = context.Request.Headers["X-Timezone-Id"].FirstOrDefault();
    
    if (!string.IsNullOrWhiteSpace(timezoneHeader))
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneHeader);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Invalid timezone ID in header '{timezoneHeader}': {ex.Message}");
            // Fall through to default
        }
    }
    
    // Fallback: Central Time (for backward compatibility with existing deployments)
    return GetCentralTimeZone();
}

/// <summary>
/// Get Central Standard Time (Bellwood's original operating timezone).
/// Handles both Windows ("Central Standard Time") and IANA ("America/Chicago") IDs.
/// </summary>
static TimeZoneInfo GetCentralTimeZone()
{
    try
    {
        // Try Windows timezone ID first
        return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    }
    catch
    {
        // Fallback for Linux/Mac (uses IANA timezone IDs)
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch
        {
            // Last resort: return local timezone (assumes server is in Central)
            Console.WriteLine("?? Warning: Could not load Central timezone, using server local time");
            return TimeZoneInfo.Local;
        }
    }
}
```

---

### Driver Rides Endpoint (Example)

**Endpoint**: `GET /driver/rides/today`

```csharp
app.MapGet("/driver/rides/today", async (HttpContext context, IBookingRepository repo) =>
{
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    // WORLDWIDE FIX: Use timezone from request header (driver's device timezone)
    var driverTz = GetRequestTimeZone(context);
    var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, driverTz);
    var tomorrowLocal = nowLocal.AddHours(24);
    
    // Log timezone for debugging
    Console.WriteLine($"?? Driver {driverUid} timezone: {driverTz.Id}, current time: {nowLocal:yyyy-MM-dd HH:mm}");

    var allBookings = await repo.ListAsync(200);
    var driverRides = allBookings
        .Where(b => b.AssignedDriverUid == driverUid
                    && b.PickupDateTime >= nowLocal
                    && b.PickupDateTime <= tomorrowLocal
                    && b.CurrentRideStatus != RideStatus.Completed
                    && b.CurrentRideStatus != RideStatus.Cancelled)
        .OrderBy(b => b.PickupDateTime)
        .Select(b =>
        {
            // Handle both UTC and Unspecified DateTime.Kind
            DateTimeOffset pickupOffset = ConvertToDateTimeOffset(b.PickupDateTime, driverTz);
            
            return new DriverRideListItemDto
            {
                Id = b.Id,
                PickupDateTime = b.PickupDateTime, // Keep for backward compatibility
                PickupDateTimeOffset = pickupOffset, // Timezone-aware version
                PickupLocation = b.PickupLocation,
                DropoffLocation = b.DropoffLocation,
                PassengerName = b.PassengerName,
                PassengerPhone = b.Draft.Passenger?.PhoneNumber ?? "N/A",
                Status = b.CurrentRideStatus ?? RideStatus.Scheduled
            };
        })
        .ToList();

    return Results.Ok(driverRides);
})
.WithName("GetDriverRidesToday")
.RequireAuthorization("DriverOnly");
```

---

### DateTime.Kind Conversion

**Critical Fix**: Handle both UTC (seed data) and Unspecified (mobile app) dates.

```csharp
/// <summary>
/// Convert DateTime to DateTimeOffset with correct timezone offset.
/// Handles both DateTime.Kind.Utc and DateTime.Kind.Unspecified.
/// </summary>
static DateTimeOffset ConvertToDateTimeOffset(DateTime dateTime, TimeZoneInfo targetTimezone)
{
    if (dateTime.Kind == DateTimeKind.Utc)
    {
        // Convert UTC to target timezone first
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, targetTimezone);
        return new DateTimeOffset(localTime, targetTimezone.GetUtcOffset(localTime));
    }
    else
    {
        // Local or Unspecified - treat as already in target timezone
        // CRITICAL: Must convert to Unspecified to avoid ArgumentException
        var unspecified = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, targetTimezone.GetUtcOffset(unspecified));
    }
}
```

**Why This Fix is Needed**:

```csharp
// ? BROKEN (throws ArgumentException)
var local = new DateTime(2024, 12, 24, 9, 0, 0, DateTimeKind.Local);
var offset = new DateTimeOffset(local, TimeSpan.FromHours(9)); 
// Error: Offset must match DateTime.Kind.Local offset

// ? FIXED
var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
var offset = new DateTimeOffset(unspecified, TimeSpan.FromHours(9));
// Success!
```

---

## ?? DateTime Handling Scenarios

### Scenario 1: Seed Data (UTC)

**Input** (from `POST /bookings/seed`):
```json
{
  "PickupDateTime": "2024-12-24T15:00:00Z" // Z = UTC
}
```

**Storage**:
```csharp
DateTime.Kind = DateTimeKind.Utc
```

**Conversion** (for driver in Tokyo, UTC+9):
```csharp
// 1. Detect Kind = Utc
// 2. Convert UTC ? Tokyo
var localTime = TimeZoneInfo.ConvertTimeFromUtc(
    new DateTime(2024, 12, 24, 15, 0, 0, DateTimeKind.Utc), // 3 PM UTC
    tokyoTz); // Result: 2025-12-25 00:00:00 (midnight Tokyo)

// 3. Create DateTimeOffset
var offset = new DateTimeOffset(localTime, tokyoTz.GetUtcOffset(localTime));
// Result: 2025-12-25T00:00:00+09:00
```

**API Response**:
```json
{
  "PickupDateTime": "2024-12-24T15:00:00Z", // Original (backward compatibility)
  "PickupDateTimeOffset": "2024-12-25T00:00:00+09:00" // Timezone-aware
}
```

---

### Scenario 2: Mobile App Data (Unspecified)

**Input** (from PassengerApp in New York, UTC-5):
```json
{
  "PickupDateTime": "2024-12-24T09:00:00" // No Z = Unspecified
}
```

**Storage**:
```csharp
DateTime.Kind = DateTimeKind.Unspecified
```

**Conversion** (for driver in Chicago, UTC-6):
```csharp
// 1. Detect Kind = Unspecified
// 2. Treat as already in driver's timezone (Chicago)
var unspecified = DateTime.SpecifyKind(
    new DateTime(2024, 12, 24, 9, 0, 0, DateTimeKind.Unspecified),
    DateTimeKind.Unspecified);

// 3. Create DateTimeOffset with Chicago offset
var offset = new DateTimeOffset(unspecified, 
    chicagoTz.GetUtcOffset(unspecified)); // -06:00
// Result: 2024-12-24T09:00:00-06:00
```

**API Response**:
```json
{
  "PickupDateTime": "2024-12-24T09:00:00", // Original
  "PickupDateTimeOffset": "2024-12-24T09:00:00-06:00" // Timezone-aware
}
```

---

### Scenario 3: Cross-Timezone Display

**Setup**:
- Booking created in New York (UTC-5)
- Driver viewing from London (UTC+0)
- PickupDateTime stored as: `2024-12-24T14:00:00` (Unspecified)

**Conversion**:
```csharp
// Driver in London requests rides
var londonTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

// Original time: 2:00 PM (Unspecified)
// Convert to DateTimeOffset with London offset
var offset = new DateTimeOffset(
    DateTime.SpecifyKind(new DateTime(2024, 12, 24, 14, 0, 0), DateTimeKind.Unspecified),
    londonTz.GetUtcOffset(new DateTime(2024, 12, 24, 14, 0, 0)));
// Result: 2024-12-24T14:00:00+00:00
```

**Display**:
- DriverApp in London shows: **"Dec 24, 2:00 PM GMT"**

---

## ?? Supported Timezones

### IANA Timezone IDs (400+)

| Region | Examples |
|--------|----------|
| **North America** | `America/New_York`, `America/Chicago`, `America/Los_Angeles`, `America/Denver` |
| **Europe** | `Europe/London`, `Europe/Paris`, `Europe/Berlin`, `Europe/Moscow` |
| **Asia** | `Asia/Tokyo`, `Asia/Shanghai`, `Asia/Dubai`, `Asia/Kolkata` |
| **Australia** | `Australia/Sydney`, `Australia/Melbourne`, `Australia/Perth` |
| **South America** | `America/Sao_Paulo`, `America/Buenos_Aires` |
| **Africa** | `Africa/Cairo`, `Africa/Johannesburg`, `Africa/Lagos` |

### Windows Timezone IDs

| IANA ID | Windows ID |
|---------|------------|
| `America/New_York` | `Eastern Standard Time` |
| `America/Chicago` | `Central Standard Time` |
| `America/Los_Angeles` | `Pacific Standard Time` |
| `Europe/London` | `GMT Standard Time` |
| `Asia/Tokyo` | `Tokyo Standard Time` |
| `Australia/Sydney` | `AUS Eastern Standard Time` |

**Note**: .NET automatically maps between IANA and Windows IDs on Windows/Linux.

---

## ?? Client Implementation

### DriverApp (MAUI)

**Detecting Device Timezone**:
```csharp
public class LocationService
{
    private readonly HttpClient _httpClient;
    
    public LocationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        // Set timezone header on all requests
        var timezoneId = TimeZoneInfo.Local.Id;
        _httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", timezoneId);
        
        Console.WriteLine($"?? Device timezone: {timezoneId}");
        // Example: "America/New_York" (IANA) or "Eastern Standard Time" (Windows)
    }
    
    public async Task<List<RideDto>> GetTodaysRidesAsync()
    {
        // Timezone header automatically included
        var response = await _httpClient.GetAsync("/driver/rides/today");
        response.EnsureSuccessStatusCode();
        
        var rides = await response.Content.ReadFromJsonAsync<List<RideDto>>();
        return rides;
    }
}
```

**Displaying DateTimeOffset**:
```csharp
public class RideViewModel
{
    public string DisplayPickupTime(RideDto ride)
    {
        // Option 1: Use PickupDateTimeOffset (timezone-aware)
        var pickupTime = ride.PickupDateTimeOffset;
        return pickupTime.ToString("MMM dd, h:mm tt zzz");
        // Output: "Dec 24, 9:00 AM +09:00"
        
        // Option 2: Convert to local time for display
        var localTime = pickupTime.ToLocalTime();
        return localTime.ToString("MMM dd, h:mm tt");
        // Output: "Dec 24, 9:00 AM" (automatically in device timezone)
    }
}
```

---

### PassengerApp (MAUI)

**Sending Timezone on Booking Creation**:
```csharp
public class BookingService
{
    private readonly HttpClient _httpClient;
    
    public BookingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", TimeZoneInfo.Local.Id);
    }
    
    public async Task<BookingDto> CreateBookingAsync(QuoteDraft draft)
    {
        // PickupDateTime sent as Unspecified (no timezone)
        draft.PickupDateTime = new DateTime(2024, 12, 24, 9, 0, 0, DateTimeKind.Unspecified);
        
        var response = await _httpClient.PostAsJsonAsync("/bookings", draft);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<BookingDto>();
    }
}
```

---

### AdminPortal (Blazor)

**Displaying Bookings in Admin Timezone**:
```csharp
@code {
    private List<BookingDto> bookings;
    private string adminTimezone = "America/Chicago"; // Default
    
    protected override async Task OnInitializedAsync()
    {
        // Detect admin's timezone (browser-based)
        adminTimezone = await JSRuntime.InvokeAsync<string>("getTimezone");
        // JavaScript: return Intl.DateTimeFormat().resolvedOptions().timeZone;
        
        // Fetch bookings with admin timezone header
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-Timezone-Id", adminTimezone);
        
        var response = await http.GetAsync("https://localhost:5206/bookings/list");
        bookings = await response.Content.ReadFromJsonAsync<List<BookingDto>>();
    }
    
    string FormatPickupTime(BookingDto booking)
    {
        // Use DateTimeOffset for correct display
        return booking.PickupDateTimeOffset.ToString("MMM dd, h:mm tt zzz");
    }
}
```

---

## ?? Testing

### Manual Testing Workflow

**1. Test Different Timezones**:

```bash
# Driver in Tokyo (UTC+9)
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {token}" \
  -H "X-Timezone-Id: Asia/Tokyo"
# Expected: Rides filtered for Tokyo time, PickupDateTimeOffset has +09:00

# Driver in New York (UTC-5)
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {token}" \
  -H "X-Timezone-Id: America/New_York"
# Expected: Rides filtered for NY time, PickupDateTimeOffset has -05:00

# Driver in London (UTC+0)
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {token}" \
  -H "X-Timezone-Id: Europe/London"
# Expected: Rides filtered for London time, PickupDateTimeOffset has +00:00
```

**2. Test Missing Header (Backward Compatibility)**:

```bash
# No X-Timezone-Id header (should fallback to Central)
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {token}"
# Expected: Rides filtered for Central time, PickupDateTimeOffset has -06:00 (or -05:00 during DST)
```

**3. Test DateTime.Kind Handling**:

```powershell
# Seed data with UTC dates
curl -X POST https://localhost:5206/bookings/seed \
  -H "Authorization: Bearer {adminToken}"
# Creates bookings with DateTime.Kind = Utc

# Create booking from PassengerApp (Unspecified)
curl -X POST https://localhost:5206/bookings \
  -H "Authorization: Bearer {passengerToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "booker": {...},
    "passenger": {...},
    "pickupDateTime": "2024-12-24T09:00:00", // No Z = Unspecified
    "pickupLocation": "Airport",
    "dropoffLocation": "Hotel"
  }'

# Get bookings (should see both types converted correctly)
curl -X GET https://localhost:5206/bookings/list?take=20 \
  -H "Authorization: Bearer {adminToken}" \
  -H "X-Timezone-Id: America/New_York"
# Expected: All PickupDateTimeOffset values have correct offset
```

---

### Unit Tests

**Testing GetRequestTimeZone**:

```csharp
[Fact]
public void GetRequestTimeZone_WithValidHeader_ReturnsCorrectTimezone()
{
    // Arrange
    var context = new DefaultHttpContext();
    context.Request.Headers["X-Timezone-Id"] = "Asia/Tokyo";
    
    // Act
    var timezone = GetRequestTimeZone(context);
    
    // Assert
    Assert.Equal("Asia/Tokyo", timezone.Id);
}

[Fact]
public void GetRequestTimeZone_WithInvalidHeader_FallsBackToCentral()
{
    // Arrange
    var context = new DefaultHttpContext();
    context.Request.Headers["X-Timezone-Id"] = "Invalid/Timezone";
    
    // Act
    var timezone = GetRequestTimeZone(context);
    
    // Assert
    Assert.True(timezone.Id == "Central Standard Time" || 
                timezone.Id == "America/Chicago");
}

[Fact]
public void GetRequestTimeZone_WithoutHeader_FallsBackToCentral()
{
    // Arrange
    var context = new DefaultHttpContext();
    
    // Act
    var timezone = GetRequestTimeZone(context);
    
    // Assert
    Assert.True(timezone.Id == "Central Standard Time" || 
                timezone.Id == "America/Chicago");
}
```

**Testing DateTime.Kind Conversion**:

```csharp
[Fact]
public void ConvertToDateTimeOffset_WithUtc_ConvertsCorrectly()
{
    // Arrange
    var utcDateTime = new DateTime(2024, 12, 24, 15, 0, 0, DateTimeKind.Utc); // 3 PM UTC
    var tokyoTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
    
    // Act
    var offset = ConvertToDateTimeOffset(utcDateTime, tokyoTz);
    
    // Assert
    Assert.Equal(new DateTimeOffset(2024, 12, 25, 0, 0, 0, TimeSpan.FromHours(9)), offset);
    // 3 PM UTC = Midnight Tokyo (next day)
}

[Fact]
public void ConvertToDateTimeOffset_WithUnspecified_PreservesTime()
{
    // Arrange
    var unspecifiedDateTime = new DateTime(2024, 12, 24, 9, 0, 0, DateTimeKind.Unspecified);
    var nyTz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    
    // Act
    var offset = ConvertToDateTimeOffset(unspecifiedDateTime, nyTz);
    
    // Assert
    Assert.Equal(9, offset.Hour); // Time preserved
    Assert.Equal(24, offset.Day);  // Date preserved
    Assert.Equal(TimeSpan.FromHours(-5), offset.Offset); // Offset correct (EST)
}
```

---

## ?? Troubleshooting

### Issue 1: Pickup Times 6 Hours Off

**Symptom**: All pickup times in DriverApp show incorrect time (e.g., 3 AM instead of 9 AM)

**Cause**: Seed data uses UTC, but app expects local time

**Diagnosis**:
```bash
# Check PickupDateTime.Kind in database
# Seed data: DateTime.Kind = Utc
# Mobile app: DateTime.Kind = Unspecified

# Check if X-Timezone-Id header is sent
curl -v https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {token}"
# Look for: X-Timezone-Id: America/Chicago
```

**Fix**: API now handles both:
```csharp
// Before (broken)
var offset = new DateTimeOffset(pickup, userTz.GetUtcOffset(pickup));
// If pickup.Kind = Utc, offset is wrong!

// After (fixed)
if (pickup.Kind == DateTimeKind.Utc)
{
    var local = TimeZoneInfo.ConvertTimeFromUtc(pickup, userTz);
    var offset = new DateTimeOffset(local, userTz.GetUtcOffset(local));
}
```

---

### Issue 2: ArgumentException on DateTimeOffset Creation

**Symptom**: API throws exception:
```
System.ArgumentException: The supplied DateTime must have the Kind property set to Unspecified or its offset must match the offset of the supplied TimeZoneInfo
```

**Cause**: Trying to create DateTimeOffset with Local DateTime and custom offset

**Broken Code**:
```csharp
var local = new DateTime(2024, 12, 24, 9, 0, 0, DateTimeKind.Local);
var offset = new DateTimeOffset(local, TimeSpan.FromHours(9)); 
// Error! Local kind must use system local offset, not custom offset
```

**Fix**:
```csharp
var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
var offset = new DateTimeOffset(unspecified, TimeSpan.FromHours(9));
// Success!
```

---

### Issue 3: Driver Sees No Rides (Timezone Filter)

**Symptom**: DriverApp shows empty list despite assigned rides

**Cause**: Rides filtered based on wrong timezone

**Diagnosis**:
```bash
# Check server logs for timezone detection
# Expected: "?? Driver driver-001 timezone: Asia/Tokyo, current time: 2024-12-24 20:00"

# If missing, X-Timezone-Id header not sent
```

**Fix**: Ensure DriverApp sends header:
```csharp
// DriverApp initialization
_httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", TimeZoneInfo.Local.Id);
```

**Temporary Workaround**: Use Central Time manually:
```bash
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {token}" \
  -H "X-Timezone-Id: America/Chicago"
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system integration
- `10-Real-Time-Tracking.md` - GPS tracking implementation
- `13-Driver-Integration.md` - Driver endpoints
- `20-API-Reference.md` - Complete endpoint documentation
- `32-Troubleshooting.md` - Common issues & solutions

---

## ?? Future Enhancements

### Phase 3+ Roadmap

1. **Store Timezone with Booking**:
   ```csharp
   public class BookingRecord
   {
       // Existing fields...
       
       // Phase 3: Store timezone ID for reference
       public string? PickupTimezoneId { get; set; } // e.g., "America/New_York"
   }
   ```

2. **Timezone Conversion API**:
   ```csharp
   // GET /api/timezones/convert?from=2024-12-24T15:00:00Z&fromTz=UTC&toTz=Asia/Tokyo
   app.MapGet("/api/timezones/convert", (
       DateTime dateTime,
       string fromTz,
       string toTz) =>
   {
       var fromTimezone = TimeZoneInfo.FindSystemTimeZoneById(fromTz);
       var toTimezone = TimeZoneInfo.FindSystemTimeZoneById(toTz);
       
       var utc = TimeZoneInfo.ConvertTimeToUtc(dateTime, fromTimezone);
       var converted = TimeZoneInfo.ConvertTimeFromUtc(utc, toTimezone);
       
       return Results.Ok(new {
           original = dateTime,
           converted = converted,
           offset = toTimezone.GetUtcOffset(converted)
       });
   });
   ```

3. **Timezone-Aware Search**:
   - Search bookings by pickup time in specific timezone
   - "Show me all rides between 8 AM - 10 AM EST"

4. **DST Handling Improvements**:
   - Warn when pickup time falls during DST transition
   - Auto-adjust for spring forward / fall back

5. **Multi-Timezone Dashboard**:
   - AdminPortal shows rides in multiple timezones simultaneously
   - Clock widget showing current time in all active timezones

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Next Steps**: See Phase 3 Roadmap above
