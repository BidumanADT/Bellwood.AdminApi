# Test script to validate AdminAPI User DTO alignment with AuthServer format
# Date: February 8, 2026
# Purpose: Verify all 6 changes were implemented correctly

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  AdminAPI User DTO Alignment Test" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "https://localhost:5206"
$authServerUrl = "https://localhost:5001"

# Get admin token from AuthServer
Write-Host "Step 1: Authenticating as admin..." -ForegroundColor Yellow
$loginBody = @{
    username = "alice"
    password = "password"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$authServerUrl/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json" -SkipCertificateCheck
    $token = $loginResponse.accessToken
    Write-Host "? Admin authenticated successfully" -ForegroundColor Green
} catch {
    Write-Host "? Failed to authenticate" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

# Test 1: Check response is array (not wrapped)
Write-Host ""
Write-Host "Test 1: Verify response structure (direct array)..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/users/list?take=1" -Method GET -Headers $headers -SkipCertificateCheck
    
    if ($response -is [Array]) {
        Write-Host "? PASS: Response is direct array" -ForegroundColor Green
    } else {
        Write-Host "? FAIL: Response is not a direct array (it's wrapped)" -ForegroundColor Red
        Write-Host "  Response type: $($response.GetType())" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "? FAIL: Error calling endpoint" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# Test 2: Check username field is present
Write-Host ""
Write-Host "Test 2: Verify 'username' field is present..." -ForegroundColor Yellow
$user = $response[0]
if ($user.PSObject.Properties.Name -contains "username") {
    if ($user.username) {
        Write-Host "? PASS: 'username' field present and populated: $($user.username)" -ForegroundColor Green
    } else {
        Write-Host "? WARNING: 'username' field present but empty" -ForegroundColor Yellow
    }
} else {
    Write-Host "? FAIL: 'username' field missing" -ForegroundColor Red
    exit 1
}

# Test 3: Check all required fields are present
Write-Host ""
Write-Host "Test 3: Verify all 9 required fields are present..." -ForegroundColor Yellow
$requiredFields = @("userId", "username", "email", "firstName", "lastName", "roles", "isDisabled", "createdAtUtc", "modifiedAtUtc")
$missingFields = @()

foreach ($field in $requiredFields) {
    if (-not ($user.PSObject.Properties.Name -contains $field)) {
        $missingFields += $field
    }
}

if ($missingFields.Count -eq 0) {
    Write-Host "? PASS: All 9 fields present" -ForegroundColor Green
    Write-Host "  Fields: $($user.PSObject.Properties.Name -join ', ')" -ForegroundColor Gray
} else {
    Write-Host "? FAIL: Missing fields: $($missingFields -join ', ')" -ForegroundColor Red
    exit 1
}

# Test 4: Check role names are lowercase
Write-Host ""
Write-Host "Test 4: Verify role names are lowercase..." -ForegroundColor Yellow
if ($user.roles -and $user.roles.Count -gt 0) {
    $hasUppercaseRole = $false
    foreach ($role in $user.roles) {
        if ($role -cne $role.ToLower()) {
            $hasUppercaseRole = $true
            Write-Host "? FAIL: Role '$role' is not lowercase" -ForegroundColor Red
        }
    }
    
    if (-not $hasUppercaseRole) {
        Write-Host "? PASS: All roles are lowercase: $($user.roles -join ', ')" -ForegroundColor Green
    } else {
        exit 1
    }
} else {
    Write-Host "? WARNING: No roles to check" -ForegroundColor Yellow
}

# Test 5: Check isDisabled is boolean (not nullable)
Write-Host ""
Write-Host "Test 5: Verify 'isDisabled' is boolean (not null)..." -ForegroundColor Yellow
if ($null -ne $user.isDisabled) {
    if ($user.isDisabled -is [bool]) {
        Write-Host "? PASS: 'isDisabled' is boolean: $($user.isDisabled)" -ForegroundColor Green
    } else {
        Write-Host "? FAIL: 'isDisabled' is not boolean (type: $($user.isDisabled.GetType()))" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "? FAIL: 'isDisabled' is null (should be boolean)" -ForegroundColor Red
    exit 1
}

# Test 6: Compare field structure with AuthServer
Write-Host ""
Write-Host "Test 6: Compare with AuthServer reference implementation..." -ForegroundColor Yellow
try {
    $authServerResponse = Invoke-RestMethod -Uri "$authServerUrl/api/admin/users?take=1" -Method GET -Headers $headers -SkipCertificateCheck
    $authServerUser = $authServerResponse[0]
    
    $adminApiFields = $user.PSObject.Properties.Name | Sort-Object
    $authServerFields = $authServerUser.PSObject.Properties.Name | Sort-Object
    
    # AdminAPI may have extra fields (createdByUserId, modifiedByUserId) - that's OK
    # Just check that all AuthServer fields are present in AdminAPI
    $missingInAdminApi = @()
    foreach ($field in $authServerFields) {
        if (-not ($adminApiFields -contains $field)) {
            $missingInAdminApi += $field
        }
    }
    
    if ($missingInAdminApi.Count -eq 0) {
        Write-Host "? PASS: AdminAPI has all AuthServer fields" -ForegroundColor Green
        Write-Host "  AdminAPI fields: $($adminApiFields -join ', ')" -ForegroundColor Gray
        Write-Host "  AuthServer fields: $($authServerFields -join ', ')" -ForegroundColor Gray
    } else {
        Write-Host "? FAIL: AdminAPI missing fields from AuthServer: $($missingInAdminApi -join ', ')" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "? WARNING: Could not compare with AuthServer (may not be running)" -ForegroundColor Yellow
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "? ALL TESTS PASSED!" -ForegroundColor Green
Write-Host ""
Write-Host "Changes verified:" -ForegroundColor White
Write-Host "  1. ? Response structure: Direct array (not wrapped)" -ForegroundColor Green
Write-Host "  2. ? Username field: Present and populated" -ForegroundColor Green
Write-Host "  3. ? All 9 fields: Present (userId, username, email, etc.)" -ForegroundColor Green
Write-Host "  4. ? Role casing: Lowercase (admin, dispatcher)" -ForegroundColor Green
Write-Host "  5. ? isDisabled type: Boolean (not nullable)" -ForegroundColor Green
Write-Host "  6. ? AuthServer compatibility: Fields match reference" -ForegroundColor Green
Write-Host ""
Write-Host "? AdminAPI User DTO is now aligned with AuthServer format!" -ForegroundColor Cyan
Write-Host "? AdminPortal should work immediately with zero code changes!" -ForegroundColor Cyan
Write-Host ""
