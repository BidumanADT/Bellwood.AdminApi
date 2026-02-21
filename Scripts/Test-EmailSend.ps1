#Requires -Version 5.1
<#
.SYNOPSIS
    Smoke-test the email configuration by triggering one of each major email type.

.DESCRIPTION
    Exercises three email send paths against a running AdminAPI instance:
      1. Quote submission     (Quote.Submitted)   - POST /quotes
      2. Booking submission   (Booking.Submitted) - POST /bookings
      3. Driver assignment    (Booking.DriverAssigned) - POST /bookings/{id}/assign-driver

    In AlphaSandbox mode all three emails are intercepted to the
    OverrideRecipients.Address (central-inbox@bellwood-alpha.test by default).
    Check that address in your Mailgun inbox after the run.

    Prerequisites:
      - AdminAPI running with ASPNETCORE_ENVIRONMENT=Alpha (or equivalent)
      - Email:Mode = AlphaSandbox in active config
      - Email:Smtp:* secrets set (From, Host, Username, Password)
      - AuthServer running and reachable
      - At least one seeded affiliate+driver (run Seed-Affiliates.ps1 first)

.PARAMETER ApiBaseUrl
    Base URL of the AdminAPI.  Default: https://localhost:5206

.PARAMETER AuthServerUrl
    Base URL of the AuthServer.  Default: https://localhost:5001

.EXAMPLE
    .\Test-EmailSend.ps1

.EXAMPLE
    .\Test-EmailSend.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl    = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Email Configuration Smoke Test" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script triggers one email of each type and verifies the" -ForegroundColor Yellow
Write-Host "API calls succeed.  Then check your Mailtrap inbox to confirm" -ForegroundColor Yellow
Write-Host "delivery." -ForegroundColor Yellow
Write-Host ""
Write-Host "Email types under test:" -ForegroundColor Gray
Write-Host "  1. Quote.Submitted       (POST /quotes)" -ForegroundColor Gray
Write-Host "  2. Booking.Submitted     (POST /bookings)" -ForegroundColor Gray
Write-Host "  3. Booking.DriverAssigned (POST /bookings/{id}/assign-driver)" -ForegroundColor Gray
Write-Host ""

# ----------------------------------------------------------------
# PowerShell 5.1 - trust all TLS certs (localhost dev certs)
# ----------------------------------------------------------------
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

# ----------------------------------------------------------------
# Test counters + shared state
# ----------------------------------------------------------------
$testsPassed  = 0
$testsFailed  = 0
$totalTests   = 5   # 3 email triggers + 1 affiliate + 1 driver setup
$script:currentTest = 1

$testData = @{
    QuoteId     = $null
    BookingId   = $null
    AffiliateId = $null
    DriverId    = $null
}

# ----------------------------------------------------------------
# Helper: decode JWT so we can log claims
# ----------------------------------------------------------------
function Get-JwtPayload {
    param([string]$Token)
    $parts = $Token.Split('.')
    if ($parts.Length -ne 3) { return $null }
    $payload = $parts[1]
    $padding = (4 - ($payload.Length % 4)) % 4
    $payload  = $payload + ("=" * $padding)
    $bytes    = [Convert]::FromBase64String($payload)
    $json     = [System.Text.Encoding]::UTF8.GetString($bytes)
    return $json | ConvertFrom-Json
}

# ----------------------------------------------------------------
# Helper: run one named test, return result hashtable
# ----------------------------------------------------------------
function Test-Endpoint {
    param(
        [string]$TestName,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body         = $null,
        [int]$ExpectedStatus,
        [string]$Description,
        [scriptblock]$Validation = $null
    )

    Write-Host "`n?????????????????????????????????????????????????????" -ForegroundColor DarkGray
    Write-Host "[TEST $script:currentTest/$totalTests] $TestName" -ForegroundColor Yellow
    Write-Host "?????????????????????????????????????????????????????" -ForegroundColor DarkGray
    Write-Host "? Description: $Description" -ForegroundColor Cyan
    Write-Host "? Endpoint: $Method $Url" -ForegroundColor Gray
    $script:currentTest++

    try {
        $response = if ($Body) {
            Write-Host "? Request Body:" -ForegroundColor Gray
            Write-Host ($Body | ConvertTo-Json -Depth 8) -ForegroundColor DarkGray
            Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers `
                -Body ($Body | ConvertTo-Json -Depth 8) `
                -ContentType "application/json" -UseBasicParsing -ErrorAction SilentlyContinue
        } else {
            Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers `
                -UseBasicParsing -ErrorAction SilentlyContinue
        }

        $actualStatus = $response.StatusCode
        $responseData = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    }
    catch {
        $actualStatus = $_.Exception.Response.StatusCode.value__
        $responseData = $null
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $responseData = $reader.ReadToEnd() | ConvertFrom-Json
            $reader.Close(); $stream.Close()
        } catch { }
    }

    if ($responseData) {
        Write-Host "? Response ($actualStatus):" -ForegroundColor Gray
        Write-Host ($responseData | ConvertTo-Json -Depth 5) -ForegroundColor DarkGray
    }

    $statusOk = $actualStatus -eq $ExpectedStatus
    if ($statusOk) {
        Write-Host "? Status: PASS (got $ExpectedStatus)" -ForegroundColor Green
    } else {
        Write-Host "? Status: FAIL (expected $ExpectedStatus, got $actualStatus)" -ForegroundColor Red
    }

    $validationOk = $true
    if ($Validation -and $responseData) {
        Write-Host "? Running validation..." -ForegroundColor Gray
        try {
            $validationOk = & $Validation -Data $responseData
            if ($validationOk) {
                Write-Host "? Validation: PASS" -ForegroundColor Green
            } else {
                Write-Host "? Validation: FAIL" -ForegroundColor Red
            }
        } catch {
            Write-Host "? Validation error: $($_.Exception.Message)" -ForegroundColor Red
            $validationOk = $false
        }
    }

    $pass = $statusOk -and $validationOk
    if ($pass) {
        Write-Host "? OVERALL: PASS" -ForegroundColor Green -BackgroundColor DarkGreen
        $script:testsPassed++
    } else {
        Write-Host "? OVERALL: FAIL" -ForegroundColor Red -BackgroundColor DarkRed
        $script:testsFailed++
    }

    return @{ Success = $pass; Status = $actualStatus; Data = $responseData }
}

# ================================================================
# AUTHENTICATION
# ================================================================

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  AUTHENTICATION                                                ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

# Authenticate as Alice (admin) - needed for booking + driver assignment
Write-Host "`n? Authenticating as admin (alice)..." -ForegroundColor Yellow
try {
    $aliceResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST -ContentType "application/json" -UseBasicParsing `
        -Body (@{ username = "alice"; password = "password" } | ConvertTo-Json)

    $aliceToken   = $aliceResponse.accessToken
    $aliceHeaders = @{ "Authorization" = "Bearer $aliceToken" }
    $aliceClaims  = Get-JwtPayload -Token $aliceToken

    Write-Host "? Alice authenticated" -ForegroundColor Green
    Write-Host "   Role: $($aliceClaims.role) | UserId: $($aliceClaims.userId)" -ForegroundColor Gray
}
catch {
    Write-Host "? Failed to authenticate Alice: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Authenticate as Chris (booker) - quote submission requires a booker role
Write-Host "`n? Authenticating as booker (chris)..." -ForegroundColor Yellow
try {
    $chrisResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST -ContentType "application/json" -UseBasicParsing `
        -Body (@{ username = "chris"; password = "password" } | ConvertTo-Json)

    $chrisToken   = $chrisResponse.accessToken
    $chrisHeaders = @{ "Authorization" = "Bearer $chrisToken" }
    $chrisClaims  = Get-JwtPayload -Token $chrisToken

    Write-Host "? Chris authenticated" -ForegroundColor Green
    Write-Host "   Role: $($chrisClaims.role) | UserId: $($chrisClaims.userId)" -ForegroundColor Gray
}
catch {
    Write-Host "? Failed to authenticate Chris: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ================================================================
# SETUP: AFFILIATE + DRIVER (needed for driver-assignment email)
# ================================================================

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  SETUP: AFFILIATE + DRIVER                                     ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$affiliateBody = @{
    name           = "Email Test Transport Co."
    pointOfContact = "Grace Affiliate"
    phone          = "312-555-9001"
    email          = "grace@emailtest-transport.example.com"
    streetAddress  = "900 Email Test Blvd"
    city           = "Chicago"
    state          = "IL"
}

$affiliateResult = Test-Endpoint `
    -TestName    "Create test affiliate" `
    -Method      "POST" `
    -Url         "$ApiBaseUrl/affiliates" `
    -Headers     $aliceHeaders `
    -Body        $affiliateBody `
    -ExpectedStatus 201 `
    -Description "Create a throwaway affiliate so we have an email address for the driver-assignment send" `
    -Validation  {
        param($Data)
        if (-not $Data.id) {
            Write-Host "  ? Missing affiliate ID" -ForegroundColor Red
            return $false
        }
        $script:testData.AffiliateId = $Data.id
        Write-Host "  ? Affiliate ID: $($Data.id)" -ForegroundColor Cyan
        return $true
    }

if (-not $affiliateResult.Success) {
    Write-Host "`n? Cannot proceed without affiliate. Aborting." -ForegroundColor Red
    exit 1
}

$driverBody = @{
    name    = "Email Test Driver"
    phone   = "312-555-9002"
    userUid = "email-test-driver-001"
}

$driverResult = Test-Endpoint `
    -TestName    "Create test driver under affiliate" `
    -Method      "POST" `
    -Url         "$ApiBaseUrl/affiliates/$($testData.AffiliateId)/drivers" `
    -Headers     $aliceHeaders `
    -Body        $driverBody `
    -ExpectedStatus 201 `
    -Description "Create a driver linked to the test affiliate so assign-driver has a real email target" `
    -Validation  {
        param($Data)
        if (-not $Data.id) {
            Write-Host "  ? Missing driver ID" -ForegroundColor Red
            return $false
        }
        $script:testData.DriverId = $Data.id
        Write-Host "  ? Driver ID: $($Data.id)" -ForegroundColor Cyan
        return $true
    }

if (-not $driverResult.Success) {
    Write-Host "`n? Cannot proceed without driver. Aborting." -ForegroundColor Red
    exit 1
}

# ================================================================
# EMAIL TEST 1: QUOTE SUBMISSION  (Quote.Submitted)
# ================================================================

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  EMAIL 1 OF 3: Quote.Submitted                                 ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "? Expects one email in Mailtrap after this call." -ForegroundColor Yellow

$quoteBody = @{
    booker = @{
        firstName   = "Email"
        lastName    = "TestBooker"
        phoneNumber = "312-555-0101"
        emailAddress = "email.testbooker@bellwood-alpha.test"
    }
    passenger = @{
        firstName   = "Email"
        lastName    = "TestPassenger"
        phoneNumber = "312-555-0102"
        emailAddress = "email.testpassenger@bellwood-alpha.test"
    }
    vehicleClass    = "Sedan"
    pickupDateTime  = (Get-Date).AddDays(3).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation  = "O'Hare International Airport — Terminal 3"
    pickupStyle     = "MeetAndGreet"
    pickupSignText  = "TEST PASSENGER / Bellwood"
    dropoffLocation = "The Langham, Chicago"
    roundTrip       = $false
    passengerCount  = 2
    checkedBags     = 1
    carryOnBags     = 2
    outboundFlight  = @{
        flightNumber = "AA1234"
        tailNumber   = $null
    }
}

$quoteResult = Test-Endpoint `
    -TestName    "POST /quotes — trigger Quote.Submitted email" `
    -Method      "POST" `
    -Url         "$ApiBaseUrl/quotes" `
    -Headers     $chrisHeaders `
    -Body        $quoteBody `
    -ExpectedStatus 202 `
    -Description "Submit a Meet-and-Greet quote with flight details; should fire Quote.Submitted to the override inbox" `
    -Validation  {
        param($Data)
        if (-not $Data.id) {
            Write-Host "  ? No quote ID returned" -ForegroundColor Red
            return $false
        }
        $script:testData.QuoteId = $Data.id
        Write-Host "  ? Quote ID: $($Data.id)" -ForegroundColor Cyan
        Write-Host "  ? Check Mailtrap for subject: [Quote] $($Data.id)" -ForegroundColor Yellow
        return $true
    }

# ================================================================
# EMAIL TEST 2: BOOKING SUBMISSION  (Booking.Submitted)
# ================================================================

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  EMAIL 2 OF 3: Booking.Submitted                               ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "? Expects one email in Mailtrap after this call." -ForegroundColor Yellow

$bookingBody = @{
    booker = @{
        firstName    = "Email"
        lastName     = "TestBooker"
        phoneNumber  = "312-555-0201"
        emailAddress = "email.testbooker@bellwood-alpha.test"
    }
    passenger = @{
        firstName    = "Email"
        lastName     = "TestPassenger"
        phoneNumber  = "312-555-0202"
        emailAddress = "email.testpassenger@bellwood-alpha.test"
    }
    vehicleClass    = "SUV"
    pickupDateTime  = (Get-Date).AddDays(5).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation  = "Midway Airport — Southwest Terminal"
    pickupStyle     = "Curbside"
    dropoffLocation = "Peninsula Hotel Chicago"
    roundTrip       = $false
    passengerCount  = 3
    checkedBags     = 3
    carryOnBags     = 1
}

$bookingResult = Test-Endpoint `
    -TestName    "POST /bookings — trigger Booking.Submitted email" `
    -Method      "POST" `
    -Url         "$ApiBaseUrl/bookings" `
    -Headers     $aliceHeaders `
    -Body        $bookingBody `
    -ExpectedStatus 202 `
    -Description "Submit a direct booking (SUV, Curbside, 3 pax); should fire Booking.Submitted to the override inbox" `
    -Validation  {
        param($Data)
        if (-not $Data.id) {
            Write-Host "  ? No booking ID returned" -ForegroundColor Red
            return $false
        }
        $script:testData.BookingId = $Data.id
        Write-Host "  ? Booking ID: $($Data.id)" -ForegroundColor Cyan
        Write-Host "  ? Check Mailtrap for subject: Bellwood Elite - New Booking Request" -ForegroundColor Yellow
        return $true
    }

# ================================================================
# EMAIL TEST 3: DRIVER ASSIGNMENT  (Booking.DriverAssigned)
# ================================================================

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  EMAIL 3 OF 3: Booking.DriverAssigned                          ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "? Expects one email in Mailtrap after this call." -ForegroundColor Yellow

if (-not $testData.BookingId) {
    Write-Host "??  Skipping driver assignment email — no booking ID available." -ForegroundColor Yellow
    $script:testsFailed++
}
else {
    $assignBody = @{ driverId = $testData.DriverId }

    $assignResult = Test-Endpoint `
        -TestName    "POST /bookings/{id}/assign-driver — trigger Booking.DriverAssigned email" `
        -Method      "POST" `
        -Url         "$ApiBaseUrl/bookings/$($testData.BookingId)/assign-driver" `
        -Headers     $aliceHeaders `
        -Body        $assignBody `
        -ExpectedStatus 200 `
        -Description "Assign the test driver to the booking; should fire Booking.DriverAssigned to grace@emailtest-transport.example.com (intercepted to override inbox)" `
        -Validation  {
            param($Data)
            $ok = $true

            if ($Data.assignedDriverId -eq $script:testData.DriverId) {
                Write-Host "  ? assignedDriverId matches" -ForegroundColor Green
            } else {
                Write-Host "  ? assignedDriverId mismatch" -ForegroundColor Red
                $ok = $false
            }

            if ($Data.assignedDriverName -eq "Email Test Driver") {
                Write-Host "  ? assignedDriverName correct" -ForegroundColor Green
            } else {
                Write-Host "  ? assignedDriverName unexpected: $($Data.assignedDriverName)" -ForegroundColor Red
                $ok = $false
            }

            Write-Host "  ? Check Mailtrap for subject: Bellwood Elite - Driver Assignment" -ForegroundColor Yellow
            Write-Host "  ? Subject should include [orig: grace@emailtest-transport.example.com] if IncludeOriginalRecipientInSubject=true" -ForegroundColor Gray
            return $ok
        }
}

# ================================================================
# SUMMARY
# ================================================================

Write-Host ""
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?                     TEST SUMMARY                              ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Tests : $totalTests"      -ForegroundColor White
Write-Host "Passed      : $testsPassed"     -ForegroundColor Green
Write-Host "Failed      : $testsFailed"     -ForegroundColor $(if ($testsFailed -gt 0) { "Red" } else { "Green" })
Write-Host ""
Write-Host "Test Data Created:" -ForegroundColor Yellow
Write-Host "  Quote ID      : $(if ($testData.QuoteId)    { $testData.QuoteId }    else { '(not created)' })" -ForegroundColor Gray
Write-Host "  Booking ID    : $(if ($testData.BookingId)   { $testData.BookingId }   else { '(not created)' })" -ForegroundColor Gray
Write-Host "  Affiliate ID  : $(if ($testData.AffiliateId) { $testData.AffiliateId } else { '(not created)' })" -ForegroundColor Gray
Write-Host "  Driver ID     : $(if ($testData.DriverId)    { $testData.DriverId }    else { '(not created)' })" -ForegroundColor Gray

if ($testsFailed -eq 0) {
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host "?  ? ALL API CALLS SUCCEEDED                                   ?" -ForegroundColor Green
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host ""
    Write-Host "Now verify in your email inbox (Mailgun) that exactly 3 emails arrived:" -ForegroundColor Cyan
    Write-Host "  1. Subject contains '[Quote]'             — Quote.Submitted" -ForegroundColor Gray
    Write-Host "  2. Subject contains 'New Booking Request' — Booking.Submitted" -ForegroundColor Gray
    Write-Host "  3. Subject contains 'Driver Assignment'   — Booking.DriverAssigned" -ForegroundColor Gray
    Write-Host ""
    Write-Host "All three should be addressed To: central-inbox@bellwood-alpha.test" -ForegroundColor Gray
    Write-Host "and From: whatever Email:Smtp:From is set to in your user-secrets." -ForegroundColor Gray
    Write-Host ""
    exit 0
}
else {
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host "?  ? SOME TESTS FAILED — check output above                    ?" -ForegroundColor Red
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common causes:" -ForegroundColor Yellow
    Write-Host "  - Email:Smtp:* secrets not set (check startup log for '*** NOT SET ***')" -ForegroundColor Gray
    Write-Host "  - Email:Mode is not 'AlphaSandbox'" -ForegroundColor Gray
    Write-Host "  - AdminAPI not running with the Alpha launch profile" -ForegroundColor Gray
    Write-Host "  - AuthServer not reachable at $AuthServerUrl" -ForegroundColor Gray
    Write-Host "  - Mailgun SMTP credentials incorrect (check Host/Port/Username/Password secrets)" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
