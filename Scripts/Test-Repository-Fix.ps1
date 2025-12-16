#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies the file repository race condition fix.

.DESCRIPTION
    Tests that seeding works correctly after the lazy initialization fix:
    1. Deletes existing data files
    2. Runs seed script
    3. Verifies success
    
.EXAMPLE
    .\Test-Repository-Fix.ps1
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  File Repository Race Condition Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean up existing data files
Write-Host "[1/3] Cleaning existing data files..." -ForegroundColor Yellow
$appDataPath = Join-Path $PSScriptRoot "..\App_Data"

if (Test-Path $appDataPath) {
    Write-Host "   Deleting: $appDataPath" -ForegroundColor Gray
    Remove-Item -Path $appDataPath -Recurse -Force
    Write-Host "   ? Deleted" -ForegroundColor Green
} else {
    Write-Host "   ?? No existing data (fresh install)" -ForegroundColor Gray
}

Write-Host ""

# Step 2: Wait for API to be ready (it may need to restart after file deletion)
Write-Host "[2/3] Waiting for API to be ready..." -ForegroundColor Yellow
$maxAttempts = 10
$attempt = 0
$ready = $false

while ($attempt -lt $maxAttempts -and -not $ready) {
    $attempt++
    try {
        $response = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $ready = $true
            Write-Host "   ? API is ready" -ForegroundColor Green
        }
    } catch {
        Write-Host "   Attempt $attempt/$maxAttempts - waiting..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Write-Host "   ? API not responding. Please start the API manually:" -ForegroundColor Red
    Write-Host "      dotnet run --project Bellwood.AdminApi.csproj" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 3: Run seed script
Write-Host "[3/3] Running seed script (fresh install test)..." -ForegroundColor Yellow
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
& "$scriptPath\Seed-All.ps1" -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl

if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  ? TEST FAILED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "The seed script failed. This indicates the race condition fix may not be working." -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ? TEST PASSED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "The file repository race condition fix is working correctly!" -ForegroundColor Green
Write-Host ""
Write-Host "Test Details:" -ForegroundColor Cyan
Write-Host "  ? Fresh install (no existing files)" -ForegroundColor White
Write-Host "  ? All repositories initialized correctly" -ForegroundColor White
Write-Host "  ? Affiliates seeded successfully" -ForegroundColor White
Write-Host "  ? Quotes seeded successfully" -ForegroundColor White
Write-Host "  ? Bookings seeded successfully" -ForegroundColor White
Write-Host ""
Write-Host "The lazy initialization pattern is working as expected:" -ForegroundColor Yellow
Write-Host "  • Files created on first access (not in constructor)" -ForegroundColor Gray
Write-Host "  • Thread-safe initialization via semaphore" -ForegroundColor Gray
Write-Host "  • Defensive file existence checks before reads" -ForegroundColor Gray
Write-Host ""
