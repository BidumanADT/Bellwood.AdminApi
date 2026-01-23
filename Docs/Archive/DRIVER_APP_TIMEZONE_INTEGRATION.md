# Driver App Integration Guide - Worldwide Timezone Support

## Overview

The AdminAPI now supports worldwide timezone operations. The Driver App must send the device's timezone in every API request to ensure rides appear correctly regardless of where the driver is located.

## Required Changes

### 1. Add Timezone Header to All API Requests

The Driver App must include the `X-Timezone-Id` header with the device's timezone identifier.

### Implementation (C# / .NET MAUI)

#### Option A: Add to HttpClient Setup (Recommended)

```csharp
// In your API client service (e.g., Services/BellwoodApiService.cs)

public class BellwoodApiService
{
    private readonly HttpClient _httpClient;
    
    public BellwoodApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        // Get device timezone and add to all requests
        var timezoneId = TimeZoneInfo.Local.Id;
        _httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", timezoneId);
        
        Console.WriteLine($"?? Device timezone: {timezoneId}");
    }
    
    // Your existing API methods...
    public async Task<List<RideDto>> GetTodaysRidesAsync()
    {
        // X-Timezone-Id header is automatically included
        var response = await _httpClient.GetAsync("/driver/rides/today");
        // ...
    }
}
```

#### Option B: Add Per-Request (Alternative)

```csharp
public async Task<List<RideDto>> GetTodaysRidesAsync()
{
    var request = new HttpRequestMessage(HttpMethod.Get, "/driver/rides/today");
    
    // Add timezone header to this specific request
    request.Headers.Add("X-Timezone-Id", TimeZoneInfo.Local.Id);
    
    var response = await _httpClient.SendAsync(request);
    // ...
}
```

### 2. Verify Timezone Detection

Add logging to verify the correct timezone is being sent:

```csharp
// In app startup or API service initialization
var timezoneId = TimeZoneInfo.Local.Id;
var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

Console.WriteLine($"?? Device Timezone ID: {timezoneId}");
Console.WriteLine($"? Current UTC Offset: {offset.TotalHours:+0;-0} hours");
Console.WriteLine($"?? Current Local Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
```

### 3. Test Scenarios

#### Test Case 1: Driver in New York
```
Expected Header: X-Timezone-Id: America/New_York
Expected Behavior: Rides scheduled in EST/EDT time appear correctly
```

#### Test Case 2: Driver in London
```
Expected Header: X-Timezone-Id: Europe/London
Expected Behavior: Rides scheduled in GMT/BST time appear correctly
```

#### Test Case 3: Driver in Tokyo
```
Expected Header: X-Timezone-Id: Asia/Tokyo
Expected Behavior: Rides scheduled in JST time appear correctly
```

## Platform-Specific Notes

### Android
- Returns IANA timezone IDs (e.g., "America/New_York")
- Works directly with backend ?

### iOS
- Returns IANA timezone IDs (e.g., "America/New_York")
- Works directly with backend ?

### Windows
- Returns Windows timezone IDs (e.g., "Eastern Standard Time")
- Backend automatically converts to IANA format ?

## Common Timezone IDs

| Location | Timezone ID |
|----------|-------------|
| **USA** |
| New York / Eastern | America/New_York |
| Chicago / Central | America/Chicago |
| Denver / Mountain | America/Denver |
| Los Angeles / Pacific | America/Los_Angeles |
| **Europe** |
| London | Europe/London |
| Paris / Berlin | Europe/Paris |
| **Asia** |
| Tokyo | Asia/Tokyo |
| Singapore | Asia/Singapore |
| Dubai | Asia/Dubai |
| Hong Kong | Asia/Hong_Kong |
| **Australia** |
| Sydney | Australia/Sydney |
| **South America** |
| São Paulo | America/Sao_Paulo |

## Debugging

### Check Header is Being Sent

Add request logging:

```csharp
public class LoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"?? Request: {request.Method} {request.RequestUri}");
        
        if (request.Headers.TryGetValues("X-Timezone-Id", out var values))
        {
            Console.WriteLine($"?? Timezone Header: {string.Join(", ", values)}");
        }
        else
        {
            Console.WriteLine($"?? WARNING: No timezone header found!");
        }
        
        return await base.SendAsync(request, cancellationToken);
    }
}

// Register in DI:
builder.Services.AddHttpClient<BellwoodApiService>()
    .AddHttpMessageHandler<LoggingHandler>();
```

### Check Backend Logs

The backend logs timezone detection:
```
?? Driver driver-001 timezone: America/New_York, current time: 2025-12-14 20:04
```

If you see:
```
?? Warning: Could not load Central timezone, using server local time
```
...the header is either missing or invalid.

## Backward Compatibility

If the header is **not sent**, the backend defaults to **Central Time** (America/Chicago) for backward compatibility. However, this will cause incorrect ride filtering for drivers outside the Central timezone.

**Therefore, including the header is strongly recommended for correct operation.**

## FAQ

### Q: What if the device timezone is wrong?

**A:** The app will send the device's configured timezone. If the user has manually set an incorrect timezone in their device settings, rides will appear based on that incorrect time. This is expected behavior - the user should correct their device settings.

### Q: Do I need to update the header if the device timezone changes?

**A:** Yes, but this is rare. If the user travels to a different timezone and their device updates automatically, the app should restart or the HTTP client should be recreated to pick up the new timezone.

### Q: What happens during Daylight Saving Time transitions?

**A:** `TimeZoneInfo.Local` automatically reflects the current DST status. The backend also handles DST automatically via `TimeZoneInfo.ConvertTime()`. No special handling required.

### Q: Can I hardcode a specific timezone for testing?

**A:** Yes, for testing only:

```csharp
// FOR TESTING ONLY - Override with specific timezone
var testTimezone = "Asia/Tokyo";
_httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", testTimezone);
```

Remember to remove this and use `TimeZoneInfo.Local.Id` for production.

## Migration Plan

1. **Phase 1**: Add header to Driver App (this change)
2. **Phase 2**: Verify logs show correct timezone detection
3. **Phase 3**: Test with drivers in different timezones
4. **Phase 4**: Remove hardcoded Central Time fallback (future)

## Support

If you encounter issues:
1. Check device timezone settings
2. Verify header is being sent (see Debugging section)
3. Check backend logs for timezone detection
4. Test with known ride at specific time

## Summary

**Required Change:**
```csharp
// Add this to your API client setup:
_httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", TimeZoneInfo.Local.Id);
```

**Result:**
- ? Rides appear correctly for drivers worldwide
- ? No code changes needed for different locations
- ? Automatic DST handling
- ? Cross-platform compatible
