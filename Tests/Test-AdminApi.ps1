# AdminAPI Test Orchestrator
# PowerShell 5.1 Compatible
# Runs comprehensive tests for all AdminAPI endpoints

#Requires -Version 5.1

param(
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001",
    [string]$AdminUsername = "alice",
    [string]$AdminPassword = "password",
    [switch]$SkipCleanup,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$Global:TestResults = @()
$Global:AdminToken = $null
$Global:TestData = @{
    CreatedUserIds = @()
    CreatedQuoteIds = @()
    CreatedBookingIds = @()
    CreatedAffiliateIds = @()
    CreatedDriverIds = @()
}

# ===================================================================
# UTILITY FUNCTIONS
# ===================================================================

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = ""
    )
    
    $result = @{
        TestName = $TestName
        Passed = $Passed
        Message = $Message
        Timestamp = Get-Date
    }
    
    $Global:TestResults += $result
    
    if ($Passed) {
        Write-Host "? PASS: $TestName" -ForegroundColor Green
        if ($Message) { Write-Host "  ? $Message" -ForegroundColor Gray }
    } else {
        Write-Host "? FAIL: $TestName" -ForegroundColor Red
        if ($Message) { Write-Host "  ? $Message" -ForegroundColor Yellow }
    }
}

function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null,
        [string]$Token = $null,
        [switch]$ExpectFailure
    )
    
    $headers = @{
        "Content-Type" = "application/json"
    }
    
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }
    
    try {
        $params = @{
            Method = $Method
            Uri = $Uri
            Headers = $headers
            UseBasicParsing = $true
        }
        
        # Skip SSL validation for localhost testing
        if ($Uri -match "https://localhost") {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-WebRequest @params
        
        $result = @{
            Success = $true
            StatusCode = $response.StatusCode
            Content = $null
        }
        
        if ($response.Content) {
            try {
                $result.Content = $response.Content | ConvertFrom-Json
            } catch {
                $result.Content = $response.Content
            }
        }
        
        return $result
        
    } catch {
        if ($ExpectFailure) {
            return @{
                Success = $false
                StatusCode = $_.Exception.Response.StatusCode.value__
                Content = $_.ErrorDetails.Message
            }
        }
        
        Write-Host "API Request Failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
        throw
    }
}

# ===================================================================
# TEST DATA CLEANUP
# ===================================================================

function Clear-TestData {
    Write-TestHeader "CLEANING UP TEST DATA"
    
    if ($SkipCleanup) {
        Write-Host "? Cleanup skipped (use -SkipCleanup flag)" -ForegroundColor Yellow
        return
    }
    
    Write-Host "Removing App_Data directory to clear all test data..." -ForegroundColor Yellow
    
    $appDataPath = Join-Path $PSScriptRoot "..\App_Data"
    
    if (Test-Path $appDataPath) {
        try {
            Remove-Item -Path $appDataPath -Recurse -Force -ErrorAction Stop
            Write-Host "? Test data cleared successfully" -ForegroundColor Green
        } catch {
            Write-Host "? Warning: Could not remove App_Data: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "  This may be because the API is running and has the files locked." -ForegroundColor Yellow
            Write-Host "  Stop the API, run cleanup again, then restart the API." -ForegroundColor Yellow
        }
    } else {
        Write-Host "? No existing test data found" -ForegroundColor Green
    }
    
    # Reset test data tracking
    $Global:TestData = @{
        CreatedUserIds = @()
        CreatedQuoteIds = @()
        CreatedBookingIds = @()
        CreatedAffiliateIds = @()
        CreatedDriverIds = @()
    }
}

# ===================================================================
# AUTHENTICATION
# ===================================================================

function Get-AdminToken {
    Write-TestHeader "AUTHENTICATION"
    
    Write-Host "Authenticating as admin user '$AdminUsername'..." -ForegroundColor Cyan
    
    $loginBody = @{
        username = $AdminUsername
        password = $AdminPassword
    }
    
    $result = Invoke-ApiRequest -Method POST -Uri "$AuthServerUrl/api/auth/login" -Body $loginBody
    
    if ($result.Success -and $result.Content.accessToken) {
        $Global:AdminToken = $result.Content.accessToken
        Write-TestResult -TestName "Admin Authentication" -Passed $true -Message "Token acquired"
        return $true
    } else {
        Write-TestResult -TestName "Admin Authentication" -Passed $false -Message "Failed to get token"
        return $false
    }
}

# ===================================================================
# HEALTH CHECK TESTS
# ===================================================================

function Test-HealthEndpoints {
    Write-TestHeader "HEALTH CHECK TESTS"
    
    # Test basic health endpoint
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/health"
    Write-TestResult -TestName "GET /health" -Passed ($result.Success -and $result.Content.status -eq "ok")
    
    # Test live health check
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/health/live"
    Write-TestResult -TestName "GET /health/live" -Passed ($result.Success)
    
    # Test ready health check
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/health/ready"
    Write-TestResult -TestName "GET /health/ready" -Passed ($result.Success)
}

# ===================================================================
# USER MANAGEMENT TESTS
# ===================================================================

function Test-UserManagement {
    Write-TestHeader "USER MANAGEMENT TESTS"
    
    # Test: List users (should be empty initially)
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/users/list?take=50&skip=0" -Token $Global:AdminToken
    Write-TestResult -TestName "GET /users/list" -Passed $result.Success
    
    # Test: Create dispatcher user
    $createUserBody = @{
        email = "test.dispatcher@example.com"
        firstName = "Test"
        lastName = "Dispatcher"
        tempPassword = "TempPass123!"
        roles = @("Dispatcher")
    }
    
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/users" -Body $createUserBody -Token $Global:AdminToken
    $passed = $result.Success -and $result.StatusCode -eq 201
    Write-TestResult -TestName "POST /users (Create Dispatcher)" -Passed $passed
    
    if ($passed -and $result.Content.userId) {
        $userId = $result.Content.userId
        $Global:TestData.CreatedUserIds += $userId
        
        # Test: Update user roles
        $updateRolesBody = @{
            roles = @("Admin", "Dispatcher")
        }
        
        $result = Invoke-ApiRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/roles" -Body $updateRolesBody -Token $Global:AdminToken
        Write-TestResult -TestName "PUT /users/{userId}/roles" -Passed $result.Success
        
        # Test: Disable user
        $disableBody = @{
            isDisabled = $true
        }
        
        $result = Invoke-ApiRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/disable" -Body $disableBody -Token $Global:AdminToken
        Write-TestResult -TestName "PUT /users/{userId}/disable (Disable)" -Passed $result.Success
        
        # Test: Enable user
        $enableBody = @{
            isDisabled = $false
        }
        
        $result = Invoke-ApiRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/disable" -Body $enableBody -Token $Global:AdminToken
        Write-TestResult -TestName "PUT /users/{userId}/disable (Enable)" -Passed $result.Success
    }
    
    # Test: Create user with invalid password (should fail)
    $invalidUserBody = @{
        email = "invalid@example.com"
        tempPassword = "short"
        roles = @("Dispatcher")
    }
    
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/users" -Body $invalidUserBody -Token $Global:AdminToken -ExpectFailure
    Write-TestResult -TestName "POST /users (Invalid Password - Should Fail)" -Passed ($result.StatusCode -eq 400)
    
    # Test: Create user with invalid role (should fail)
    $invalidRoleBody = @{
        email = "invalidrole@example.com"
        tempPassword = "TempPass123!"
        roles = @("InvalidRole")
    }
    
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/users" -Body $invalidRoleBody -Token $Global:AdminToken -ExpectFailure
    Write-TestResult -TestName "POST /users (Invalid Role - Should Fail)" -Passed ($result.StatusCode -eq 400)
}

# ===================================================================
# QUOTE TESTS
# ===================================================================

function Test-Quotes {
    Write-TestHeader "QUOTE TESTS"
    
    # Test: Seed quotes
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/quotes/seed" -Token $Global:AdminToken
    Write-TestResult -TestName "POST /quotes/seed" -Passed ($result.Success -and $result.Content.added -gt 0)
    
    # Test: List quotes
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/quotes/list?take=50" -Token $Global:AdminToken
    $passed = $result.Success
    Write-TestResult -TestName "GET /quotes/list" -Passed $passed
    
    if ($passed -and $result.Content.Count -gt 0) {
        $quoteId = $result.Content[0].id
        $Global:TestData.CreatedQuoteIds += $quoteId
        
        # Test: Get quote details
        $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/quotes/$quoteId" -Token $Global:AdminToken
        Write-TestResult -TestName "GET /quotes/{id}" -Passed $result.Success
        
        # Test: Acknowledge quote (if in Pending status)
        if ($result.Content.status -eq "Pending") {
            $ackResult = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/quotes/$quoteId/acknowledge" -Token $Global:AdminToken
            Write-TestResult -TestName "POST /quotes/{id}/acknowledge" -Passed $ackResult.Success
            
            # Test: Respond to quote
            if ($ackResult.Success) {
                $respondBody = @{
                    estimatedPrice = 150.00
                    estimatedPickupTime = (Get-Date).AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
                    notes = "Test response from automated tests"
                }
                
                $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/quotes/$quoteId/respond" -Body $respondBody -Token $Global:AdminToken
                Write-TestResult -TestName "POST /quotes/{id}/respond" -Passed $result.Success
            }
        }
    }
}

# ===================================================================
# BOOKING TESTS
# ===================================================================

function Test-Bookings {
    Write-TestHeader "BOOKING TESTS"
    
    # Test: Seed bookings
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/bookings/seed" -Token $Global:AdminToken
    Write-TestResult -TestName "POST /bookings/seed" -Passed ($result.Success -and $result.Content.added -gt 0)
    
    # Test: List bookings
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/bookings/list?take=50" -Token $Global:AdminToken
    $passed = $result.Success
    Write-TestResult -TestName "GET /bookings/list" -Passed $passed
    
    if ($passed -and $result.Content.Count -gt 0) {
        $bookingId = $result.Content[0].id
        $Global:TestData.CreatedBookingIds += $bookingId
        
        # Test: Get booking details
        $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/bookings/$bookingId" -Token $Global:AdminToken
        Write-TestResult -TestName "GET /bookings/{id}" -Passed $result.Success
    }
}

# ===================================================================
# AFFILIATE & DRIVER TESTS
# ===================================================================

function Test-AffiliatesAndDrivers {
    Write-TestHeader "AFFILIATE & DRIVER TESTS"
    
    # Test: Seed affiliates and drivers
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/dev/seed-affiliates" -Token $Global:AdminToken
    Write-TestResult -TestName "POST /dev/seed-affiliates" -Passed ($result.Success -and $result.Content.affiliatesAdded -gt 0)
    
    # Test: List affiliates
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/affiliates/list" -Token $Global:AdminToken
    $passed = $result.Success
    Write-TestResult -TestName "GET /affiliates/list" -Passed $passed
    
    if ($passed -and $result.Content.Count -gt 0) {
        $affiliateId = $result.Content[0].id
        $Global:TestData.CreatedAffiliateIds += $affiliateId
        
        # Test: Get affiliate details
        $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/affiliates/$affiliateId" -Token $Global:AdminToken
        Write-TestResult -TestName "GET /affiliates/{id}" -Passed $result.Success
        
        # Test: Create driver under affiliate
        $createDriverBody = @{
            name = "Test Driver"
            phone = "555-TEST-001"
            userUid = "test-driver-001"
        }
        
        $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/affiliates/$affiliateId/drivers" -Body $createDriverBody -Token $Global:AdminToken
        $passed = $result.Success -and $result.StatusCode -eq 201
        Write-TestResult -TestName "POST /affiliates/{affiliateId}/drivers" -Passed $passed
        
        if ($passed -and $result.Content.id) {
            $driverId = $result.Content.id
            $Global:TestData.CreatedDriverIds += $driverId
            
            # Test: Get driver by ID
            $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/drivers/$driverId" -Token $Global:AdminToken
            Write-TestResult -TestName "GET /drivers/{id}" -Passed $result.Success
            
            # Test: Get driver by UserUid
            $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/drivers/by-uid/test-driver-001" -Token $Global:AdminToken
            Write-TestResult -TestName "GET /drivers/by-uid/{userUid}" -Passed $result.Success
        }
    }
    
    # Test: List all drivers
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/drivers/list" -Token $Global:AdminToken
    Write-TestResult -TestName "GET /drivers/list" -Passed $result.Success
}

# ===================================================================
# OAUTH CREDENTIAL TESTS
# ===================================================================

function Test-OAuthCredentials {
    Write-TestHeader "OAUTH CREDENTIAL TESTS"
    
    # Test: Get OAuth credentials (should be unconfigured initially)
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/api/admin/oauth" -Token $Global:AdminToken
    Write-TestResult -TestName "GET /api/admin/oauth" -Passed $result.Success
    
    # Test: Update OAuth credentials
    $updateOAuthBody = @{
        clientId = "test-client-id"
        clientSecret = "test-client-secret-1234567890"
        description = "Test OAuth credentials"
    }
    
    $result = Invoke-ApiRequest -Method PUT -Uri "$AdminApiUrl/api/admin/oauth" -Body $updateOAuthBody -Token $Global:AdminToken
    Write-TestResult -TestName "PUT /api/admin/oauth" -Passed $result.Success
    
    # Test: Get OAuth credentials (should show masked secret)
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/api/admin/oauth" -Token $Global:AdminToken
    $passed = $result.Success -and $result.Content.credentials.clientSecretMasked -match "\.\.\."
    Write-TestResult -TestName "GET /api/admin/oauth (Verify Masked Secret)" -Passed $passed
}

# ===================================================================
# AUDIT LOG TESTS
# ===================================================================

function Test-AuditLogs {
    Write-TestHeader "AUDIT LOG TESTS"
    
    # Test: Get audit logs
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs?take=50&skip=0" -Token $Global:AdminToken
    Write-TestResult -TestName "GET /api/admin/audit-logs" -Passed $result.Success
    
    if ($result.Success -and $result.Content.logs.Count -gt 0) {
        $logId = $result.Content.logs[0].id
        
        # Test: Get specific audit log
        $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs/$logId" -Token $Global:AdminToken
        Write-TestResult -TestName "GET /api/admin/audit-logs/{id}" -Passed $result.Success
    }
}

# ===================================================================
# DATA RETENTION TESTS
# ===================================================================

function Test-DataRetention {
    Write-TestHeader "DATA RETENTION TESTS"
    
    # Test: Get data retention policy
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/api/admin/data-retention/policy" -Token $Global:AdminToken
    Write-TestResult -TestName "GET /api/admin/data-retention/policy" -Passed $result.Success
    
    # Test: Data protection test
    $result = Invoke-ApiRequest -Method POST -Uri "$AdminApiUrl/api/admin/data-protection/test" -Token $Global:AdminToken
    $passed = $result.Success -and $result.Content.success -eq $true
    Write-TestResult -TestName "POST /api/admin/data-protection/test" -Passed $passed
}

# ===================================================================
# AUTHORIZATION TESTS
# ===================================================================

function Test-Authorization {
    Write-TestHeader "AUTHORIZATION TESTS"
    
    # Test: Access admin endpoint without token (should fail)
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/users/list" -ExpectFailure
    Write-TestResult -TestName "Access /users/list without token (Should Fail)" -Passed ($result.StatusCode -eq 401)
    
    # Test: Access admin endpoint with invalid token (should fail)
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/users/list" -Token "invalid-token" -ExpectFailure
    Write-TestResult -TestName "Access /users/list with invalid token (Should Fail)" -Passed ($result.StatusCode -eq 401)
}

# ===================================================================
# GENERATE TEST REPORT
# ===================================================================

function Show-TestReport {
    Write-TestHeader "TEST SUMMARY"
    
    $totalTests = $Global:TestResults.Count
    $passedTests = ($Global:TestResults | Where-Object { $_.Passed }).Count
    $failedTests = $totalTests - $passedTests
    $passRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 2) } else { 0 }
    
    Write-Host "Total Tests:  $totalTests" -ForegroundColor Cyan
    Write-Host "Passed:       $passedTests" -ForegroundColor Green
    Write-Host "Failed:       $failedTests" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
    Write-Host "Pass Rate:    $passRate%" -ForegroundColor $(if ($passRate -eq 100) { "Green" } elseif ($passRate -ge 80) { "Yellow" } else { "Red" })
    
    if ($failedTests -gt 0) {
        Write-Host "`nFailed Tests:" -ForegroundColor Red
        $Global:TestResults | Where-Object { -not $_.Passed } | ForEach-Object {
            Write-Host "  ? $($_.TestName)" -ForegroundColor Red
            if ($_.Message) {
                Write-Host "    ? $($_.Message)" -ForegroundColor Yellow
            }
        }
    }
    
    # Export results to JSON
    $reportPath = Join-Path $PSScriptRoot "test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $Global:TestResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "`nTest results exported to: $reportPath" -ForegroundColor Cyan
    
    # Return exit code based on test results
    if ($failedTests -gt 0) {
        exit 1
    } else {
        exit 0
    }
}

# ===================================================================
# MAIN EXECUTION
# ===================================================================

try {
    Write-Host @"
    
????????????????????????????????????????????????????????????????
?                                                              ?
?          AdminAPI Comprehensive Test Suite                   ?
?          PowerShell 5.1 Compatible                           ?
?                                                              ?
????????????????????????????????????????????????????????????????

"@ -ForegroundColor Cyan

    Write-Host "Target AdminAPI:  $AdminApiUrl" -ForegroundColor Gray
    Write-Host "Target AuthServer: $AuthServerUrl" -ForegroundColor Gray
    Write-Host "Admin User:        $AdminUsername" -ForegroundColor Gray
    Write-Host ""
    
    # Step 1: Clean up existing test data
    Clear-TestData
    
    # Step 2: Authenticate
    if (-not (Get-AdminToken)) {
        throw "Failed to authenticate. Cannot proceed with tests."
    }
    
    # Step 3: Run all test suites
    Test-HealthEndpoints
    Test-UserManagement
    Test-Quotes
    Test-Bookings
    Test-AffiliatesAndDrivers
    Test-OAuthCredentials
    Test-AuditLogs
    Test-DataRetention
    Test-Authorization
    
    # Step 4: Generate report
    Show-TestReport
    
} catch {
    Write-Host "`n? CRITICAL ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Yellow
    exit 1
}
