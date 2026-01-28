# AdminAPI Authorization Issue - Detailed Report

**Date**: January 20, 2026  
**Reported By**: AdminPortal Development Team  
**Severity**: ?? **HIGH** - Blocks Phase 3 audit logging functionality  
**Status**: Requires AdminAPI Team Investigation

---

## ?? Issue Summary

**Endpoint**: `PUT /api/admin/users/{username}/role`  
**Expected Behavior**: Accept request from authenticated admin user and update role  
**Actual Behavior**: Returns 401 Unauthorized despite valid admin JWT token  
**Impact**: Role changes fail, blocking audit log generation and testing

---

## ?? Evidence

### AdminPortal Logs (Client Side)

```
[UserManagement] Updating role for charlie to dispatcher via AdminAPI
info: System.Net.Http.HttpClient.AdminAPI.LogicalHandler[100]
      Start processing HTTP request PUT https://localhost:5206/api/admin/users/charlie/role
info: System.Net.Http.HttpClient.AdminAPI.ClientHandler[101]
      Received HTTP response headers after 191.6959ms - 401
info: System.Net.Http.HttpClient.AdminAPI.LogicalHandler[101]
      End processing HTTP request after 191.9341ms - 401
fail: Bellwood.AdminPortal.Services.UserManagementService[0]
      [UserManagement] Role update failed: Unauthorized - {
        "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        "title": "Failed to update user role",
        "status": 401,
        "detail": ""
      }
```

### AdminAPI Logs (Server Side)

```
? Token VALIDATED successfully
   User: alice
   Claims: sub=alice, uid=bfdb90a8-4e2b-4d97-bfb4-20eae23b6808, userId=bfdb90a8-4e2b-4d97-bfb4-20eae23b6808, role=admin, email=alice.admin@bellwood.example, exp=1768958628
   IsAuthenticated: True
   ? Role found: admin

? Audit: User.RoleAssignment by alice (admin) on User charlie - Failed
warn: Bellwood.AdminApi.Middleware.ErrorTrackingMiddleware[0]
      Unauthorized access attempt: PUT /api/admin/users/charlie/role from ::1 - User: alice
```

---

## ?? Analysis

### What's Working ?

1. **JWT Token Validation**: AdminAPI successfully validates the JWT token
2. **User Authentication**: AdminAPI recognizes alice is authenticated
3. **Role Extraction**: AdminAPI correctly extracts "admin" role from JWT claims
4. **Endpoint Routing**: Request reaches the correct controller endpoint
5. **Request Headers**: All required headers present (Authorization, X-Admin-ApiKey)

### What's Failing ?

1. **Authorization Check**: Despite valid admin role, authorization fails
2. **401 Status Code**: Returns Unauthorized (should be 200 OK)
3. **Audit Log Created**: Marked as "Failed" instead of "Success"
4. **Error Tracking**: Logged as "unauthorized access attempt" for valid admin

---

## ?? Suspected Root Causes

### Cause #1: Missing `RoleClaimType` Configuration (Most Likely)

**Symptom**: JWT validation succeeds, but role-based authorization fails

**Explanation**:
- AdminAPI validates JWT token ?
- AdminAPI extracts `role` claim from JWT ?
- AdminAPI does NOT map `role` claim to `ClaimsPrincipal.IsInRole()` ?
- `[Authorize(Roles = "admin")]` fails because role claim not mapped ?

**Fix Required in AdminAPI `Program.cs` or `Startup.cs`**:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:5001"; // AuthServer URL
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            
            // ? ADD THESE TWO LINES:
            RoleClaimType = "role",  // Maps "role" claim to User.IsInRole()
            NameClaimType = "sub"    // Maps "sub" claim to User.Identity.Name
        };
    });
```

**Why This Matters**:
- By default, .NET expects role claim to be named `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`
- AuthServer uses short claim name: `role`
- Without `RoleClaimType = "role"`, .NET can't find the role
- `[Authorize(Roles = "admin")]` fails even though role is in the JWT

---

### Cause #2: Incorrect Authorization Attribute (Less Likely)

**Check Controller Endpoint**:

```csharp
// ? WRONG (if using generic policy without role mapping)
[Authorize(Policy = "AdminOnly")]
[HttpPut("api/admin/users/{username}/role")]
public async Task<IActionResult> UpdateUserRole(...)

// ? CORRECT (if RoleClaimType is configured)
[Authorize(Roles = "admin")]
[HttpPut("api/admin/users/{username}/role")]
public async Task<IActionResult> UpdateUserRole(...)
```

**If using policy**, verify it's configured correctly:

```csharp
// ? WRONG
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("role", "admin")); // Uses RequireClaim
});

// ? CORRECT
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin")); // Uses RequireRole (simpler)
});
```

---

### Cause #3: Middleware Order Issue (Least Likely)

**Verify middleware order in AdminAPI `Program.cs`**:

```csharp
app.UseAuthentication();  // Must come before UseAuthorization
app.UseAuthorization();   // Must come after UseAuthentication
app.MapControllers();     // Must come after both
```

**Wrong order causes**:
- 401 Unauthorized even with valid token
- Authorization checks fail silently

---

## ?? Diagnostic Steps

### Step 1: Verify JWT Claims in AdminAPI

**Add temporary logging in AdminAPI controller**:

```csharp
[Authorize(Roles = "admin")]
[HttpPut("api/admin/users/{username}/role")]
public async Task<IActionResult> UpdateUserRole(string username, [FromBody] UpdateRoleRequest request)
{
    // Temporary diagnostic logging
    _logger.LogInformation("User: {User}", User.Identity?.Name);
    _logger.LogInformation("IsAuthenticated: {Auth}", User.Identity?.IsAuthenticated);
    _logger.LogInformation("Claims: {Claims}", string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
    _logger.LogInformation("IsInRole('admin'): {InRole}", User.IsInRole("admin"));
    
    // ... rest of method
}
```

**Expected Output** (if RoleClaimType configured correctly):
```
User: alice
IsAuthenticated: True
Claims: sub=alice, uid=..., role=admin, email=...
IsInRole('admin'): True  ? Should be TRUE
```

**Actual Output** (if RoleClaimType NOT configured):
```
User: alice
IsAuthenticated: True
Claims: sub=alice, uid=..., role=admin, email=...
IsInRole('admin'): False  ? Currently FALSE (hence 401)
```

---

### Step 2: Test Endpoint Directly

**PowerShell Test Script**:

```powershell
# Login to get token
$loginResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/login" `
    -Method Post `
    -Body (@{username="alice";password="password"} | ConvertTo-Json) `
    -ContentType "application/json" `
    -SkipCertificateCheck

$token = $loginResponse.accessToken
Write-Host "Token obtained: $($token.Substring(0, 20))..." -ForegroundColor Green

# Attempt role update
$roleUpdate = @{
    role = "dispatcher"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "https://localhost:5206/api/admin/users/charlie/role" `
        -Method Put `
        -Headers @{
            "Authorization" = "Bearer $token"
            "X-Admin-ApiKey" = "dev-secret-123"
        } `
        -Body $roleUpdate `
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

## ?? Recommended Fix

**Priority 1**: Add `RoleClaimType` to JWT configuration

**In AdminAPI `Program.cs`** (or wherever JWT authentication is configured):

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:5001";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "role",  // ? ADD THIS LINE
            NameClaimType = "sub"
        };
    });
```

**Expected Result After Fix**:
```
PUT /api/admin/users/charlie/role ? 200 OK ?
Audit: User.RoleAssignment by alice (admin) on User charlie - Success ?
Role updated in AuthServer ?
AdminPortal receives success response ?
```

---

## ?? Verification Checklist

After applying fix, verify:

- [ ] PUT `/api/admin/users/{username}/role` returns 200 OK
- [ ] Audit log shows "Success" instead of "Failed"
- [ ] No "unauthorized access attempt" warnings
- [ ] AdminPortal receives success response
- [ ] Role change persists in AuthServer
- [ ] User can see role change in User Management page

---

## ?? Related Information

**Working Endpoints** (for comparison):
- `GET /api/admin/users` - Works fine (same admin requirement)
- `GET /api/admin/audit-logs` - Works fine (same admin requirement)

**Timeline**:
- **Before**: Role changes via `PUT https://localhost:5001/api/admin/users/{username}/role` (AuthServer direct) ? Worked ?
- **After**: Role changes via `PUT https://localhost:5206/api/admin/users/{username}/role` (AdminAPI proxy) ? Fails with 401 ?

**AdminPortal Code** (verified correct):
- Uses identical pattern to other AdminAPI services
- Sends correct headers (Authorization, X-Admin-ApiKey)
- Payload format matches API specification

---

## ?? Contact

**Reported By**: AdminPortal Development Team  
**Date**: January 20, 2026  
**Urgency**: High - Blocks Phase 3 testing

**Questions?**
- AdminPortal logs available
- Can provide additional diagnostic data
- Available for testing once fix is deployed

---

## ?? Summary

**Issue**: AdminAPI endpoint returns 401 Unauthorized for valid admin user  
**Root Cause**: Missing `RoleClaimType = "role"` in JWT configuration  
**Fix**: Add one line to JWT authentication configuration  
**Impact**: Once fixed, all role changes will create audit logs automatically  
**Priority**: High - Blocks alpha testing preparation

---

**Thank you for investigating this issue!** The AdminPortal is ready and waiting for this fix to proceed with Phase 3 testing. ??

---

**Last Updated**: January 20, 2026  
**Status**: Awaiting AdminAPI Team Fix  
**Tracking**: Phase 3 - Audit Logging Implementation
