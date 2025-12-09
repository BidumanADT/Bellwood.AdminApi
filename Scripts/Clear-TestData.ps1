#Requires -Version 5.1
<#
.SYNOPSIS
    Wipes all test data from the Bellwood AdminAPI JSON data stores.

.DESCRIPTION
    Deletes all JSON files in the App_Data directory to reset the system to a clean state.
    This includes:
    - affiliates.json (affiliates)
    - drivers.json (drivers - stored separately for scalability)
    - bookings.json (bookings)
    - quotes.json (quotes)

.PARAMETER DataDirectory
    The path to the App_Data directory. Default: ./App_Data

.PARAMETER Confirm
    If set, will prompt for confirmation before deleting files.

.EXAMPLE
    .\Clear-TestData.ps1
    
.EXAMPLE
    .\Clear-TestData.ps1 -DataDirectory "C:\MyApp\App_Data" -Confirm
#>

param(
    [string]$DataDirectory = "./App_Data",
    [switch]$Confirm = $false
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Red
Write-Host "  Clear Bellwood Test Data" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""

# Resolve full path
$DataDirectory = Resolve-Path $DataDirectory -ErrorAction SilentlyContinue

if (-not $DataDirectory) {
    Write-Host "? Data directory not found: $DataDirectory" -ForegroundColor Red
    Write-Host "  Make sure you're running this from the AdminAPI project root." -ForegroundColor Yellow
    exit 1
}

Write-Host "Target directory: $DataDirectory" -ForegroundColor Cyan
Write-Host ""

# Define data files
$dataFiles = @(
    "affiliates.json",
    "drivers.json",
    "bookings.json",
    "quotes.json"
)

# Check which files exist
$existingFiles = @()
foreach ($file in $dataFiles) {
    $filePath = Join-Path $DataDirectory $file
    if (Test-Path $filePath) {
        $existingFiles += $file
        $fileInfo = Get-Item $filePath
        Write-Host "  [Found] $file ($($fileInfo.Length) bytes)" -ForegroundColor Yellow
    }
    else {
        Write-Host "  [Missing] $file (will be created as empty on next API start)" -ForegroundColor Gray
    }
}

if ($existingFiles.Count -eq 0) {
    Write-Host ""
    Write-Host "? No data files found. System is already clean." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "This will delete $($existingFiles.Count) data file(s) and reset the system." -ForegroundColor Red
Write-Host "All quotes, bookings, affiliates, and drivers will be removed!" -ForegroundColor Red
Write-Host ""

if ($Confirm) {
    $response = Read-Host "Are you sure you want to continue? (yes/no)"
    if ($response -ne "yes") {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Delete files
Write-Host "Deleting data files..." -ForegroundColor Yellow
$deletedCount = 0

foreach ($file in $existingFiles) {
    $filePath = Join-Path $DataDirectory $file
    try {
        Remove-Item $filePath -Force
        Write-Host "  ? Deleted: $file" -ForegroundColor Green
        $deletedCount++
    }
    catch {
        Write-Host "  ? Failed to delete $file : $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ? Data wipe complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Deleted $deletedCount file(s)." -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Restart the AdminAPI (if running)" -ForegroundColor Gray
Write-Host "  2. Run .\Scripts\Seed-All.ps1 to repopulate test data" -ForegroundColor Gray
Write-Host ""
