# Alpha: Audit Log Management Endpoints

**Feature**: Admin-only audit log statistics and clearing  
**Status**: ? Implemented  
**Build**: ? Passing  
**Auth**: Admin only (`AdminOnly` policy)

---

## Overview

Two new endpoints allow administrators to view audit log statistics and safely clear all audit logs. These are primarily for testing and demo purposes during alpha development.

**?? PRODUCTION WARNING**: The clear endpoint should be **heavily restricted or removed** in production deployments.

---

## Endpoints

### 1. GET `/api/admin/audit-logs/stats`

Get audit log statistics (count and date range).

**Authorization**: Admin only  
**HTTP Method**: `GET`  
**Tags**: `Admin`, `Audit`

#### Request

```http
GET /api/admin/audit-logs/stats HTTP/1.1
Host: localhost:5206
Authorization: Bearer {admin_jwt_token}
```

#### Response (200 OK)

```json
{
  "count": 42,
  "oldestUtc": "2025-01-10T08:30:00Z",
  "newestUtc": "2025-01-15T14:22:10Z"
}
```

#### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `count` | `int` | Total number of audit log entries |
| `oldestUtc` | `DateTime?` | Timestamp of oldest log entry (null if no logs) |
| `newestUtc` | `DateTime?` | Timestamp of newest log entry (null if no logs) |

#### Error Responses

| Code | Description |
|------|-------------|
| 401 | Unauthorized (no JWT token) |
| 403 | Forbidden (user is not admin) |

#### Audit Trail

**Action**: `AuditLog.Stats.Viewed`  
**Details**:
```json
{
  "count": 42,
  "oldestUtc": "2025-01-10T08:30:00Z",
  "newestUtc": "2025-01-15T14:22:10Z"
}
```

---

### 2. POST `/api/admin/audit-logs/clear`

Clear all audit logs (requires confirmation).

**Authorization**: Admin only  
**HTTP Method**: `POST`  
**Tags**: `Admin`, `Audit`

**?? DANGEROUS OPERATION**: This permanently deletes ALL audit logs.

#### Request

```http
POST /api/admin/audit-logs/clear HTTP/1.1
Host: localhost:5206
Authorization: Bearer {admin_jwt_token}
Content-Type: application/json

{
  "confirm": "CLEAR"
}
```

#### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `confirm` | `string` | ? Yes | Must be exactly `"CLEAR"` (case-sensitive) |

#### Response (200 OK)

```json
{
  "deletedCount": 42,
  "clearedAtUtc": "2025-01-15T14:30:00Z",
  "clearedByUserId": "user-123",
  "clearedByUsername": "alice",
  "message": "All audit logs have been cleared successfully"
}
```

#### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `deletedCount` | `int` | Number of audit logs deleted |
| `clearedAtUtc` | `DateTime` | Timestamp when logs were cleared |
| `clearedByUserId` | `string?` | User ID who performed the action |
| `clearedByUsername` | `string` | Username who performed the action |
| `message` | `string` | Success message |

#### Error Responses

| Code | Description | Example |
|------|-------------|---------|
| 400 | Invalid confirmation phrase | `{"error": "Confirmation phrase must be exactly 'CLEAR' (case-sensitive)"}` |
| 401 | Unauthorized (no JWT token) | - |
| 403 | Forbidden (user is not admin) | - |

#### Audit Trail

**Action**: `AuditLog.Cleared`  
**Details**:
```json
{
  "deletedCount": 42,
  "clearedAtUtc": "2025-01-15T14:30:00Z",
  "clearedByUserId": "user-123",
  "clearedByUsername": "alice"
}
```

**?? NOTE**: The clear action is audited AFTER the clear operation, so this audit entry will be the ONLY entry in the system immediately after clearing.

---

## Manual Testing Steps

### Test 1: View Stats (Empty State)

**Prerequisites**: Clean database (no audit logs)

```bash
# 1. Authenticate as admin
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}' \
  | jq -r '.accessToken' > token.txt

TOKEN=$(cat token.txt)

# 2. Get stats (should be empty)
curl -X GET http://localhost:5206/api/admin/audit-logs/stats \
  -H "Authorization: Bearer $TOKEN" \
  | jq
```

**Expected Output**:
```json
{
  "count": 0,
  "oldestUtc": null,
  "newestUtc": null
}
```

---

### Test 2: View Stats (After Seeding)

**Prerequisites**: Seed test data to create audit logs

```bash
# 1. Seed quotes (creates audit logs)
curl -X POST http://localhost:5206/quotes/seed \
  -H "Authorization: Bearer $TOKEN"

# 2. Seed bookings (creates more audit logs)
curl -X POST http://localhost:5206/bookings/seed \
  -H "Authorization: Bearer $TOKEN"

# 3. Get stats (should show count and timestamps)
curl -X GET http://localhost:5206/api/admin/audit-logs/stats \
  -H "Authorization: Bearer $TOKEN" \
  | jq
```

**Expected Output**:
```json
{
  "count": 15,
  "oldestUtc": "2025-01-15T14:20:00Z",
  "newestUtc": "2025-01-15T14:25:00Z"
}
```

---

### Test 3: Clear Logs (Invalid Confirmation)

**Prerequisites**: Audit logs exist

```bash
# Attempt to clear with wrong confirmation
curl -X POST http://localhost:5206/api/admin/audit-logs/clear \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"confirm":"clear"}' \
  | jq
```

**Expected Output** (400 Bad Request):
```json
{
  "error": "Confirmation phrase must be exactly 'CLEAR' (case-sensitive)"
}
```

**Audit Trail**: Should log a failure with details about invalid confirmation.

---

### Test 4: Clear Logs (Valid Confirmation)

**Prerequisites**: Audit logs exist

```bash
# Clear all logs with correct confirmation
curl -X POST http://localhost:5206/api/admin/audit-logs/clear \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"confirm":"CLEAR"}' \
  | jq
```

**Expected Output** (200 OK):
```json
{
  "deletedCount": 15,
  "clearedAtUtc": "2025-01-15T14:30:00Z",
  "clearedByUserId": "user-123",
  "clearedByUsername": "alice",
  "message": "All audit logs have been cleared successfully"
}
```

**Verification**:
```bash
# Check stats (should show only the "cleared" audit entry)
curl -X GET http://localhost:5206/api/admin/audit-logs/stats \
  -H "Authorization: Bearer $TOKEN" \
  | jq
```

**Expected Output**:
```json
{
  "count": 1,
  "oldestUtc": "2025-01-15T14:30:00Z",
  "newestUtc": "2025-01-15T14:30:00Z"
}
```

---

### Test 5: Authorization (Non-Admin User)

**Prerequisites**: Dispatcher or booker user

```bash
# 1. Authenticate as dispatcher
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"david","password":"password"}' \
  | jq -r '.accessToken' > dispatcher_token.txt

DISPATCHER_TOKEN=$(cat dispatcher_token.txt)

# 2. Attempt to view stats (should be forbidden)
curl -X GET http://localhost:5206/api/admin/audit-logs/stats \
  -H "Authorization: Bearer $DISPATCHER_TOKEN"
```

**Expected Output** (403 Forbidden):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

---

## Integration Testing

### Test Script

Save as `Tests/Test-AuditLogManagement.ps1`:

```powershell
#Requires -Version 5.1
<#
.SYNOPSIS
    Test audit log management endpoints (stats and clear).
#>

param(
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001",
    [string]$AdminUsername = "alice",
    [string]$AdminPassword = "password"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Audit Log Management Tests" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Authenticate
Write-Host "Authenticating as $AdminUsername..." -ForegroundColor Cyan
$loginBody = @{ username = $AdminUsername; password = $AdminPassword } | ConvertTo-Json
$loginResponse = Invoke-RestMethod -Method POST -Uri "$AuthServerUrl/api/auth/login" -Body $loginBody -ContentType "application/json"
$token = $loginResponse.accessToken

# Test 1: Get stats (should have at least 1 entry from login)
Write-Host "`nTest 1: Get Audit Log Stats" -ForegroundColor Yellow
$headers = @{ Authorization = "Bearer $token" }
$stats = Invoke-RestMethod -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs/stats" -Headers $headers

Write-Host "  Count: $($stats.count)" -ForegroundColor White
Write-Host "  Oldest: $($stats.oldestUtc)" -ForegroundColor White
Write-Host "  Newest: $($stats.newestUtc)" -ForegroundColor White

if ($stats.count -gt 0) {
    Write-Host "  ? Stats retrieved successfully" -ForegroundColor Green
} else {
    Write-Host "  ? Expected at least 1 audit log" -ForegroundColor Red
    exit 1
}

# Test 2: Clear with invalid confirmation (should fail)
Write-Host "`nTest 2: Clear with Invalid Confirmation" -ForegroundColor Yellow
$invalidBody = @{ confirm = "clear" } | ConvertTo-Json  # Wrong case
try {
    Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/api/admin/audit-logs/clear" -Headers $headers -Body $invalidBody -ContentType "application/json"
    Write-Host "  ? Should have returned 400 Bad Request" -ForegroundColor Red
    exit 1
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-Host "  ? Correctly rejected invalid confirmation" -ForegroundColor Green
    } else {
        Write-Host "  ? Unexpected status code: $statusCode" -ForegroundColor Red
        exit 1
    }
}

# Test 3: Clear with valid confirmation (should succeed)
Write-Host "`nTest 3: Clear All Audit Logs" -ForegroundColor Yellow
$validBody = @{ confirm = "CLEAR" } | ConvertTo-Json
$clearResponse = Invoke-RestMethod -Method POST -Uri "$AdminApiUrl/api/admin/audit-logs/clear" -Headers $headers -Body $validBody -ContentType "application/json"

Write-Host "  Deleted: $($clearResponse.deletedCount)" -ForegroundColor White
Write-Host "  Cleared At: $($clearResponse.clearedAtUtc)" -ForegroundColor White
Write-Host "  Cleared By: $($clearResponse.clearedByUsername)" -ForegroundColor White

if ($clearResponse.deletedCount -gt 0) {
    Write-Host "  ? Audit logs cleared successfully" -ForegroundColor Green
} else {
    Write-Host "  ? Expected to delete at least 1 log" -ForegroundColor Red
    exit 1
}

# Test 4: Verify stats after clear (should show 1 entry - the clear action)
Write-Host "`nTest 4: Verify Stats After Clear" -ForegroundColor Yellow
$statsAfterClear = Invoke-RestMethod -Method GET -Uri "$AdminApiUrl/api/admin/audit-logs/stats" -Headers $headers

Write-Host "  Count: $($statsAfterClear.count)" -ForegroundColor White

if ($statsAfterClear.count -eq 1) {
    Write-Host "  ? Exactly 1 audit log remains (the clear action)" -ForegroundColor Green
} else {
    Write-Host "  ? Expected exactly 1 log, found $($statsAfterClear.count)" -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  ? ALL TESTS PASSED" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green
```

### Run Tests

```powershell
# Run all audit log management tests
.\Tests\Test-AuditLogManagement.ps1
```

---

## Security Considerations

### 1. Admin-Only Access

Both endpoints require the `AdminOnly` authorization policy:

```csharp
.RequireAuthorization("AdminOnly")
```

**? Only users with `role=admin` can access these endpoints.**

### 2. Confirmation Phrase

The clear endpoint requires an exact confirmation phrase (`"CLEAR"`) to prevent accidental deletions:

```csharp
if (request.Confirm != "CLEAR")
{
    return Results.BadRequest(new
    {
        error = "Confirmation phrase must be exactly 'CLEAR' (case-sensitive)"
    });
}
```

**? Case-sensitive, exact match required.**

### 3. Audit Trail

Even the clear action is audited:

```csharp
await auditLogger.LogSuccessAsync(
    context.User,
    AuditActions.AuditLogCleared,
    "AuditLog",
    details: new
    {
        deletedCount,
        clearedAtUtc,
        clearedByUserId = currentUserId,
        clearedByUsername = username
    },
    httpContext: context);
```

**? Record remains after clearing for accountability.**

### 4. Logging

All clear operations are logged to console:

```csharp
log.LogWarning("Audit logs cleared by {Username} ({UserId}). Deleted count: {DeletedCount}",
    username, currentUserId, deletedCount);
```

**? Operators can track clear operations in application logs.**

---

## Production Considerations

### ?? DO NOT USE IN PRODUCTION

The **clear endpoint** should be:

1. **Removed entirely** before production deployment, OR
2. **Disabled via feature flag**, OR
3. **Restricted to specific environment** (e.g., only in Development)

### Example: Environment-Based Restriction

```csharp
app.MapPost("/api/admin/audit-logs/clear", async (...) =>
{
    // Only allow in Development environment
    if (!app.Environment.IsDevelopment())
    {
        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "Audit log clearing is disabled in production");
    }

    // ... rest of implementation
})
```

### Why Clear is Dangerous in Production

1. **Compliance Violation**: Many regulations (GDPR, HIPAA, SOX) REQUIRE audit logs
2. **Accountability Loss**: Impossible to track who did what
3. **Incident Investigation**: Cannot investigate security breaches or errors
4. **Legal Evidence**: Audit logs may be required for legal proceedings

**? Use the cleanup endpoint (`DELETE /api/admin/audit-logs/cleanup`) instead for production data retention.**

---

## Summary

### ? What Was Implemented

1. **GET `/api/admin/audit-logs/stats`** - View audit log statistics
2. **POST `/api/admin/audit-logs/clear`** - Clear all audit logs (with confirmation)
3. **Repository methods**:
   - `GetStatsAsync()` - Get count and date range
   - `ClearAllAsync()` - Delete all logs
4. **Audit actions**:
   - `AuditLog.Stats.Viewed`
   - `AuditLog.Cleared`
5. **Request DTO**: `ClearAuditLogsRequest`
6. **Comprehensive documentation** with testing steps

### ? Build Status

- **Compilation**: ? Passing
- **No errors**: ? Verified
- **Repository implementation**: ? Complete
- **Endpoint implementation**: ? Complete
- **Authorization**: ? Admin-only enforced

### ? Next Steps

1. Run manual tests (see above)
2. Create PowerShell test script (see above)
3. Verify audit trail entries
4. Document in README.md
5. Add to master test orchestrator

---

**Last Updated**: January 2026  
**Author**: AI Assistant  
**Status**: Ready for Testing ?

