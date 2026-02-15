#Requires -Version 5.1
<#
.SYNOPSIS
    Test audit log management endpoints (stats and clear).

.DESCRIPTION
    Comprehensive test suite for audit log statistics and clearing functionality.
    Tests authorization, validation, and data consistency.

.PARAMETER AdminApiUrl
    The AdminAPI base URL (default: https://localhost:5206)

.PARAMETER AuthServerUrl
    The AuthServer base URL (default: https://localhost:5001)

.PARAMETER AdminUsername
    Admin username for authentication (default: alice)

.PARAMETER AdminPassword
    Admin password (default: password)

.EXAMPLE
    .\Test-AuditLogManagement.ps1
    
.EXAMPLE
    .\Test-AuditLogManagement.ps1 -AdminApiUrl "https://api.bellwood.com"
#>

param(
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001",
    [string]$AdminUsername = "alice",
    [string]$AdminPassword = "password"
)

$ErrorActionPreference = "Stop"
$testsPassed = 0
$testsFailed = 0

function Write-TestResult {
    param([string]$TestName, [bool]$Passed, [string]$Message = "")
    if ($Passed) {
        Write-Host "  ? PASS: $TestName" -ForegroundColor Green
        if ($Message) { Write-Host "     $Message" -ForegroundColor Gray }
        $script:testsPassed++
    } else {
        Write-Host "  ? FAIL: $TestName" -ForegroundColor Red
        if ($Message) { Write-Host "     $Message" -ForegroundColor Yellow }
        $script:testsFailed++
    }
}

Write-Host @"

????????????????????????????????????????????????????????????????
?                                                              ?
?          Audit Log Management Test Suite                     ?
?          PowerShell 5.1 Compatible                          ?
?                                                              ?
????????????????????????????????????????????????????????????????

"@ -ForegroundColor Cyan

Write-Host "Target AdminAPI:  $AdminApiUrl" -ForegroundColor Gray
Write-Host "Target AuthServer: $AuthServerUrl" -ForegroundColor Gray
Write-Host "Admin User:        $AdminUsername" -ForegroundColor Gray
Write-Host ""

# ===================================================================
# STEP 1: AUTHENTICATION
# ===================================================================

Write-Host "`n=== STEP 1: AUTHENTICATION ===" -ForegroundColor Cyan

try {
    $loginBody = @{ username = $AdminUsername; password = $AdminPassword } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod -Method POST -Uri "$AuthServerUrl/api/auth/login" `
        -Body $loginBody -ContentType "application/json" -UseBasicParsing
    
    $token = $loginResponse.accessToken
    $headers = @{ Authorization = "Bearer $token" }
    
    Write-TestResult "Admin Authentication" $true "Token acquired"
} catch {
    Write-TestResult "Admin Authentication" $false $_.Exception.Message
    exit 1
}

# ===================================================================
# STEP 2: GET STATS (BEFORE SEEDING)
# ===================================================================

Write-Host "`n=== STEP 2: GET STATS (INITIAL STATE) ===" -ForegroundColor Cyan

try {
    $statsInitial = Invoke-RestMethod -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs/stats" `
        -Headers $headers -UseBasicParsing
    
    Write-Host "  Initial Count: $($statsInitial.count)" -ForegroundColor White
    Write-Host "  Oldest: $($statsInitial.oldestUtc)" -ForegroundColor White
    Write-Host "  Newest: $($statsInitial.newestUtc)" -ForegroundColor White
    
    Write-TestResult "GET /api/admin/audit-logs/stats" $true "Stats retrieved"
} catch {
    Write-TestResult "GET /api/admin/audit-logs/stats" $false $_.Exception.Message
}

# ===================================================================
# STEP 3: SEED DATA (CREATE AUDIT LOGS)
# ===================================================================

Write-Host "`n=== STEP 3: SEED DATA (CREATE AUDIT LOGS) ===" -ForegroundColor Cyan

try {
    # Seed quotes
    $quoteResponse = Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/quotes/seed" `
        -Headers $headers -UseBasicParsing
    Write-Host "  ? Seeded $($quoteResponse.added) quotes" -ForegroundColor Gray
    
    # Seed bookings
    $bookingResponse = Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/bookings/seed" `
        -Headers $headers -UseBasicParsing
    Write-Host "  ? Seeded $($bookingResponse.added) bookings" -ForegroundColor Gray
    
    Write-TestResult "Data Seeding" $true "Quotes and bookings seeded"
} catch {
    Write-TestResult "Data Seeding" $false $_.Exception.Message
}

# ===================================================================
# STEP 4: GET STATS (AFTER SEEDING)
# ===================================================================

Write-Host "`n=== STEP 4: GET STATS (AFTER SEEDING) ===" -ForegroundColor Cyan

try {
    $statsAfterSeed = Invoke-RestMethod -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs/stats" `
        -Headers $headers -UseBasicParsing
    
    Write-Host "  Count: $($statsAfterSeed.count)" -ForegroundColor White
    Write-Host "  Oldest: $($statsAfterSeed.oldestUtc)" -ForegroundColor White
    Write-Host "  Newest: $($statsAfterSeed.newestUtc)" -ForegroundColor White
    
    if ($statsAfterSeed.count -gt $statsInitial.count) {
        Write-TestResult "Stats Count Increased" $true "Count increased from $($statsInitial.count) to $($statsAfterSeed.count)"
    } else {
        Write-TestResult "Stats Count Increased" $false "Expected count to increase"
    }
} catch {
    Write-TestResult "Stats After Seeding" $false $_.Exception.Message
}

# ===================================================================
# STEP 5: CLEAR WITH INVALID CONFIRMATION (SHOULD FAIL)
# ===================================================================

Write-Host "`n=== STEP 5: CLEAR WITH INVALID CONFIRMATION ===" -ForegroundColor Cyan

try {
    $invalidBody = @{ confirm = "clear" } | ConvertTo-Json  # Wrong case
    Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/api/admin/audit-logs/clear" `
        -Headers $headers -Body $invalidBody -ContentType "application/json" -UseBasicParsing
    
    Write-TestResult "Invalid Confirmation Rejected" $false "Should have returned 400 Bad Request"
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-TestResult "Invalid Confirmation Rejected" $true "Correctly returned 400 Bad Request"
    } else {
        Write-TestResult "Invalid Confirmation Rejected" $false "Unexpected status code: $statusCode"
    }
}

# ===================================================================
# STEP 6: CLEAR WITH EMPTY CONFIRMATION (SHOULD FAIL)
# ===================================================================

Write-Host "`n=== STEP 6: CLEAR WITH EMPTY CONFIRMATION ===" -ForegroundColor Cyan

try {
    $emptyBody = @{ confirm = "" } | ConvertTo-Json
    Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/api/admin/audit-logs/clear" `
        -Headers $headers -Body $emptyBody -ContentType "application/json" -UseBasicParsing
    
    Write-TestResult "Empty Confirmation Rejected" $false "Should have returned 400 Bad Request"
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-TestResult "Empty Confirmation Rejected" $true "Correctly returned 400 Bad Request"
    } else {
        Write-TestResult "Empty Confirmation Rejected" $false "Unexpected status code: $statusCode"
    }
}

# ===================================================================
# STEP 7: CLEAR WITH VALID CONFIRMATION (SHOULD SUCCEED)
# ===================================================================

Write-Host "`n=== STEP 7: CLEAR ALL AUDIT LOGS (VALID CONFIRMATION) ===" -ForegroundColor Cyan

try {
    $validBody = @{ confirm = "CLEAR" } | ConvertTo-Json
    $clearResponse = Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/api/admin/audit-logs/clear" `
        -Headers $headers -Body $validBody -ContentType "application/json" -UseBasicParsing
    
    Write-Host "  Deleted Count: $($clearResponse.deletedCount)" -ForegroundColor White
    Write-Host "  Cleared At: $($clearResponse.clearedAtUtc)" -ForegroundColor White
    Write-Host "  Cleared By: $($clearResponse.clearedByUsername) ($($clearResponse.clearedByUserId))" -ForegroundColor White
    
    if ($clearResponse.deletedCount -gt 0) {
        Write-TestResult "Clear All Audit Logs" $true "Deleted $($clearResponse.deletedCount) logs"
    } else {
        Write-TestResult "Clear All Audit Logs" $false "Expected to delete at least 1 log"
    }
} catch {
    Write-TestResult "Clear All Audit Logs" $false $_.Exception.Message
}

# ===================================================================
# STEP 8: VERIFY STATS AFTER CLEAR (SHOULD SHOW 1 ENTRY)
# ===================================================================

Write-Host "`n=== STEP 8: VERIFY STATS AFTER CLEAR ===" -ForegroundColor Cyan

try {
    $statsAfterClear = Invoke-RestMethod -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs/stats" `
        -Headers $headers -UseBasicParsing
    
    Write-Host "  Final Count: $($statsAfterClear.count)" -ForegroundColor White
    Write-Host "  Oldest: $($statsAfterClear.oldestUtc)" -ForegroundColor White
    Write-Host "  Newest: $($statsAfterClear.newestUtc)" -ForegroundColor White
    
    # Should be exactly 1 (the clear action audit entry)
    if ($statsAfterClear.count -eq 1) {
        Write-TestResult "Stats After Clear" $true "Exactly 1 audit log remains (the clear action)"
    } else {
        Write-TestResult "Stats After Clear" $false "Expected exactly 1 log, found $($statsAfterClear.count)"
    }
} catch {
    Write-TestResult "Stats After Clear" $false $_.Exception.Message
}

# ===================================================================
# STEP 9: GET AUDIT LOGS (VERIFY CLEAR ACTION WAS AUDITED)
# ===================================================================

Write-Host "`n=== STEP 9: VERIFY CLEAR ACTION WAS AUDITED ===" -ForegroundColor Cyan

try {
    $logsResponse = Invoke-RestMethod -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs?take=10" `
        -Headers $headers -UseBasicParsing
    
    Write-Host "  Returned Logs: $($logsResponse.logs.Count)" -ForegroundColor White
    
    if ($logsResponse.logs.Count -gt 0) {
        $clearLog = $logsResponse.logs | Where-Object { $_.Action -eq "AuditLog.Cleared" } | Select-Object -First 1
        
        if ($clearLog) {
            Write-Host "  ? Clear Action: $($clearLog.Action)" -ForegroundColor Gray
            Write-Host "  ? Cleared By: $($clearLog.Username)" -ForegroundColor Gray
            Write-Host "  ? Timestamp: $($clearLog.Timestamp)" -ForegroundColor Gray
            
            Write-TestResult "Clear Action Audited" $true "AuditLog.Cleared entry found"
        } else {
            Write-TestResult "Clear Action Audited" $false "No AuditLog.Cleared entry found"
        }
    } else {
        Write-TestResult "Clear Action Audited" $false "No audit logs found"
    }
} catch {
    Write-TestResult "Clear Action Audited" $false $_.Exception.Message
}

# ===================================================================
# SUMMARY
# ===================================================================

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$totalTests = $testsPassed + $testsFailed
$passRate = if ($totalTests -gt 0) { [math]::Round(($testsPassed / $totalTests) * 100, 2) } else { 0 }

Write-Host "Total Tests:  $totalTests" -ForegroundColor White
Write-Host "Passed:       $testsPassed" -ForegroundColor Green
Write-Host "Failed:       $testsFailed" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host "Pass Rate:    $passRate%" -ForegroundColor $(if ($passRate -eq 100) { "Green" } elseif ($passRate -ge 80) { "Yellow" } else { "Red" })

if ($testsFailed -eq 0) {
    Write-Host "`n? ALL TESTS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n? SOME TESTS FAILED!" -ForegroundColor Red
    exit 1
}

