# AdminAPI Test Suite

Professional-level automated test suite for Bellwood AdminAPI. All tests are PowerShell 5.1 compatible.

## Overview

This test suite provides comprehensive coverage of all AdminAPI functionality including:

- Health checks and service availability
- User management (create, update, disable/enable, roles)
- Quote lifecycle (create, acknowledge, respond, accept, cancel)
- Booking management (create, list, assign drivers)
- Affiliate and driver management
- OAuth credential management
- Audit logging
- Data retention and protection
- Authorization and authentication

## Quick Start

### Prerequisites

- PowerShell 5.1 or higher
- AdminAPI running at `https://localhost:5206` (or specify custom URL)
- AuthServer running at `https://localhost:5001` (or specify custom URL)
- Admin user credentials (default: alice/password)

### Run All Tests

```powershell
cd Tests
.\Test-AdminApi.ps1
```

### Custom Configuration

```powershell
.\Test-AdminApi.ps1 `
    -AdminApiUrl "https://localhost:5206" `
    -AuthServerUrl "https://localhost:5001" `
    -AdminUsername "alice" `
    -AdminPassword "password" `
    -Verbose
```

### Skip Test Data Cleanup

```powershell
.\Test-AdminApi.ps1 -SkipCleanup
```

## Test Files

### Orchestrator Script

**`Test-AdminApi.ps1`** - Main orchestrator script that:
- Cleans up existing test data
- Authenticates with AuthServer
- Runs all test suites in sequence
- Generates test report with pass/fail statistics
- Exports results to JSON

### Modular Test Scripts

**`Test-QuoteLifecycle.ps1`** - Tests complete quote workflow:
1. Create quote
2. Acknowledge quote (dispatcher)
3. Respond with price/ETA
4. Accept quote (creates booking)
5. Verify booking linkage

**`Test-DriverAssignment.ps1`** - Tests driver assignment workflow:
1. Create affiliate
2. Create driver with UserUid
3. Create booking
4. Assign driver to booking
5. Verify assignment in booking details
6. Test driver lookup by UserUid

**`Test-UserManagementWorkflow.ps1`** - Tests user management workflow:
1. Create dispatcher user
2. Verify user appears in list
3. Update user roles (Dispatcher ? Admin + Dispatcher)
4. Disable user
5. Verify disabled user cannot login
6. Re-enable user
7. Verify re-enabled user can login
8. Test invalid role validation

## Test Data Cleanup

The orchestrator script automatically cleans up test data before running tests by:

1. Removing the `App_Data` directory (contains all file-backed storage)
2. Resetting test data tracking variables

**Important:** The API must be stopped before cleanup can succeed (locked files). If cleanup fails:

1. Stop the AdminAPI
2. Run cleanup: `.\Test-AdminApi.ps1 -SkipCleanup:$false`
3. Restart the AdminAPI
4. Run tests: `.\Test-AdminApi.ps1`

## Test Results

### Console Output

Tests display color-coded results in real-time:
- ? Green = Pass
- ? Red = Fail
- ? Yellow = Warning

### Summary Report

After all tests complete, a summary shows:
- Total tests run
- Tests passed/failed
- Pass rate percentage
- List of failed tests with details

### JSON Export

Results are exported to timestamped JSON files:
```
test-results-20240115-143022.json
```

## Exit Codes

- `0` - All tests passed
- `1` - One or more tests failed or critical error occurred

## Running Individual Test Modules

Each modular test script can run independently:

```powershell
# Get admin token first
$token = "your-admin-jwt-token"

# Run quote lifecycle test
.\Test-QuoteLifecycle.ps1 -AdminToken $token

# Run driver assignment test
.\Test-DriverAssignment.ps1 -AdminToken $token

# Run user management workflow test
.\Test-UserManagementWorkflow.ps1 -AdminToken $token
```

## Adding New Tests

To add new test functionality:

1. **Add to orchestrator** - Add new test function to `Test-AdminApi.ps1`
2. **Create modular test** - Create new `Test-[Feature].ps1` file for complex workflows
3. **Update this README** - Document new tests

### Example Test Function

```powershell
function Test-NewFeature {
    Write-TestHeader "NEW FEATURE TESTS"
    
    # Test implementation
    $result = Invoke-ApiRequest -Method GET -Uri "$AdminApiUrl/new-endpoint" -Token $Global:AdminToken
    Write-TestResult -TestName "GET /new-endpoint" -Passed $result.Success
    
    # Add more tests...
}
```

## Test Coverage

### Health Checks
- ? Basic health endpoint
- ? Liveness check
- ? Readiness check

### User Management
- ? List users
- ? Create user (Dispatcher, Admin, Booker roles)
- ? Update user roles
- ? Disable user
- ? Enable user
- ? Invalid password validation
- ? Invalid role validation

### Quotes
- ? Seed sample quotes
- ? List quotes
- ? Get quote details
- ? Create quote
- ? Acknowledge quote
- ? Respond to quote
- ? Accept quote (creates booking)
- ? Cancel quote

### Bookings
- ? Seed sample bookings
- ? List bookings
- ? Get booking details
- ? Create booking
- ? Assign driver to booking
- ? Cancel booking

### Affiliates & Drivers
- ? Seed affiliates and drivers
- ? List affiliates
- ? Get affiliate details
- ? Create affiliate
- ? Create driver
- ? Get driver by ID
- ? Get driver by UserUid
- ? List all drivers

### OAuth Credentials
- ? Get OAuth credentials
- ? Update OAuth credentials
- ? Verify secret masking

### Audit Logs
- ? Get audit logs with filtering
- ? Get audit log by ID

### Data Retention
- ? Get data retention policy
- ? Data protection test

### Authorization
- ? Access without token (should fail)
- ? Access with invalid token (should fail)

## Troubleshooting

### SSL Certificate Errors

The test scripts automatically bypass SSL certificate validation for localhost testing. For production testing, remove the certificate callback override.

### Connection Refused

Ensure both AdminAPI and AuthServer are running:
```powershell
# Check if services are running
Test-NetConnection -ComputerName localhost -Port 5206  # AdminAPI
Test-NetConnection -ComputerName localhost -Port 5001  # AuthServer
```

### Authentication Failures

Verify admin credentials are correct:
```powershell
# Test login manually
Invoke-WebRequest -Method POST -Uri "https://localhost:5001/api/auth/login" `
    -Body '{"username":"alice","password":"password"}' `
    -ContentType "application/json" -UseBasicParsing
```

### File Lock Errors During Cleanup

Stop the AdminAPI before running cleanup:
```powershell
# Stop API (if running as process)
Stop-Process -Name "Bellwood.AdminApi" -Force

# Run cleanup
.\Test-AdminApi.ps1

# Start API again
```

## CI/CD Integration

### Azure DevOps

```yaml
- task: PowerShell@2
  displayName: 'Run AdminAPI Tests'
  inputs:
    filePath: 'Tests/Test-AdminApi.ps1'
    arguments: '-AdminApiUrl $(AdminApiUrl) -AuthServerUrl $(AuthServerUrl)'
    errorActionPreference: 'stop'
    failOnStderr: true
```

### GitHub Actions

```yaml
- name: Run AdminAPI Tests
  shell: pwsh
  run: |
    ./Tests/Test-AdminApi.ps1 `
      -AdminApiUrl ${{ secrets.ADMIN_API_URL }} `
      -AuthServerUrl ${{ secrets.AUTH_SERVER_URL }}
```

## Contributing

When adding new API features:

1. Add corresponding tests to the orchestrator
2. Create modular test scripts for complex workflows
3. Update test coverage section in this README
4. Ensure all tests pass before committing

## License

Copyright © 2024 Bellwood Transportation. All rights reserved.
