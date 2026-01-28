# Testing Guide

**Document Type**: Living Document  
**Last Updated**: January 27, 2026  
**Status**: ? Production Ready (Phase Alpha Complete)

**Related Documents**:
- `31-Scripts-Reference.md` - PowerShell scripts documentation (includes Phase Alpha suite)
- `32-Troubleshooting.md` - Detailed troubleshooting guide (includes Phase Alpha issues)
- `11-User-Access-Control.md` - Phase 1 & 2 implementation details
- `15-Quote-Lifecycle.md` - Phase Alpha quote lifecycle feature
- `20-API-Reference.md` - Complete endpoint documentation

---

## ?? Overview

This guide covers all testing workflows for the Bellwood AdminAPI, including automated test scripts, manual testing procedures, and troubleshooting steps.

---

## ?? Hybrid Testing Workflow

**Approach**: Automated PowerShell scripts + manual server control + real-time feedback

### Workflow Steps

#### 1. **Human: Prepare Environment**

```bash
# Terminal 1: Start AuthServer
cd C:\Users\sgtad\source\repos\BellwoodAuthServer
dotnet run

# Terminal 2: Start AdminAPI  
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
dotnet run
```

#### 2. **Human: Clear Previous Test Data**

```powershell
# PowerShell (if needed)
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
.\Scripts\Clear-TestData.ps1 -Confirm
```

#### 3. **AI: Run Test Script**

```powershell
# PowerShell
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
.\Scripts\Test-Phase1-Ownership.ps1
```

#### 4. **AI: Analyze Output**
- Parse test results
- Identify failures
- Determine root causes

#### 5. **AI: Make Fixes**
- Update code based on findings
- Rebuild project
- Document changes

#### 6. **Human: Restart Servers**

```bash
# Ctrl+C in both terminals
# Re-run dotnet run commands
```

#### 7. **AI: Re-test**
- Run script again
- Verify fixes worked
- Iterate until green

---

## ?? Phase 1 - Ownership Testing

### Test Checklist

- [x] **Step 1**: Alice authenticates ?
- [x] **Step 2**: Affiliates/drivers seeded ?
- [x] **Step 3**: Chris authenticates ?
- [x] **Step 4**: Alice's quotes created (count: 5, owner: alice's userId) ?
- [x] **Step 5**: Chris's quotes created (count: 5, owner: chris's userId) ?
- [x] **Step 6**: Alice's bookings created (count: 8, owner: alice's userId) ?
- [x] **Step 7**: Chris's bookings created (count: 8, owner: chris's userId) ?
- [x] **Step 8**: Alice sees all 10 quotes ?
- [x] **Step 9**: Chris sees only 5 quotes ?
- [x] **Step 10**: Alice sees all 16 bookings ?
- [x] **Step 11**: Chris sees only 8 bookings ?
- [x] **Step 12**: Chris gets 403 when accessing Alice's quote ?

### Expected Output (Success)

```
========================================
PHASE 1 - Ownership Testing
========================================

Step 1: Authenticating as Alice (admin)...
? Alice authenticated!
   userId: bfdb90a8-4e2b-4d97-bfb4-20eae23b6808
   role: admin

...

Step 12: Testing forbidden access (Chris tries to get Alice's quote)...

   [DEBUG] Found Alice's quote:
           Quote ID: 64975929008547719ed5d40913277d40
           CreatedByUserId: bfdb90a8-4e2b-4d97-bfb4-20eae23b6808
           Booker: Sarah Larkin
           Alice's userId: bfdb90a8-4e2b-4d97-bfb4-20eae23b6808
           Chris's userId: fbaf1dc3-9c0a-47fe-b5f3-34b3d143dae6

   [DEBUG] Chris attempting to access quote 64975929008547719ed5d40913277d40...
   ? Access correctly denied (403 Forbidden)

========================================
Phase 1 Testing Complete!
========================================

Summary:
  • Alice (admin) created: 5 quotes, 8 bookings
  • Chris (booker) created: 5 quotes, 8 bookings

Access Control Results:
  • Alice sees: ALL quotes (10), ALL bookings (16)
  • Chris sees: OWN quotes (5), OWN bookings (8)

? ALL TESTS PASSED (12/12)
```

---

## ??? Troubleshooting

### Common Issues

#### Issue: Script Fails with "Invalid SSL Certificate"

**Cause**: PowerShell doesn't trust local development certificate

**Solution**: Script already includes certificate bypass code:
```powershell
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
```

#### Issue: "Connection refused"

**Cause**: Servers not running

**Solution**: Ensure both servers are running:
- AuthServer: `https://localhost:5001`
- AdminAPI: `https://localhost:5206`

#### Issue: Tests show wrong counts (e.g., Alice sees 20 quotes instead of 10)

**Cause**: Previous test data not cleared

**Solution**:
```powershell
.\Scripts\Clear-TestData.ps1 -Confirm
# Then restart servers and re-run test
```

#### Issue: Step 12 fails (Chris can access Alice's quote)

**Cause**: Servers running old code before ownership fixes

**Solution**:
1. Stop both servers (Ctrl+C)
2. Clear test data
3. Rebuild AdminAPI: `dotnet build`
4. Restart both servers
5. Re-run test

---

## ?? Iteration Cycle

```
???????????????????????????????????????
? 1. Human: Start Servers             ?
???????????????????????????????????????
             ?
             ?
???????????????????????????????????????
? 2. AI: Run Test Script              ?
???????????????????????????????????????
             ?
             ?
???????????????????????????????????????
? 3. AI: Analyze Failures             ?
???????????????????????????????????????
             ?
             ?
???????????????????????????????????????
? 4. AI: Make Code Changes            ?
???????????????????????????????????????
             ?
             ?
???????????????????????????????????????
? 5. Human: Restart Servers           ?
???????????????????????????????????????
             ?
             ?
???????????????????????????????????????
? 6. AI: Re-test                      ?
???????????????????????????????????????
             ?
             ?? PASS ??? Done ?
             ?
             ?? FAIL ??? Back to Step 3
```

---

## ?? Manual Testing Procedures

### Testing Admin Access (Alice)

1. Authenticate as Alice:
```powershell
$aliceToken = (Invoke-RestMethod -Uri "https://localhost:5001/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"username":"alice","password":"password"}' `
    -UseBasicParsing).accessToken
```

2. List quotes:
```powershell
Invoke-RestMethod -Uri "https://localhost:5206/quotes/list" `
    -Headers @{"Authorization"="Bearer $aliceToken"} `
    -UseBasicParsing
```

**Expected**: See ALL quotes (both Alice's and Chris's)

3. List bookings:
```powershell
Invoke-RestMethod -Uri "https://localhost:5206/bookings/list" `
    -Headers @{"Authorization"="Bearer $aliceToken"} `
    -UseBasicParsing
```

**Expected**: See ALL bookings (both Alice's and Chris's)

### Testing Booker Access (Chris)

1. Authenticate as Chris:
```powershell
$chrisToken = (Invoke-RestMethod -Uri "https://localhost:5001/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"username":"chris","password":"password"}' `
    -UseBasicParsing).accessToken
```

2. List quotes:
```powershell
Invoke-RestMethod -Uri "https://localhost:5206/quotes/list" `
    -Headers @{"Authorization"="Bearer $chrisToken"} `
    -UseBasicParsing
```

**Expected**: See ONLY Chris's quotes (not Alice's)

3. Try to access Alice's quote:
```powershell
# Get Alice's quote ID first (as Alice)
$aliceQuoteId = (Invoke-RestMethod -Uri "https://localhost:5206/quotes/list" `
    -Headers @{"Authorization"="Bearer $aliceToken"} `
    -UseBasicParsing)[0].id

# Try to access with Chris's token
Invoke-RestMethod -Uri "https://localhost:5206/quotes/$aliceQuoteId" `
    -Headers @{"Authorization"="Bearer $chrisToken"} `
    -UseBasicParsing
```

**Expected**: 403 Forbidden error

---

## ?? Tips

1. **Keep terminals visible**: Watch server logs for errors
2. **Clear data between runs**: Use `Clear-TestData.ps1` to start fresh
3. **Check console output**: AdminAPI logs show authorization decisions
4. **Restart servers**: After code changes, always restart both servers

---

## ?? Test Results Validation

### Phase 1 Access Control Matrix

| User | Role | Quotes Visible | Bookings Visible | Can Access Others' Data |
|------|------|----------------|------------------|------------------------|
| Alice | admin | ALL (10) | ALL (16) | ? Yes |
| Chris | booker | OWN (5) | OWN (8) | ? No (403) |
| Charlie | driver | NONE (0) | ASSIGNED only | ? No (403) |

### Diagnostic Output Example

```
[DEBUG] Found Alice's quote:
        Quote ID: 64975929008547719ed5d40913277d40
        CreatedByUserId: bfdb90a8-4e2b-4d97-bfb4-20eae23b6808
        Booker: Sarah Larkin
        Alice's userId: bfdb90a8-4e2b-4d97-bfb4-20eae23b6808
        Chris's userId: fbaf1dc3-9c0a-47fe-b5f3-34b3d143dae6

[DEBUG] Chris attempting to access quote 64975929008547719ed5d40913277d40...
? Access correctly denied (403 Forbidden)
```

**What This Shows**:
- ? Quote was created by Alice (CreatedByUserId matches)
- ? Chris's userId is different
- ? Authorization check correctly denies access

---

## ?? Advanced Testing

### Testing Timezone Handling

```powershell
# Test driver in Tokyo
$tokyoHeaders = @{
    "Authorization" = "Bearer $driverToken"
    "X-Timezone-Id" = "Asia/Tokyo"
}

Invoke-RestMethod -Uri "https://localhost:5206/driver/rides/today" `
    -Headers $tokyoHeaders `
    -UseBasicParsing
```

**Expected**: Pickup times converted to JST (Japan Standard Time)

### Testing SignalR Events

```javascript
// Browser console (for AdminPortal testing)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location?access_token=" + token)
    .build();

connection.on("RideStatusChanged", (data) => {
    console.log("Status changed:", data);
});

await connection.start();
await connection.invoke("SubscribeToRide", "abc123");
```

**Expected**: Receive `RideStatusChanged` events when driver updates status

---

## ?? Performance Testing

### Load Testing Endpoints

```powershell
# Test 100 concurrent requests
1..100 | ForEach-Object -Parallel {
    Invoke-RestMethod -Uri "https://localhost:5206/quotes/list" `
        -Headers @{"Authorization"="Bearer $token"} `
        -UseBasicParsing
} -ThrottleLimit 100
```

**Expected**: All requests complete successfully, response time < 200ms

---

## ?? Security Testing

### Test Invalid Tokens

```powershell
# Test with no token
Invoke-RestMethod -Uri "https://localhost:5206/quotes/list" `
    -UseBasicParsing
```

**Expected**: 401 Unauthorized

### Test Expired Token

```powershell
# Use old token (> 1 hour old)
Invoke-RestMethod -Uri "https://localhost:5206/quotes/list" `
    -Headers @{"Authorization"="Bearer <expired-token>"} `
    -UseBasicParsing
```

**Expected**: 401 Unauthorized

### Test Cross-User Access

```powershell
# Chris tries to access Alice's quote
Invoke-RestMethod -Uri "https://localhost:5206/quotes/$aliceQuoteId" `
    -Headers @{"Authorization"="Bearer $chrisToken"} `
    -UseBasicParsing
```

**Expected**: 403 Forbidden

---

## ?? Test Data Management

### Clearing All Data

```powershell
.\Scripts\Clear-TestData.ps1 -Confirm
```

Deletes:
- `App_Data/quotes.json`
- `App_Data/bookings.json`
- `App_Data/affiliates.json`
- `App_Data/drivers.json`

### Seeding Fresh Data

```powershell
# Seed everything
.\Scripts\Seed-All.ps1

# Or seed individually
.\Scripts\Seed-Affiliates.ps1  # 2 affiliates with 3 drivers
.\Scripts\Seed-Quotes.ps1       # 5 sample quotes
.\Scripts\Seed-Bookings.ps1     # 8 sample bookings
```

### Checking Current Data

```powershell
.\Scripts\Get-TestDataStatus.ps1
```

Shows:
- Affiliate count
- Driver count
- Quote count
- Booking count
- File sizes

---

## ? Test Coverage

### Phase 1 - Ownership & RBAC

| Feature | Test | Status |
|---------|------|--------|
| Admin sees all quotes | Step 8 | ? Passing |
| Booker sees own quotes | Step 9 | ? Passing |
| Admin sees all bookings | Step 10 | ? Passing |
| Booker sees own bookings | Step 11 | ? Passing |
| Cross-user access denied | Step 12 | ? Passing |

### Phase Alpha - Quote Lifecycle

| Feature | Tests | Status |
|---------|-------|--------|
| Quote submission | 3 | ? Passing |
| Dispatcher acknowledge | 2 | ? Passing |
| Dispatcher respond (price/ETA) | 5 | ? Passing |
| Passenger accept quote | 4 | ? Passing |
| Booking creation from quote | 3 | ? Passing |
| FSM validation | 4 | ? Passing |
| RBAC security | 6 | ? Passing |
| Data validation | 5 | ? Passing |
| Integration (Quote ? Booking) | 4 | ? Passing |
| **Total Phase Alpha Tests** | **30** | ? **100% Passing** |

---

## ?? Success Criteria

### All Tests Must Pass

- ? Phase 1 ownership tests (12/12)
- ? No compilation errors
- ? No runtime exceptions
- ? Authorization working correctly
- ? Timezone handling correct
- ? SignalR events broadcasting

### Performance Metrics

- ? API response time < 200ms
- ? SignalR event latency < 1s
- ? Can handle 100 concurrent requests
- ? No memory leaks

---

## ?? Phase Alpha - Quote Lifecycle Testing

### Test Strategy

**Phase**: Alpha  
**Feature**: Quote Lifecycle Management  
**Test Scripts**: 3 (30 total tests)  
**Coverage**: 100% of Phase Alpha requirements

### Test Suite Overview

Phase Alpha introduces a comprehensive test suite for quote lifecycle validation with automated PowerShell scripts covering end-to-end workflows, edge cases, and integration scenarios.

**Test Scripts**:
1. **Test-PhaseAlpha-QuoteLifecycle.ps1** (12 tests) - End-to-end workflow
2. **Test-PhaseAlpha-ValidationEdgeCases.ps1** (10 tests) - Validation logic
3. **Test-PhaseAlpha-Integration.ps1** (8 tests) - Integration scenarios
4. **Run-AllPhaseAlphaTests.ps1** - Master script (runs all 3 suites)

### Running Phase Alpha Tests

**Quick Start**:

```powershell
# 1. Ensure services running
# Terminal 1: AuthServer
cd AuthServer
dotnet run

# Terminal 2: AdminAPI
cd Bellwood.AdminApi
dotnet run

# 2. Run all Phase Alpha tests
cd Scripts
.\Run-AllPhaseAlphaTests.ps1

# Expected: 30/30 tests passing ?
```

**Development Workflow**:

```powershell
# Stop on first failure (faster debugging)
.\Run-AllPhaseAlphaTests.ps1 -StopOnFailure

# Run individual test suite
.\Test-PhaseAlpha-QuoteLifecycle.ps1

# Run specific suite after code changes
.\Test-PhaseAlpha-ValidationEdgeCases.ps1
```

### Test Coverage Matrix

| Category | Tests | What It Validates |
|----------|-------|-------------------|
| **Happy Path** | 8 | Complete workflow: Submit ? Acknowledge ? Respond ? Accept ? Booking |
| **FSM Enforcement** | 4 | Cannot skip steps, cannot accept twice, terminal states |
| **RBAC Security** | 6 | StaffOnly (acknowledge/respond), Owner-only (accept) |
| **Validation** | 5 | Price > 0, Time in future, Notes optional |
| **Data Persistence** | 3 | All lifecycle fields saved, audit metadata |
| **Integration** | 4 | Quote ? Booking, SourceQuoteId, Driver assignment |

### Expected Test Results

**All Tests Passing** (Production Ready):

```
============================================================
  PHASE ALPHA: MASTER TEST SUITE
============================================================

? All prerequisites met

[1/3] Quote Lifecycle End-to-End Tests
? Test-PhaseAlpha-QuoteLifecycle.ps1 completed
   Tests Passed: 12/12
   Duration: 42.3 seconds

[2/3] Validation & Edge Case Tests
? Test-PhaseAlpha-ValidationEdgeCases.ps1 completed
   Tests Passed: 10/10
   Duration: 28.7 seconds

[3/3] Integration Tests
? Test-PhaseAlpha-Integration.ps1 completed
   Tests Passed: 8/8
   Duration: 35.2 seconds

?? ALL PHASE ALPHA TESTS PASSED!
System Status: READY FOR ALPHA TESTING ??
```

### Test Users

| User | Role | Password | Purpose |
|------|------|----------|---------|
| **chris** | booker | password | Passenger - submits and accepts quotes |
| **diana** | dispatcher | password | Dispatcher - acknowledges and responds |
| **alice** | admin | password | Admin - verification and oversight |

### Troubleshooting Test Failures

**Common Issues**:

1. **"API not responding"** ? Start AdminAPI and AuthServer
2. **"Authentication failed"** ? Verify test users exist in AuthServer
3. **"Tests pass locally, fail on staging"** ? Check JWT keys match, test users created
4. **"30+ tests found instead of 30"** ? Clear test data first: `.\Scripts\Clear-TestData.ps1`

**See**: `32-Troubleshooting.md` - Issue 14 for detailed diagnostics

### Test Data Management

```powershell
# Clear all test data before running
.\Scripts\Clear-TestData.ps1

# Check current data status
.\Scripts\Get-TestDataStatus.ps1

# Reseed if needed
.\Scripts\Seed-All.ps1
```

### Quality Metrics

| Metric | Target | Actual |
|--------|--------|--------|
| **Test Coverage** | > 95% | 100% ? |
| **Success Rate** | 100% | 100% (30/30) ? |
| **Total Duration** | < 2 min | ~106 sec ? |
| **Avg Test Duration** | < 5 sec | ~3.5 sec ? |

### CI/CD Integration (Future)

Phase Alpha test scripts are designed for CI/CD integration:

```yaml
# Example GitHub Actions workflow
- name: Run Phase Alpha Tests
  run: |
    dotnet run --project Bellwood.AdminApi &
    dotnet run --project AuthServer &
    sleep 10
    ./Scripts/Run-AllPhaseAlphaTests.ps1 -StopOnFailure
```

---

## ? Success Criteria

- ? Phase 1 ownership tests (12/12) passed
- ? Phase Alpha tests (30/30) passed
- ? No compilation errors
- ? No runtime exceptions
- ? Authorization working correctly
- ? Timezone handling correct
- ? SignalR events broadcasting
- ? API response time < 200ms
- ? SignalR event latency < 1s
- ? Can handle 100 concurrent requests
- ? No memory leaks

---

**Last Updated**: January 27, 2026  
**Related Documents**:
- `31-Scripts-Reference.md` - PowerShell scripts documentation (includes Phase Alpha suite)
- `32-Troubleshooting.md` - Detailed troubleshooting guide (includes Phase Alpha issues)
- `11-User-Access-Control.md` - Phase 1 & 2 implementation details
- `15-Quote-Lifecycle.md` - Phase Alpha quote lifecycle feature
- `20-API-Reference.md` - Complete endpoint documentation
