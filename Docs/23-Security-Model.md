# Security Model & Authorization

**Document Type**: Living Document - Technical Reference  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides complete security and authorization documentation for the Bellwood AdminAPI, including JWT authentication, role-based access control (RBAC), ownership verification, and encryption strategies.

**Authentication Method**: Bearer JWT tokens  
**Token Issuer**: AuthServer (`https://localhost:5001`)  
**Authorization Strategy**: Policy-based with custom ownership checks

---

## ?? Authentication

### JWT Token Format

**Header**:
```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

**Payload** (Claims):
```json
{
  "sub": "alice",                    // Username
  "uid": "user-guid-123",            // Unique user identifier
  "email": "alice@example.com",      // Email address
  "role": "admin",                   // User role
  "exp": 1735050000,                 // Expiration timestamp
  "iat": 1735046400                  // Issued at timestamp
}
```

**Signature**: HMAC-SHA256 using shared secret key

**Critical Configuration**:
```csharp
options.MapInboundClaims = false;  // ? CRITICAL: Prevents claim type remapping
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = false,
    ValidateAudience = false,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = signingKey,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero,
    RoleClaimType = "role",      // Map "role" claim to roles
    NameClaimType = "sub"        // Map "sub" claim to username
};
```

**Why `MapInboundClaims = false`?**
- Prevents ASP.NET Core from remapping "role" to "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
- Ensures `context.User.IsInRole("admin")` works correctly

---

### Required Claims

| Claim | Type | Required For | Description |
|-------|------|--------------|-------------|
| `sub` | string | All users | Username (mapped to `User.Identity.Name`) |
| `uid` | string | Drivers, ownership checks | Unique user identifier |
| `email` | string | Passengers | Email address (for ownership verification) |
| `role` | string | Staff, drivers | User role ("admin", "dispatcher", "driver", "booker") |
| `exp` | int | All users | Expiration timestamp (Unix epoch) |
| `iat` | int | All users | Issued at timestamp (Unix epoch) |

---

## ?? User Roles

### Role Hierarchy

```
admin
  ?? Full system access
  ?? Manage OAuth credentials
  ?? Seed test data
  ?? View billing information
  ?? Assign drivers

dispatcher
  ?? Operational access
  ?? View quotes/bookings
  ?? Assign drivers
  ?? View locations
  ?? Billing fields MASKED

driver
  ?? View assigned rides
  ?? Update ride status
  ?? Send location updates
  ?? No admin/billing access

booker (passenger)
  ?? Create quotes/bookings
  ?? View own quotes/bookings
  ?? Cancel own bookings
  ?? Track own rides (via email)
```

---

## ??? Authorization Policies

### Policy Definitions

**File**: `Program.cs`

```csharp
builder.Services.AddAuthorization(options =>
{
    // Phase 1: Driver policy
    options.AddPolicy("DriverOnly", policy =>
        policy.RequireRole("driver"));
    
    // Phase 2: Admin-only policy (sensitive operations)
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
    
    // Phase 2: Staff policy (admin OR dispatcher - operational access)
    options.AddPolicy("StaffOnly", policy =>
        policy.RequireRole("admin", "dispatcher"));
    
    // Phase 2: Booker policy (optional - for future use)
    options.AddPolicy("BookerOnly", policy =>
        policy.RequireRole("booker"));
});
```

---

### Policy Matrix

| Policy | Roles | Use Cases |
|--------|-------|-----------|
| `AdminOnly` | `admin` | OAuth management, seeding data, billing access |
| `StaffOnly` | `admin`, `dispatcher` | Quotes, bookings, driver assignment, locations |
| `DriverOnly` | `driver` | Driver rides, status updates, location updates |
| `BookerOnly` | `booker` | (Future) Self-service booking management |
| *(Generic Auth)* | Any authenticated | Creating quotes/bookings, affiliate/driver CRUD |

---

## ?? Endpoint Authorization

### Authorization Matrix

| Endpoint | Policy | Additional Checks | Allowed Roles |
|----------|--------|-------------------|---------------|
| **Health Check** |
| `GET /health` | None | - | Anonymous |
| **Quote Management** |
| `POST /quotes` | Auth | Ownership stored | All authenticated |
| `GET /quotes/list` | `StaffOnly` | Ownership filter | admin, dispatcher |
| `GET /quotes/{id}` | `StaffOnly` | Ownership check | admin, dispatcher |
| `POST /quotes/seed` | `AdminOnly` | - | admin |
| **Booking Management** |
| `POST /bookings` | Auth | Ownership stored | All authenticated |
| `GET /bookings/list` | `StaffOnly` | Ownership/driver filter | admin, dispatcher |
| `GET /bookings/{id}` | `StaffOnly` | Ownership check | admin, dispatcher |
| `POST /bookings/{id}/cancel` | Auth | Ownership check | Owner, staff |
| `POST /bookings/{id}/assign-driver` | `StaffOnly` | - | admin, dispatcher |
| `POST /bookings/seed` | `AdminOnly` | - | admin |
| **Driver Endpoints** |
| `GET /driver/rides/today` | `DriverOnly` | UID filter | driver |
| `GET /driver/rides/{id}` | `DriverOnly` | Ownership check | driver |
| `POST /driver/rides/{id}/status` | `DriverOnly` | Ownership check | driver |
| `POST /driver/location/update` | `DriverOnly` | Ownership check | driver |
| **Location Tracking** |
| `GET /driver/location/{rideId}` | Auth | Role-based check | driver (own), staff |
| `GET /passenger/rides/{rideId}/location` | Auth | Email check | Passenger (email match) |
| `GET /admin/locations` | `StaffOnly` | - | admin, dispatcher |
| `GET /admin/locations/rides` | `StaffOnly` | - | admin, dispatcher |
| **Affiliate & Driver Management** |
| `GET /affiliates/list` | Auth | - | All authenticated |
| `POST /affiliates` | Auth | - | All authenticated |
| `GET /affiliates/{id}` | Auth | - | All authenticated |
| `PUT /affiliates/{id}` | Auth | - | All authenticated |
| `DELETE /affiliates/{id}` | Auth | - | All authenticated |
| `POST /affiliates/{id}/drivers` | Auth | UserUid uniqueness | All authenticated |
| `GET /drivers/list` | Auth | - | All authenticated |
| `GET /drivers/by-uid/{userUid}` | Auth | - | All authenticated |
| `GET /drivers/{id}` | Auth | - | All authenticated |
| `PUT /drivers/{id}` | Auth | UserUid uniqueness | All authenticated |
| `DELETE /drivers/{id}` | Auth | - | All authenticated |
| `POST /dev/seed-affiliates` | `AdminOnly` | - | admin |
| **OAuth Management** |
| `GET /api/admin/oauth` | `AdminOnly` | Secret masked | admin |
| `PUT /api/admin/oauth` | `AdminOnly` | Audit trail | admin |
| **Audit Log Management** |
| `GET /api/admin/audit-logs` | `AdminOnly` | - | admin |
| `GET /api/admin/audit-logs/{id}` | `AdminOnly` | Meta-audit | admin |
| `GET /api/admin/audit-logs/stats` | `AdminOnly` | Meta-audit | admin |
| `DELETE /api/admin/audit-logs/cleanup` | `AdminOnly` | System audit | admin |
| `POST /api/admin/audit-logs/clear` | `AdminOnly` | Safety confirmation | admin |

---

## ?? Ownership Verification

### Phase 1: User Data Access Enforcement

**Purpose**: Ensure users can only access their own data.

**Implementation**:

```csharp
// Helper: Get user ID from JWT claims
static string? GetUserId(ClaimsPrincipal user)
{
    return user.FindFirst("uid")?.Value;
}

// Helper: Check if user is staff (admin or dispatcher)
static bool IsStaffOrAdmin(ClaimsPrincipal user)
{
    return user.IsInRole("admin") || user.IsInRole("dispatcher");
}

// Helper: Check if user is a driver
static bool IsDriver(ClaimsPrincipal user)
{
    return user.IsInRole("driver");
}

// Helper: Check if user can access a record
static bool CanAccessRecord(ClaimsPrincipal user, string? recordOwnerId)
{
    // Staff can access all records
    if (IsStaffOrAdmin(user))
        return true;
    
    // Non-staff must own the record
    var currentUserId = GetUserId(user);
    return !string.IsNullOrEmpty(recordOwnerId) && 
           recordOwnerId == currentUserId;
}

// Helper: Check if user can access a booking
static bool CanAccessBooking(ClaimsPrincipal user, string? createdByUserId, string? assignedDriverUid)
{
    // Staff can access all bookings
    if (IsStaffOrAdmin(user))
        return true;
    
    // Drivers can access assigned bookings
    if (IsDriver(user))
    {
        var driverUid = user.FindFirst("uid")?.Value;
        return !string.IsNullOrEmpty(assignedDriverUid) && 
               assignedDriverUid == driverUid;
    }
    
    // Bookers can access their own bookings
    var currentUserId = GetUserId(user);
    return !string.IsNullOrEmpty(createdByUserId) && 
           createdByUserId == currentUserId;
}
```

**Ownership Fields**:

```csharp
// Stored on QuoteRecord and BookingRecord
public string? CreatedByUserId { get; set; }   // User who created
public string? ModifiedByUserId { get; set; }  // User who last modified
public DateTime? ModifiedOnUtc { get; set; }   // Last modification time
```

**Usage Example**:

```csharp
// GET /bookings/{id}
var booking = await repo.GetAsync(id);
if (!CanAccessBooking(context.User, booking.CreatedByUserId, booking.AssignedDriverUid))
{
    return Results.Problem(
        statusCode: 403,
        title: "Forbidden",
        detail: "You do not have permission to view this booking");
}
```

---

### Email-Based Authorization (Passengers)

**Purpose**: Allow passengers to track their own rides without user accounts.

**Implementation**:

```csharp
// GET /passenger/rides/{rideId}/location
var userEmail = context.User.FindFirst("email")?.Value;
bool isPassengerAuthorized = false;

// Check booker email
if (!string.IsNullOrEmpty(userEmail) && 
    !string.IsNullOrEmpty(booking.Draft?.Booker?.EmailAddress) &&
    userEmail.Equals(booking.Draft.Booker.EmailAddress, StringComparison.OrdinalIgnoreCase))
{
    isPassengerAuthorized = true;
}

// Check passenger email (if different from booker)
if (!isPassengerAuthorized && 
    !string.IsNullOrEmpty(userEmail) &&
    !string.IsNullOrEmpty(booking.Draft?.Passenger?.EmailAddress) &&
    userEmail.Equals(booking.Draft.Passenger.EmailAddress, StringComparison.OrdinalIgnoreCase))
{
    isPassengerAuthorized = true;
}

if (!isPassengerAuthorized)
{
    return Results.Problem(
        statusCode: 403,
        title: "Forbidden",
        detail: "You can only view location for your own bookings");
}
```

**Security Notes**:
- Case-insensitive email comparison
- Checks both booker and passenger email addresses
- No passenger ID claim (future enhancement)

---

## ?? Phase 2: Field Masking

### Purpose

Hide sensitive billing information from dispatchers while allowing operational access.

**Design**: Reflection-based masking applied to response DTOs.

**Implementation**:

```csharp
// File: Services/UserAuthorizationHelper.cs

/// <summary>
/// Phase 2: Check if user is a dispatcher (not admin).
/// Dispatchers have operational access but cannot see billing info.
/// </summary>
public static bool IsDispatcher(ClaimsPrincipal user)
{
    return user.IsInRole("dispatcher") && !user.IsInRole("admin");
}

/// <summary>
/// Phase 2: Mask billing fields for dispatchers using reflection.
/// Admins see full data, dispatchers see null billing fields.
/// </summary>
public static void MaskBillingFields<T>(ClaimsPrincipal user, T dto) where T : class
{
    // Only mask for dispatchers (admins see full data)
    if (!IsDispatcher(user))
        return;
    
    // Billing field names to mask
    var billingFields = new[]
    {
        "PaymentMethodId", 
        "PaymentMethodLast4", 
        "PaymentAmount", 
        "TotalAmount", 
        "TotalFare",
        "EstimatedCost",
        "BillingNotes"
    };
    
    var type = typeof(T);
    foreach (var fieldName in billingFields)
    {
        var prop = type.GetProperty(fieldName);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(dto, null);
        }
    }
}
```

**Usage Example**:

```csharp
// GET /bookings/{id}
var response = new BookingDetailResponseDto
{
    Id = rec.Id,
    // ... other fields
    
    // Phase 2: Billing fields (will be masked for dispatchers)
    PaymentMethodId = rec.PaymentMethodId,
    PaymentAmount = rec.PaymentAmount,
    TotalAmount = rec.TotalAmount
};

// Mask billing fields for dispatchers
MaskBillingFields(user, response);

return Results.Ok(response);
```

**Masked Fields**:

| Field | Admin Sees | Dispatcher Sees |
|-------|------------|-----------------|
| `PaymentMethodId` | `"pm_1234..."` | `null` |
| `PaymentMethodLast4` | `"4242"` | `null` |
| `PaymentAmount` | `150.00` | `null` |
| `TotalAmount` | `165.00` | `null` |
| `TotalFare` | `150.00` | `null` |
| `EstimatedCost` | `150.00` | `null` |
| `BillingNotes` | `"VIP customer"` | `null` |

**Extensibility**:
- Add new field names to `billingFields` array
- Reflection-based approach avoids code duplication
- Works with any DTO type

---

## ?? Encryption

### OAuth Credentials Encryption

**Purpose**: Protect LimoAnywhere OAuth credentials at rest.

**Technology**: ASP.NET Core Data Protection API

**Configuration**:

```csharp
// Service registration
builder.Services.AddDataProtection();

// File storage
var protector = provider.CreateProtector("Bellwood.OAuthCredentials.v1");
var encryptedSecret = protector.Protect(credentials.ClientSecret);

// Decryption
var decryptedSecret = protector.Unprotect(encryptedSecret);
```

**Key Storage**:
- **Development**: `%LocalAppData%\ASP.NET\DataProtection-Keys\`
- **Production**: Azure Key Vault (recommended)

**Purpose String**: `"Bellwood.OAuthCredentials.v1"`
- Provides scope isolation
- Prevents accidental decryption with wrong purpose

**Security Properties**:
- Keys automatically rotated (90-day lifetime)
- Encrypted with Windows DPAPI (Windows) or keychain (macOS)
- Supports distributed deployments (shared key ring)

---

### Secret Masking

**Purpose**: Never expose full secrets in API responses.

**Implementation**:

```csharp
static string MaskSecret(string secret)
{
    if (string.IsNullOrWhiteSpace(secret)) return "********";
    if (secret.Length <= 8) return "********";
    
    // Show first 4 and last 4 characters, mask the middle
    return $"{secret[..4]}...{secret[^4..]}";
}
```

**Examples**:

| Original Secret | Masked Version |
|----------------|----------------|
| `super-secret-key-12345` | `supe...2345` |
| `abcdefghijklmnop` | `abcd...mnop` |
| `short` | `********` |

**Usage**:

```csharp
// GET /api/admin/oauth
var response = new OAuthCredentialsResponseDto
{
    ClientId = credentials.ClientId,
    ClientSecretMasked = MaskSecret(credentials.ClientSecret), // ? Safe
    // ClientSecret = credentials.ClientSecret  ? NEVER DO THIS!
};
```

---

## ?? SignalR Authentication

### WebSocket Authentication

**Challenge**: HTTP headers not available for WebSocket connections.

**Solution**: Pass JWT token in query string.

**Client Implementation**:

**JavaScript**:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location?access_token=" + token)
    .build();
```

**C#**:
```csharp
var hubConnection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5206/hubs/location", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .Build();
```

**Server Configuration**:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        
        // If the request is for our SignalR hub, extract token from query string
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/location"))
        {
            context.Token = accessToken;
        }
        return Task.CompletedTask;
    }
};
```

**Security Considerations**:
- Query string tokens may appear in server logs
- Use HTTPS to prevent token interception
- Consider short token expiration (15-30 minutes)

---

## ??? Security Best Practices

### Input Validation

**Email Addresses**:
```csharp
// Case-insensitive comparison
userEmail.Equals(booking.Draft.Booker.EmailAddress, StringComparison.OrdinalIgnoreCase)
```

**UserUid Uniqueness**:
```csharp
// Prevent duplicate UserUid assignments
var isUnique = await driverRepo.IsUserUidUniqueAsync(driver.UserUid);
if (!isUnique)
    return Results.BadRequest(new { error = "UserUid already assigned" });
```

**Ride Ownership**:
```csharp
// Verify driver owns the ride before allowing status update
if (booking.AssignedDriverUid != driverUid)
    return Results.Forbid();
```

---

### Rate Limiting

**Location Updates**:
- **Minimum Interval**: 10 seconds
- **Enforcement**: Server-side (`ILocationService.TryUpdateLocation`)
- **Response**: 429 Too Many Requests

**Implementation**:
```csharp
if (now - existing.StoredAt < TimeSpan.FromSeconds(10))
{
    return false; // Rate limited
}
```

---

### Audit Trail

**Ownership Tracking**:
```csharp
rec.CreatedByUserId = GetUserId(context.User);
rec.ModifiedByUserId = GetUserId(context.User);
rec.ModifiedOnUtc = DateTime.UtcNow;
```

**OAuth Updates**:
```csharp
credentials.LastUpdatedBy = context.User.FindFirst("sub")?.Value ?? "unknown";
credentials.LastUpdatedUtc = DateTime.UtcNow;
```

**Audit Log Clearing (Safety)**:
```csharp
// Requires exact confirmation phrase "CLEAR" (case-sensitive)
if (request.Confirm != "CLEAR")
{
    await auditLogger.LogFailureAsync(
        context.User,
        AuditActions.AuditLogCleared,
        "AuditLog",
        errorMessage: "Invalid confirmation phrase",
        httpContext: context);
    
    return Results.BadRequest(new { error = "Confirmation phrase must be exactly 'CLEAR'" });
}

// Clear all logs
var deletedCount = await auditRepo.ClearAllAsync(ct);

// Record one final audit event AFTER clearing
await auditLogger.LogSuccessAsync(
    context.User,
    AuditActions.AuditLogCleared,
    "AuditLog",
    details: new {
        deletedCount,
        clearedAtUtc = DateTime.UtcNow,
        clearedByUserId = currentUserId,
        clearedByUsername = username
    },
    httpContext: context);
```

**Meta-Auditing**:
```csharp
// Audit the act of viewing audit logs
await auditLogger.LogSuccessAsync(
    context.User,
    "AuditLog.Viewed",
    "AuditLog",
    id,
    httpContext: context);
```

**Logging**:
```csharp
log.LogInformation("OAuth credentials updated by admin {AdminUsername}", adminUsername);
log.LogWarning("Audit logs cleared by {Username} ({UserId}). Deleted count: {DeletedCount}",
    username, currentUserId, deletedCount);
log.LogWarning("User {UserId} attempted to cancel booking {BookingId} they don't own", 
    currentUserId, id);
```

---

## ?? Common Security Pitfalls

### ? Pitfall 1: Not Setting `MapInboundClaims = false`

**Problem**: Role claim is remapped, breaking `IsInRole()` checks.

**Symptom**: Authorization policies always fail.

**Fix**:
```csharp
options.MapInboundClaims = false; // ? Add this!
```

---

### ? Pitfall 2: Returning Full Secrets

**Problem**: Exposing OAuth secrets in API responses.

**Symptom**: Secrets visible in network traffic.

**Fix**: Always mask secrets:
```csharp
ClientSecretMasked = MaskSecret(credentials.ClientSecret) // ?
// ClientSecret = credentials.ClientSecret  ? NEVER!
```

---

### ? Pitfall 3: Skipping Ownership Checks

**Problem**: Users can access other users' data.

**Symptom**: Passenger A can view Passenger B's bookings.

**Fix**: Always verify ownership:
```csharp
if (!CanAccessBooking(user, booking.CreatedByUserId, booking.AssignedDriverUid))
{
    return Results.Problem(statusCode: 403, title: "Forbidden");
}
```

---

### ? Pitfall 4: Client-Side Rate Limiting

**Problem**: Relying on DriverApp to rate-limit location updates.

**Symptom**: Malicious clients can spam location updates.

**Fix**: Enforce rate limiting server-side:
```csharp
if (!locationService.TryUpdateLocation(driverUid, update))
{
    return Results.StatusCode(429); // Server enforces limit
}
```

---

### ? Pitfall 5: Hardcoded Admin Checks

**Problem**: Checking `role == "admin"` instead of using policies.

**Symptom**: Dispatchers can't access operational endpoints.

**Fix**: Use `StaffOnly` policy:
```csharp
.RequireAuthorization("StaffOnly") // ? Includes admin + dispatcher
// .RequireAuthorization("AdminOnly") ? Excludes dispatcher
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system design
- `11-User-Access-Control.md` - RBAC implementation details
- `14-Passenger-Tracking.md` - Email-based authorization
- `20-API-Reference.md` - Endpoint documentation
- `21-SignalR-Events.md` - SignalR authentication
- `22-Data-Models.md` - Entity schemas

---

## ?? Future Enhancements

### Phase 3+ Security Roadmap

1. **OAuth2 Token Refresh**:
   - Implement automatic token refresh for LimoAnywhere API
   - Cache access tokens with expiration
   - Handle 401 responses with retry logic

2. **Multi-Factor Authentication (MFA)**:
   - SMS or authenticator app for admin logins
   - Integration with AuthServer MFA flows

3. **API Key Authentication**:
   - Allow third-party integrations via API keys
   - Scoped permissions per API key

4. **Azure Key Vault Integration**:
   - Store OAuth credentials in Azure Key Vault
   - Automatic secret rotation
   - Distributed key sharing for multi-instance deployments

5. **Fine-Grained Permissions**:
   - Permission matrix beyond roles (e.g., "CanAssignDrivers", "CanViewBilling")
   - Resource-based authorization (e.g., "CanEditAffiliate{id}")

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Security Version**: 2.0
