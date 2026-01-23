# Bellwood AdminAPI Smoke Test Suite
# Phase 4: Comprehensive endpoint testing for alpha test readiness
# Last Updated: January 18, 2026

param(
    [string]$BaseUrl = "https://localhost:5206",
    [string]$AuthUrl = "https://localhost:5001"
)

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Bellwood AdminAPI Smoke Tests" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$ErrorCount = 0
$SuccessCount = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [string]$Token = $null,
        [object]$Body = $null,
        [int]$ExpectedStatus = 200
    )

    Write-Host "Testing: $Name..." -NoNewline

    try {
        $headers = @{}
        if ($Token) {
            $headers["Authorization"] = "Bearer $Token"
        }

        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $headers
            SkipCertificateCheck = $true
            ErrorAction = "Stop"
        }

        if ($Body) {
            $params["Body"] = ($Body | ConvertTo-Json)
            $params["ContentType"] = "application/json"
        }

        $response = Invoke-WebRequest @params

        if ($response.StatusCode -eq $ExpectedStatus) {
            Write-Host " ? PASS" -ForegroundColor Green
            $script:SuccessCount++
            return $true
        } else {
            Write-Host " ? FAIL (Status: $($response.StatusCode), Expected: $ExpectedStatus)" -ForegroundColor Red
            $script:ErrorCount++
            return $false
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq $ExpectedStatus) {
            Write-Host " ? PASS (Expected error: $statusCode)" -ForegroundColor Green
            $script:SuccessCount++
            return $true
        }
        Write-Host " ? FAIL (Error: $($_.Exception.Message))" -ForegroundColor Red
        $script:ErrorCount++
        return $false
    }
}

Write-Host "Phase 1: Authentication & Authorization" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

# Get admin token
Write-Host "Logging in as admin (alice)..." -NoNewline
try {
    $loginResponse = Invoke-RestMethod -Uri "$AuthUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body '{"username":"alice","password":"password"}' `
        -SkipCertificateCheck
    
    $AdminToken = $loginResponse.accessToken
    Write-Host " ? SUCCESS" -ForegroundColor Green
    $SuccessCount++
}
catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "Cannot continue without admin token" -ForegroundColor Red
    exit 1
}

# Get dispatcher token
Write-Host "Logging in as dispatcher (diana)..." -NoNewline
try {
    $loginResponse = Invoke-RestMethod -Uri "$AuthUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body '{"username":"diana","password":"password"}' `
        -SkipCertificateCheck
    
    $DispatcherToken = $loginResponse.accessToken
    Write-Host " ? SUCCESS" -ForegroundColor Green
    $SuccessCount++
}
catch {
    Write-Host " ? FAILED (Dispatcher user may not exist - this is OK for testing)" -ForegroundColor Yellow
}

# Get driver token
Write-Host "Logging in as driver (charlie)..." -NoNewline
try {
    $loginResponse = Invoke-RestMethod -Uri "$AuthUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body '{"username":"charlie","password":"password"}' `
        -SkipCertificateCheck
    
    $DriverToken = $loginResponse.accessToken
    Write-Host " ? SUCCESS" -ForegroundColor Green
    $SuccessCount++
}
catch {
    Write-Host " ? FAILED" -ForegroundColor Red
}

Write-Host ""
Write-Host "Phase 2: Health Check Endpoints" -ForegroundColor Yellow
Write-Host "--------------------------------" -ForegroundColor Yellow

Test-Endpoint "Basic health check" "GET" "$BaseUrl/health"
Test-Endpoint "Liveness probe" "GET" "$BaseUrl/health/live"
Test-Endpoint "Readiness probe" "GET" "$BaseUrl/health/ready"

Write-Host ""
Write-Host "Phase 3: Quote Endpoints" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow

Test-Endpoint "List quotes (admin)" "GET" "$BaseUrl/quotes/list?take=10" -Token $AdminToken
Test-Endpoint "List quotes (no auth)" "GET" "$BaseUrl/quotes/list?take=10" -ExpectedStatus 401

Write-Host ""
Write-Host "Phase 4: Booking Endpoints" -ForegroundColor Yellow
Write-Host "--------------------------" -ForegroundColor Yellow

Test-Endpoint "List bookings (admin)" "GET" "$BaseUrl/bookings/list?take=10" -Token $AdminToken
Test-Endpoint "List bookings (no auth)" "GET" "$BaseUrl/bookings/list?take=10" -ExpectedStatus 401

Write-Host ""
Write-Host "Phase 5: Driver Endpoints" -ForegroundColor Yellow
Write-Host "-------------------------" -ForegroundColor Yellow

Test-Endpoint "List drivers (admin)" "GET" "$BaseUrl/drivers/list" -Token $AdminToken
Test-Endpoint "List affiliates (admin)" "GET" "$BaseUrl/affiliates/list" -Token $AdminToken

Write-Host ""
Write-Host "Phase 6: Admin-Only Endpoints" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Yellow

Test-Endpoint "OAuth credentials (admin)" "GET" "$BaseUrl/api/admin/oauth" -Token $AdminToken
Test-Endpoint "OAuth credentials (no auth)" "GET" "$BaseUrl/api/admin/oauth" -ExpectedStatus 401

if ($DispatcherToken) {
    Test-Endpoint "OAuth credentials (dispatcher - should fail)" "GET" "$BaseUrl/api/admin/oauth" -Token $DispatcherToken -ExpectedStatus 403
}

Write-Host ""
Write-Host "Phase 7: Audit Log Endpoints (Phase 3A)" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

Test-Endpoint "Get audit logs (admin)" "GET" "$BaseUrl/api/admin/audit-logs?take=10" -Token $AdminToken
Test-Endpoint "Get audit logs (no auth)" "GET" "$BaseUrl/api/admin/audit-logs" -ExpectedStatus 401

Write-Host ""
Write-Host "Phase 8: Data Retention Endpoints (Phase 3C)" -ForegroundColor Yellow
Write-Host "---------------------------------------------" -ForegroundColor Yellow

Test-Endpoint "Get retention policy (admin)" "GET" "$BaseUrl/api/admin/data-retention/policy" -Token $AdminToken
Test-Endpoint "Test data protection (admin)" "POST" "$BaseUrl/api/admin/data-protection/test" -Token $AdminToken

Write-Host ""
Write-Host "Phase 9: LimoAnywhere Endpoints (Phase 4 Stub)" -ForegroundColor Yellow
Write-Host "-----------------------------------------------" -ForegroundColor Yellow

Test-Endpoint "Test LimoAnywhere connection (admin)" "GET" "$BaseUrl/api/admin/limoanywhere/test-connection" -Token $AdminToken

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Passed: $SuccessCount" -ForegroundColor Green
Write-Host "Failed: $ErrorCount" -ForegroundColor Red
Write-Host ""

if ($ErrorCount -eq 0) {
    Write-Host "? ALL TESTS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "? SOME TESTS FAILED" -ForegroundColor Red
    exit 1
}
