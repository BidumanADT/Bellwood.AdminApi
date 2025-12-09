#Requires -Version 5.1
<#
.SYNOPSIS
    Checks the status of test data in the Bellwood AdminAPI.

.DESCRIPTION
    Displays a summary of all data files and their contents without making API calls.
    Useful for quickly seeing what test data exists without needing authentication.

.PARAMETER DataDirectory
    The path to the App_Data directory. Default: ./App_Data

.EXAMPLE
    .\Get-TestDataStatus.ps1
    
.EXAMPLE
    .\Get-TestDataStatus.ps1 -DataDirectory "C:\MyApp\App_Data"
#>

param(
    [string]$DataDirectory = "./App_Data"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Bellwood Test Data Status" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Resolve full path
$resolvedPath = Resolve-Path $DataDirectory -ErrorAction SilentlyContinue

if (-not $resolvedPath) {
    Write-Host "? Data directory not found: $DataDirectory" -ForegroundColor Red
    Write-Host "  Make sure you're running this from the AdminAPI project root." -ForegroundColor Yellow
    exit 1
}

$DataDirectory = $resolvedPath
Write-Host "Data directory: $DataDirectory" -ForegroundColor Gray
Write-Host ""

# Define data files
$dataFiles = @(
    @{ Name = "Affiliates"; File = "affiliates.json"; Icon = "??" },
    @{ Name = "Drivers"; File = "drivers.json"; Icon = "??" },
    @{ Name = "Bookings"; File = "bookings.json"; Icon = "??" },
    @{ Name = "Quotes"; File = "quotes.json"; Icon = "??" }
)

$hasData = $false

foreach ($item in $dataFiles) {
    $filePath = Join-Path $DataDirectory $item.File
    
    if (Test-Path $filePath) {
        $hasData = $true
        $fileInfo = Get-Item $filePath
        
        try {
            $content = Get-Content $filePath -Raw | ConvertFrom-Json
            $count = 0
            
            if ($content -is [Array]) {
                $count = $content.Count
            }
            elseif ($content) {
                $count = 1
            }
            
            $color = if ($count -gt 0) { "Green" } else { "Yellow" }
            Write-Host "$($item.Icon) $($item.Name):" -NoNewline
            Write-Host " $count record(s) " -ForegroundColor $color -NoNewline
            Write-Host "($([math]::Round($fileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Gray
            
            # Show sample data for small counts
            if ($count -gt 0 -and $count -le 3) {
                if ($item.Name -eq "Affiliates" -and $content -is [Array]) {
                    foreach ($affiliate in $content) {
                        Write-Host "    • $($affiliate.Name)" -ForegroundColor Gray
                    }
                }
                elseif ($item.Name -eq "Drivers" -and $content -is [Array]) {
                    foreach ($driver in $content) {
                        $uidDisplay = if ($driver.UserUid) { " [UID: $($driver.UserUid)]" } else { " [No UID]" }
                        Write-Host "    • $($driver.Name)$uidDisplay" -ForegroundColor Gray
                    }
                }
                elseif ($item.Name -eq "Bookings" -and $content -is [Array]) {
                    foreach ($booking in $content) {
                        $statusColor = switch ($booking.Status) {
                            "Scheduled" { "Cyan" }
                            "Completed" { "Green" }
                            "Cancelled" { "Red" }
                            default { "Gray" }
                        }
                        Write-Host "    • " -NoNewline
                        Write-Host "$($booking.Status)" -ForegroundColor $statusColor -NoNewline
                        Write-Host " - $($booking.PassengerName)" -ForegroundColor Gray
                    }
                }
                elseif ($item.Name -eq "Quotes" -and $content -is [Array]) {
                    foreach ($quote in $content) {
                        $statusColor = switch ($quote.Status) {
                            "Submitted" { "Yellow" }
                            "InReview" { "Cyan" }
                            "Priced" { "Green" }
                            "Rejected" { "Red" }
                            "Closed" { "Gray" }
                            default { "White" }
                        }
                        Write-Host "    • " -NoNewline
                        Write-Host "$($quote.Status)" -ForegroundColor $statusColor -NoNewline
                        Write-Host " - $($quote.PassengerName)" -ForegroundColor Gray
                    }
                }
            }
        }
        catch {
            Write-Host "$($item.Icon) $($item.Name):" -NoNewline
            Write-Host " ERROR reading file" -ForegroundColor Red
            Write-Host "    $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "$($item.Icon) $($item.Name):" -NoNewline
        Write-Host " File not found" -ForegroundColor DarkGray
    }
    
    Write-Host ""
}

if (-not $hasData) {
    Write-Host "No test data found. Run .\Scripts\Seed-All.ps1 to create test data." -ForegroundColor Yellow
}
else {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Actions:" -ForegroundColor Cyan
    Write-Host "  • To add more data: .\Scripts\Seed-All.ps1" -ForegroundColor Gray
    Write-Host "  • To clear all data: .\Scripts\Clear-TestData.ps1 -Confirm" -ForegroundColor Gray
}

Write-Host ""
