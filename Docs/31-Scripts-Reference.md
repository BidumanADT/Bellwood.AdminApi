# Scripts Reference

**Document Type**: Living Document - Deployment & Operations  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides complete documentation for all PowerShell scripts in the `Scripts/` directory, including seeding scripts, test scripts, and utility scripts.

**Location**: `Scripts/` directory  
**Requirements**: PowerShell 5.1 or later

---

## ?? Script Directory Structure

```
Scripts/
??? Seed Scripts (Development Data)
?   ??? Seed-All.ps1                    # Master script - seeds everything
?   ??? Seed-Affiliates.ps1             # Seed affiliates & drivers
?   ??? Seed-Quotes.ps1                 # Seed sample quotes
?   ??? Seed-Bookings.ps1               # Seed sample bookings
?
??? Test Scripts (Integration Testing)
?   ??? Test-Phase1-Ownership.ps1       # Phase 1 ownership tests
?   ??? Test-Phase2-Dispatcher.ps1      # Phase 2 RBAC & field masking tests
?   ??? Test-Repository-Fix.ps1         # Repository fix verification
?
??? Utility Scripts (Data Management)
    ??? Clear-TestData.ps1              # Wipe all test data
    ??? Get-TestDataStatus.ps1          # View current data status
```

---

## ?? Seed Scripts

### Seed-All.ps1

**Purpose**: Master script that seeds all test data in the correct order.

**Location**: `Scripts/Seed-All.ps1`

**Prerequisites**:
- AuthServer running on `https://localhost:5001`
- AdminAPI running on `https://localhost:5206`
- Admin user (`alice`) exists in AuthServer

**Usage**:

```powershell
# Default (localhost)
.\Scripts\Seed-All.ps1

# Custom URLs
.\Scripts\Seed-All.ps1 `
  -ApiBaseUrl "https://api.staging.bellwood.com" `
  -AuthServerUrl "https://auth.staging.bellwood.com"
```

**Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ApiBaseUrl` | string | `https://localhost:5206` | AdminAPI base URL |
| `AuthServerUrl` | string | `https://localhost:5001` | AuthServer base URL |

**What It Seeds**:

1. **Affiliates & Drivers** (`Seed-Affiliates.ps1`):
   - 2 affiliates
   - 3 drivers (Charlie, Sarah, Robert)

2. **Quotes** (`Seed-Quotes.ps1`):
   - 5 sample quotes
   - Various statuses (Submitted, InReview, Priced, Rejected, Closed)

3. **Bookings** (`Seed-Bookings.ps1`):
   - 8 sample bookings
   - All statuses (Requested, Confirmed, Scheduled, InProgress, Completed, Cancelled, NoShow)
   - Charlie (driver-001) has 2 scheduled rides

**Output Example**:

```
========================================
  Bellwood AdminAPI - Seed All Data
========================================

[1/3] Seeding Affiliates and Drivers...
? 2 affiliates and 3 drivers seeded

[2/3] Seeding Quotes...
? 5 quotes seeded

[3/3] Seeding Bookings...
? 8 bookings seeded

========================================
  ? All test data seeded successfully!
========================================

Summary:
  ?? 2 affiliates with 3 drivers
  ?? 5 quotes with various statuses
  ?? 8 bookings covering all statuses

Key Test Scenarios:
  ?? Charlie (driver-001) has 2 scheduled rides
    - Jordan Chen in 5 hours
    - Emma Watson in 48 hours

Next Steps:
  1. View data in AdminPortal
  2. Login as 'charlie' in DriverApp
  3. Verify Charlie sees 2 upcoming rides
```

**Exit Codes**:
- `0` - Success
- `1` - Failure (authentication or seeding failed)

---

### Seed-Affiliates.ps1

**Purpose**: Seed affiliates and drivers.

**Location**: `Scripts/Seed-Affiliates.ps1`

**Usage**:

```powershell
.\Scripts\Seed-Affiliates.ps1
```

**What It Creates**:

**Affiliates**:
1. **Chicago Limo Service**
   - Contact: John Smith
   - Phone: 312-555-1234
   - Email: dispatch@chicagolimo.com

2. **Suburban Chauffeurs**
   - Contact: Emily Davis
   - Phone: 847-555-9876
   - Email: emily@suburbanchauffeurs.com

**Drivers**:
1. **Charlie Johnson** (Chicago Limo)
   - Phone: 312-555-0001
   - **UserUid**: `driver-001` ? (matches AuthServer user "charlie")

2. **Sarah Lee** (Chicago Limo)
   - Phone: 312-555-0002
   - **UserUid**: `driver-002`

3. **Robert Brown** (Suburban Chauffeurs)
   - Phone: 847-555-1000
   - **UserUid**: `driver-003`

**Critical**: Charlie's `UserUid` matches the AuthServer test user for DriverApp testing.

---

### Seed-Quotes.ps1

**Purpose**: Seed sample quotes with various statuses.

**Location**: `Scripts/Seed-Quotes.ps1`

**Usage**:

```powershell
.\Scripts\Seed-Quotes.ps1
```

**What It Creates**:

| Quote | Booker | Passenger | Status | Vehicle | Route |
|-------|--------|-----------|--------|---------|-------|
| 1 | Alice Morgan | Taylor Reed | Submitted | Sedan | Langham ? O'Hare |
| 2 | Chris Bailey | Jordan Chen | InReview | SUV | O'Hare FBO ? Downtown |
| 3 | Lisa Gomez | Derek James | Priced | S-Class | Midway ? Langham |
| 4 | Evan Ross | Mia Park | Rejected | Sprinter | ORD FBO ? Indiana Dunes |
| 5 | Sarah Larkin | James Miller | Closed | SUV | O'Hare FBO ? Langham |

**Use Cases**:
- Test quote list views
- Test quote detail views
- Test quote status workflows
- Test ownership filtering (Phase 1)

---

### Seed-Bookings.ps1

**Purpose**: Seed sample bookings covering all statuses and scenarios.

**Location**: `Scripts/Seed-Bookings.ps1`

**Usage**:

```powershell
.\Scripts\Seed-Bookings.ps1
```

**What It Creates**:

| Booking | Passenger | Status | Driver | Pickup Time | Use Case |
|---------|-----------|--------|--------|-------------|----------|
| 1 | Maria Garcia | Requested | - | +24h | Awaiting approval |
| 2 | Patricia Brown | Confirmed | - | +36h | Approved, not assigned |
| 3 | Jordan Chen | Scheduled | Charlie | +5h | Charlie's first ride ? |
| 4 | Emma Watson | Scheduled | Charlie | +48h | Charlie's second ride ? |
| 5 | Taylor Reed | InProgress | Sarah | -30min | Ride active |
| 6 | Derek James | Completed | Sarah | -1d | Ride finished |
| 7 | Jennifer Taylor | Cancelled | Robert | -2d | Ride cancelled |
| 8 | Susan Clark | NoShow | Robert | -3d | Passenger no-show |

**? Key Scenarios**:
- **Charlie (driver-001)** has 2 scheduled rides for DriverApp testing
- All booking statuses represented
- All ride statuses represented
- Ownership tracking (Phase 1)

---

## ?? Test Scripts

### Test-Phase1-Ownership.ps1

**Purpose**: Test Phase 1 ownership verification and data access enforcement.

**Location**: `Scripts/Test-Phase1-Ownership.ps1`

**Usage**:

```powershell
.\Scripts\Test-Phase1-Ownership.ps1
```

**What It Tests**:

1. **Admin Authentication** (alice)
2. **Booker Authentication** (betty)
3. **Admin Can View All Data**
   - All quotes (including others' quotes)
   - All bookings (including others' bookings)
4. **Booker Can Only View Own Data**
   - Only sees own quotes (CreatedByUserId match)
   - Only sees own bookings (CreatedByUserId match)
5. **Ownership Filtering**
   - Legacy records (null CreatedByUserId) hidden from non-staff
   - Staff sees all records

**Expected Results**:
- ? 8/8 tests passing
- Admin sees all records
- Booker only sees own records

**Exit Codes**:
- `0` - All tests passed
- `1` - Some tests failed

---

### Test-Phase2-Dispatcher.ps1

**Purpose**: Test Phase 2 RBAC policies, dispatcher role, and field masking.

**Location**: `Scripts/Test-Phase2-Dispatcher.ps1`

**Usage**:

```powershell
.\Scripts\Test-Phase2-Dispatcher.ps1
```

**What It Tests**:

1. **Authentication**
   - Admin (alice) login
   - Dispatcher (diana) login

2. **AdminOnly Policy**
   - Admin can seed data ?
   - Dispatcher CANNOT seed data ? (403 Forbidden)

3. **StaffOnly Policy**
   - Admin can access operational endpoints ?
   - Dispatcher can access operational endpoints ?

4. **Field Masking**
   - Admin sees billing fields (when populated)
   - Dispatcher sees null billing fields (masked)

5. **OAuth Management**
   - Admin can view/update credentials ?
   - Dispatcher CANNOT access credentials ? (403 Forbidden)

**Test Matrix**:

| Test | Admin | Dispatcher | Expected |
|------|-------|------------|----------|
| Seed affiliates | ? 200 | ? 403 | AdminOnly |
| Seed quotes | ? 200 | ? 403 | AdminOnly |
| Seed bookings | ? 200 | ? 403 | AdminOnly |
| List quotes | ? 200 | ? 200 | StaffOnly |
| List bookings | ? 200 | ? 200 | StaffOnly |
| GET OAuth credentials | ? 200 | ? 403 | AdminOnly |
| Billing fields | Full data | Masked (null) | Field masking |

**Expected Results**:
- ? 10/10 tests passing
- Dispatcher has operational access
- Dispatcher cannot access sensitive operations
- Billing fields masked for dispatcher

**Exit Codes**:
- `0` - All tests passed
- `1` - Some tests failed

---

### Test-Repository-Fix.ps1

**Purpose**: Verify repository initialization fix (empty file handling).

**Location**: `Scripts/Test-Repository-Fix.ps1`

**Prerequisites**: Empty or missing JSON files

**Usage**:

```powershell
.\Scripts\Test-Repository-Fix.ps1
```

**What It Tests**:

1. **Empty JSON File Handling**
   - Repository creates empty array `[]` if file missing
   - No errors on first read

2. **Concurrent Access**
   - Multiple reads don't fail
   - Proper lock handling

3. **Data Persistence**
   - First write succeeds
   - Subsequent reads return data

**Expected Results**:
- ? All tests passing
- No "file not found" errors
- No "invalid JSON" errors

---

## ??? Utility Scripts

### Clear-TestData.ps1

**Purpose**: Wipe all test data and reset system to clean state.

**Location**: `Scripts/Clear-TestData.ps1`

**Usage**:

```powershell
# Default (current directory)
.\Scripts\Clear-TestData.ps1

# With confirmation prompt
.\Scripts\Clear-TestData.ps1 -Confirm

# Custom data directory
.\Scripts\Clear-TestData.ps1 -DataDirectory "C:\MyApp\App_Data"
```

**Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `DataDirectory` | string | `./App_Data` | Path to App_Data directory |
| `Confirm` | switch | `$false` | Prompt before deleting |

**What It Deletes**:

```
App_Data/
??? affiliates.json     ? Deleted
??? drivers.json        ? Deleted
??? bookings.json       ? Deleted
??? quotes.json         ? Deleted
```

**Output Example**:

```
========================================
  Clear Bellwood Test Data
========================================

Target directory: C:\Repos\Bellwood.AdminApi\App_Data

  [Found] affiliates.json (1234 bytes)
  [Found] drivers.json (567 bytes)
  [Found] bookings.json (8901 bytes)
  [Found] quotes.json (4567 bytes)

This will delete 4 data file(s) and reset the system.
All quotes, bookings, affiliates, and drivers will be removed!

Deleting data files...
  ? Deleted: affiliates.json
  ? Deleted: drivers.json
  ? Deleted: bookings.json
  ? Deleted: quotes.json

========================================
  ? Data wipe complete!
========================================

Deleted 4 file(s).

Next steps:
  1. Restart the AdminAPI (if running)
  2. Run .\Scripts\Seed-All.ps1 to repopulate test data
```

**Warning**: This is destructive! All data will be lost.

**Use Cases**:
- Reset to clean state
- Remove corrupted data
- Prepare for fresh test run

---

### Get-TestDataStatus.ps1

**Purpose**: View current data status (counts and summaries).

**Location**: `Scripts/Get-TestDataStatus.ps1`

**Usage**:

```powershell
.\Scripts\Get-TestDataStatus.ps1
```

**What It Shows**:

- Affiliate count
- Driver count
- Quote count (by status)
- Booking count (by status)
- Current ride status counts
- File sizes

**Output Example**:

```
========================================
  Bellwood Test Data Status
========================================

Data Directory: C:\Repos\Bellwood.AdminApi\App_Data

Affiliates:
  Total: 2 affiliates

Drivers:
  Total: 3 drivers
  By Affiliate:
    - Chicago Limo Service: 2 drivers
    - Suburban Chauffeurs: 1 driver

Quotes:
  Total: 5 quotes
  By Status:
    - Submitted: 1
    - InReview: 1
    - Priced: 1
    - Rejected: 1
    - Closed: 1

Bookings:
  Total: 8 bookings
  By Status:
    - Requested: 1
    - Confirmed: 1
    - Scheduled: 2
    - InProgress: 1
    - Completed: 1
    - Cancelled: 1
    - NoShow: 1
  
  Driver Assignments:
    - Charlie Johnson (driver-001): 2 rides
    - Sarah Lee (driver-002): 2 rides
    - Robert Brown (driver-003): 2 rides

File Sizes:
  affiliates.json: 1.2 KB
  drivers.json: 0.6 KB
  bookings.json: 8.9 KB
  quotes.json: 4.6 KB
  TOTAL: 15.3 KB
```

**Use Cases**:
- Verify data seeding
- Check data integrity
- Monitor data growth

---

## ?? Script Best Practices

### Error Handling

All scripts use strict error handling:

```powershell
$ErrorActionPreference = "Stop"

try {
    # Script logic
}
catch {
    Write-Host "? Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
```

---

### Certificate Validation

For HTTPS localhost testing (PowerShell 5.1):

```powershell
# Trust all certificates (development only!)
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
```

**Warning**: Only use in development! Production should use valid certificates.

---

### JWT Token Decoding

Helper function to decode JWT payload:

```powershell
function Get-JwtPayload {
    param([string]$Token)
    
    $parts = $Token.Split('.')
    if ($parts.Length -ne 3) {
        Write-Host "Invalid JWT format" -ForegroundColor Red
        return $null
    }
    
    $payload = $parts[1]
    # Add padding if needed
    $padding = (4 - ($payload.Length % 4)) % 4
    $payload = $payload + ("=" * $padding)
    
    $bytes = [Convert]::FromBase64String($payload)
    $json = [System.Text.Encoding]::UTF8.GetString($bytes)
    return $json | ConvertFrom-Json
}

# Usage
$claims = Get-JwtPayload -Token $accessToken
Write-Host "Role: $($claims.role)"
Write-Host "UserId: $($claims.uid)"
```

---

### Color-Coded Output

Consistent color scheme:

```powershell
Write-Host "? Success message" -ForegroundColor Green
Write-Host "??  Warning message" -ForegroundColor Yellow
Write-Host "? Error message" -ForegroundColor Red
Write-Host "??  Info message" -ForegroundColor Cyan
Write-Host "   Detail/note" -ForegroundColor Gray
```

---

## ?? Common Workflows

### Fresh Development Setup

```powershell
# 1. Clear any existing data
.\Scripts\Clear-TestData.ps1

# 2. Seed all test data
.\Scripts\Seed-All.ps1

# 3. Verify data status
.\Scripts\Get-TestDataStatus.ps1

# 4. Run Phase 2 tests
.\Scripts\Test-Phase2-Dispatcher.ps1
```

---

### Test Suite Execution

```powershell
# Run all test scripts
.\Scripts\Test-Phase1-Ownership.ps1
.\Scripts\Test-Phase2-Dispatcher.ps1
.\Scripts\Test-Repository-Fix.ps1
```

---

### Production Deployment Verification

```powershell
# Test against production endpoints
.\Scripts\Test-Phase2-Dispatcher.ps1 `
  -ApiBaseUrl "https://api.bellwood.com" `
  -AuthServerUrl "https://auth.bellwood.com"
```

---

## ?? Related Documentation

- `02-Testing-Guide.md` - Testing strategies
- `30-Deployment-Guide.md` - Deployment instructions
- `32-Troubleshooting.md` - Common issues & solutions

---

## ?? Future Enhancements

### Planned Scripts

1. **Load-Testing.ps1**:
   - Simulate concurrent users
   - Test SignalR connections
   - Measure response times

2. **Database-Migration.ps1**:
   - Migrate JSON data to SQL Server
   - Preserve ownership fields
   - Validate migration success

3. **Backup-Data.ps1**:
   - Backup all JSON files
   - Compress to ZIP
   - Upload to cloud storage

4. **Restore-Data.ps1**:
   - Download backup from cloud
   - Restore JSON files
   - Verify data integrity

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Scripts Version**: 2.0
