# AdminAPI Test Suite - Quick Reference

## Common Commands

### Run All Tests (with cleanup)
```powershell
cd Tests
.\Test-AdminApi.ps1
```

### Run All Tests (skip cleanup)
```powershell
.\Test-AdminApi.ps1 -SkipCleanup
```

### Run with Custom URLs
```powershell
.\Test-AdminApi.ps1 `
    -AdminApiUrl "https://localhost:5206" `
    -AuthServerUrl "https://localhost:5001"
```

### Run with Different Admin User
```powershell
.\Test-AdminApi.ps1 `
    -AdminUsername "admin" `
    -AdminPassword "AdminPass123!"
```

### Enable Verbose Output
```powershell
.\Test-AdminApi.ps1 -Verbose
```

## Individual Test Modules

### Get Admin Token First
```powershell
# Login to get token
$loginBody = @{
    username = "alice"
    password = "password"
} | ConvertTo-Json

$response = Invoke-WebRequest -Method POST `
    -Uri "https://localhost:5001/api/auth/login" `
    -Body $loginBody `
    -ContentType "application/json" `
    -UseBasicParsing

$token = ($response.Content | ConvertFrom-Json).accessToken
```

### Run Quote Lifecycle Test
```powershell
.\Test-QuoteLifecycle.ps1 -AdminToken $token
```

### Run Driver Assignment Test
```powershell
.\Test-DriverAssignment.ps1 -AdminToken $token
```

### Run User Management Workflow Test
```powershell
.\Test-UserManagementWorkflow.ps1 -AdminToken $token
```

## Manual Cleanup

### Remove Test Data
```powershell
# Stop AdminAPI first, then:
Remove-Item -Path "..\App_Data" -Recurse -Force
```

### Clear Specific Data Types
```powershell
# Quotes only
Remove-Item -Path "..\App_Data\quotes.json" -Force

# Bookings only
Remove-Item -Path "..\App_Data\bookings.json" -Force

# Users (managed by AuthServer)
# Cannot be cleared from AdminAPI
```

## Test Results

### View Last Test Results
```powershell
# Find latest JSON report
Get-ChildItem -Filter "test-results-*.json" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 | 
    Get-Content | 
    ConvertFrom-Json | 
    Format-Table TestName, Passed, Message
```

### Export Failed Tests
```powershell
# Get failed tests from latest report
Get-ChildItem -Filter "test-results-*.json" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 | 
    Get-Content | 
    ConvertFrom-Json | 
    Where-Object { -not $_.Passed } | 
    Format-Table TestName, Message
```

## Troubleshooting

### Check Services Running
```powershell
# AdminAPI
Test-NetConnection -ComputerName localhost -Port 5206

# AuthServer
Test-NetConnection -ComputerName localhost -Port 5001
```

### Test Authentication Manually
```powershell
Invoke-WebRequest -Method POST `
    -Uri "https://localhost:5001/api/auth/login" `
    -Body '{"username":"alice","password":"password"}' `
    -ContentType "application/json" `
    -UseBasicParsing
```

### Test Endpoint Manually
```powershell
# Get admin token first (see above)
Invoke-WebRequest -Method GET `
    -Uri "https://localhost:5206/health" `
    -UseBasicParsing

Invoke-WebRequest -Method GET `
    -Uri "https://localhost:5206/users/list" `
    -Headers @{Authorization="Bearer $token"} `
    -UseBasicParsing
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0    | All tests passed |
| 1    | One or more tests failed |

## Test Categories

| Category | Tests | Command |
|----------|-------|---------|
| Health | 3 tests | Included in orchestrator |
| User Management | 8 tests | Included in orchestrator |
| Quotes | 6 tests | Included in orchestrator |
| Bookings | 3 tests | Included in orchestrator |
| Affiliates/Drivers | 7 tests | Included in orchestrator |
| OAuth | 3 tests | Included in orchestrator |
| Audit Logs | 2 tests | Included in orchestrator |
| Data Retention | 2 tests | Included in orchestrator |
| Authorization | 2 tests | Included in orchestrator |
| **Quote Lifecycle** | 6 steps | `.\Test-QuoteLifecycle.ps1` |
| **Driver Assignment** | 6 steps | `.\Test-DriverAssignment.ps1` |
| **User Workflow** | 8 steps | `.\Test-UserManagementWorkflow.ps1` |

## CI/CD Examples

### Run in CI Pipeline
```powershell
# With error handling
try {
    .\Test-AdminApi.ps1 -AdminApiUrl $env:ADMIN_API_URL
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE"
    }
} catch {
    Write-Error "Test execution failed: $_"
    exit 1
}
```

### Parse Results in CI
```powershell
# Get latest results
$results = Get-ChildItem -Filter "test-results-*.json" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 | 
    Get-Content | 
    ConvertFrom-Json

# Calculate metrics
$total = $results.Count
$passed = ($results | Where-Object { $_.Passed }).Count
$failed = $total - $passed
$passRate = [math]::Round(($passed / $total) * 100, 2)

Write-Host "##vso[task.setvariable variable=TestTotal]$total"
Write-Host "##vso[task.setvariable variable=TestPassed]$passed"
Write-Host "##vso[task.setvariable variable=TestFailed]$failed"
Write-Host "##vso[task.setvariable variable=TestPassRate]$passRate"
```
