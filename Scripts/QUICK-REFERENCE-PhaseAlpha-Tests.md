# Phase Alpha Testing - Quick Reference Card

## ?? Quick Start Commands

### Run Everything
```powershell
cd Scripts
.\Run-AllPhaseAlphaTests.ps1
```

### Run With Stop-on-Failure
```powershell
.\Run-AllPhaseAlphaTests.ps1 -StopOnFailure
```

### Run Individual Suites
```powershell
.\Test-PhaseAlpha-QuoteLifecycle.ps1      # Core workflow (18 tests)
.\Test-PhaseAlpha-ValidationEdgeCases.ps1  # Input validation (12 tests)
.\Test-PhaseAlpha-Integration.ps1          # System integration (10 tests)
```

---

## ?? Test Suite Overview

| Script | Tests | Focus | Duration |
|--------|-------|-------|----------|
| **QuoteLifecycle** | 18 | End-to-end workflow, FSM, RBAC | ~30-45s |
| **ValidationEdgeCases** | 12 | Input validation, edge cases | ~25-35s |
| **Integration** | 10 | System integration, data isolation | ~30-40s |
| **Master (All)** | 40 | Complete coverage | ~90-120s |

---

## ?? What Gets Tested

### ? Quote Lifecycle Workflow
1. Passenger submits quote ? **Submitted**
2. Dispatcher acknowledges ? **Acknowledged**
3. Dispatcher responds with price/ETA ? **Responded**
4. Passenger accepts ? **Accepted** + Booking created
5. (Optional) Passenger/Staff cancels ? **Cancelled**

### ? Security & RBAC
- Passenger cannot acknowledge/respond (StaffOnly)
- Staff cannot accept others' quotes (Ownership)
- Data isolation between users
- Admin privilege verification

### ? Validation Rules
- Price must be > 0
- Pickup time must be in future
- Notes field optional (handles long text)
- FSM prevents invalid state transitions

### ? Integration Points
- Quote ? Booking linkage (SourceQuoteId)
- Driver assignment to quote-originated bookings
- Quote list filtering
- Audit trail population

---

## ?? Test Users (AuthServer)

| Username | Password | Role | Used For |
|----------|----------|------|----------|
| chris | password | booker | Passenger (submits/accepts quotes) |
| diana | password | dispatcher | Staff (acknowledges/responds) |
| alice | password | admin | Admin (full access) |
| bob | password | booker | Second passenger (data isolation tests) |

---

## ?? Expected Results

### ? SUCCESS (All Tests Pass)
```
Total Tests: 40
Passed: 40
Failed: 0

?? ALL PHASE ALPHA TESTS PASSED!
System Status: READY FOR ALPHA TESTING ??
```

### ? FAILURE (Some Tests Fail)
```
Total Tests: 40
Passed: 35
Failed: 5

??  SOME TESTS FAILED
Action Required:
  1. Review failed test output above
  2. Fix identified issues in code
  3. Re-run test suite
```

---

## ?? Common Issues & Fixes

| Issue | Solution |
|-------|----------|
| **AdminAPI not responding** | Ensure `dotnet run` is running in AdminAPI project |
| **AuthServer not responding** | Verify AuthServer is running on port 5001 |
| **Authentication failed** | Run AuthServer seed script to create test users |
| **Quote/Booking not found** | Run `Clear-TestData.ps1` to reset state |
| **Tests pass individually but fail together** | Use `-SkipSetup` flag or add delays |

---

## ?? Test Progression

```
1. Prerequisites Check
   ?? API Availability ?
   ?? AuthServer Availability ?

2. Authentication
   ?? Chris (Passenger) ?
   ?? Diana (Dispatcher) ?
   ?? Alice (Admin) ?

3. Quote Lifecycle Tests (18)
   ?? Submit ? Acknowledge ? Respond ? Accept ?
   ?? Booking Creation ?
   ?? FSM Validation ?
   ?? RBAC Enforcement ?

4. Validation Tests (12)
   ?? Price Validation ?
   ?? Time Validation ?
   ?? Data Persistence ?
   ?? Audit Metadata ?

5. Integration Tests (10)
   ?? Quote Lists ?
   ?? Driver Assignment ?
   ?? Data Isolation ?
   ?? Happy Path ?

6. Final Report
   ?? Summary Statistics
   ?? Detailed Results
   ?? PASS/FAIL Verdict
```

---

## ?? Reading Test Output

### Status Indicators
```
? PASS       - Test succeeded
? FAIL       - Test failed
??  WARNING   - Non-critical issue
? INFO        - Informational message
? STEP        - Test progression
????????????  - Section separator
```

### Test Flow Example
```
[TEST 5/18] Dispatcher responds with price/ETA
???????????????????????????????????????????????
? Description: Diana sends price ($125.50) and ETA
? Endpoint: POST /quotes/{id}/respond
? Response (200): { status: "Responded", ... }
? Status Check: PASS
? Validation: PASS
? OVERALL: PASS
```

---

## ?? Parameters Reference

### Master Script (`Run-AllPhaseAlphaTests.ps1`)
```powershell
-ApiBaseUrl <url>      # Default: https://localhost:5206
-AuthServerUrl <url>   # Default: https://localhost:5001
-StopOnFailure         # Stop at first failure
-SkipSetup             # Skip data cleanup
```

### Individual Scripts
```powershell
-ApiBaseUrl <url>      # Default: https://localhost:5206
-AuthServerUrl <url>   # Default: https://localhost:5001
```

---

## ?? Typical Development Workflow

```powershell
# 1. Make code changes
# ... edit files ...

# 2. Run quick test
.\Test-PhaseAlpha-QuoteLifecycle.ps1

# 3. If passes, run full suite
.\Run-AllPhaseAlphaTests.ps1

# 4. If all pass, commit changes
git add .
git commit -m "feat: implement quote lifecycle feature"

# 5. Before pushing, run one final check
.\Run-AllPhaseAlphaTests.ps1 -StopOnFailure
```

---

## ?? Need Help?

1. **Check test output** for specific error messages
2. **Verify prerequisites** (APIs running, test users exist)
3. **Review README-PhaseAlpha-Tests.md** for detailed docs
4. **Check logs** in AdminAPI and AuthServer console
5. **Clear test data** and retry: `.\Clear-TestData.ps1`

---

## ? Success Criteria

Before deploying to alpha:

- [ ] All 40 tests pass ?
- [ ] No warnings in test output ?
- [ ] APIs respond within 2 seconds ?
- [ ] No database/JSON file errors ?
- [ ] Audit logs populated correctly ?

---

**Pro Tip:** Run tests after every significant code change to catch issues early! ??

**Time to Green:** ~2 minutes for full suite ??

**Confidence Level:** Production-ready when all tests pass ??
