#Requires -Version 5.1
<#
.SYNOPSIS
    Seeds test booking data to the Bellwood AdminAPI.

.DESCRIPTION
    Creates sample bookings with all statuses and assigned drivers for testing purposes.
    Includes specific bookings for Charlie (driver-001) at 5 hours and 48 hours in the future.

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Seed-Bookings.ps1
    
.EXAMPLE
    .\Seed-Bookings.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Bellwood - Seed Bookings" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# For PowerShell 5.1 - ignore certificate validation
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

# Step 1: Get JWT Token
Write-Host "Step 1: Authenticating with AuthServer..." -ForegroundColor Yellow
try {
    $loginBody = @{
        username = "alice"
        password = "password"
    } | ConvertTo-Json

    $loginResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginBody `
        -UseBasicParsing

    $token = $loginResponse.accessToken
    Write-Host "? Authentication successful!" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure AuthServer is running on $AuthServerUrl" -ForegroundColor Yellow
    exit 1
}

# Step 2: Seed bookings
Write-Host "Step 2: Seeding bookings..." -ForegroundColor Yellow
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }

    $seedResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/bookings/seed" `
        -Method POST `
        -Headers $headers `
        -UseBasicParsing

    Write-Host "? Success: $($seedResponse.added) bookings created" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Error seeding bookings: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: List bookings to show what was created
Write-Host "Step 3: Listing created bookings..." -ForegroundColor Yellow
try {
    $bookings = Invoke-RestMethod -Uri "$ApiBaseUrl/bookings/list?take=20" `
        -Method GET `
        -Headers $headers `
        -UseBasicParsing

    Write-Host "? Current bookings in system:" -ForegroundColor Green
    Write-Host ""
    
    $statusColors = @{
        "Requested" = "Yellow"
        "Confirmed" = "Cyan"
        "Scheduled" = "Blue"
        "InProgress" = "Magenta"
        "Completed" = "Green"
        "Cancelled" = "Red"
        "NoShow" = "DarkRed"
    }

    foreach ($booking in $bookings) {
        $statusColor = $statusColors[$booking.status]
        if (-not $statusColor) { $statusColor = "White" }
        
        Write-Host "  ?? " -NoNewline
        Write-Host "$($booking.status)" -ForegroundColor $statusColor -NoNewline
        Write-Host " - $($booking.passengerName)" -ForegroundColor White
        Write-Host "     Pickup: $($booking.pickupLocation)" -ForegroundColor Gray
        Write-Host "     Time: $($booking.pickupDateTime)" -ForegroundColor Gray
        
        if ($booking.assignedDriverName) {
            Write-Host "     Driver: $($booking.assignedDriverName)" -ForegroundColor Cyan
            if ($booking.assignedDriverUid) {
                Write-Host "     UID: $($booking.assignedDriverUid)" -ForegroundColor DarkGray
            }
        }
        else {
            Write-Host "     Driver: Unassigned" -ForegroundColor DarkGray
        }
        Write-Host ""
    }
}
catch {
    Write-Host "? Failed to list bookings: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Seeding Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Key Test Bookings:" -ForegroundColor Yellow
Write-Host "  • Charlie's ride (5 hours): Jordan Chen - Scheduled" -ForegroundColor White
Write-Host "  • Charlie's ride (48 hours): Emma Watson - Scheduled" -ForegroundColor White
Write-Host "  • Various status examples for testing workflows" -ForegroundColor White
Write-Host ""
Write-Host "Note: Bookings assigned to driver-001 (Charlie) will appear" -ForegroundColor Gray
Write-Host "      in the DriverApp when Charlie logs in (username: charlie)" -ForegroundColor Gray
Write-Host ""

exit 0
