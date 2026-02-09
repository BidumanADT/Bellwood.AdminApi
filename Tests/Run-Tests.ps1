# Simple Test Runner
# Convenience wrapper for running AdminAPI tests
# PowerShell 5.1 Compatible

#Requires -Version 5.1

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("All", "Health", "Users", "Quotes", "Bookings", "Drivers", "QuoteLifecycle", "DriverAssignment", "UserWorkflow")]
    [string]$Suite = "All",
    
    [switch]$SkipCleanup,
    [switch]$Verbose,
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

$ErrorActionPreference = "Stop"

Write-Host @"

????????????????????????????????????????????????????????????????
?                                                              ?
?          AdminAPI Test Runner                                ?
?          Simple interface for running test suites            ?
?                                                              ?
????????????????????????????????????????????????????????????????

"@ -ForegroundColor Cyan

# Helper function to get admin token
function Get-AdminToken {
    param(
        [string]$AuthServerUrl,
        [string]$Username = "alice",
        [string]$Password = "password"
    )
    
    Write-Host "Authenticating as '$Username'..." -ForegroundColor Cyan
    
    $loginBody = @{
        username = $Username
        password = $Password
    } | ConvertTo-Json
    
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        
        $response = Invoke-WebRequest -Method POST `
            -Uri "$AuthServerUrl/api/auth/login" `
            -Body $loginBody `
            -ContentType "application/json" `
            -UseBasicParsing
        
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.accessToken) {
            Write-Host "? Authentication successful" -ForegroundColor Green
            return $result.accessToken
        } else {
            throw "No access token in response"
        }
        
    } catch {
        Write-Host "? Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

try {
    Write-Host "Test Suite: $Suite" -ForegroundColor Yellow
    Write-Host "AdminAPI:   $AdminApiUrl" -ForegroundColor Gray
    Write-Host "AuthServer: $AuthServerUrl" -ForegroundColor Gray
    Write-Host ""
    
    switch ($Suite) {
        "All" {
            Write-Host "Running full test suite..." -ForegroundColor Cyan
            
            $params = @{
                AdminApiUrl = $AdminApiUrl
                AuthServerUrl = $AuthServerUrl
            }
            
            if ($SkipCleanup) { $params.SkipCleanup = $true }
            if ($Verbose) { $params.Verbose = $true }
            
            & "$PSScriptRoot\Test-AdminApi.ps1" @params
        }
        
        "QuoteLifecycle" {
            Write-Host "Running Quote Lifecycle integration test..." -ForegroundColor Cyan
            $token = Get-AdminToken -AuthServerUrl $AuthServerUrl
            
            & "$PSScriptRoot\Test-QuoteLifecycle.ps1" -AdminApiUrl $AdminApiUrl -AdminToken $token
        }
        
        "DriverAssignment" {
            Write-Host "Running Driver Assignment integration test..." -ForegroundColor Cyan
            $token = Get-AdminToken -AuthServerUrl $AuthServerUrl
            
            & "$PSScriptRoot\Test-DriverAssignment.ps1" -AdminApiUrl $AdminApiUrl -AdminToken $token
        }
        
        "UserWorkflow" {
            Write-Host "Running User Management Workflow test..." -ForegroundColor Cyan
            $token = Get-AdminToken -AuthServerUrl $AuthServerUrl
            
            & "$PSScriptRoot\Test-UserManagementWorkflow.ps1" `
                -AdminApiUrl $AdminApiUrl `
                -AuthServerUrl $AuthServerUrl `
                -AdminToken $token
        }
        
        default {
            Write-Host "Suite-specific tests coming soon..." -ForegroundColor Yellow
            Write-Host "For now, use -Suite All or one of the integration test suites:" -ForegroundColor Yellow
            Write-Host "  - QuoteLifecycle" -ForegroundColor Cyan
            Write-Host "  - DriverAssignment" -ForegroundColor Cyan
            Write-Host "  - UserWorkflow" -ForegroundColor Cyan
        }
    }
    
    Write-Host "`n? Test runner completed" -ForegroundColor Green
    
} catch {
    Write-Host "`n? Test runner failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Yellow
    exit 1
}
