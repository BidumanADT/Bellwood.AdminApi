# Phase Alpha: Quote Lifecycle Test Suite

Comprehensive testing scripts for the Phase Alpha quote lifecycle implementation in the Bellwood Admin API.

## ?? Overview

This test suite validates the complete quote lifecycle workflow:

```
Submitted ? Acknowledged ? Responded ? Accepted ? Booking Created
                    ?
                Cancelled (optional)
```

## ?? Test Coverage

### 1. **Test-PhaseAlpha-QuoteLifecycle.ps1**
End-to-end workflow testing covering:
- ? Passenger quote submission
- ? Dispatcher acknowledgment
- ? Dispatcher price/ETA response
- ? Passenger acceptance ? booking creation
- ? Quote cancellation
- ? FSM validation (invalid state transitions)
- ? RBAC policy enforcement
- ? Ownership verification

**Tests:** 18  
**Duration:** ~30-45 seconds

### 2. **Test-PhaseAlpha-ValidationEdgeCases.ps1**
Input validation and edge case testing:
- ? Price validation (negative, zero, very small values)
- ? Pickup time validation (past, current, future)
- ? Data persistence verification
- ? Notes field handling (empty, long text)
- ? Audit metadata population

**Tests:** 12  
**Duration:** ~25-35 seconds

### 3. **Test-PhaseAlpha-Integration.ps1**
Integration with existing systems:
- ? Quote list filtering by status
- ? Quote ? Booking integration
- ? Driver assignment to quote-originated bookings
- ? Data isolation between users
- ? Admin/Staff privilege verification
- ? Complete happy path workflow

**Tests:** 10  
**Duration:** ~30-40 seconds

### 4. **Run-AllPhaseAlphaTests.ps1** (Master Script)
Executes all test suites sequentially with:
- ? Prerequisites check (API availability)
- ? Optional test data cleanup
- ? Comprehensive final report
- ? Stop-on-failure option

**Total Tests:** 40  
**Total Duration:** ~90-120 seconds

## ?? Quick Start

### Run All Tests
```powershell
cd Scripts
.\Run-AllPhaseAlphaTests.ps1
```

### Run Individual Test Suite
```powershell
.\Test-PhaseAlpha-QuoteLifecycle.ps1
.\Test-PhaseAlpha-ValidationEdgeCases.ps1
.\Test-PhaseAlpha-Integration.ps1
```

### Custom Configuration
```powershell
# Different API endpoints
.\Run-AllPhaseAlphaTests.ps1 -ApiBaseUrl "https://api.example.com" -AuthServerUrl "https://auth.example.com"

# Stop on first failure
.\Run-AllPhaseAlphaTests.ps1 -StopOnFailure

# Skip initial cleanup (for debugging)
.\Run-AllPhaseAlphaTests.ps1 -SkipSetup
```

## ?? Prerequisites

### Running Services
1. **Bellwood.AdminApi** must be running at `https://localhost:5206` (or custom URL)
2. **AuthServer** must be running at `https://localhost:5001` (or custom URL)

### Test Users (from AuthServer seed data)
- **chris** (password: `password`) - Passenger/Booker role
- **diana** (password: `password`) - Dispatcher role
- **alice** (password: `password`) - Admin role
- **bob** (password: `password`) - Additional passenger (for data isolation tests)

### PowerShell Requirements
- PowerShell 5.1 or higher
- No additional modules required (uses built-in `Invoke-WebRequest` and `Invoke-RestMethod`)

## ?? Understanding Test Output

### Success Indicators
```
? PASS - Test passed successfully
? - Informational message
???????? - Test section separator
```

### Failure Indicators
```
? FAIL - Test failed
?? - Warning (non-critical issue)
? - Unexpected behavior
```

### Test Progress
```
[TEST 5/18] Dispatcher responds with price/ETA
?????????????????????????????????????????????????????
? Description: Diana sends estimated price ($125.50) and pickup time
? Endpoint: POST /quotes/{id}/respond
? Request Body: { estimatedPrice: 125.50, ... }
? Response (200): { status: "Responded", ... }
? Status Check: PASS (got 200)
? Running custom validation...
  ? Status changed to 'Responded'
  ? EstimatedPrice is correct: $125.50
  ? RespondedAt is populated
? Validation: PASS
? OVERALL: PASS
```

## ?? Troubleshooting

### Common Issues

#### 1. "AdminAPI is not responding"
**Solution:** Ensure AdminAPI is running:
```bash
cd Bellwood.AdminApi
dotnet run
```

#### 2. "AuthServer is not responding"
**Solution:** Ensure AuthServer is running at port 5001

#### 3. "Authentication failed"
**Solution:** Verify test users exist in AuthServer:
- Run AuthServer seed script to create test users
- Verify user credentials match the test scripts

#### 4. "Quote not found" or "Booking not found"
**Solution:** Clear test data and re-run:
```powershell
.\Clear-TestData.ps1
.\Run-AllPhaseAlphaTests.ps1
```

#### 5. Tests pass individually but fail in master script
**Solution:** Add delays between test suites or run with `-SkipSetup`:
```powershell
.\Run-AllPhaseAlphaTests.ps1 -SkipSetup
```

## ?? Test Metrics

### Coverage Summary
- **API Endpoints Tested:** 12
- **User Roles Tested:** 3 (Passenger, Dispatcher, Admin)
- **Quote Statuses Tested:** 5 (Submitted, Acknowledged, Responded, Accepted, Cancelled)
- **Validation Rules Tested:** 8 (Price, Time, Ownership, RBAC, FSM, etc.)
- **Integration Points Tested:** 6 (Bookings, Drivers, Audit, Listings, etc.)

### Expected Results
All 40 tests should **PASS** for production readiness:
- Quote Lifecycle: 18/18 ?
- Validation: 12/12 ?
- Integration: 10/10 ?

## ?? Detailed Diagnostic Output

Each test includes:

1. **Test Description** - What is being tested and why
2. **Request Details** - HTTP method, URL, request body
3. **Response Data** - Status code and response body
4. **Validation Steps** - Each validation check with pass/fail
5. **Overall Result** - Combined status with diagnostics

### Example Diagnostic Flow
```
? Description: Chris (passenger) accepts quote
? Endpoint: POST /quotes/{id}/accept
? Response (200): {
  "quoteStatus": "Accepted",
  "bookingId": "abc123def456",
  "bookingStatus": "Requested",
  "sourceQuoteId": "quote789"
}
? Running custom validation...
  ? Quote status changed to 'Accepted'
  ? Booking created with ID: abc123def456
  ? Booking status is 'Requested' (correct workflow)
  ? SourceQuoteId links back to quote: quote789
? Validation: PASS
? OVERALL: PASS
```

## ?? Extending Tests

### Adding New Test Cases

1. **Add to existing script:**
```powershell
# In Test-PhaseAlpha-QuoteLifecycle.ps1
Test-Endpoint `
    -TestName "My new test" `
    -Method "POST" `
    -Url "$ApiBaseUrl/quotes/{id}/myaction" `
    -Headers $testHeaders `
    -ExpectedStatus 200 `
    -Description "What this test validates" `
    -Validation {
        param($Data)
        # Custom validation logic
        return $Data.someField -eq "expectedValue"
    }
```

2. **Create new test script:**
   - Copy one of the existing test files
   - Update test counter: `$totalTests = X`
   - Add new test cases
   - Update master script to include it

## ?? Customization

### Color Scheme
Tests use color-coded output:
- **Green** ? - Success
- **Red** ? - Failure
- **Yellow** ? - Important info / Warnings
- **Cyan** ? - Headers / Test names
- **Gray** - Supplementary details

### Verbosity Levels
Current scripts are **verbose** for alpha testing. For production:
- Remove `Write-Host ($responseData | ConvertTo-Json)` lines for less output
- Comment out detailed validation messages
- Keep only final PASS/FAIL indicators

## ?? Security Notes

- Test scripts use self-signed certificate bypass for localhost testing
- **DO NOT** use certificate bypass in production
- Test credentials are for development only
- Sensitive data (tokens, passwords) are not logged

## ?? Related Documentation

- [Phase Alpha Test Preparation](../Docs/Temp/alpha-test-preparation.md)
- [Quote Lifecycle API Documentation](../README.md)
- [Phase 2 Summary](../Docs/Phase2-Summary.md)

## ?? Tips

1. **Run tests frequently** during development to catch regressions early
2. **Review failed test output** carefully - diagnostic messages pinpoint issues
3. **Use `-StopOnFailure`** when debugging to save time
4. **Keep test data clean** - run cleanup scripts between major test runs
5. **Monitor test duration** - significant slowdowns may indicate API issues

## ?? Contributing

When adding new quote lifecycle features:

1. Write tests **before** implementing the feature (TDD)
2. Ensure all existing tests still pass
3. Add tests to the appropriate suite (Lifecycle, Validation, or Integration)
4. Update `$totalTests` counter
5. Document new test cases in this README

## ?? Support

If tests fail unexpectedly:

1. Check API and AuthServer are running
2. Review test output for specific error messages
3. Verify database/JSON storage is accessible
4. Check recent code changes for breaking changes
5. Run individual test suites to isolate issues

---

**Last Updated:** Phase Alpha Implementation  
**Maintained By:** Bellwood Development Team  
**Test Framework:** PowerShell with Invoke-WebRequest
