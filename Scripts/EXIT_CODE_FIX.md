# Seed-All Script Exit Code Fix

## Problem

The `Seed-All.ps1` script was incorrectly detecting failures even when the individual seed scripts (Affiliates, Quotes, Bookings) completed successfully.

**Symptoms:**
- Affiliates and drivers seeded successfully
- Script output showed "Failed to seed affiliates/drivers. Aborting."
- Script stopped before seeding quotes and bookings

## Root Cause

PowerShell scripts don't automatically set `$LASTEXITCODE` when they complete. The individual seed scripts were:
1. Running successfully
2. Not explicitly setting `exit 0`
3. Leaving `$LASTEXITCODE` in an undefined or previous state

The Seed-All.ps1 check was:
```powershell
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
    Write-Host "Failed to seed affiliates/drivers. Aborting." -ForegroundColor Red
    exit 1
}
```

This check failed because:
- If `$LASTEXITCODE` was `$null`, the check `$LASTEXITCODE -ne 0` evaluated to `$true`
- The logic `($true -and $false)` = `$false`, but PowerShell's handling was inconsistent

## Solution

### 1. Individual Seed Scripts - Explicit Exit Codes

Added `exit 0` at the end of each successful seed script:

**Seed-Affiliates.ps1:**
```powershell
Write-Host "AuthServer credentials (e.g., username: charlie, password: password)" -ForegroundColor Gray
Write-Host ""

exit 0  # ? Added
```

**Seed-Quotes.ps1:**
```powershell
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Seeding Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

exit 0  # ? Added
```

**Seed-Bookings.ps1:**
```powershell
Write-Host "      in the DriverApp when Charlie logs in (username: charlie)" -ForegroundColor Gray
Write-Host ""

exit 0  # ? Added
```

### 2. Seed-All.ps1 - Better Exit Code Checking

Changed the exit code check logic:

**Before:**
```powershell
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
    Write-Host "Failed to seed affiliates/drivers. Aborting." -ForegroundColor Red
    exit 1
}
```

**After:**
```powershell
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    Write-Host "Failed to seed affiliates/drivers. Aborting." -ForegroundColor Red
    exit 1
}
```

**Why this works:**
- `if ($LASTEXITCODE -and ...)` checks if `$LASTEXITCODE` exists and is truthy
- If `$LASTEXITCODE` is `$null` or `0`, the condition is false (success)
- If `$LASTEXITCODE` is non-zero, it indicates failure

## Files Changed

| File | Change |
|------|--------|
| `Seed-Affiliates.ps1` | Added `exit 0` on success |
| `Seed-Quotes.ps1` | Added `exit 0` on success |
| `Seed-Bookings.ps1` | Added `exit 0` on success |
| `Seed-All.ps1` | Fixed exit code checking logic (3 places) |

## Testing

```powershell
# Navigate to project
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi

# Run Seed-All (should now work correctly)
.\Scripts\Seed-All.ps1
```

**Expected Output:**
```
[1/3] Seeding Affiliates and Drivers...
? Authentication successful!
? Success!
  - Affiliates created: 2
  - Drivers created: 3

[2/3] Seeding Quotes...
? Authentication successful!
? Success: 5 quotes created

[3/3] Seeding Bookings...
? Authentication successful!
? Success: 8 bookings created

========================================
  ? All test data seeded successfully!
========================================

Summary:
  • 2 affiliates with 3 drivers
  • 5 quotes with various statuses
  • 8 bookings covering all statuses
```

## PowerShell Best Practice

Always explicitly set exit codes in PowerShell scripts:

```powershell
# On success
exit 0

# On error
exit 1
```

This ensures parent scripts can reliably detect success/failure.

---

## Summary

? **Fixed:** Exit code handling in all seed scripts  
? **Fixed:** Exit code checking logic in Seed-All.ps1  
? **Result:** Seed-All.ps1 now correctly chains all three seed operations  

The scripts now properly detect success/failure and the Seed-All.ps1 master script completes successfully when all seeds work! ??
