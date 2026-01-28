# Phase Alpha Troubleshooting - Add to 32-Troubleshooting.md

**To be added after line 642 in `32-Troubleshooting.md`**

---

## ?? Phase Alpha Troubleshooting

### Issue 11: CreatedByUserId not populated on quotes/bookings

**Symptom**: `CreatedByUserId` field is null on newly created quotes or bookings.

**Cause**: JWT token doesn't include `userId` or `uid` claim.

**Solution**: AuthServer must include `uid` claim in JWT. AdminAPI extracts `userId` from `uid` claim using `GetUserId()` helper.

**Fix Applied**: ? Complete (using `uid` claim from JWT)

---

### Issue 12: Lifecycle fields missing from `GET /quotes/{id}` response

**Symptom**: `AcknowledgedAt`, `RespondedAt`, `EstimatedPrice` not returned in quote detail API.

**Solution**: Return full `QuoteRecord` from `GET /quotes/{id}` endpoint.

**Fix Applied**: ? Complete (all lifecycle fields now returned)

---

### Issue 13: `SourceQuoteId` missing from booking detail

**Symptom**: Booking created from quote, but `sourceQuoteId` is null.

**Solution**: Set `SourceQuoteId` when creating booking from quote acceptance.

**Fix Applied**: ? Complete (`SourceQuoteId` populated on quote acceptance)

---

### Issue 14: Admin can accept quotes on behalf of passengers (CRITICAL SECURITY BUG!)

**Symptom**: Admin user can accept any quote, even those created by other users.

**Impact**: **CRITICAL** - Allows fraudulent quote acceptance, bypassing ownership checks.

**Solution**: Block staff from accepting quotes - **only the booker who created the quote can accept it**.

**Fix Applied**: ? Complete (staff now get 403 Forbidden when attempting to accept quotes)

**Security Impact**:
- Prevents fraudulent quote acceptance
- Strengthens Phase 1 ownership model
- Ensures passenger consent before booking creation

---

### Issue 15: DateTime validation too strict (near-future times rejected)

**Symptom**: Valid pickup times (e.g., 30 seconds from now) rejected as "not in future".

**Cause**: Clock skew between test script and server.

**Solution**: Add 1-minute grace period for clock skew tolerance.

**Fix Applied**: ? Complete (1-minute grace period added to time validation)

---

## ?? Phase Alpha Test Failures

### All Tests Passing But Email Not Received

**Symptom**: Tests show `30/30 passing`, but passenger doesn't receive quote response email.

**Possible Causes**:
1. Email service exception (non-blocking)
2. SMTP credentials incorrect
3. Email in spam folder
4. Email address typo

**Diagnostic**: Check AdminAPI logs for email errors

**Solution**: See **Issue 10: SMTP email sending fails** above.

**Note**: Email failures are non-blocking - quote response is saved even if email fails.

---

### Tests Pass Locally But Fail on Staging

**Symptom**:
```
? Local: 30/30 passing
? Staging: 12/30 passing
```

**Possible Causes**:
1. Test users don't exist in staging AuthServer
2. JWT signing key mismatch
3. CORS configuration different
4. Data files not empty on staging

**Solution**:
1. Create test users in staging AuthServer
2. Verify JWT keys match between AdminAPI and AuthServer
3. Clear staging data before running tests

---

## ?? Quick Diagnostic Commands

```powershell
# Health checks
curl https://localhost:5206/health
curl https://localhost:5001/health

# Check data status
.\Scripts\Get-TestDataStatus.ps1

# Clear and reseed data
.\Scripts\Clear-TestData.ps1
.\Scripts\Seed-All.ps1

# Run Phase Alpha tests
.\Scripts\Run-AllPhaseAlphaTests.ps1

# Check logs
Get-Content ./logs/stdout*.log -Tail 50
```

---

**Phase Alpha Troubleshooting**: ? Complete  
**Critical Fixes Applied**: 5  
**Last Updated**: January 27, 2026
