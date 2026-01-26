# How to Test Phase Alpha Quote Lifecycle

## Prerequisites (2 minutes)

1. **Start AdminAPI**
   ```bash
   cd Bellwood.AdminApi
   dotnet run
   ```
   Wait for: `? Bellwood AdminAPI starting...`

2. **Start AuthServer** (separate terminal)
   ```bash
   cd path/to/AuthServer
   dotnet run
   ```
   Wait for: `Now listening on: https://localhost:5001`

3. **Verify APIs are running**
   - AdminAPI: https://localhost:5206/health should return `{"status":"ok"}`
   - AuthServer: https://localhost:5001/health should return status

---

## Run Tests (2 minutes)

### Option 1: Run Everything (Recommended)
```powershell
cd Scripts
.\Run-AllPhaseAlphaTests.ps1
```

**Expected Output:**
```
============================================================
  PHASE ALPHA: MASTER TEST SUITE
============================================================

? All prerequisites met

[1/3] Quote Lifecycle End-to-End Tests
? Test-PhaseAlpha-QuoteLifecycle.ps1 completed successfully

[2/3] Validation & Edge Case Tests
? Test-PhaseAlpha-ValidationEdgeCases.ps1 completed successfully

[3/3] Integration Tests
? Test-PhaseAlpha-Integration.ps1 completed successfully

============================================================
  FINAL TEST REPORT
============================================================

Total Tests: 40
Passed: 40
Failed: 0

?? ALL PHASE ALPHA TESTS PASSED!
System Status: READY FOR ALPHA TESTING ??
```

### Option 2: Quick Test (30 seconds)
```powershell
.\Test-PhaseAlpha-QuoteLifecycle.ps1
```
Runs only the 18 core workflow tests.

---

## What If Tests Fail?

### Common Issue #1: APIs Not Running
```
? AdminAPI is not responding at https://localhost:5206
```
**Fix:** Start AdminAPI with `dotnet run`

### Common Issue #2: Test Users Don't Exist
```
? Failed to authenticate Chris: 401 Unauthorized
```
**Fix:** Run AuthServer seed script to create test users

### Common Issue #3: Old Test Data
```
? Quote status should be 'Submitted', got: 'Acknowledged'
```
**Fix:** Clear test data:
```powershell
.\Clear-TestData.ps1
.\Run-AllPhaseAlphaTests.ps1
```

---

## What Gets Tested?

### Complete Quote Workflow ?
```
Passenger (chris) submits quote
    ?
Dispatcher (diana) acknowledges
    ?
Dispatcher (diana) responds with price
    ?
Passenger (chris) accepts
    ?
Booking created automatically
```

### Security ?
- Passenger cannot acknowledge quotes (StaffOnly)
- Passenger cannot respond to quotes (StaffOnly)
- Passenger cannot accept other people's quotes (Ownership)
- Data isolation verified

### Validation ?
- Price must be > 0
- Pickup time must be in future
- Cannot skip workflow steps (FSM)
- Cannot accept quote twice

### Integration ?
- Booking links back to quote (SourceQuoteId)
- Quote appears in lists
- Driver assignment works
- Audit trails populated

---

## Reading Test Results

### ? Success
```
[TEST 5/18] Dispatcher responds with price/ETA
? Status Check: PASS (got 200)
? Validation: PASS
? OVERALL: PASS
```

### ? Failure
```
[TEST 5/18] Dispatcher responds with price/ETA
? Status Check: FAIL (expected 200, got 400)
Response: { "error": "EstimatedPrice must be greater than 0" }
? OVERALL: FAIL
```

---

## Test User Credentials

These users must exist in AuthServer:

| Username | Password | Role | Purpose |
|----------|----------|------|---------|
| chris | password | booker | Passenger (submits/accepts quotes) |
| diana | password | dispatcher | Staff (acknowledges/responds) |
| alice | password | admin | Admin (full access) |
| bob | password | booker | Second passenger (data isolation) |

---

## Quick Commands

```powershell
# Run everything (recommended)
.\Run-AllPhaseAlphaTests.ps1

# Run with stop-on-failure (faster debugging)
.\Run-AllPhaseAlphaTests.ps1 -StopOnFailure

# Run individual test suites
.\Test-PhaseAlpha-QuoteLifecycle.ps1      # Core workflow
.\Test-PhaseAlpha-ValidationEdgeCases.ps1  # Input validation
.\Test-PhaseAlpha-Integration.ps1          # System integration

# Clear test data (if tests behaving strangely)
.\Clear-TestData.ps1
```

---

## Success Criteria

**You're ready to deploy when:**
- ? All 40 tests pass
- ? No warnings in output
- ? Total duration < 150 seconds
- ? All APIs responding

**Expected Test Results:**
```
Total Tests: 40
Passed: 40 ?
Failed: 0
```

---

## Need Help?

1. **Check test output** - Error messages explain what's wrong
2. **Verify prerequisites** - Both APIs must be running
3. **Clear test data** - `.\Clear-TestData.ps1`
4. **Review logs** - Check AdminAPI and AuthServer console output
5. **Read full docs** - See `README-PhaseAlpha-Tests.md`

---

## Typical Session

```powershell
# Terminal 1: Start AdminAPI
cd Bellwood.AdminApi
dotnet run

# Terminal 2: Start AuthServer  
cd path/to/AuthServer
dotnet run

# Terminal 3: Run tests
cd Bellwood.AdminApi/Scripts
.\Run-AllPhaseAlphaTests.ps1

# ? See results in ~2 minutes
# ?? All tests pass? You're ready for alpha!
```

---

**Time Required:** 4-5 minutes total  
**Test Duration:** ~2 minutes  
**Confidence:** Production-ready when all tests pass ??

**Pro Tip:** Run tests after every code change to catch issues early! ??
