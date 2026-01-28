# Authorization Fix - Testing Guide

**Date**: January 20, 2026  
**Issue**: PUT /api/admin/users/{username}/role returns 401 Unauthorized  
**Status**: ? Diagnostic logging added - Ready for testing

---

## ?? **Changes Made**

### **Enhanced Diagnostic Logging**

Added comprehensive logging to the `OnForbidden` JWT event handler to capture:

- ? User identity information
- ? Authentication status
- ? All role claims
- ? **`User.IsInRole()` test results** (critical!)
- ? Complete claims list

**Location**: `Program.cs` lines ~165-180

---

## ?? **Testing Steps**

### **Step 1: Start AdminAPI**

```powershell
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
dotnet run
```

**Expected Output**:
```
? Bellwood AdminAPI starting...
   Environment: Development
   Listening on: https://localhost:5206
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5206
```

---

### **Step 2: Get Admin Token from AuthServer**

```powershell
# Login as alice (admin)
$loginResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/login" `
    -Method Post `
    -Body (@{username="alice";password="password"} | ConvertTo-Json) `
    -ContentType "application/json" `
    -SkipCertificateCheck

$token = $loginResponse.accessToken
Write-Host "Token obtained: $($token.Substring(0, 20))..." -ForegroundColor Green
```

**Expected**: Token successfully retrieved

---

### **Step 3: Attempt Role Change**

```powershell
# Try to change charlie's role to dispatcher
try {
    $response = Invoke-RestMethod -Uri "https://localhost:5206/api/admin/users/charlie/role" `
        -Method Put `
        -Headers @{
            "Authorization" = "Bearer $token"
        } `
        -Body (@{role="dispatcher"} | ConvertTo-Json) `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    Write-Host "? SUCCESS!" -ForegroundColor Green
    $response | ConvertTo-Json
} catch {
    Write-Host "? FAILED: $($_.Exception.Message)" -ForegroundColor Red
    
    # Extract status code
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "Status Code: $statusCode" -ForegroundColor Yellow
    
    # Read error body
    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    $errorBody = $reader.ReadToEnd()
    Write-Host "Error Body:" -ForegroundColor Yellow
    Write-Host $errorBody -ForegroundColor Red
}
```

---

### **Step 4: Check AdminAPI Console Output**

**Watch for this diagnostic output in the AdminAPI console**:

#### **If Token Validation Succeeds (Expected)**:

```
? Token VALIDATED successfully
   User: alice
   Claims: sub=alice, uid=bfdb90a8-4e2b-4d97-bfb4-20eae23b6808, userId=bfdb90a8-4e2b-4d97-bfb4-20eae23b6808, role=admin, email=alice.admin@bellwood.example, exp=1768958628
   IsAuthenticated: True
   ? Role found: admin
```

#### **If Authorization Fails (Current Issue)**:

```
? Authorization FORBIDDEN (403)
   User: alice
   IsAuthenticated: True
   Roles: admin
   IsInRole('admin'): ???  <--- KEY DIAGNOSTIC LINE
   IsInRole('dispatcher'): ???  <--- KEY DIAGNOSTIC LINE
   All Claims: sub=alice, uid=..., role=admin, ...
```

---

## ?? **What to Look For**

### **Scenario A: `IsInRole('admin') = false` (Root Cause Confirmed)**

**Diagnostic Output**:
```
IsInRole('admin'): False  ?
Roles: admin  ? (claim exists!)
```

**Problem**: `RoleClaimType` mapping not working despite configuration

**Possible Causes**:
1. Claim type name mismatch (e.g., JWT has `http://schemas.../role` instead of `role`)
2. `MapInboundClaims = false` not working as expected
3. Authorization policy configured incorrectly

**Fix Required**: Investigate claim type mapping in `TokenValidationParameters`

---

### **Scenario B: `IsInRole('admin') = true` (Authorization Policy Issue)**

**Diagnostic Output**:
```
IsInRole('admin'): True  ?
Roles: admin  ?
```

**Problem**: `RequireAuthorization("AdminOnly")` policy not evaluating correctly

**Possible Causes**:
1. Policy defined incorrectly in `AddAuthorization()`
2. Endpoint using wrong policy name
3. Middleware order issue

**Fix Required**: Check `AdminOnly` policy definition (line ~188 in Program.cs)

---

### **Scenario C: Token Validation Fails (Signature Issue)**

**Diagnostic Output**:
```
? Authentication FAILED: SecurityTokenInvalidSignatureException
   Message: IDX10503: Signature validation failed
```

**Problem**: JWT signing keys don't match between AuthServer and AdminAPI

**Fix Required**: Verify `Jwt:Key` in appsettings.json matches AuthServer's key

---

## ?? **Expected Diagnostic Output (Success Path)**

```
? Token VALIDATED successfully
   User: alice
   Claims: sub=alice, uid=..., userId=..., role=admin, email=..., exp=...
   IsAuthenticated: True
   ? Role found: admin

? Audit: User.RoleAssignment by alice (admin) on User charlie - Success

? Role updated successfully!
```

**No `OnForbidden` event should fire!**

---

## ?? **Next Steps Based on Results**

### **If `IsInRole('admin') = false`**

**AdminAPI Team Action**:
1. Inspect claim type in JWT token (use jwt.io to decode)
2. Verify claim name is exactly `"role"` (not `http://schemas.../role`)
3. Check if `MapInboundClaims = false` is actually applied

**Potential Fix**:
```csharp
// Try explicit claim transformation
options.TokenValidationParameters = new TokenValidationParameters
{
    // ... existing settings ...
    RoleClaimType = ClaimTypes.Role,  // Use standard claim type
    NameClaimType = ClaimTypes.Name
};
```

---

### **If `IsInRole('admin') = true`**

**AdminAPI Team Action**:
1. Verify `AdminOnly` policy exists and is correctly defined
2. Check if endpoint is using correct policy name
3. Ensure `UseAuthorization()` is after `UseAuthentication()`

**Current Policy Definition** (verify this exists in Program.cs ~188):
```csharp
options.AddPolicy("AdminOnly", policy =>
    policy.RequireRole("admin"));
```

---

## ?? **Communication Protocol**

### **For AdminPortal Team**

**Please provide**:
1. Complete console output from Step 4 (especially `IsInRole` lines)
2. HTTP status code from Step 3 (401 vs 403)
3. Full error response body

### **For AdminAPI Team**

**If Issue Confirmed**:
- Share diagnostic output showing `IsInRole('admin') = false` despite `role=admin` claim
- Investigate claim type mapping
- Test with explicit `RequireRole("admin")` instead of policy

---

## ? **Success Criteria**

- [ ] Token validation succeeds (green checkmark in console)
- [ ] Role claim extracted correctly ("Role found: admin")
- [ ] `IsInRole('admin')` returns `true`
- [ ] `PUT /api/admin/users/{username}/role` returns **200 OK**
- [ ] Audit log shows **"Success"** result
- [ ] No "unauthorized access attempt" warnings

---

## ?? **Rollback Plan**

If testing reveals unexpected behavior, rollback is simple:

```powershell
git checkout HEAD -- Program.cs
dotnet run
```

Diagnostic logging is additive - no breaking changes made.

---

**Last Updated**: January 20, 2026  
**Status**: Ready for collaborative testing  
**Goal**: Identify exact root cause of authorization failure

---

**Thank you for your collaboration! Together we'll nail this! ??**
