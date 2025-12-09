# PowerShell Scripts Update - Authentication & Booking Expansion

## Changes Made

### 1. Automatic Authentication

All scripts now automatically authenticate with AuthServer instead of prompting for tokens:

**Before:**
```powershell
# Had to manually get token and pass it
.\Seed-All.ps1 -AuthToken "manual-jwt-token-here"
```

**After:**
```powershell
# Just run it - authentication handled automatically
.\Seed-All.ps1
```

**How it works:**
1. Script connects to AuthServer (`https://localhost:5001`)
2. Logs in as `alice` (password: `password`)
3. Gets JWT token automatically
4. Uses token for all API calls

### 2. Expanded Booking Seed Data

**Before:** 3 bookings with limited statuses

**After:** 8 bookings covering **all** booking statuses:

| Status | Count | Description |
|--------|-------|-------------|
| Requested | 1 | Initial booking from passenger app |
| Confirmed | 1 | Approved by Bellwood staff |
| Scheduled | 2 | Charlie's test rides (5h and 48h) ? |
| InProgress | 1 | Passenger onboard, en route |
| Completed | 1 | Successfully finished |
| Cancelled | 1 | Booking cancelled |
| NoShow | 1 | Passenger didn't show |

### 3. Charlie's Specific Test Rides

Two bookings specifically created for testing Charlie (driver-001):

1. **Jordan Chen** - Scheduled for **5 hours** from seed time
2. **Emma Watson** - Scheduled for **48 hours** from seed time

Both appear in DriverApp when Charlie logs in!

---

## Updated Scripts

### All Seed Scripts Now Use:

```powershell
param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"  # NEW
)

# Automatic authentication
$loginBody = @{
    username = "alice"
    password = "password"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body $loginBody

$token = $loginResponse.accessToken
```

### Updated Files

| File | Change |
|------|--------|
| `Seed-All.ps1` | Uses AuthServer auth, updated to show all 8 bookings |
| `Seed-Affiliates.ps1` | Uses AuthServer auth, improved output formatting |
| `Seed-Quotes.ps1` | Uses AuthServer auth |
| `Seed-Bookings.ps1` | Uses AuthServer auth, expanded output to show all statuses |
| `Program.cs` | Expanded booking seed from 3 to 8 bookings |
| `README.md` | Removed token management docs, added booking status table |

---

## Testing the Changes

### Quick Test

```powershell
# 1. Make sure AuthServer is running
cd C:\path\to\BellwoodAuthServer
dotnet run

# 2. Make sure AdminAPI is running
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
dotnet run

# 3. Clear existing data
.\Scripts\Clear-TestData.ps1

# 4. Seed all data (no token needed!)
.\Scripts\Seed-All.ps1
```

### Expected Output

```
========================================
  Bellwood AdminAPI - Seed All Data
========================================

[1/3] Seeding Affiliates and Drivers...

Step 1: Authenticating with AuthServer...
? Authentication successful!

Step 2: Seeding affiliates and drivers...
? Success!
  - Affiliates created: 2
  - Drivers created: 3

[2/3] Seeding Quotes...
? Success: 5 quotes created

[3/3] Seeding Bookings...
? Success: 8 bookings created

========================================
  ? All test data seeded successfully!
========================================

Summary:
  • 2 affiliates with 3 drivers
  • 5 quotes with various statuses
  • 8 bookings covering all statuses

Key Test Scenarios:
  ? Charlie (driver-001) has 2 scheduled rides
    - Jordan Chen in 5 hours
    - Emma Watson in 48 hours
```

---

## Benefits

### ? Simpler Usage

No need to manually get and manage JWT tokens. Just run the script!

### ? Complete Status Coverage

All 7 booking statuses are now represented in test data:
- Requested
- Confirmed
- Scheduled
- InProgress
- Completed
- Cancelled
- NoShow

### ? Better Test Scenarios

Two specific Charlie rides at meaningful times:
- 5 hours (near-term testing)
- 48 hours (future ride visibility)

### ? Consistent with Portal Scripts

Now matches the authentication pattern from your AdminPortal seed scripts.

---

## Migration Notes

### If You Have Existing Scripts

Old scripts with `-AuthToken` parameter will no longer work. Update them to use the new pattern:

**Old:**
```powershell
$token = "manually-get-token"
.\Seed-Affiliates.ps1 -AuthToken $token
```

**New:**
```powershell
# Token is automatically obtained
.\Seed-Affiliates.ps1
```

### Custom Authentication

If you need to use different credentials:

1. Edit the script
2. Change the `username` and `password` in the login body:

```powershell
$loginBody = @{
    username = "your-username"  # Change this
    password = "your-password"  # Change this
} | ConvertTo-Json
```

---

## Booking Status Reference

### Complete Workflow

```
Requested ? Confirmed ? Scheduled ? InProgress ? Completed
     ?           ?            ?
Cancelled   Cancelled    Cancelled
                    ?
                  NoShow
```

### Status Descriptions

| Status | Trigger | Who | When |
|--------|---------|-----|------|
| **Requested** | Passenger submits booking | Passenger App | Initial |
| **Confirmed** | Staff approves | Admin Portal | After review |
| **Scheduled** | Driver assigned | Admin Portal | After assignment |
| **InProgress** | Driver picks up passenger | Driver App | During ride |
| **Completed** | Ride finishes successfully | Driver App | End of ride |
| **Cancelled** | Booking cancelled | Any | Before completion |
| **NoShow** | Passenger doesn't show | Driver App | At pickup time |

---

## Charlie's Test Rides Detail

### Ride 1: Jordan Chen (5 hours)

```
Passenger: Jordan Chen
Pickup: Langham Hotel
Dropoff: Midway Airport
Vehicle: Sedan
Status: Scheduled
Time: +5 hours from seed
Driver: Charlie Johnson (driver-001)
```

**Testing:** Near-term ride visibility, status updates

### Ride 2: Emma Watson (48 hours)

```
Passenger: Emma Watson
Pickup: O'Hare FBO
Dropoff: Peninsula Hotel, Chicago
Vehicle: S-Class
Status: Scheduled
Time: +48 hours from seed
Driver: Charlie Johnson (driver-001)
Pickup Style: Meet and Greet
Sign Text: WATSON / Bellwood
```

**Testing:** Future ride planning, date filtering

---

## Summary

The scripts are now:
- ? Easier to use (no manual token management)
- ? More comprehensive (all booking statuses)
- ? Better for testing (specific Charlie rides)
- ? Consistent with portal scripts (same auth pattern)

Just run `.\Scripts\Seed-All.ps1` and you're ready to test! ??
