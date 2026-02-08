# User Management Test Module
# PowerShell 5.1 Compatible
# Tests complete user management workflow

#Requires -Version 5.1

param(
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001",
    [string]$AdminToken,
    [switch]$Verbose
)

function Test-UserManagementWorkflow {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  USER MANAGEMENT WORKFLOW TEST" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    $headers = @{
        "Authorization" = "Bearer $AdminToken"
        "Content-Type" = "application/json"
    }
    
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        
        # Step 1: Create dispatcher user
        Write-Host "Step 1: Creating dispatcher user..." -ForegroundColor Yellow
        
        $createUserBody = @{
            email = "workflow.dispatcher@example.com"
            firstName = "Workflow"
            lastName = "Dispatcher"
            tempPassword = "WorkflowTest123!"
            roles = @("Dispatcher")
        }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/users" `
            -Headers $headers -Body ($createUserBody | ConvertTo-Json) -UseBasicParsing
        
        if ($response.StatusCode -ne 201) {
            Write-Host "? User creation failed with status: $($response.StatusCode)" -ForegroundColor Red
            return $false
        }
        
        $user = $response.Content | ConvertFrom-Json
        $userId = $user.userId
        
        Write-Host "? User created successfully" -ForegroundColor Green
        Write-Host "  User ID: $userId" -ForegroundColor Gray
        Write-Host "  Email: $($user.email)" -ForegroundColor Gray
        Write-Host "  Roles: $($user.roles -join ', ')" -ForegroundColor Gray
        
        # Step 2: Verify user appears in list
        Write-Host "`nStep 2: Verifying user appears in list..." -ForegroundColor Yellow
        
        $response = Invoke-WebRequest -Method GET -Uri "$AdminApiUrl/users/list?take=100&skip=0" `
            -Headers $headers -UseBasicParsing
        
        $userList = ($response.Content | ConvertFrom-Json).users
        $foundUser = $userList | Where-Object { $_.userId -eq $userId }
        
        if ($foundUser) {
            Write-Host "? User found in list" -ForegroundColor Green
            Write-Host "  Email: $($foundUser.email)" -ForegroundColor Gray
        } else {
            Write-Host "? User not found in list" -ForegroundColor Red
            return $false
        }
        
        # Step 3: Update user roles (add Admin role)
        Write-Host "`nStep 3: Updating user roles (Dispatcher -> Admin + Dispatcher)..." -ForegroundColor Yellow
        
        $updateRolesBody = @{
            roles = @("Admin", "Dispatcher")
        }
        
        $response = Invoke-WebRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/roles" `
            -Headers $headers -Body ($updateRolesBody | ConvertTo-Json) -UseBasicParsing
        
        $updatedUser = $response.Content | ConvertFrom-Json
        
        if ($updatedUser.roles -contains "Admin" -and $updatedUser.roles -contains "Dispatcher") {
            Write-Host "? Roles updated successfully" -ForegroundColor Green
            Write-Host "  New roles: $($updatedUser.roles -join ', ')" -ForegroundColor Gray
        } else {
            Write-Host "? Role update failed" -ForegroundColor Red
            Write-Host "  Expected: Admin, Dispatcher" -ForegroundColor Yellow
            Write-Host "  Got: $($updatedUser.roles -join ', ')" -ForegroundColor Yellow
            return $false
        }
        
        # Step 4: Disable user
        Write-Host "`nStep 4: Disabling user..." -ForegroundColor Yellow
        
        $disableBody = @{
            isDisabled = $true
        }
        
        $response = Invoke-WebRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/disable" `
            -Headers $headers -Body ($disableBody | ConvertTo-Json) -UseBasicParsing
        
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.isDisabled -eq $true) {
            Write-Host "? User disabled successfully" -ForegroundColor Green
        } else {
            Write-Host "? User disable failed" -ForegroundColor Red
            return $false
        }
        
        # Step 5: Verify disabled user cannot login
        Write-Host "`nStep 5: Verifying disabled user cannot login..." -ForegroundColor Yellow
        
        $loginBody = @{
            username = "workflow.dispatcher@example.com"
            password = "WorkflowTest123!"
        }
        
        try {
            $response = Invoke-WebRequest -Method POST -Uri "$AuthServerUrl/api/auth/login" `
                -Body ($loginBody | ConvertTo-Json) -ContentType "application/json" -UseBasicParsing
            
            Write-Host "? Disabled user was able to login (should fail)" -ForegroundColor Red
            return $false
            
        } catch {
            if ($_.Exception.Response.StatusCode.value__ -eq 401 -or $_.Exception.Response.StatusCode.value__ -eq 403) {
                Write-Host "? Disabled user cannot login (expected)" -ForegroundColor Green
            } else {
                Write-Host "? Unexpected error during login test: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
        
        # Step 6: Re-enable user
        Write-Host "`nStep 6: Re-enabling user..." -ForegroundColor Yellow
        
        $enableBody = @{
            isDisabled = $false
        }
        
        $response = Invoke-WebRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/disable" `
            -Headers $headers -Body ($enableBody | ConvertTo-Json) -UseBasicParsing
        
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.isDisabled -eq $false) {
            Write-Host "? User re-enabled successfully" -ForegroundColor Green
        } else {
            Write-Host "? User enable failed" -ForegroundColor Red
            return $false
        }
        
        # Step 7: Verify re-enabled user can login
        Write-Host "`nStep 7: Verifying re-enabled user can login..." -ForegroundColor Yellow
        
        try {
            $response = Invoke-WebRequest -Method POST -Uri "$AuthServerUrl/api/auth/login" `
                -Body ($loginBody | ConvertTo-Json) -ContentType "application/json" -UseBasicParsing
            
            $loginResult = $response.Content | ConvertFrom-Json
            
            if ($loginResult.accessToken) {
                Write-Host "? Re-enabled user can login successfully" -ForegroundColor Green
                Write-Host "  Token acquired: $($loginResult.accessToken.Substring(0, 20))..." -ForegroundColor Gray
            } else {
                Write-Host "? Login succeeded but no token received" -ForegroundColor Red
                return $false
            }
            
        } catch {
            Write-Host "? Re-enabled user cannot login: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
        
        # Step 8: Test role validation
        Write-Host "`nStep 8: Testing invalid role validation..." -ForegroundColor Yellow
        
        $invalidRoleBody = @{
            roles = @("InvalidRole", "Admin")
        }
        
        try {
            $response = Invoke-WebRequest -Method PUT -Uri "$AdminApiUrl/users/$userId/roles" `
                -Headers $headers -Body ($invalidRoleBody | ConvertTo-Json) -UseBasicParsing
            
            Write-Host "? Invalid role was accepted (should fail)" -ForegroundColor Red
            return $false
            
        } catch {
            if ($_.Exception.Response.StatusCode.value__ -eq 400) {
                Write-Host "? Invalid role rejected (expected)" -ForegroundColor Green
            } else {
                Write-Host "? Unexpected error: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
        
        Write-Host "`n? USER MANAGEMENT WORKFLOW TEST PASSED" -ForegroundColor Green
        return $true
        
    } catch {
        Write-Host "? USER MANAGEMENT WORKFLOW TEST FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
        return $false
    }
}

# Run test if called directly
if ($AdminToken) {
    $result = Test-UserManagementWorkflow
    if (-not $result) {
        exit 1
    }
} else {
    Write-Host "Error: AdminToken parameter required" -ForegroundColor Red
    exit 1
}
