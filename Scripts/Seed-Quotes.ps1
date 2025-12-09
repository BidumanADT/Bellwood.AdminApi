#Requires -Version 5.1
<#
.SYNOPSIS
    Seeds test quote data to the Bellwood AdminAPI.

.DESCRIPTION
    Creates sample quotes with various statuses for testing purposes.

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Seed-Quotes.ps1
    
.EXAMPLE
    .\Seed-Quotes.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Bellwood - Seed Quotes" -ForegroundColor Cyan
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

# Step 2: Seed quotes
Write-Host "Step 2: Seeding quotes..." -ForegroundColor Yellow
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }

    $seedResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/seed" `
        -Method POST `
        -Headers $headers `
        -UseBasicParsing

    Write-Host "? Success: $($seedResponse.added) quotes created" -ForegroundColor Green
    Write-Host ""
    Write-Host "Created quotes with statuses:" -ForegroundColor White
    Write-Host "  - Submitted (1)" -ForegroundColor Gray
    Write-Host "  - InReview (1)" -ForegroundColor Gray
    Write-Host "  - Priced (1)" -ForegroundColor Gray
    Write-Host "  - Rejected (1)" -ForegroundColor Gray
    Write-Host "  - Closed (1)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Error seeding quotes: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Seeding Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

exit 0
