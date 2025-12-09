#Requires -Version 5.1
<#
.SYNOPSIS
    Seeds test affiliate and driver data to the Bellwood AdminAPI.

.DESCRIPTION
    Creates sample affiliates with drivers that have UserUid values matching AuthServer test users.
    This ensures test drivers can log into the DriverApp and see assigned rides.

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Seed-Affiliates.ps1
    
.EXAMPLE
    .\Seed-Affiliates.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Bellwood - Seed Affiliates & Drivers" -ForegroundColor Cyan
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

# Step 2: Seed affiliates and drivers
Write-Host "Step 2: Seeding affiliates and drivers..." -ForegroundColor Yellow
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }

    $seedResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/dev/seed-affiliates" `
        -Method POST `
        -Headers $headers `
        -UseBasicParsing

    Write-Host "? Success!" -ForegroundColor Green
    Write-Host "  - Affiliates created: $($seedResponse.affiliatesAdded)" -ForegroundColor White
    Write-Host "  - Drivers created: $($seedResponse.driversAdded)" -ForegroundColor White
    Write-Host ""
    Write-Host "Note: $($seedResponse.note)" -ForegroundColor Yellow
    Write-Host ""
}
catch {
    Write-Host "? Failed to seed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: List all affiliates
Write-Host "Step 3: Listing all affiliates..." -ForegroundColor Yellow
try {
    $affiliates = Invoke-RestMethod -Uri "$ApiBaseUrl/affiliates/list" `
        -Method GET `
        -Headers $headers `
        -UseBasicParsing

    Write-Host "? Current affiliates in system:" -ForegroundColor Green
    Write-Host ""
    
    foreach ($affiliate in $affiliates) {
        Write-Host "  ?? $($affiliate.name)" -ForegroundColor Cyan
        Write-Host "     Contact: $($affiliate.pointOfContact)" -ForegroundColor White
        Write-Host "     Phone: $($affiliate.phone)" -ForegroundColor White
        Write-Host "     Email: $($affiliate.email)" -ForegroundColor White
        Write-Host "     Drivers: $($affiliate.drivers.Count)" -ForegroundColor White
        
        foreach ($driver in $affiliate.drivers) {
            Write-Host "       ?? $($driver.name) - $($driver.phone)" -ForegroundColor Gray
            if ($driver.userUid) {
                Write-Host "          UserUID: $($driver.userUid)" -ForegroundColor DarkGray
            }
            else {
                Write-Host "          ??  No UserUID (cannot login to DriverApp)" -ForegroundColor Yellow
            }
        }
        Write-Host ""
    }
}
catch {
    Write-Host "? Failed to list affiliates: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Seeding Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Test Drivers Created:" -ForegroundColor Cyan
Write-Host "  1. Charlie Johnson (UserUid: driver-001) - Chicago Limo Service" -ForegroundColor White
Write-Host "  2. Sarah Lee (UserUid: driver-002) - Chicago Limo Service" -ForegroundColor White
Write-Host "  3. Robert Brown (UserUid: driver-003) - Suburban Chauffeurs" -ForegroundColor White
Write-Host ""
Write-Host "These drivers can log into the DriverApp using their corresponding" -ForegroundColor Gray
Write-Host "AuthServer credentials (e.g., username: charlie, password: password)" -ForegroundColor Gray
Write-Host ""

exit 0
