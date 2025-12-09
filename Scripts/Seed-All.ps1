#Requires -Version 5.1
<#
.SYNOPSIS
    Seeds all test data to the Bellwood AdminAPI in the correct order.

.DESCRIPTION
    Executes all seed scripts in the proper sequence:
    1. Affiliates and Drivers (must exist before booking assignment)
    2. Quotes
    3. Bookings

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Seed-All.ps1
    
.EXAMPLE
    .\Seed-All.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Bellwood AdminAPI - Seed All Data" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Step 1: Seed Affiliates and Drivers
Write-Host "[1/3] Seeding Affiliates and Drivers..." -ForegroundColor Yellow
Write-Host ""
& "$scriptPath\Seed-Affiliates.ps1" -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    Write-Host "Failed to seed affiliates/drivers. Aborting." -ForegroundColor Red
    exit 1
}

# Step 2: Seed Quotes
Write-Host "[2/3] Seeding Quotes..." -ForegroundColor Yellow
Write-Host ""
& "$scriptPath\Seed-Quotes.ps1" -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    Write-Host "Failed to seed quotes. Aborting." -ForegroundColor Red
    exit 1
}

# Step 3: Seed Bookings
Write-Host "[3/3] Seeding Bookings..." -ForegroundColor Yellow
Write-Host ""
& "$scriptPath\Seed-Bookings.ps1" -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    Write-Host "Failed to seed bookings. Aborting." -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Green
Write-Host "  ? All test data seeded successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  • 2 affiliates with 3 drivers" -ForegroundColor White
Write-Host "  • 5 quotes with various statuses" -ForegroundColor White
Write-Host "  • 8 bookings covering all statuses" -ForegroundColor White
Write-Host ""
Write-Host "Key Test Scenarios:" -ForegroundColor Cyan
Write-Host "  ? Charlie (driver-001) has 2 scheduled rides" -ForegroundColor Green
Write-Host "    - Jordan Chen in 5 hours" -ForegroundColor Gray
Write-Host "    - Emma Watson in 48 hours" -ForegroundColor Gray
Write-Host ""
Write-Host "  ? All booking statuses represented:" -ForegroundColor Green
Write-Host "    - Requested, Confirmed, Scheduled" -ForegroundColor Gray
Write-Host "    - InProgress, Completed, Cancelled, NoShow" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. View data in AdminPortal" -ForegroundColor White
Write-Host "  2. Login as 'charlie' (password: password) in DriverApp" -ForegroundColor White
Write-Host "  3. Verify Charlie sees 2 upcoming rides" -ForegroundColor White
Write-Host "  4. Test booking workflows and status transitions" -ForegroundColor White
Write-Host ""
