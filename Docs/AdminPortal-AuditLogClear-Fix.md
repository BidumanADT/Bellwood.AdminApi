# AdminPortal - Fix Required for Audit Log Clear Feature

**Date**: February 8, 2026  
**Priority**: ?? HIGH (Feature Currently Broken)  
**Component**: AdminPortal `AuditLogService.cs`

---

## ?? Problem Summary

The **Clear Audit Logs** feature in the AdminPortal is currently failing because the HTTP request is being sent **without a request body**. The AdminAPI expects a JSON body containing `{"confirm": "CLEAR"}`, but the portal is sending `Content-Length: 0`.

### Error Details

**AdminAPI Log**:
```
Required parameter "ClearAuditLogsRequest request" was not provided from body.
```

**AdminPortal Log**:
```
Content-Length: 0
```

**Expected Request**:
```http
POST https://localhost:5206/api/admin/audit-logs/clear
Content-Type: application/json
Authorization: Bearer {token}

{"confirm": "CLEAR"}
```

**Actual Request** (What you're sending):
```http
POST https://localhost:5206/api/admin/audit-logs/clear
Content-Type: application/json
Authorization: Bearer {token}
Content-Length: 0

(empty body)
```

---

## ?? Fix Required in AdminPortal

**Location**: `Services/AuditLogService.cs` ? `ClearAuditLogsAsync()` method

### Current Code (Incorrect - Missing Body)

```csharp
// ? INCORRECT - Missing body
var response = await _httpClient.PostAsync(
    "/api/admin/audit-logs/clear",
    null,  // <-- This is the problem
    cancellationToken);
```

### Corrected Code (Required)

```csharp
// ? CORRECT - Include JSON body with confirmation
var confirmRequest = new { confirm = "CLEAR" };

var jsonContent = JsonSerializer.Serialize(confirmRequest);
var content = new StringContent(
    jsonContent,
    Encoding.UTF8,
    "application/json");

var response = await _httpClient.PostAsync(
    "/api/admin/audit-logs/clear",
    content,
    cancellationToken);
```

---

## ?? API Contract

### Endpoint

**URL**: `POST /api/admin/audit-logs/clear`  
**Auth**: Admin Only  
**Content-Type**: `application/json`

### Required Headers

```http
Authorization: Bearer {adminToken}
Content-Type: application/json
```

### Required Body (JSON)

```json
{
  "confirm": "CLEAR"
}
```

**Important**: The confirmation phrase is **case-sensitive**. It must be exactly `"CLEAR"` (all uppercase).

---

## ?? Expected Responses

### Success (200 OK)

```json
{
  "deletedCount": 580,
  "clearedAtUtc": "2026-02-15T15:30:00Z",
  "clearedByUserId": "914562c8-f4d2-4bb8-ad7a-f59526356132",
  "clearedByUsername": "alice",
  "message": "All audit logs have been cleared successfully"
}
```

### Error - Invalid/Missing Confirmation (400 Bad Request)

```json
{
  "error": "Confirmation phrase must be exactly 'CLEAR' (case-sensitive)"
}
```

### Error - Unauthorized (401 Unauthorized)

```json
{
  "error": "Authentication required"
}
```

### Error - Forbidden (403 Forbidden)

```json
{
  "error": "Admin role required"
}
```

---

## ?? Complete Working Example

### Full Implementation

```csharp
public async Task<AuditLogClearResult> ClearAuditLogsAsync(CancellationToken ct = default)
{
    _logger.LogWarning("[AuditLog] CLEARING ALL AUDIT LOGS - This action is irreversible!");

    try
    {
        // Create confirmation request
        var confirmRequest = new { confirm = "CLEAR" };
        
        // Serialize to JSON
        var jsonContent = JsonSerializer.Serialize(confirmRequest);
        
        // Create HTTP content with correct content type
        var content = new StringContent(
            jsonContent,
            Encoding.UTF8,
            "application/json");

        // Send POST request with body
        var response = await _httpClient.PostAsync(
            "/api/admin/audit-logs/clear",
            content,
            ct);

        // Handle error responses
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "[AuditLog] Clear failed: {StatusCode} - {Error}",
                response.StatusCode,
                errorBody);

            return new AuditLogClearResult
            {
                Success = false,
                ErrorMessage = $"Failed to clear audit logs: {errorBody}",
                DeletedCount = 0
            };
        }

        // Parse success response
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<AuditLogClearResponse>(
            responseBody,
            _jsonOptions);

        _logger.LogInformation(
            "[AuditLog] Successfully cleared {Count} audit logs",
            result?.DeletedCount ?? 0);

        return new AuditLogClearResult
        {
            Success = true,
            DeletedCount = result?.DeletedCount ?? 0,
            ClearedAtUtc = result?.ClearedAtUtc ?? DateTime.UtcNow,
            ClearedByUsername = result?.ClearedByUsername
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[AuditLog] Exception during clear operation");

        return new AuditLogClearResult
        {
            Success = false,
            ErrorMessage = $"Exception: {ex.Message}",
            DeletedCount = 0
        };
    }
}
```

### Response DTO (Add if Missing)

```csharp
public class AuditLogClearResponse
{
    [JsonPropertyName("deletedCount")]
    public int DeletedCount { get; set; }

    [JsonPropertyName("clearedAtUtc")]
    public DateTime ClearedAtUtc { get; set; }

    [JsonPropertyName("clearedByUserId")]
    public string? ClearedByUserId { get; set; }

    [JsonPropertyName("clearedByUsername")]
    public string? ClearedByUsername { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
```

---

## ?? Guardrails & Testing

### 1. Confirmation Phrase is Case-Sensitive

- ? Must be exactly `"CLEAR"` (all caps)
- ? `"clear"`, `"Clear"`, `"CLEARME"` will all fail with 400 Bad Request

### 2. Test Cases to Verify

**Test 1: Valid Clear Request**
```bash
POST /api/admin/audit-logs/clear
Content-Type: application/json
Authorization: Bearer {adminToken}

{"confirm": "CLEAR"}
```
Expected: `200 OK` with `deletedCount` > 0

**Test 2: Invalid Confirmation**
```bash
POST /api/admin/audit-logs/clear
Content-Type: application/json
Authorization: Bearer {adminToken}

{"confirm": "clear"}
```
Expected: `400 Bad Request` with hint

**Test 3: Missing Body** (Your current issue)
```bash
POST /api/admin/audit-logs/clear
Authorization: Bearer {adminToken}

(empty body)
```
Expected: `400 Bad Request` with hint

---

## ?? Testing with cURL

**Before deploying**, test with this cURL command to verify the API works:

```bash
# Get admin token first
TOKEN=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}' \
  | jq -r '.accessToken')

# Test clear endpoint
curl -X POST https://localhost:5206/api/admin/audit-logs/clear \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"confirm":"CLEAR"}' \
  -v
```

Expected: `200 OK` with `deletedCount` in response.

---

## ?? Summary

| Component | Status | Action Required |
|-----------|--------|-----------------|
| **AdminAPI** | ? Working | No changes needed |
| **AdminPortal** | ? Broken | Add JSON body to request |

**The Issue**: Missing request body  
**The Fix**: Add JSON body with `{"confirm": "CLEAR"}`  
**Impact**: One-line change in `ClearAuditLogsAsync()`  
**Test**: Verify with cURL before deploying

---

## ?? Deployment Checklist

- [ ] Add JSON body to `ClearAuditLogsAsync()` method
- [ ] Add `AuditLogClearResponse` DTO (if missing)
- [ ] Test locally with AdminAPI running
- [ ] Verify 4 test cases pass
- [ ] Deploy to staging
- [ ] Test in staging environment
- [ ] Deploy to production

---

**Status**: ?? **READY FOR FIX**  
**Estimated Effort**: 5 minutes  
**Risk**: Low (isolated change)

**Questions?** Contact the AdminAPI team or reference:
- `Docs/20-API-Reference.md` - Complete API documentation
- `Docs/32-Troubleshooting.md` - Common issues

---

**Last Updated**: February 8, 2026  
**AdminAPI Version**: 2.0 (Phase Alpha)
