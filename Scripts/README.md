# Bellwood AdminAPI - Test Data Scripts

PowerShell 5.1 scripts for managing test data in the Bellwood AdminAPI.

## Prerequisites

- PowerShell 5.1 or later
- **AuthServer running** (default: `https://localhost:5001`)
- **AdminAPI running** (default: `https://localhost:5206`)

## Authentication

All scripts automatically authenticate with the AuthServer using the test user `alice` (password: `password`). No manual token management required!

The scripts will:
1. Connect to AuthServer at `https://localhost:5001`
2. Login as `alice` to get a JWT token
3. Use that token for all AdminAPI calls

If you need to use different URLs, you can specify them:

```powershell
.\Scripts\Seed-All.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
```

## Scripts

### 1. Seed-All.ps1

Seeds all test data in the correct order (affiliates ? quotes ? bookings).

**Usage:**

```powershell
# Use default URLs (localhost)
.\Scripts\Seed-All.ps1

# Use custom URLs
.\Scripts\Seed-All.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
```

**What it creates:**
- 2 affiliates with 3 drivers total
- 5 quotes with various statuses
- 8 bookings with all status types

---

### 2. Seed-Affiliates.ps1

Seeds test affiliates and drivers with proper UserUid linkage to AuthServer.

**Usage:**

```powershell
.\Scripts\Seed-Affiliates.ps1
```

**Creates:**

| Affiliate | Driver | UserUid | Phone |
|-----------|--------|---------|-------|
| Chicago Limo Service | Charlie Johnson | `driver-001` | 312-555-0001 |
| Chicago Limo Service | Sarah Lee | `driver-002` | 312-555-0002 |
| Suburban Chauffeurs | Robert Brown | `driver-003` | 847-555-1000 |

**Important:** Charlie can login to DriverApp with:
- Username: `charlie`
- Password: `password`
- Will see rides assigned to `driver-001`

---

### 3. Seed-Quotes.ps1

Seeds 5 sample quotes with different statuses.

**Usage:**

```powershell
.\Scripts\Seed-Quotes.ps1
```

**Creates:**

| Status | Count | Sample |
|--------|-------|--------|
| Submitted | 1 | Alice Morgan ? Taylor Reed |
| InReview | 1 | Chris Bailey ? Jordan Chen |
| Priced | 1 | Lisa Gomez ? Derek James |
| Rejected | 1 | Evan Ross ? Mia Park |
| Closed | 1 | Sarah Larkin ? James Miller |

---

### 4. Seed-Bookings.ps1

Seeds 8 sample bookings covering all booking statuses.

**Usage:**

```powershell
.\Scripts\Seed-Bookings.ps1
```

**Creates:**

| Status | Driver UID | Passenger | Pickup Time |
|--------|-----------|-----------|-------------|
| Requested | (unassigned) | Maria Garcia | +24 hours |
| Confirmed | (unassigned) | Patricia Brown | +36 hours |
| Scheduled | driver-001 | Jordan Chen | **+5 hours** ? |
| Scheduled | driver-001 | Emma Watson | **+48 hours** ? |
| InProgress | driver-002 | Taylor Reed | -30 minutes |
| Completed | driver-002 | Derek James | -1 day |
| Cancelled | driver-003 | Jennifer Taylor | -2 days |
| NoShow | driver-003 | Susan Clark | -3 days |

**? Charlie's Test Rides:**
- The two bookings marked with stars are assigned to `driver-001` (Charlie)
- One is 5 hours from seed time, one is 48 hours out
- Both will appear in DriverApp when Charlie logs in

---

### 5. Clear-TestData.ps1

Deletes all JSON data files to reset the system to a clean state.

**Usage:**

```powershell
# Delete without confirmation prompt
.\Scripts\Clear-TestData.ps1

# Prompt for confirmation before deleting
.\Scripts\Clear-TestData.ps1 -Confirm

# Specify custom data directory
.\Scripts\Clear-TestData.ps1 -DataDirectory "C:\MyApp\App_Data"
```

**Deletes:**
- `affiliates.json`
- `drivers.json`
- `bookings.json`
- `quotes.json`

**Warning:** This operation is **irreversible**. All data will be lost.

---

### 6. Get-TestDataStatus.ps1

Shows current test data status without making API calls.

**Usage:**

```powershell
.\Scripts\Get-TestDataStatus.ps1
```

**Shows:**
- Record counts for each data type
- File sizes
- Sample data for small datasets
- Driver UserUid linkage status

---

## Common Workflows

### Complete Reset and Re-seed

```powershell
# 1. Clear all existing data
.\Scripts\Clear-TestData.ps1 -Confirm

# 2. Restart AdminAPI (if running)

# 3. Seed fresh test data
.\Scripts\Seed-All.ps1
```

### Seed Only Specific Data

```powershell
# Only affiliates and drivers
.\Scripts\Seed-Affiliates.ps1

# Only quotes
.\Scripts\Seed-Quotes.ps1

# Only bookings (requires drivers to exist first!)
.\Scripts\Seed-Bookings.ps1
```

### Test Driver Assignment Flow

```powershell
# 1. Seed affiliates and drivers
.\Scripts\Seed-Affiliates.ps1

# 2. Seed bookings (which will include Charlie's assigned rides)
.\Scripts\Seed-Bookings.ps1

# 3. Login to DriverApp as 'charlie' (password: password)
#    You should see 2 scheduled rides:
#    - Jordan Chen (5 hours from seed time)
#    - Emma Watson (48 hours from seed time)
```

---

## Troubleshooting

### "Authentication failed" error

Make sure AuthServer is running and accessible.

**Solution:**
```powershell
# Check if AuthServer is running
Invoke-WebRequest -Uri "https://localhost:5001/health" -UseBasicParsing

# If not running, start AuthServer first
cd C:\path\to\BellwoodAuthServer
dotnet run
```

### "Cannot assign driver without a UserUid" error

This shouldn't happen with the built-in seed scripts, but if you manually create drivers without UserUid, the assignment will fail.

**Solution:**
```powershell
# Always seed affiliates using the provided script
.\Scripts\Seed-Affiliates.ps1
```

### SSL Certificate Errors

The scripts are configured to ignore SSL certificate errors for localhost development. This is normal and expected.

### "Data directory not found" when clearing data

**Solution:**
```powershell
# Run from the AdminAPI project root directory
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
.\Scripts\Clear-TestData.ps1
```

---

## Script Parameters Reference

### Seed Scripts Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-ApiBaseUrl` | String | `https://localhost:5206` | Base URL of AdminAPI |
| `-AuthServerUrl` | String | `https://localhost:5001` | Base URL of AuthServer |

### Clear-TestData.ps1 Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-DataDirectory` | String | `./App_Data` | Path to data directory |
| `-Confirm` | Switch | `$false` | Prompt before deleting |

### Get-TestDataStatus.ps1 Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-DataDirectory` | String | `./App_Data` | Path to data directory |

---

## Integration with CI/CD

These scripts can be integrated into automated testing pipelines:

```yaml
# Azure DevOps example
steps:
  - task: PowerShell@2
    displayName: 'Seed Test Data'
    inputs:
      filePath: 'Scripts/Seed-All.ps1'
      arguments: '-ApiBaseUrl "$(ApiUrl)" -AuthServerUrl "$(AuthServerUrl)"'
```

```yaml
# GitHub Actions example
- name: Seed Test Data
  shell: pwsh
  run: |
    .\Scripts\Seed-All.ps1 -ApiBaseUrl "${{ env.API_URL }}" -AuthServerUrl "${{ env.AUTH_URL }}"
```

---

## What Gets Created

### Complete Test Data Set

When you run `Seed-All.ps1`, you get:

**Affiliates & Drivers (3 total):**
- Chicago Limo Service
  - Charlie Johnson (UserUid: `driver-001`) ? Test driver
  - Sarah Lee (UserUid: `driver-002`)
- Suburban Chauffeurs
  - Robert Brown (UserUid: `driver-003`)

**Quotes (5 total):**
- Submitted, InReview, Priced, Rejected, Closed

**Bookings (8 total - covers all statuses):**
- **Requested** - New booking from passenger app
- **Confirmed** - Approved by Bellwood staff
- **Scheduled** - Driver assigned (Charlie's 5-hour ride) ?
- **Scheduled** - Driver assigned (Charlie's 48-hour ride) ?
- **InProgress** - Passenger onboard, en route
- **Completed** - Successfully finished ride
- **Cancelled** - Booking was cancelled
- **NoShow** - Passenger didn't show up

---

## Security Notes

?? **Important:**

1. **These scripts are for DEV/TEST only** - Never use in production
2. **Test credentials** - Scripts use `alice/password` for authentication
3. **SSL certificates** - Certificate validation is disabled for localhost
4. **Sensitive data** - Clear logs before sharing screenshots

---

## Related Documentation

- [Driver Assignment Fix Summary](../DRIVER_ASSIGNMENT_FIX_SUMMARY.md)
- [System Integration Guide](../BELLWOOD_SYSTEM_INTEGRATION.md)
- [Scripts Summary](./SCRIPTS_SUMMARY.md)
