#Requires -Version 5.1
<#
.SYNOPSIS
    Phase Alpha: Test complete quote lifecycle (Submitted ? Acknowledged ? Responded ? Accepted ? Booking).

.DESCRIPTION
    Comprehensive end-to-end test script for Phase Alpha quote lifecycle implementation:
    - Tests passenger (Chris) submitting quote
    - Tests dispatcher (Diana) acknowledging quote
    - Tests dispatcher (Diana) responding with price/ETA
    - Tests passenger (Chris) accepting quote ? creates booking
    - Tests passenger (Chris) canceling quote
    - Tests FSM validation (invalid status transitions)
    - Tests RBAC (only staff can acknowledge/respond)
    - Tests ownership (passenger can only accept/cancel own quotes)
    - Validates all audit trails and data persistence

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Test-PhaseAlpha-QuoteLifecycle.ps1
    
.EXAMPLE
    .\Test-PhaseAlpha-QuoteLifecycle.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Phase Alpha: Quote Lifecycle End-to-End Test" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Testing complete workflow:" -ForegroundColor Yellow
Write-Host "  1. Passenger submits quote ? Status: Submitted" -ForegroundColor Gray
Write-Host "  2. Dispatcher acknowledges ? Status: Acknowledged" -ForegroundColor Gray
Write-Host "  3. Dispatcher responds with price ? Status: Responded" -ForegroundColor Gray
Write-Host "  4. Passenger accepts ? Status: Accepted + Booking created" -ForegroundColor Gray
Write-Host "  5. Security & FSM validation tests" -ForegroundColor Gray
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

# Test counters
$testsPassed = 0
$testsFailed = 0
$totalTests = 18  # Updated for all planned tests

# Test data storage
$testData = @{
    QuoteId = $null
    BookingId = $null
    SecondQuoteId = $null
}

# Helper function to decode JWT payload
function Get-JwtPayload {
    param([string]$Token)
    
    $parts = $Token.Split('.')
    if ($parts.Length -ne 3) {
        Write-Host "? Invalid JWT format" -ForegroundColor Red
        return $null
    }
    
    $payload = $parts[1]
    # Add padding if needed
    $padding = (4 - ($payload.Length % 4)) % 4
    $payload = $payload + ("=" * $padding)
    
    $bytes = [Convert]::FromBase64String($payload)
    $json = [System.Text.Encoding]::UTF8.GetString($bytes)
    return $json | ConvertFrom-Json
}

# Helper function to test endpoint and return result
function Test-Endpoint {
    param(
        [string]$TestName,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body = $null,
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
            Write-Host ($Body | ConvertTo-Json -Depth 5) -ForegroundColor DarkGray
            
            Invoke-WebRequest -Uri $Url `
                -Method $Method `
                -Headers $Headers `
                -Body ($Body | ConvertTo-Json -Depth 5) `
                -ContentType "application/json" `
                -UseBasicParsing `
                -ErrorAction SilentlyContinue
        } else {
            Invoke-WebRequest -Uri $Url `
                -Method $Method `
                -Headers $Headers `
                -UseBasicParsing `
                -ErrorAction SilentlyContinue
        }
        
        $actualStatus = $response.StatusCode
        $responseData = if ($response.Content) {
            $response.Content | ConvertFrom-Json
        } else {
            $null
        }
    } catch {
        $actualStatus = $_.Exception.Response.StatusCode.value__
        $responseData = $null
        
        # Try to get error response body
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $errorBody = $reader.ReadToEnd()
            $responseData = $errorBody | ConvertFrom-Json
            $reader.Close()
            $stream.Close()
        } catch {
            # Ignore parse errors
        }
    }
    
    # Display response
    if ($responseData) {
        Write-Host "? Response ($actualStatus):" -ForegroundColor Gray
        Write-Host ($responseData | ConvertTo-Json -Depth 5) -ForegroundColor DarkGray
    }
    
    # Check status code
    $statusMatch = $actualStatus -eq $ExpectedStatus
    if ($statusMatch) {
        Write-Host "? Status Check: PASS (got $ExpectedStatus)" -ForegroundColor Green
    } else {
        Write-Host "? Status Check: FAIL (expected $ExpectedStatus, got $actualStatus)" -ForegroundColor Red
    }
    
    # Run custom validation if provided
    $validationPass = $true
    if ($Validation -and $responseData) {
        Write-Host "? Running custom validation..." -ForegroundColor Gray
        try {
            $validationPass = & $Validation -Data $responseData
            if ($validationPass) {
                Write-Host "? Validation: PASS" -ForegroundColor Green
            } else {
                Write-Host "? Validation: FAIL" -ForegroundColor Red
            }
        } catch {
            Write-Host "? Validation Error: $($_.Exception.Message)" -ForegroundColor Red
            $validationPass = $false
        }
    }
    
    # Overall result
    $testPass = $statusMatch -and $validationPass
    if ($testPass) {
        Write-Host "? OVERALL: PASS" -ForegroundColor Green -BackgroundColor DarkGreen
        $script:testsPassed++
    } else {
        Write-Host "? OVERALL: FAIL" -ForegroundColor Red -BackgroundColor DarkRed
        $script:testsFailed++
    }
    
    return @{
        Success = $testPass
        Status = $actualStatus
        Data = $responseData
    }
}

$script:currentTest = 1

# ???????????????????????????????????????????????????????????????????
# PHASE 1: AUTHENTICATION
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 1: AUTHENTICATION & SETUP                              ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

# Authenticate as Chris (passenger/booker)
Write-Host "`n? Authenticating as passenger (chris)..." -ForegroundColor Yellow
try {
    $chrisLogin = @{
        username = "chris"
        password = "password"
    } | ConvertTo-Json

    $chrisResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $chrisLogin `
        -UseBasicParsing

    $chrisToken = $chrisResponse.accessToken
    $chrisHeaders = @{ "Authorization" = "Bearer $chrisToken" }
    
    $chrisClaims = Get-JwtPayload -Token $chrisToken
    Write-Host "? Chris authenticated successfully" -ForegroundColor Green
    Write-Host "   Role: $($chrisClaims.role)" -ForegroundColor Gray
    Write-Host "   UserId: $($chrisClaims.userId)" -ForegroundColor Gray
    Write-Host "   Email: $($chrisClaims.email)" -ForegroundColor Gray
    
    if ($chrisClaims.role -ne "booker") {
        Write-Host "??  Warning: Chris should have booker role, got: $($chrisClaims.role)" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "? Failed to authenticate Chris: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Authenticate as Diana (dispatcher)
Write-Host "`n? Authenticating as dispatcher (diana)..." -ForegroundColor Yellow
try {
    $dianaLogin = @{
        username = "diana"
        password = "password"
    } | ConvertTo-Json

    $dianaResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $dianaLogin `
        -UseBasicParsing

    $dianaToken = $dianaResponse.accessToken
    $dianaHeaders = @{ "Authorization" = "Bearer $dianaToken" }
    
    $dianaClaims = Get-JwtPayload -Token $dianaToken
    Write-Host "? Diana authenticated successfully" -ForegroundColor Green
    Write-Host "   Role: $($dianaClaims.role)" -ForegroundColor Gray
    Write-Host "   UserId: $($dianaClaims.userId)" -ForegroundColor Gray
    
    if ($dianaClaims.role -ne "dispatcher") {
        Write-Host "? Diana should have dispatcher role!" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "? Failed to authenticate Diana: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Authenticate as Alice (admin) for second passenger test
Write-Host "`n? Authenticating as admin (alice)..." -ForegroundColor Yellow
try {
    $aliceLogin = @{
        username = "alice"
        password = "password"
    } | ConvertTo-Json

    $aliceResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $aliceLogin `
        -UseBasicParsing

    $aliceToken = $aliceResponse.accessToken
    $aliceHeaders = @{ "Authorization" = "Bearer $aliceToken" }
    
    $aliceClaims = Get-JwtPayload -Token $aliceToken
    Write-Host "? Alice authenticated successfully" -ForegroundColor Green
    Write-Host "   Role: $($aliceClaims.role)" -ForegroundColor Gray
}
catch {
    Write-Host "? Failed to authenticate Alice: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ???????????????????????????????????????????????????????????????????
# PHASE 2: PASSENGER SUBMITS QUOTE
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 2: PASSENGER SUBMITS QUOTE                             ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$quoteRequest = @{
    booker = @{
        firstName = "Chris"
        lastName = "Bailey"
        phoneNumber = "312-555-1234"
        emailAddress = "chris.bailey@example.com"
    }
    passenger = @{
        firstName = "Jordan"
        lastName = "Chen"
        phoneNumber = "312-555-5678"
        emailAddress = "jordan.chen@example.com"
    }
    vehicleClass = "Sedan"
    pickupDateTime = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation = "O'Hare International Airport"
    pickupStyle = "Curbside"
    dropoffLocation = "Downtown Chicago"
    roundTrip = $false
    passengerCount = 2
    checkedBags = 2
    carryOnBags = 1
}

$result = Test-Endpoint `
    -TestName "Passenger submits quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes" `
    -Headers $chrisHeaders `
    -Body $quoteRequest `
    -ExpectedStatus 202 `
    -Description "Chris (passenger) submits a quote request for airport transfer" `
    -Validation {
        param($Data)
        if (-not $Data.id) {
            Write-Host "  ? Missing quote ID in response" -ForegroundColor Red
            return $false
        }
        $script:testData.QuoteId = $Data.id
        Write-Host "  ? Quote ID: $($Data.id)" -ForegroundColor Cyan
        return $true
    }

if (-not $result.Success) {
    Write-Host "`n? Critical failure: Cannot proceed without quote ID" -ForegroundColor Red
    exit 1
}

# ???????????????????????????????????????????????????????????????????
# PHASE 3: VERIFY QUOTE STATUS (SUBMITTED)
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 3: VERIFY QUOTE STATUS                                 ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$result = Test-Endpoint `
    -TestName "Get quote detail (verify status = Submitted)" `
    -Method "GET" `
    -Url "$ApiBaseUrl/quotes/$($testData.QuoteId)" `
    -Headers $dianaHeaders `
    -ExpectedStatus 200 `
    -Description "Dispatcher views quote detail to verify initial status" `
    -Validation {
        param($Data)
        
        $checks = @()
        
        # Check status
        if ($Data.status -eq "Submitted") {
            Write-Host "  ? Status is 'Submitted'" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Status should be 'Submitted', got: $($Data.status)" -ForegroundColor Red
            $checks += $false
        }
        
        # Check lifecycle fields are null
        if ($null -eq $Data.acknowledgedAt) {
            Write-Host "  ? AcknowledgedAt is null (not yet acknowledged)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? AcknowledgedAt should be null" -ForegroundColor Red
            $checks += $false
        }
        
        if ($null -eq $Data.respondedAt) {
            Write-Host "  ? RespondedAt is null (not yet responded)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? RespondedAt should be null" -ForegroundColor Red
            $checks += $false
        }
        
        # Check ownership
        if ($Data.createdByUserId) {
            Write-Host "  ? CreatedByUserId is populated: $($Data.createdByUserId)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? CreatedByUserId should be populated" -ForegroundColor Red
            $checks += $false
        }
        
        return ($checks -notcontains $false)
    }

# ???????????????????????????????????????????????????????????????????
# PHASE 4: DISPATCHER ACKNOWLEDGES QUOTE
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 4: DISPATCHER ACKNOWLEDGES QUOTE                       ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$result = Test-Endpoint `
    -TestName "Dispatcher acknowledges quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.QuoteId)/acknowledge" `
    -Headers $dianaHeaders `
    -ExpectedStatus 200 `
    -Description "Diana (dispatcher) acknowledges receipt of quote request" `
    -Validation {
        param($Data)
        
        $checks = @()
        
        if ($Data.status -eq "Acknowledged") {
            Write-Host "  ? Status changed to 'Acknowledged'" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Status should be 'Acknowledged', got: $($Data.status)" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.acknowledgedAt) {
            Write-Host "  ? AcknowledgedAt is populated: $($Data.acknowledgedAt)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? AcknowledgedAt should be populated" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.acknowledgedBy) {
            Write-Host "  ? AcknowledgedBy is populated: $($Data.acknowledgedBy)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? AcknowledgedBy should be populated" -ForegroundColor Red
            $checks += $false
        }
        
        return ($checks -notcontains $false)
    }

# ???????????????????????????????????????????????????????????????????
# PHASE 5: DISPATCHER RESPONDS WITH PRICE/ETA
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 5: DISPATCHER RESPONDS WITH PRICE/ETA                  ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$quoteResponse = @{
    estimatedPrice = 125.50
    estimatedPickupTime = (Get-Date).AddDays(7).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss")
    notes = "Traffic expected during rush hour. VIP service confirmed."
}

$result = Test-Endpoint `
    -TestName "Dispatcher sends price/ETA response" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.QuoteId)/respond" `
    -Headers $dianaHeaders `
    -Body $quoteResponse `
    -ExpectedStatus 200 `
    -Description "Diana sends estimated price ($125.50) and pickup time to passenger" `
    -Validation {
        param($Data)
        
        $checks = @()
        
        if ($Data.status -eq "Responded") {
            Write-Host "  ? Status changed to 'Responded'" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Status should be 'Responded', got: $($Data.status)" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.estimatedPrice -eq 125.50) {
            Write-Host "  ? EstimatedPrice is correct: `$$($Data.estimatedPrice)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? EstimatedPrice mismatch: expected 125.50, got $($Data.estimatedPrice)" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.respondedAt) {
            Write-Host "  ? RespondedAt is populated: $($Data.respondedAt)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? RespondedAt should be populated" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.respondedBy) {
            Write-Host "  ? RespondedBy is populated: $($Data.respondedBy)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? RespondedBy should be populated" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.notes -eq "Traffic expected during rush hour. VIP service confirmed.") {
            Write-Host "  ? Notes preserved correctly" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Notes mismatch" -ForegroundColor Red
            $checks += $false
        }
        
        return ($checks -notcontains $false)
    }

# ???????????????????????????????????????????????????????????????????
# PHASE 6: PASSENGER ACCEPTS QUOTE ? CREATES BOOKING
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 6: PASSENGER ACCEPTS QUOTE (CREATES BOOKING)           ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$result = Test-Endpoint `
    -TestName "Passenger accepts quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.QuoteId)/accept" `
    -Headers $chrisHeaders `
    -ExpectedStatus 200 `
    -Description "Chris (passenger) accepts quote, which creates a new booking" `
    -Validation {
        param($Data)
        
        $checks = @()
        
        if ($Data.quoteStatus -eq "Accepted") {
            Write-Host "  ? Quote status changed to 'Accepted'" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Quote status should be 'Accepted', got: $($Data.quoteStatus)" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.bookingId) {
            Write-Host "  ? Booking created with ID: $($Data.bookingId)" -ForegroundColor Green
            $script:testData.BookingId = $Data.bookingId
            $checks += $true
        } else {
            Write-Host "  ? BookingId should be populated" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.bookingStatus -eq "Requested") {
            Write-Host "  ? Booking status is 'Requested' (correct workflow)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Booking status should be 'Requested', got: $($Data.bookingStatus)" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.sourceQuoteId -eq $script:testData.QuoteId) {
            Write-Host "  ? SourceQuoteId links back to quote: $($Data.sourceQuoteId)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? SourceQuoteId mismatch" -ForegroundColor Red
            $checks += $false
        }
        
        return ($checks -notcontains $false)
    }

# Verify booking was created with correct data
$result = Test-Endpoint `
    -TestName "Verify booking has quote data" `
    -Method "GET" `
    -Url "$ApiBaseUrl/bookings/$($testData.BookingId)" `
    -Headers $dianaHeaders `
    -ExpectedStatus 200 `
    -Description "Verify booking was created with data from quote" `
    -Validation {
        param($Data)
        
        $checks = @()
        
        if ($Data.sourceQuoteId -eq $script:testData.QuoteId) {
            Write-Host "  ? Booking has SourceQuoteId: $($Data.sourceQuoteId)" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? SourceQuoteId mismatch in booking" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.passengerName -eq "Jordan Chen") {
            Write-Host "  ? Passenger name transferred correctly" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Passenger name mismatch" -ForegroundColor Red
            $checks += $false
        }
        
        if ($Data.vehicleClass -eq "Sedan") {
            Write-Host "  ? Vehicle class transferred correctly" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Vehicle class mismatch" -ForegroundColor Red
            $checks += $false
        }
        
        return ($checks -notcontains $false)
    }

# ???????????????????????????????????????????????????????????????????
# PHASE 7: FSM VALIDATION (INVALID TRANSITIONS)
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 7: FSM VALIDATION (INVALID TRANSITIONS)                ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

# Create a second quote for FSM testing
$result = Test-Endpoint `
    -TestName "Create second quote for FSM tests" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes" `
    -Headers $chrisHeaders `
    -Body $quoteRequest `
    -ExpectedStatus 202 `
    -Description "Create another quote to test invalid state transitions" `
    -Validation {
        param($Data)
        $script:testData.SecondQuoteId = $Data.id
        Write-Host "  ? Second Quote ID: $($Data.id)" -ForegroundColor Cyan
        return $true
    }

# Try to respond without acknowledging (should fail)
$invalidResponse = @{
    estimatedPrice = 100.00
    estimatedPickupTime = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss")
    notes = "Test"
}

$result = Test-Endpoint `
    -TestName "FSM: Cannot respond to Submitted quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/respond" `
    -Headers $dianaHeaders `
    -Body $invalidResponse `
    -ExpectedStatus 400 `
    -Description "FSM should reject response to quote in 'Submitted' status (must be 'Acknowledged' first)" `
    -Validation {
        param($Data)
        if ($Data.error -like "*Acknowledged*") {
            Write-Host "  ? Error message mentions 'Acknowledged' requirement" -ForegroundColor Green
            return $true
        }
        Write-Host "  ??  Error message unclear" -ForegroundColor Yellow
        return $true  # Status 400 is still correct
    }

# Try to acknowledge already accepted quote (should fail)
$result = Test-Endpoint `
    -TestName "FSM: Cannot acknowledge Accepted quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.QuoteId)/acknowledge" `
    -Headers $dianaHeaders `
    -ExpectedStatus 400 `
    -Description "FSM should reject acknowledgment of quote in 'Accepted' status" `
    -Validation {
        param($Data)
        if ($Data.error -like "*Submitted*") {
            Write-Host "  ? Error message mentions 'Submitted' requirement" -ForegroundColor Green
            return $true
        }
        Write-Host "  ??  Error message unclear" -ForegroundColor Yellow
        return $true
    }

# Try to accept quote that hasn't been responded to (should fail)
$result = Test-Endpoint `
    -TestName "FSM: Cannot accept quote without response" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/accept" `
    -Headers $chrisHeaders `
    -ExpectedStatus 400 `
    -Description "FSM should reject acceptance of quote not in 'Responded' status" `
    -Validation {
        param($Data)
        if ($Data.error -like "*Responded*") {
            Write-Host "  ? Error message mentions 'Responded' requirement" -ForegroundColor Green
            return $true
        }
        Write-Host "  ??  Error message unclear" -ForegroundColor Yellow
        return $true
    }

# ???????????????????????????????????????????????????????????????????
# PHASE 8: SECURITY/RBAC VALIDATION
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 8: SECURITY & RBAC VALIDATION                          ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

# Passenger cannot acknowledge quote (StaffOnly)
$result = Test-Endpoint `
    -TestName "RBAC: Passenger cannot acknowledge quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/acknowledge" `
    -Headers $chrisHeaders `
    -ExpectedStatus 403 `
    -Description "StaffOnly policy should prevent passenger from acknowledging" `
    -Validation {
        param($Data)
        Write-Host "  ? Access correctly denied to non-staff user" -ForegroundColor Green
        return $true
    }

# Passenger cannot respond to quote (StaffOnly)
$result = Test-Endpoint `
    -TestName "RBAC: Passenger cannot respond to quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/respond" `
    -Headers $chrisHeaders `
    -Body $invalidResponse `
    -ExpectedStatus 403 `
    -Description "StaffOnly policy should prevent passenger from responding" `
    -Validation {
        param($Data)
        Write-Host "  ? Access correctly denied to non-staff user" -ForegroundColor Green
        return $true
    }

# Test ownership: Alice cannot accept Chris's quote
# First, acknowledge and respond to second quote so it's in Responded status
Write-Host "`n? Preparing second quote for ownership test..." -ForegroundColor Gray
try {
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/acknowledge" `
        -Method POST `
        -Headers $dianaHeaders `
        -UseBasicParsing | Out-Null
    
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/respond" `
        -Method POST `
        -Headers $dianaHeaders `
        -Body ($invalidResponse | ConvertTo-Json) `
        -ContentType "application/json" `
        -UseBasicParsing | Out-Null
    
    Write-Host "? Second quote now in 'Responded' status" -ForegroundColor Green
} catch {
    Write-Host "??  Could not prepare second quote: $($_.Exception.Message)" -ForegroundColor Yellow
}

$result = Test-Endpoint `
    -TestName "Ownership: User cannot accept other's quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/accept" `
    -Headers $aliceHeaders `
    -ExpectedStatus 403 `
    -Description "Alice (admin) should not be able to accept Chris's quote (ownership check)" `
    -Validation {
        param($Data)
        if ($Data.detail -like "*permission*") {
            Write-Host "  ? Error message mentions permission denial" -ForegroundColor Green
            return $true
        }
        Write-Host "  ??  Error message unclear" -ForegroundColor Yellow
        return $true
    }

# ???????????????????????????????????????????????????????????????????
# PHASE 9: QUOTE CANCELLATION
# ???????????????????????????????????????????????????????????????????

Write-Host "`n?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  PHASE 9: QUOTE CANCELLATION                                  ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan

$result = Test-Endpoint `
    -TestName "Passenger cancels quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.SecondQuoteId)/cancel" `
    -Headers $chrisHeaders `
    -ExpectedStatus 200 `
    -Description "Chris cancels the second quote (ownership validated)" `
    -Validation {
        param($Data)
        
        $checks = @()
        
        if ($Data.status -eq "Cancelled") {
            Write-Host "  ? Quote status changed to 'Cancelled'" -ForegroundColor Green
            $checks += $true
        } else {
            Write-Host "  ? Status should be 'Cancelled', got: $($Data.status)" -ForegroundColor Red
            $checks += $false
        }
        
        return ($checks -notcontains $false)
    }

# Try to cancel already accepted quote (should fail)
$result = Test-Endpoint `
    -TestName "FSM: Cannot cancel Accepted quote" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/$($testData.QuoteId)/cancel" `
    -Headers $chrisHeaders `
    -ExpectedStatus 400 `
    -Description "FSM should reject cancellation of quote in 'Accepted' status" `
    -Validation {
        param($Data)
        if ($Data.error -like "*Accepted*" -or $Data.error -like "*Cancelled*") {
            Write-Host "  ? Error message mentions invalid status for cancellation" -ForegroundColor Green
            return $true
        }
        Write-Host "  ??  Error message unclear" -ForegroundColor Yellow
        return $true
    }

# ???????????????????????????????????????????????????????????????????
# FINAL SUMMARY
# ???????????????????????????????????????????????????????????????????

Write-Host ""
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?                     TEST SUMMARY                              ?" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $testsPassed" -ForegroundColor Green
Write-Host "Failed: $testsFailed" -ForegroundColor Red
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host "?  ? ALL TESTS PASSED! PHASE ALPHA COMPLETE!                  ?" -ForegroundColor Green
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host ""
    Write-Host "Phase Alpha Implementation Summary:" -ForegroundColor Cyan
    Write-Host "  ? Quote submission working" -ForegroundColor Green
    Write-Host "  ? Dispatcher acknowledgment working" -ForegroundColor Green
    Write-Host "  ? Dispatcher response with price/ETA working" -ForegroundColor Green
    Write-Host "  ? Passenger acceptance working" -ForegroundColor Green
    Write-Host "  ? Booking creation from quote working" -ForegroundColor Green
    Write-Host "  ? SourceQuoteId linkage working" -ForegroundColor Green
    Write-Host "  ? FSM validation working" -ForegroundColor Green
    Write-Host "  ? RBAC policies working" -ForegroundColor Green
    Write-Host "  ? Ownership checks working" -ForegroundColor Green
    Write-Host "  ? Quote cancellation working" -ForegroundColor Green
    Write-Host ""
    Write-Host "Test Data Created:" -ForegroundColor Yellow
    Write-Host "  Quote ID: $($testData.QuoteId)" -ForegroundColor Gray
    Write-Host "  Booking ID: $($testData.BookingId)" -ForegroundColor Gray
    Write-Host "  Second Quote ID: $($testData.SecondQuoteId)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Ready for alpha testing! ??" -ForegroundColor Yellow
    Write-Host ""
    exit 0
} else {
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host "?  ? SOME TESTS FAILED                                         ?" -ForegroundColor Red
    Write-Host "?????????????????????????????????????????????????????????????????" -ForegroundColor Red
    Write-Host ""
    Write-Host "Review failed tests above and fix issues." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}
