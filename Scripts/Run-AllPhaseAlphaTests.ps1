#Requires -Version 5.1
<#
.SYNOPSIS
    Phase Alpha: Master test suite - runs all quote lifecycle tests.

.DESCRIPTION
    Executes all Phase Alpha test scripts in the correct order:
    1. Quote Lifecycle End-to-End Tests
    2. Validation & Edge Case Tests
    3. Integration Tests
    
    Provides a comprehensive report and stops on first failure if -StopOnFailure is specified.

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.PARAMETER StopOnFailure
    Stop execution if any test script fails

.PARAMETER SkipSetup
    Skip initial data cleanup (useful for debugging)

.EXAMPLE
    .\Run-AllPhaseAlphaTests.ps1
    
.EXAMPLE
    .\Run-AllPhaseAlphaTests.ps1 -StopOnFailure
    
.EXAMPLE
    .\Run-AllPhaseAlphaTests.ps1 -ApiBaseUrl "https://localhost:5206" -StopOnFailure
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001",
    [switch]$StopOnFailure,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"

# Console formatting
$script:Width = 80

function Write-Banner {
    param([string]$Text, [ConsoleColor]$Color = "Cyan")
    
    $padding = [Math]::Max(0, ($script:Width - $Text.Length - 4) / 2)
    $paddingStr = "=" * $padding
    
    Write-Host ""
    Write-Host ("=" * $script:Width) -ForegroundColor $Color
    Write-Host "$paddingStr  $Text  $paddingStr" -ForegroundColor $Color
    Write-Host ("=" * $script:Width) -ForegroundColor $Color
    Write-Host ""
}

function Write-Section {
    param([string]$Text)
    Write-Host "`n$('-' * $script:Width)" -ForegroundColor DarkGray
    Write-Host "  $Text" -ForegroundColor Yellow
    Write-Host "$('-' * $script:Width)" -ForegroundColor DarkGray
}

function Write-Step {
    param([string]$Text, [int]$Step, [int]$Total)
    Write-Host "`n[$Step/$Total] $Text" -ForegroundColor Cyan
}

# Test results tracking
$script:TestResults = @{
    Scripts = @()
    TotalPassed = 0
    TotalFailed = 0
    StartTime = Get-Date
}

function Invoke-TestScript {
    param(
        [string]$ScriptName,
        [string]$Description,
        [int]$Step,
        [int]$TotalSteps
    )
    
    Write-Step -Text $Description -Step $Step -Total $TotalSteps
    
    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    
    if (-not (Test-Path $scriptPath)) {
        Write-Host "? Script not found: $scriptPath" -ForegroundColor Red
        $script:TestResults.Scripts += @{
            Name = $ScriptName
            Description = $Description
            Status = "NotFound"
            ExitCode = -1
            Duration = 0
            Error = "Script file not found"
        }
        return $false
    }
    
    Write-Host "? Executing: $scriptPath" -ForegroundColor Gray
    Write-Host "? Arguments: -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl" -ForegroundColor Gray
    
    $startTime = Get-Date
    
    try {
        # Execute script and capture output
        $output = & $scriptPath -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl 2>&1
        $exitCode = $LASTEXITCODE
        
        # Display output
        $output | ForEach-Object {
            Write-Host $_
        }
        
        $duration = ((Get-Date) - $startTime).TotalSeconds
        
        if ($exitCode -eq 0) {
            Write-Host "`n? $ScriptName completed successfully" -ForegroundColor Green
            Write-Host "   Duration: $([Math]::Round($duration, 2)) seconds" -ForegroundColor Gray
            
            $script:TestResults.Scripts += @{
                Name = $ScriptName
                Description = $Description
                Status = "Passed"
                ExitCode = 0
                Duration = $duration
                Error = $null
            }
            
            return $true
        } else {
            Write-Host "`n? $ScriptName failed with exit code $exitCode" -ForegroundColor Red
            Write-Host "   Duration: $([Math]::Round($duration, 2)) seconds" -ForegroundColor Gray
            
            $script:TestResults.Scripts += @{
                Name = $ScriptName
                Description = $Description
                Status = "Failed"
                ExitCode = $exitCode
                Duration = $duration
                Error = "Exit code: $exitCode"
            }
            
            return $false
        }
    }
    catch {
        $duration = ((Get-Date) - $startTime).TotalSeconds
        Write-Host "`n? $ScriptName threw an exception:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        Write-Host "   Duration: $([Math]::Round($duration, 2)) seconds" -ForegroundColor Gray
        
        $script:TestResults.Scripts += @{
            Name = $ScriptName
            Description = $Description
            Status = "Error"
            ExitCode = -1
            Duration = $duration
            Error = $_.Exception.Message
        }
        
        return $false
    }
}

# ???????????????????????????????????????????????????????????????????
# HEADER
# ???????????????????????????????????????????????????????????????????

Write-Banner -Text "PHASE ALPHA: MASTER TEST SUITE" -Color Cyan

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  AdminAPI URL:    $ApiBaseUrl" -ForegroundColor Gray
Write-Host "  AuthServer URL:  $AuthServerUrl" -ForegroundColor Gray
Write-Host "  Stop on Failure: $StopOnFailure" -ForegroundColor Gray
Write-Host "  Skip Setup:      $SkipSetup" -ForegroundColor Gray
Write-Host ""

# ???????????????????????????????????????????????????????????????????
# PREREQUISITES CHECK
# ???????????????????????????????????????????????????????????????????

Write-Section -Text "Prerequisites Check"

Write-Host "? Checking API availability..." -ForegroundColor Yellow

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

# Check AdminAPI
try {
    $response = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -Method GET -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "? AdminAPI is responding at $ApiBaseUrl" -ForegroundColor Green
    } else {
        Write-Host "??  AdminAPI returned status $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? AdminAPI is not responding at $ApiBaseUrl" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure the AdminAPI is running before executing tests." -ForegroundColor Yellow
    exit 1
}

# Check AuthServer
try {
    $response = Invoke-WebRequest -Uri "$AuthServerUrl/health" -Method GET -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "? AuthServer is responding at $AuthServerUrl" -ForegroundColor Green
    } else {
        Write-Host "??  AuthServer returned status $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? AuthServer is not responding at $AuthServerUrl" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure the AuthServer is running before executing tests." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "? All prerequisites met" -ForegroundColor Green

# ???????????????????????????????????????????????????????????????????
# SETUP (OPTIONAL)
# ???????????????????????????????????????????????????????????????????

if (-not $SkipSetup) {
    Write-Section -Text "Setup: Clean Test Environment"
    
    Write-Host "? Cleaning existing test data..." -ForegroundColor Yellow
    Write-Host "  (This ensures tests start from a clean state)" -ForegroundColor Gray
    
    # Check if Clear-TestData script exists
    $clearScript = Join-Path $PSScriptRoot "Clear-TestData.ps1"
    if (Test-Path $clearScript) {
        try {
            & $clearScript -ApiBaseUrl $ApiBaseUrl -AuthServerUrl $AuthServerUrl -Confirm:$false
            Write-Host "? Test data cleared" -ForegroundColor Green
        } catch {
            Write-Host "??  Could not clear test data: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "  Continuing anyway..." -ForegroundColor Gray
        }
    } else {
        Write-Host "??  Clear-TestData.ps1 not found, skipping cleanup" -ForegroundColor Yellow
    }
}

# ???????????????????????????????????????????????????????????????????
# TEST EXECUTION
# ???????????????????????????????????????????????????????????????????

Write-Banner -Text "EXECUTING PHASE ALPHA TESTS" -Color Yellow

$testScripts = @(
    @{
        Name = "Test-PhaseAlpha-QuoteLifecycle.ps1"
        Description = "Quote Lifecycle End-to-End Tests"
    },
    @{
        Name = "Test-PhaseAlpha-ValidationEdgeCases.ps1"
        Description = "Validation & Edge Case Tests"
    },
    @{
        Name = "Test-PhaseAlpha-Integration.ps1"
        Description = "Integration Tests"
    }
)

$totalScripts = $testScripts.Count
$currentScript = 0

foreach ($script in $testScripts) {
    $currentScript++
    
    $success = Invoke-TestScript `
        -ScriptName $script.Name `
        -Description $script.Description `
        -Step $currentScript `
        -TotalSteps $totalScripts
    
    if (-not $success -and $StopOnFailure) {
        Write-Host ""
        Write-Host "? Stopping test execution due to failure (StopOnFailure flag set)" -ForegroundColor Red
        break
    }
    
    # Brief pause between scripts
    if ($currentScript -lt $totalScripts) {
        Write-Host ""
        Write-Host "? Pausing for 2 seconds before next test suite..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

# ???????????????????????????????????????????????????????????????????
# FINAL REPORT
# ???????????????????????????????????????????????????????????????????

$totalDuration = ((Get-Date) - $script:TestResults.StartTime).TotalSeconds
$passedScripts = ($script:TestResults.Scripts | Where-Object { $_.Status -eq "Passed" }).Count
$failedScripts = ($script:TestResults.Scripts | Where-Object { $_.Status -in @("Failed", "Error", "NotFound") }).Count

Write-Banner -Text "FINAL TEST REPORT" -Color Cyan

Write-Host "Execution Summary:" -ForegroundColor Yellow
Write-Host "  Total Scripts:    $totalScripts" -ForegroundColor White
Write-Host "  Passed:           $passedScripts" -ForegroundColor Green
Write-Host "  Failed:           $failedScripts" -ForegroundColor Red
Write-Host "  Total Duration:   $([Math]::Round($totalDuration, 2)) seconds" -ForegroundColor Gray
Write-Host ""

Write-Host "Detailed Results:" -ForegroundColor Yellow
Write-Host ""

foreach ($result in $script:TestResults.Scripts) {
    $statusColor = switch ($result.Status) {
        "Passed" { "Green" }
        "Failed" { "Red" }
        "Error" { "Red" }
        "NotFound" { "Red" }
        default { "Yellow" }
    }
    
    $statusIcon = switch ($result.Status) {
        "Passed" { "?" }
        "Failed" { "?" }
        "Error" { "??" }
        "NotFound" { "?" }
        default { "?" }
    }
    
    Write-Host "$statusIcon $($result.Name)" -ForegroundColor $statusColor
    Write-Host "   Description: $($result.Description)" -ForegroundColor Gray
    Write-Host "   Status:      $($result.Status)" -ForegroundColor $statusColor
    Write-Host "   Duration:    $([Math]::Round($result.Duration, 2))s" -ForegroundColor Gray
    
    if ($result.Error) {
        Write-Host "   Error:       $($result.Error)" -ForegroundColor Red
    }
    
    Write-Host ""
}

# ???????????????????????????????????????????????????????????????????
# FINAL VERDICT
# ???????????????????????????????????????????????????????????????????

Write-Host ("=" * $script:Width) -ForegroundColor Cyan

if ($failedScripts -eq 0) {
    Write-Host ""
    Write-Host "  ???????  ?????? ??????????????????????????????? " -ForegroundColor Green
    Write-Host "  ????????????????????????????????????????????????" -ForegroundColor Green
    Write-Host "  ??????????????????????????????????????  ???  ???" -ForegroundColor Green
    Write-Host "  ??????? ??????????????????????????????  ???  ???" -ForegroundColor Green
    Write-Host "  ???     ???  ???????????????????????????????????" -ForegroundColor Green
    Write-Host "  ???     ???  ?????????????????????????????????? " -ForegroundColor Green
    Write-Host ""
    Write-Host ("=" * $script:Width) -ForegroundColor Green
    Write-Host ""
    Write-Host "  ?? ALL PHASE ALPHA TESTS PASSED!" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Phase Alpha Quote Lifecycle Implementation:" -ForegroundColor Cyan
    Write-Host "    ? Quote submission, acknowledgment, and response" -ForegroundColor Green
    Write-Host "    ? Passenger acceptance and booking creation" -ForegroundColor Green
    Write-Host "    ? FSM validation and state transitions" -ForegroundColor Green
    Write-Host "    ? RBAC policies and ownership checks" -ForegroundColor Green
    Write-Host "    ? Input validation and edge cases" -ForegroundColor Green
    Write-Host "    ? Integration with existing systems" -ForegroundColor Green
    Write-Host "    ? Data persistence and audit trails" -ForegroundColor Green
    Write-Host ""
    Write-Host "  System Status: READY FOR ALPHA TESTING ??" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Next Steps:" -ForegroundColor Cyan
    Write-Host "    1. Deploy to alpha test environment" -ForegroundColor Gray
    Write-Host "    2. Onboard alpha testers (passengers, dispatchers)" -ForegroundColor Gray
    Write-Host "    3. Monitor quote lifecycle in production" -ForegroundColor Gray
    Write-Host "    4. Collect feedback for Phase 3 (LimoAnywhere integration)" -ForegroundColor Gray
    Write-Host ""
    Write-Host ("=" * $script:Width) -ForegroundColor Green
    Write-Host ""
    
    exit 0
} else {
    Write-Host ""
    Write-Host "  ???????? ?????? ??????     ??????????????? " -ForegroundColor Red
    Write-Host "  ??????????????????????     ????????????????" -ForegroundColor Red
    Write-Host "  ??????  ??????????????     ??????  ???  ???" -ForegroundColor Red
    Write-Host "  ??????  ??????????????     ??????  ???  ???" -ForegroundColor Red
    Write-Host "  ???     ???  ??????????????????????????????" -ForegroundColor Red
    Write-Host "  ???     ???  ????????????????????????????? " -ForegroundColor Red
    Write-Host ""
    Write-Host ("=" * $script:Width) -ForegroundColor Red
    Write-Host ""
    Write-Host "  ??  SOME TESTS FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Failed Scripts: $failedScripts of $totalScripts" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Action Required:" -ForegroundColor Yellow
    Write-Host "    1. Review failed test output above" -ForegroundColor Gray
    Write-Host "    2. Fix identified issues in code" -ForegroundColor Gray
    Write-Host "    3. Re-run test suite to verify fixes" -ForegroundColor Gray
    Write-Host "    4. Ensure all tests pass before deploying" -ForegroundColor Gray
    Write-Host ""
    Write-Host ("=" * $script:Width) -ForegroundColor Red
    Write-Host ""
    
    exit 1
}
