# Troubleshooting Guide

**Document Type**: Living Document - Deployment & Operations  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides comprehensive troubleshooting guidance for common issues with the Bellwood AdminAPI, including diagnostics, solutions, and preventive measures.

**Target Audience**: Developers, DevOps, Support Teams

---

## ?? Diagnostic Checklist

Before diving into specific issues, run through this quick diagnostic checklist:

### System Health

```bash
# 1. Check AdminAPI health
curl https://localhost:5206/health
# Expected: {"status":"ok"}

# 2. Check AuthServer health
curl https://localhost:5001/health
# Expected: {"status":"Healthy"}

# 3. Verify AdminAPI is running
Get-Process -Name Bellwood.AdminApi -ErrorAction SilentlyContinue

# 4. Check data files exist
Test-Path ./App_Data/bookings.json
Test-Path ./App_Data/quotes.json
Test-Path ./App_Data/affiliates.json
Test-Path ./App_Data/drivers.json
```

### Log Files

**Locations**:
- **Development**: Console output
- **IIS**: `C:\inetpub\wwwroot\BellwoodAdminAPI\logs\stdout`
- **Docker**: `docker logs bellwood-api`
- **Azure**: Application Insights or Log Stream

**View Logs**:

```powershell
# IIS
Get-Content "C:\inetpub\wwwroot\BellwoodAdminAPI\logs\stdout*.log" -Tail 50

# Docker
docker logs bellwood-api --tail 50 --follow

# Azure
az webapp log tail --name bellwood-adminapi --resource-group BellwoodAPI
```

---

## ?? Common Issues & Solutions

### Issue 1: "HTTP 401 Unauthorized" on all endpoints

**Symptom**: Every authenticated endpoint returns 401.

**Possible Causes**:

1. **JWT token not provided**
2. **Token expired**
3. **Invalid token signature**
4. **Mismatched JWT signing keys**

**Diagnostics**:

```powershell
# Check if token is included in request
curl https://localhost:5206/quotes/list `
  -H "Authorization: Bearer $token" `
  -v
# Look for "Authorization: Bearer" in request headers

# Decode token payload
$parts = $token.Split('.')
$payload = $parts[1]
$padding = (4 - ($payload.Length % 4)) % 4
$payload = $payload + ("=" * $padding)
$bytes = [Convert]::FromBase64String($payload)
$json = [System.Text.Encoding]::UTF8.GetString($bytes)
$claims = $json | ConvertFrom-Json
$claims | Format-List

# Check expiration
$exp = [DateTimeOffset]::FromUnixTimeSeconds($claims.exp)
$now = [DateTimeOffset]::UtcNow
Write-Host "Token expires: $exp"
Write-Host "Current time:  $now"
Write-Host "Expired: $($exp -lt $now)"
```

**Solutions**:

**Solution 1: Token Expired**
```powershell
# Get a new token
$response = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
  -Method POST `
  -ContentType "application/json" `
  -Body '{"username":"alice","password":"password"}'
$token = $response.accessToken
```

**Solution 2: Mismatched Signing Keys**
```json
// Verify both appsettings.json have same key
// AuthServer/appsettings.json
{
  "Jwt": {
    "Key": "super-long-jwt-signing-secret-1234"  // ? Must match
  }
}

// AdminAPI/appsettings.json
{
  "Jwt": {
    "Key": "super-long-jwt-signing-secret-1234"  // ? Must match
  }
}
```

**Solution 3: Missing Authorization Header**
```javascript
// ? Wrong
fetch('/quotes/list')

// ? Correct
fetch('/quotes/list', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
})
```

---

### Issue 2: "HTTP 403 Forbidden" (User has valid token)

**Symptom**: Authenticated user receives 403 on specific endpoints.

**Possible Causes**:

1. **Missing role claim**
2. **Insufficient permissions**
3. **Policy requirement not met**
4. **Ownership check failed**

**Diagnostics**:

```powershell
# Check user's role claim
$claims = Get-JwtPayload -Token $token
Write-Host "Username: $($claims.sub)"
Write-Host "Role: $($claims.role)"
Write-Host "UID: $($claims.uid)"

# Check endpoint policy requirement (see 20-API-Reference.md)
# AdminOnly: Requires role = "admin"
# StaffOnly: Requires role = "admin" OR "dispatcher"
# DriverOnly: Requires role = "driver"
```

**Solutions**:

**Solution 1: Missing Role Claim**

Check AuthServer user registration:
```csharp
// AuthServer: Ensure role is set
public async Task<IActionResult> Register(RegisterRequest request)
{
    var user = new User
    {
        Username = request.Username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        Role = "admin",  // ? Must be set!
        // ...
    };
}
```

**Solution 2: Wrong Policy for Endpoint**

```csharp
// ? Wrong - Dispatcher cannot access AdminOnly
app.MapPost("/dev/seed-affiliates", ...)
    .RequireAuthorization("AdminOnly"); // Only admin

// ? Correct - Use StaffOnly for operational access
app.MapGet("/bookings/list", ...)
    .RequireAuthorization("StaffOnly"); // admin OR dispatcher
```

**Solution 3: Ownership Check Failed (Non-Staff)**

```csharp
// User must own the record OR be staff
if (!CanAccessBooking(user, booking.CreatedByUserId, booking.AssignedDriverUid))
{
    return Results.Problem(statusCode: 403, title: "Forbidden");
}

// Check if record has CreatedByUserId set
// Legacy records (null) are only visible to staff
```

---

### Issue 3: Role-based authorization not working (`MapInboundClaims`)

**Symptom**: User has correct role in JWT, but `IsInRole()` returns false.

**Cause**: `MapInboundClaims = true` (default) remaps "role" claim.

**Diagnostic**:

```csharp
// Check if role claim is being remapped
// Log claims in OnTokenValidated event:
var roleClaim = context.Principal?.FindFirst("role");
if (roleClaim != null)
{
    Console.WriteLine($"? Role found: {roleClaim.Value}");
}
else
{
    Console.WriteLine($"? NO ROLE CLAIM FOUND!");
    // Likely remapped to: http://schemas.microsoft.com/ws/2008/06/identity/claims/role
}
```

**Solution**:

```csharp
// Program.cs - CRITICAL FIX
builder.Services.AddAuthentication(...)
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;  // ? ADD THIS!
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // ...
        RoleClaimType = "role",
        NameClaimType = "sub"
    };
});
```

**Why This Works**:
- Prevents ASP.NET Core from remapping "role" to long URI claim type
- Ensures `context.User.IsInRole("admin")` works correctly

---

### Issue 4: SignalR WebSocket connection fails

**Symptom**: `connection.start()` fails with connection error.

**Possible Causes**:

1. **WebSockets not enabled (IIS)**
2. **Token not passed in query string**
3. **CORS blocking connection**
4. **SSL certificate issues**

**Diagnostics**:

```javascript
// Check browser console
connection.start()
  .then(() => console.log("? Connected"))
  .catch(err => console.error("? Connection failed:", err));

// Check if WebSockets supported
const ws = new WebSocket("wss://localhost:5206/hubs/location");
ws.onerror = (err) => console.error("WebSocket error:", err);
```

**Solutions**:

**Solution 1: Enable WebSockets (IIS)**

```powershell
# Install WebSocket feature
Install-WindowsFeature -Name Web-WebSockets

# Restart IIS
iisreset
```

**Solution 2: Pass Token in Query String**

```javascript
// ? Wrong - Headers don't work for WebSocket initial handshake
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location")
    .build();

// ? Correct - Token in query string
const connection = new signalR.HubConnectionBuilder()
    .withUrl(`https://localhost:5206/hubs/location?access_token=${token}`)
    .build();
```

**Solution 3: Configure CORS**

```csharp
// Program.cs
builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
    policy
        .WithOrigins("https://localhost:3000") // Your client app
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials())); // ? Required for SignalR
```

---

### Issue 5: "File not found" errors on startup

**Symptom**: JSON files throw "file not found" or "invalid JSON" errors.

**Cause**: Repository tries to read files before they're created.

**Diagnostic**:

```powershell
# Check if files exist
Get-ChildItem ./App_Data/*.json

# Check file contents
Get-Content ./App_Data/bookings.json
# Should be: [] (empty array, not empty file)
```

**Solution**:

**Fixed in Phase 1** - Repositories now create empty files automatically:

```csharp
// FileBookingRepository.cs
private async Task EnsureInitializedAsync()
{
    if (_initialized) return;
    
    await _gate.WaitAsync();
    try
    {
        if (_initialized) return;
        
        if (!File.Exists(_filePath))
        {
            await File.WriteAllTextAsync(_filePath, "[]");  // ? Create empty array
        }
        
        _initialized = true;
    }
    finally { _gate.Release(); }
}
```

**Manual Fix** (if needed):

```powershell
# Create App_Data directory
New-Item -ItemType Directory -Path ./App_Data -Force

# Create empty JSON files
"[]" | Out-File -FilePath ./App_Data/bookings.json -Encoding UTF8
"[]" | Out-File -FilePath ./App_Data/quotes.json -Encoding UTF8
"[]" | Out-File -FilePath ./App_Data/affiliates.json -Encoding UTF8
"[]" | Out-File -FilePath ./App_Data/drivers.json -Encoding UTF8
```

---

### Issue 6: Driver cannot see assigned rides (`/driver/rides/today`)

**Symptom**: DriverApp shows "No upcoming rides" for Charlie, but bookings exist.

**Possible Causes**:

1. **Missing `AssignedDriverUid` on booking**
2. **UID mismatch** (booking.AssignedDriverUid ? token.uid)
3. **Pickup time outside 24-hour window**
4. **Ride status is Completed or Cancelled**

**Diagnostics**:

```powershell
# Check booking data
$adminToken = "..." # Get admin token
$bookings = Invoke-RestMethod -Uri "https://localhost:5206/bookings/list" `
  -Headers @{ "Authorization" = "Bearer $adminToken" }

# Find Charlie's bookings
$charlieRides = $bookings | Where-Object { $_.assignedDriverUid -eq "driver-001" }
$charlieRides | Format-Table id, passengerName, pickupDateTime, assignedDriverUid, currentRideStatus

# Check Charlie's token
$charlieToken = "..." # Login as charlie
$claims = Get-JwtPayload -Token $charlieToken
Write-Host "Charlie's UID: $($claims.uid)"
Write-Host "Expected: driver-001"
```

**Solutions**:

**Solution 1: Missing AssignedDriverUid**

```powershell
# Assign driver correctly
$body = @{ driverId = "driver-abc-123" } | ConvertTo-Json
Invoke-RestMethod -Uri "https://localhost:5206/bookings/$bookingId/assign-driver" `
  -Method POST `
  -Headers $adminHeaders `
  -Body $body `
  -ContentType "application/json"

# Verify AssignedDriverUid is set
$updated = Invoke-RestMethod -Uri "https://localhost:5206/bookings/$bookingId" `
  -Headers $adminHeaders
$updated.assignedDriverUid  # Should be "driver-001"
```

**Solution 2: UID Mismatch**

```csharp
// Driver entity must have UserUid matching AuthServer
var driver = new Driver
{
    Name = "Charlie Johnson",
    UserUid = "driver-001",  // ? Must match AuthServer user's UID
    AffiliateId = "affiliate-abc"
};

// AuthServer user must have matching UID
var user = new User
{
    Username = "charlie",
    UID = "driver-001",  // ? Must match Driver.UserUid
    Role = "driver"
};
```

**Solution 3: Pickup Time Outside Window**

```csharp
// Rides must be within next 24 hours
var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, driverTz);
var tomorrowLocal = nowLocal.AddHours(24);

// Check if pickup is within window
var pickupTime = booking.PickupDateTime;
if (pickupTime >= nowLocal && pickupTime <= tomorrowLocal)
{
    // ? Visible to driver
}
```

---

### Issue 7: Location updates rejected (429 Too Many Requests)

**Symptom**: DriverApp location updates fail with 429 status.

**Cause**: Rate limiting enforced (10 seconds minimum between updates).

**Diagnostic**:

```csharp
// Check time since last update
var existing = _locations[rideId];
var timeSinceLastUpdate = DateTime.UtcNow - existing.StoredAt;
Console.WriteLine($"Time since last update: {timeSinceLastUpdate.TotalSeconds}s");
Console.WriteLine($"Minimum interval: 10s");
```

**Solutions**:

**Solution 1: Adjust DriverApp Update Frequency**

```csharp
// DriverApp - Don't send updates too frequently
private TimeSpan _minUpdateInterval = TimeSpan.FromSeconds(15);  // ? 15s is safe
private DateTime _lastUpdateTime = DateTime.MinValue;

public async Task SendLocationUpdate()
{
    var now = DateTime.UtcNow;
    if (now - _lastUpdateTime < _minUpdateInterval)
    {
        return; // Skip update
    }
    
    // Send update
    await _apiClient.PostAsync("/driver/location/update", update);
    _lastUpdateTime = now;
}
```

**Solution 2: Adjust Server Rate Limit** (if needed):

```csharp
// InMemoryLocationService.cs
private readonly TimeSpan _minUpdateInterval = TimeSpan.FromSeconds(10);
// ??  Don't set below 5 seconds to prevent server overload
```

---

### Issue 8: Passenger cannot see ride location (`/passenger/rides/{id}/location`)

**Symptom**: Passenger gets 403 Forbidden when trying to track ride.

**Possible Causes**:

1. **Email mismatch** (token email ? booker/passenger email)
2. **Missing email claim in token**
3. **Ride not assigned to driver yet**
4. **Location tracking not started**

**Diagnostics**:

```powershell
# Check passenger token
$passengerToken = "..." # Get passenger token
$claims = Get-JwtPayload -Token $passengerToken
Write-Host "Token email: $($claims.email)"

# Check booking data
$booking = Invoke-RestMethod -Uri "https://localhost:5206/bookings/$rideId" `
  -Headers $adminHeaders
Write-Host "Booker email: $($booking.draft.booker.emailAddress)"
Write-Host "Passenger email: $($booking.draft.passenger.emailAddress)"
```

**Solutions**:

**Solution 1: Email Mismatch**

Passenger must authenticate with email matching booking:

```csharp
// PassengerApp - Login with correct email
var loginRequest = new
{
    email = "jordan.chen@example.com",  // ? Must match booking
    bookingId = "booking-xyz"
};

// AuthServer returns token with email claim
{
  "email": "jordan.chen@example.com",  // ? Matches booking
  "sub": "passenger-123",
  // ...
}
```

**Solution 2: Location Not Started**

```javascript
// PassengerApp - Handle "tracking not active" response
const response = await fetch(`/passenger/rides/${rideId}/location`, {
  headers: { 'Authorization': `Bearer ${token}` }
});
const data = await response.json();

if (!data.trackingActive) {
  // ? Show message: "Driver hasn't started yet"
  showMessage("Your driver will start soon");
} else {
  // ? Show map with driver location
  updateMap(data.latitude, data.longitude);
}
```

---

### Issue 9: OAuth credentials not decrypting (production)

**Symptom**: "Invalid payload" or decryption errors when reading OAuth credentials.

**Cause**: Data Protection keys not shared across instances.

**Diagnostic**:

```powershell
# Check if keys exist
Get-ChildItem "C:\keys\DataProtection-Keys" # Windows
Get-ChildItem "/var/keys" # Linux

# Check if keys are being shared
# Multi-instance deployments MUST share keys
```

**Solutions**:

**Solution 1: Azure Blob Storage** (Recommended):

```csharp
// Program.cs
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(
        new Uri("https://bellwood.blob.core.windows.net/keys/dataprotection-keys"),
        new DefaultAzureCredential());
```

**Solution 2: File System** (Single Server):

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\keys"));
```

**Solution 3: Redis** (Distributed):

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
```

---

### Issue 10: SMTP email sending fails

**Symptom**: Quote/booking emails not sent, errors in logs.

**Possible Causes**:

1. **Invalid SMTP credentials**
2. **Firewall blocking port 587**
3. **Gmail "less secure apps" disabled**
4. **App-specific password required**

**Diagnostics**:

```csharp
// Check SMTP settings
public class EmailOptions
{
    public string SmtpServer { get; set; }    // "smtp.gmail.com"
    public int SmtpPort { get; set; }         // 587
    public string SmtpUsername { get; set; }  // "your-email@gmail.com"
    public string SmtpPassword { get; set; }  // "app-password"
}

// Test SMTP connection
using var client = new SmtpClient(options.SmtpServer, options.SmtpPort);
client.EnableSsl = true;
client.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword);
try
{
    await client.SendMailAsync(testEmail);
    Console.WriteLine("? SMTP test successful");
}
catch (Exception ex)
{
    Console.WriteLine($"? SMTP test failed: {ex.Message}");
}
```

**Solutions**:

**Solution 1: Gmail App Password**

1. Go to Google Account ? Security
2. Enable 2-Step Verification
3. Generate App Password
4. Use app password in `appsettings.json`

```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "abcd efgh ijkl mnop"  // ? App-specific password
  }
}
```

**Solution 2: Office 365**

```json
{
  "Email": {
    "SmtpServer": "smtp.office365.com",
    "SmtpPort": 587,
    "SmtpUsername": "noreply@bellwood.com",
    "SmtpPassword": "your-password"
  }
}
```

**Solution 3: SendGrid (Production)**

```csharp
// Use SendGrid API instead of SMTP
dotnet add package SendGrid

public async Task SendEmailAsync(string to, string subject, string body)
{
    var client = new SendGridClient(apiKey);
    var from = new EmailAddress("noreply@bellwood.com", "Bellwood");
    var toAddress = new EmailAddress(to);
    var msg = MailHelper.CreateSingleEmail(from, toAddress, subject, body, body);
    await client.SendEmailAsync(msg);
}
```

---

## ?? Advanced Diagnostics

### Enable Detailed Logging

**appsettings.Development.json**:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",  // ? More verbose
      "Microsoft.AspNetCore": "Debug",
      "Microsoft.AspNetCore.Authentication": "Trace",  // ? Auth debugging
      "Microsoft.AspNetCore.Authorization": "Trace"    // ? Authorization debugging
    }
  }
}
```

**JWT Event Handlers** (already enabled in `Program.cs`):

```csharp
options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = context =>
    {
        Console.WriteLine($"? Authentication FAILED: {context.Exception.Message}");
        return Task.CompletedTask;
    },
    OnTokenValidated = context =>
    {
        var claims = context.Principal?.Claims
            .Select(c => $"{c.Type}={c.Value}")
            .ToList();
        Console.WriteLine($"? Token VALIDATED");
        Console.WriteLine($"   Claims: {string.Join(", ", claims)}");
        return Task.CompletedTask;
    },
    OnForbidden = context =>
    {
        var roles = context.Principal?.FindAll("role").Select(c => c.Value).ToList();
        Console.WriteLine($"? Authorization FORBIDDEN");
        Console.WriteLine($"   User: {context.Principal?.Identity?.Name}");
        Console.WriteLine($"   Roles: {(roles?.Any() == true ? string.Join(", ", roles) : "NONE")}");
        return Task.CompletedTask;
    }
};
```

---

### Network Diagnostics

**Test Connectivity**:

```powershell
# Test AdminAPI
Test-NetConnection -ComputerName localhost -Port 5206

# Test AuthServer
Test-NetConnection -ComputerName localhost -Port 5001

# Test SMTP
Test-NetConnection -ComputerName smtp.gmail.com -Port 587
```

**Check Firewall**:

```powershell
# Windows Firewall
Get-NetFirewallRule -DisplayName "*5206*"

# Azure Network Security Group
az network nsg rule list --nsg-name BellwoodNSG --resource-group BellwoodAPI
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system design
- `02-Testing-Guide.md` - Testing workflows
- `20-API-Reference.md` - Endpoint documentation
- `23-Security-Model.md` - Security & authorization
- `30-Deployment-Guide.md` - Deployment instructions
- `31-Scripts-Reference.md` - Test scripts

---

## ?? Getting Help

### Documentation

1. Check this troubleshooting guide
2. Review API Reference (`20-API-Reference.md`)
3. Check Security Model (`23-Security-Model.md`)

### Logs

1. Check console output (development)
2. Check IIS logs (production)
3. Check Application Insights (Azure)

### Community

1. GitHub Issues: https://github.com/BidumanADT/Bellwood.AdminApi/issues
2. Team Slack: #bellwood-support
3. Email: support@bellwood.com

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Troubleshooting Version**: 2.0
