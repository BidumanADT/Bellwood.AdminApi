# PowerShell Test Data Scripts - Summary

## What Was Created

I've created a complete set of PowerShell 5.1 scripts for managing test data in the Bellwood AdminAPI:

### Core Scripts

| Script | Purpose | Output |
|--------|---------|--------|
| `Seed-All.ps1` | Seeds all test data in correct order | 2 affiliates, 3 drivers, 5 quotes, 3 bookings |
| `Seed-Affiliates.ps1` | Creates affiliates with linked drivers | 2 affiliates with 3 total drivers |
| `Seed-Quotes.ps1` | Creates sample quotes | 5 quotes with various statuses |
| `Seed-Bookings.ps1` | Creates bookings assigned to drivers | 3 bookings (2 for driver-001, 1 for driver-002) |
| `Clear-TestData.ps1` | Wipes all JSON data files | Deletes affiliates, drivers, bookings, quotes |
| `Get-TestDataStatus.ps1` | Shows current data without API calls | Summary of all test data files |

### Documentation

| File | Purpose |
|------|---------|
| `Scripts/README.md` | Complete usage guide with examples |

---

## Quick Start

### 1. Seed All Test Data

```powershell
# Navigate to AdminAPI project root
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi

# Run the master seed script (will prompt for JWT token)
.\Scripts\Seed-All.ps1
```

### 2. Check Data Status

```powershell
# View current test data (no authentication needed)
.\Scripts\Get-TestDataStatus.ps1
```

### 3. Clear All Data

```powershell
# Wipe all test data with confirmation
.\Scripts\Clear-TestData.ps1 -Confirm
```

---

## Key Features

### ?? Authentication Handling

- Scripts prompt for JWT token if not provided
- Token can be passed via `-AuthToken` parameter
- Secure password input (hidden on screen)

### ??? SSL Certificate Handling

- Automatically ignores self-signed certificate errors for localhost
- Safe for development environments
- Modify for production if needed

### ? Error Handling

- Comprehensive error messages
- Checks for API availability
- Shows response details on failure

### ?? Colored Output

- Green for success
- Red for errors
- Yellow for warnings
- Cyan for informational messages

### ?? Status Reporting

- Shows counts of created items
- Displays important details (driver UIDs, etc.)
- Provides next-step guidance

---

## Test Data Created

### Affiliates & Drivers

**Chicago Limo Service:**
- Charlie Johnson (UserUid: `driver-001`) - ? Matches AuthServer user "charlie"
- Sarah Lee (UserUid: `driver-002`)

**Suburban Chauffeurs:**
- Robert Brown (UserUid: `driver-003`)

### Quotes (5 total)

| Status | Passenger | Route |
|--------|-----------|-------|
| Submitted | Taylor Reed | Langham Hotel ? O'Hare |
| InReview | Jordan Chen | O'Hare FBO ? Downtown |
| Priced | Derek James | Midway ? Langham Hotel |
| Rejected | Mia Park | Signature FBO ? Indiana Dunes |
| Closed | James Miller | O'Hare FBO ? Langham Hotel |

### Bookings (3 total)

| Status | Driver | Passenger | Time | Visible to |
|--------|--------|-----------|------|------------|
| Scheduled | driver-001 | Taylor Reed | +3 hours | Charlie in DriverApp ? |
| Scheduled | driver-001 | Jordan Chen | +5 hours | Charlie in DriverApp ? |
| Completed | driver-002 | Derek James | -1 day | Sarah (if logged in) |

---

## Testing the Driver Assignment Fix

### End-to-End Test

```powershell
# 1. Clear existing data
.\Scripts\Clear-TestData.ps1

# 2. Seed fresh data
.\Scripts\Seed-All.ps1

# 3. Login to DriverApp
#    Username: charlie
#    Password: password
#
# 4. Charlie should see 2 rides in "Today's Rides"
#    - Taylor Reed pickup in 3 hours
#    - Jordan Chen pickup in 5 hours
```

### Why This Works

1. `Seed-Affiliates.ps1` creates Charlie Johnson with `UserUid: "driver-001"`
2. `Seed-Bookings.ps1` creates bookings with `AssignedDriverUid: "driver-001"`
3. AuthServer user "charlie" has JWT claim `uid: "driver-001"`
4. When Charlie logs in, the AdminAPI matches `AssignedDriverUid == jwt.uid`
5. Result: Charlie sees his assigned rides! ??

---

## Common Workflows

### Daily Development Reset

```powershell
.\Scripts\Clear-TestData.ps1
.\Scripts\Seed-All.ps1
```

### Testing New Drivers

```powershell
# Create driver in AdminPortal with UserUid
# Then seed bookings assigned to that driver
.\Scripts\Seed-Bookings.ps1
```

### Debugging Assignment Issues

```powershell
# Check what's in the data files
.\Scripts\Get-TestDataStatus.ps1

# Look for mismatches between:
# - Driver.UserUid
# - Booking.AssignedDriverUid  
# - JWT uid claim
```

---

## Integration with Other Systems

### AuthServer Alignment

The scripts create drivers with `UserUid` values that match AuthServer test users:

| Driver Record | UserUid | AuthServer User | Login Credentials |
|---------------|---------|-----------------|-------------------|
| Charlie Johnson | driver-001 | charlie | charlie / password |
| Sarah Lee | driver-002 | (not seeded yet) | - |
| Robert Brown | driver-003 | (not seeded yet) | - |

**Note:** Only `charlie` (driver-001) is currently seeded in AuthServer. To test the other drivers, you'd need to seed corresponding users in AuthServer with matching `uid` claims.

### AdminPortal Integration

After seeding:
1. Navigate to AdminPortal ? Affiliates
2. You'll see the seeded affiliates with their drivers
3. Drivers will show "Linked" badges (green) for those with UserUid
4. Navigate to Bookings to see assigned bookings

### PassengerApp Integration

Passengers can view bookings and see:
- Assigned driver name (e.g., "Charlie Johnson")
- In DEBUG mode: Driver UID and assignment details

---

## CI/CD Integration

### Azure DevOps Pipeline

```yaml
steps:
  - task: PowerShell@2
    displayName: 'Clear Test Data'
    inputs:
      filePath: 'Scripts/Clear-TestData.ps1'
      workingDirectory: '$(Build.SourcesDirectory)'

  - task: PowerShell@2
    displayName: 'Seed Test Data'
    inputs:
      filePath: 'Scripts/Seed-All.ps1'
      arguments: '-ApiBaseUrl "$(ApiUrl)" -AuthToken "$(TestAuthToken)"'
      workingDirectory: '$(Build.SourcesDirectory)'
```

### GitHub Actions

```yaml
- name: Reset Test Data
  shell: pwsh
  run: |
    .\Scripts\Clear-TestData.ps1
    .\Scripts\Seed-All.ps1 -ApiBaseUrl "${{ env.API_URL }}" -AuthToken "${{ secrets.API_TOKEN }}"
  working-directory: ./AdminApi
```

---

## Security Best Practices

### ?? Important Security Notes

1. **Never commit JWT tokens** to version control
2. **These scripts are for DEV/TEST only** - not production
3. **Use environment variables** for tokens in automation
4. **Clear logs** before sharing (may contain tokens)
5. **Rotate test credentials** regularly

### Recommended: Using Environment Variables

```powershell
# Set token as environment variable
$env:BELLWOOD_TEST_TOKEN = "your-jwt-token"

# Use in scripts
.\Scripts\Seed-All.ps1 -AuthToken $env:BELLWOOD_TEST_TOKEN
```

---

## Troubleshooting

### Issue: "Cannot assign driver without a UserUid"

**Cause:** Trying to seed bookings before affiliates/drivers exist.

**Solution:**
```powershell
.\Scripts\Seed-Affiliates.ps1
.\Scripts\Seed-Bookings.ps1
```

### Issue: "Authentication required"

**Cause:** Token expired (default 1 hour) or missing.

**Solution:**
```powershell
# Get fresh token
$login = @{ username = "charlie"; password = "password" } | ConvertTo-Json
$response = Invoke-RestMethod -Uri "https://localhost:5001/login" -Method Post -Body $login -ContentType "application/json"

# Use immediately
.\Scripts\Seed-All.ps1 -AuthToken $response.accessToken
```

### Issue: Driver can't see rides after seeding

**Checklist:**
1. ? Driver has UserUid? Check: `.\Scripts\Get-TestDataStatus.ps1`
2. ? Booking has AssignedDriverUid? Check the JSON file
3. ? AuthServer user has matching uid claim? Check JWT token
4. ? Driver logged in with correct account? Verify username

---

## Script File Locations

All scripts are in the `Scripts/` directory:

```
Bellwood.AdminApi/
??? Scripts/
?   ??? README.md                  # Full documentation
?   ??? Seed-All.ps1               # Master seed script
?   ??? Seed-Affiliates.ps1        # Seed affiliates & drivers
?   ??? Seed-Quotes.ps1            # Seed quotes
?   ??? Seed-Bookings.ps1          # Seed bookings
?   ??? Clear-TestData.ps1         # Wipe all data
?   ??? Get-TestDataStatus.ps1     # Check data status
??? App_Data/                      # JSON storage (gitignored)
    ??? affiliates.json
    ??? drivers.json
    ??? bookings.json
    ??? quotes.json
```

---

## What's Next?

1. **Test the scripts** - Run through the Quick Start workflow
2. **Review the README** - Full details in `Scripts/README.md`
3. **Verify the fix** - Login as Charlie and see assigned rides
4. **Extend as needed** - Add more test scenarios to seed scripts

---

## Summary

? All seed endpoints converted to PowerShell scripts  
? Data wipe script for clean resets  
? Status checker for quick inspection  
? Full documentation with examples  
? CI/CD integration examples  
? Security best practices included  

The scripts handle authentication, SSL certificates, error reporting, and provide colored output for easy debugging. They're ready to use for daily development and automated testing!
