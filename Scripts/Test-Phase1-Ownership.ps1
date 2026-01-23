#Requires -Version 5.1
<#
.SYNOPSIS
    Phase 1 Testing - Seeds data with multiple user accounts to test ownership filtering.

.DESCRIPTION
    Creates test data owned by different users (alice, chris) to verify Phase 1 access controls.

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Test-Phase1-Ownership.ps1
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PHASE 1 - Ownership Testing" -ForegroundColor Cyan
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

# =====================================================================
# STEP 1: Authenticate as Alice (Admin)
# =====================================================================

Write-Host "Step 1: Authenticating as Alice (admin)..." -ForegroundColor Yellow
try {
    $aliceLogin = @{
        username = "alice"
        password = "password"
    } | ConvertTo-Json

    $aliceResponse = Invoke-RestMethod -Uri "$AuthServerUrl/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $aliceLogin `
        -UseBasicParsing

    $aliceToken = $aliceResponse.accessToken
    Write-Host "? Alice authenticated!" -ForegroundColor Green
    
    # Decode JWT to show userId claim
    $aliceClaims = $aliceToken.Split('.')[1]
    $aliceClaims = $aliceClaims.PadRight(($aliceClaims.Length + (4 - $aliceClaims.Length % 4) % 4), '=')
    $alicePayload = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($aliceClaims)) | ConvertFrom-Json
    
    Write-Host "   userId: $($alicePayload.userId)" -ForegroundColor Gray
    Write-Host "   role: $($alicePayload.role)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# =====================================================================
# STEP 2: Seed Affiliates & Drivers (Alice - Admin)
# =====================================================================

Write-Host "Step 2: Seeding affiliates and drivers as Alice..." -ForegroundColor Yellow
try {
    $aliceHeaders = @{
        "Authorization" = "Bearer $aliceToken"
    }

    $seedResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/dev/seed-affiliates" `
        -Method POST `
        -Headers $aliceHeaders `
        -UseBasicParsing

    Write-Host "? Affiliates & drivers created!" -ForegroundColor Green
    Write-Host "   Affiliates: $($seedResponse.affiliatesAdded)" -ForegroundColor Gray
    Write-Host "   Drivers: $($seedResponse.driversAdded)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# =====================================================================
# STEP 3: Authenticate as Chris (Booker)
# =====================================================================

Write-Host "Step 3: Authenticating as Chris (booker)..." -ForegroundColor Yellow
try {
    $chrisLogin = @{
        username = "chris"
        password = "password"
    } | ConvertTo-Json

    $chrisResponse = Invoke-RestMethod -Uri "$AuthServerUrl/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $chrisLogin `
        -UseBasicParsing

    $chrisToken = $chrisResponse.accessToken
    Write-Host "? Chris authenticated!" -ForegroundColor Green
    
    # Decode JWT to show userId claim
    $chrisClaims = $chrisToken.Split('.')[1]
    $chrisClaims = $chrisClaims.PadRight(($chrisClaims.Length + (4 - $chrisClaims.Length % 4) % 4), '=')
    $chrisPayload = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($chrisClaims)) | ConvertFrom-Json
    
    Write-Host "   userId: $($chrisPayload.userId)" -ForegroundColor Gray
    Write-Host "   role: $($chrisPayload.role)" -ForegroundColor Gray
    Write-Host "   email: $($chrisPayload.email)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# =====================================================================
# STEP 4: Seed Quotes as Alice (Admin)
# =====================================================================

Write-Host "Step 4: Seeding quotes as Alice (admin)..." -ForegroundColor Yellow
try {
    $aliceQuotesResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/seed" `
        -Method POST `
        -Headers $aliceHeaders `
        -UseBasicParsing

    Write-Host "? Alice's quotes created!" -ForegroundColor Green
    Write-Host "   Count: $($aliceQuotesResponse.added)" -ForegroundColor Gray
    Write-Host "   CreatedByUserId: $($aliceQuotesResponse.createdByUserId)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 5: Seed Quotes as Chris (Booker)
# =====================================================================

Write-Host "Step 5: Seeding quotes as Chris (booker)..." -ForegroundColor Yellow
try {
    $chrisHeaders = @{
        "Authorization" = "Bearer $chrisToken"
    }

    $chrisQuotesResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/seed" `
        -Method POST `
        -Headers $chrisHeaders `
        -UseBasicParsing

    Write-Host "? Chris's quotes created!" -ForegroundColor Green
    Write-Host "   Count: $($chrisQuotesResponse.added)" -ForegroundColor Gray
    Write-Host "   CreatedByUserId: $($chrisQuotesResponse.createdByUserId)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 6: Seed Bookings as Alice (Admin)
# =====================================================================

Write-Host "Step 6: Seeding bookings as Alice (admin)..." -ForegroundColor Yellow
try {
    $aliceBookingsResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/bookings/seed" `
        -Method POST `
        -Headers $aliceHeaders `
        -UseBasicParsing

    Write-Host "? Alice's bookings created!" -ForegroundColor Green
    Write-Host "   Count: $($aliceBookingsResponse.added)" -ForegroundColor Gray
    Write-Host "   CreatedByUserId: $($aliceBookingsResponse.createdByUserId)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 7: Seed Bookings as Chris (Booker)
# =====================================================================

Write-Host "Step 7: Seeding bookings as Chris (booker)..." -ForegroundColor Yellow
try {
    $chrisBookingsResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/bookings/seed" `
        -Method POST `
        -Headers $chrisHeaders `
        -UseBasicParsing

    Write-Host "? Chris's bookings created!" -ForegroundColor Green
    Write-Host "   Count: $($chrisBookingsResponse.added)" -ForegroundColor Gray
    Write-Host "   CreatedByUserId: $($chrisBookingsResponse.createdByUserId)" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 8: Test Quote Access - Alice (Admin)
# =====================================================================

Write-Host "Step 8: Testing Alice's quote access (should see ALL quotes)..." -ForegroundColor Yellow
try {
    $aliceQuotesList = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/list?take=100" `
        -Method GET `
        -Headers $aliceHeaders `
        -UseBasicParsing

    Write-Host "? Alice sees $($aliceQuotesList.Count) quotes (expected: 10)" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 9: Test Quote Access - Chris (Booker)
# =====================================================================

Write-Host "Step 9: Testing Chris's quote access (should see ONLY his quotes)..." -ForegroundColor Yellow
try {
    $chrisQuotesList = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/list?take=100" `
        -Method GET `
        -Headers $chrisHeaders `
        -UseBasicParsing

    Write-Host "? Chris sees $($chrisQuotesList.Count) quotes (expected: 5)" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 10: Test Booking Access - Alice (Admin)
# =====================================================================

Write-Host "Step 10: Testing Alice's booking access (should see ALL bookings)..." -ForegroundColor Yellow
try {
    $aliceBookingsList = Invoke-RestMethod -Uri "$ApiBaseUrl/bookings/list?take=100" `
        -Method GET `
        -Headers $aliceHeaders `
        -UseBasicParsing

    Write-Host "? Alice sees $($aliceBookingsList.Count) bookings (expected: 16)" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 11: Test Booking Access - Chris (Booker)
# =====================================================================

Write-Host "Step 11: Testing Chris's booking access (should see ONLY his bookings)..." -ForegroundColor Yellow
try {
    $chrisBookingsList = Invoke-RestMethod -Uri "$ApiBaseUrl/bookings/list?take=100" `
        -Method GET `
        -Headers $chrisHeaders `
        -UseBasicParsing

    Write-Host "? Chris sees $($chrisBookingsList.Count) bookings (expected: 8)" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# =====================================================================
# STEP 12: Test Forbidden Access - Chris tries Alice's quote
# =====================================================================

Write-Host "Step 12: Testing forbidden access (Chris tries to get Alice's quote)..." -ForegroundColor Yellow
Write-Host ""
Write-Host "   [DEBUG] Fetching Alice's quote list to find one created by her..." -ForegroundColor DarkGray

# Get Alice's full quote list with details
$aliceFullQuotes = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/list?take=100" `
    -Method GET `
    -Headers $aliceHeaders `
    -UseBasicParsing

# Find first quote that Alice actually created (not Chris)
$aliceOwnedQuote = $null
foreach ($quote in $aliceFullQuotes) {
    # Get full quote details to see CreatedByUserId
    $quoteDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$($quote.id)" `
        -Method GET `
        -Headers $aliceHeaders `
        -UseBasicParsing
    
    if ($quoteDetail.createdByUserId -eq $alicePayload.userId) {
        $aliceOwnedQuote = $quoteDetail
        break
    }
}

if ($null -eq $aliceOwnedQuote) {
    Write-Host "   [ERROR] Could not find a quote owned by Alice!" -ForegroundColor Red
    Write-Host ""
} else {
    Write-Host "   [DEBUG] Found Alice's quote:" -ForegroundColor DarkGray
    Write-Host "           Quote ID: $($aliceOwnedQuote.id)" -ForegroundColor DarkGray
    Write-Host "           CreatedByUserId: $($aliceOwnedQuote.createdByUserId)" -ForegroundColor DarkGray
    Write-Host "           Booker: $($aliceOwnedQuote.bookerName)" -ForegroundColor DarkGray
    Write-Host "           Alice's userId: $($alicePayload.userId)" -ForegroundColor DarkGray
    Write-Host "           Chris's userId: $($chrisPayload.userId)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "   [DEBUG] Chris attempting to access quote $($aliceOwnedQuote.id)..." -ForegroundColor DarkGray
    
    try {
        # Try to access with Chris's token
        $forbiddenResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$($aliceOwnedQuote.id)" `
            -Method GET `
            -Headers $chrisHeaders `
            -UseBasicParsing

        Write-Host "   ? SECURITY ISSUE: Chris can access Alice's quote!" -ForegroundColor Red
        Write-Host "           Response received (should have been 403):" -ForegroundColor Red
        Write-Host "           Quote ID: $($forbiddenResponse.id)" -ForegroundColor Red
        Write-Host "           CreatedByUserId: $($forbiddenResponse.createdByUserId)" -ForegroundColor Red
        Write-Host ""
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 403) {
            Write-Host "   ? Access correctly denied (403 Forbidden)" -ForegroundColor Green
            Write-Host ""
        } else {
            Write-Host "   ??  Unexpected error: HTTP $($_.Exception.Response.StatusCode)" -ForegroundColor Yellow
            Write-Host "           Message: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host ""
        }
    }
}

# =====================================================================
# RESULTS SUMMARY
# =====================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 1 Testing Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Summary:" -ForegroundColor White
Write-Host "  • Alice (admin) created: 5 quotes, 8 bookings" -ForegroundColor Gray
Write-Host "  • Chris (booker) created: 5 quotes, 8 bookings" -ForegroundColor Gray
Write-Host ""
Write-Host "Access Control Results:" -ForegroundColor White
Write-Host "  • Alice sees: ALL quotes ($($aliceQuotesList.Count)), ALL bookings ($($aliceBookingsList.Count))" -ForegroundColor Gray
Write-Host "  • Chris sees: OWN quotes ($($chrisQuotesList.Count)), OWN bookings ($($chrisBookingsList.Count))" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor White
Write-Host "  • Verify counts match expected values" -ForegroundColor Gray
Write-Host "  • Test cancel endpoint with ownership checks" -ForegroundColor Gray
Write-Host "  • Test driver access to bookings" -ForegroundColor Gray
Write-Host ""

exit 0
