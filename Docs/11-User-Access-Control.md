# User Access Control & RBAC

**Document Type**: Living Document  
**Last Updated**: January 14, 2026  
**Status**: ?? Phase 1 Complete, Phase 2 In Progress

---

## ?? Overview

This document covers the complete user access control system including:
- **Phase 1**: Ownership tracking & basic RBAC (? Complete)
- **Phase 2**: Dispatcher role & enhanced RBAC (?? In Progress)

---

## ? Phase 1: Ownership & Basic RBAC (COMPLETE)

**Completed**: January 14, 2026  
**Status**: ? All tests passing  
**Branch**: `feature/user-data-restriction`

### Goals Achieved

1. ? **Ownership Tracking**
   - Added `CreatedByUserId` field to quotes and bookings
   - Capture user ID on record creation
   - Support for audit trail (`ModifiedByUserId`, `ModifiedOnUtc`)

2. ? **Role-Based List Filtering**
   - Admins see all records
   - Bookers see only their own records
   - Drivers see only assigned bookings
   - Legacy records (null owner) hidden from non-staff

3. ? **Detail Endpoint Authorization**
   - Ownership verification on `GET /quotes/{id}`
   - Ownership verification on `GET /bookings/{id}`
   - 403 Forbidden for unauthorized access
   - Staff bypass for admin access

4. ? **Cancel Endpoint Authorization**
   - Staff can cancel any booking
   - Bookers can only cancel their own bookings
   - Ownership check before allowing cancellation

---

### Implementation Details

#### Data Model Changes

```csharp
// QuoteRecord.cs & BookingRecord.cs
public class QuoteRecord
{
    // Existing fields...
    
    // Phase 1: Ownership & Audit Fields
    public string? CreatedByUserId { get; set; }    // User who created
    public string? ModifiedByUserId { get; set; }   // User who last modified
    public DateTime? ModifiedOnUtc { get; set; }    // Last modification time
}

public class BookingRecord
{
    // Existing fields...
    
    // Phase 1: Ownership & Audit Fields
    public string? CreatedByUserId { get; set; }    // User who created
    public string? ModifiedByUserId { get; set; }   // User who last modified
    public DateTime? ModifiedOnUtc { get; set; }    // Last modification time
}
```

#### Authorization Helper

**File**: `Services/UserAuthorizationHelper.cs`

```csharp
public static class UserAuthorizationHelper
{
    // Get user ID from JWT claims
    public static string? GetUserId(ClaimsPrincipal user)
    {
        // Prefer userId claim (always Identity GUID)
        var userId = user.FindFirst("userId")?.Value;
        if (!string.IsNullOrEmpty(userId)) return userId;
        
        // Fallback to uid claim
        var uid = user.FindFirst("uid")?.Value;
        if (!string.IsNullOrEmpty(uid)) return uid;
        
        // Last resort: sub claim (username)
        return user.FindFirst("sub")?.Value ?? user.Identity?.Name;
    }
    
    // Check if user is staff (admin or dispatcher)
    public static bool IsStaffOrAdmin(ClaimsPrincipal user)
    {
        var role = user.FindFirst("role")?.Value;
        return role == "admin" || role == "dispatcher";
    }
    
    // Check ownership or staff access
    public static bool CanAccessRecord(
        ClaimsPrincipal user, 
        string? createdByUserId)
    {
        // Staff can access all records
        if (IsStaffOrAdmin(user)) return true;
        
        // Legacy records: only staff can access
        if (string.IsNullOrEmpty(createdByUserId)) return false;
        
        // Check ownership
        var currentUserId = GetUserId(user);
        return createdByUserId == currentUserId;
    }
    
    // Comprehensive booking access check
    public static bool CanAccessBooking(
        ClaimsPrincipal user,
        string? createdByUserId,
        string? assignedDriverUid)
    {
        // Staff has full access
        if (IsStaffOrAdmin(user)) return true;
        
        // Drivers: check assignment
        if (IsDriver(user))
        {
            var driverUid = user.FindFirst("uid")?.Value;
            return assignedDriverUid == driverUid;
        }
        
        // Bookers: check ownership
        return CanAccessRecord(user, createdByUserId);
    }
}
```

#### Endpoint Implementation Examples

**Quote List Filtering** (`GET /quotes/list`):
```csharp
app.MapGet("/quotes/list", async (
    [FromQuery] int take,
    HttpContext context,
    IQuoteRepository repo) =>
{
    take = (take <= 0 || take > 200) ? 50 : take;
    var rows = await repo.ListAsync(take);
    
    var user = context.User;
    var currentUserId = GetUserId(user);
    
    IEnumerable<QuoteRecord> filteredRows;
    if (IsStaffOrAdmin(user))
    {
        // Staff sees all quotes
        filteredRows = rows;
    }
    else
    {
        // Bookers see only their own quotes
        filteredRows = rows.Where(r => 
            !string.IsNullOrEmpty(r.CreatedByUserId) && 
            r.CreatedByUserId == currentUserId);
    }
    
    return Results.Ok(filteredRows);
})
.RequireAuthorization();
```

**Quote Detail Authorization** (`GET /quotes/{id}`):
```csharp
app.MapGet("/quotes/{id}", async (
    string id,
    HttpContext context,
    IQuoteRepository repo) =>
{
    var rec = await repo.GetAsync(id);
    if (rec is null) return Results.NotFound();
    
    var user = context.User;
    
    // Staff can access all records
    if (!IsStaffOrAdmin(user))
    {
        var currentUserId = GetUserId(user);
        
        // Legacy records not accessible to non-staff
        if (string.IsNullOrEmpty(rec.CreatedByUserId))
        {
            return Results.Problem(
                statusCode: 403,
                title: "Forbidden",
                detail: "You do not have permission to view this quote");
        }
        
        // Check ownership
        if (rec.CreatedByUserId != currentUserId)
        {
            return Results.Problem(
                statusCode: 403,
                title: "Forbidden",
                detail: "You do not have permission to view this quote");
        }
    }
    
    return Results.Ok(rec);
})
.RequireAuthorization();
```

**Booking Cancellation Authorization** (`POST /bookings/{id}/cancel`):
```csharp
app.MapPost("/bookings/{id}/cancel", async (
    string id,
    HttpContext context,
    IBookingRepository repo,
    IEmailSender email) =>
{
    var user = context.User;
    var currentUserId = GetUserId(user);
    
    var booking = await repo.GetAsync(id);
    if (booking is null) return Results.NotFound();
    
    // Verify permission to cancel
    if (!CanAccessRecord(user, booking.CreatedByUserId))
    {
        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You do not have permission to cancel this booking");
    }
    
    // Proceed with cancellation...
    await repo.UpdateStatusAsync(id, BookingStatus.Cancelled, currentUserId);
    
    return Results.Ok(new { message = "Booking cancelled successfully" });
})
.RequireAuthorization();
```

---

### Testing (Phase 1)

**Test Script**: `Scripts/Test-Phase1-Ownership.ps1`

**Test Results**: ? **12/12 tests passing**

| Test | Description | Expected | Status |
|------|-------------|----------|--------|
| Step 1 | Alice authenticates | Admin role | ? Pass |
| Step 2 | Seed affiliates/drivers | 2 affiliates, 3 drivers | ? Pass |
| Step 3 | Chris authenticates | Booker role | ? Pass |
| Step 4 | Alice seeds quotes | 5 quotes owned by Alice | ? Pass |
| Step 5 | Chris seeds quotes | 5 quotes owned by Chris | ? Pass |
| Step 6 | Alice seeds bookings | 8 bookings owned by Alice | ? Pass |
| Step 7 | Chris seeds bookings | 8 bookings owned by Chris | ? Pass |
| Step 8 | Alice lists quotes | See all 10 quotes | ? Pass |
| Step 9 | Chris lists quotes | See only own 5 quotes | ? Pass |
| Step 10 | Alice lists bookings | See all 16 bookings | ? Pass |
| Step 11 | Chris lists bookings | See only own 8 bookings | ? Pass |
| Step 12 | Chris accesses Alice's quote | 403 Forbidden | ? Pass |

---

## ?? Phase 2: Dispatcher Role & Enhanced RBAC (IN PROGRESS)

**Target**: Q1 2026  
**Status**: ?? Planning ? Implementation  
**Reference**: `Docs/AdminAPI-Phase2-Reference.md`

### Goals

1. **Introduce Dispatcher Role**
   - Operational staff with limited access
   - Can see all bookings/quotes (operational data)
   - **Cannot** see billing information
   - Cannot manage users or assign roles

2. **Enhanced Authorization Policies**
   - `AdminOnly` - Requires admin role
   - `StaffOnly` - Requires admin OR dispatcher role
   - Field-level authorization (mask sensitive data)

3. **Billing Data Protection**
   - Mask payment fields for dispatchers
   - Create admin-only billing endpoints
   - Audit logging for billing access

---

### AuthServer Phase 2 (COMPLETE)

**Status**: ? Complete (see `Docs/AdminAPI-Phase2-Reference.md`)

**What's Ready**:
- ? Dispatcher role created
- ? Test user: `diana` (password: `password`)
- ? Authorization policies implemented
- ? Role assignment endpoint: `PUT /api/admin/users/{username}/role`
- ? Admin endpoints protected with `AdminOnly` policy

**JWT Structure for Dispatcher**:
```json
{
  "sub": "diana",
  "uid": "guid-xxx...",
  "userId": "guid-xxx...",
  "role": "dispatcher",
  "email": "diana.dispatcher@bellwood.example",
  "exp": 1704996000
}
```

---

### AdminAPI Phase 2 Implementation Plan

#### Step 1: Add Authorization Policies

**File**: `Program.cs`

```csharp
// Register authorization policies
builder.Services.AddAuthorization(options =>
{
    // Existing: Driver policy
    options.AddPolicy("DriverOnly", policy =>
        policy.RequireRole("driver"));
    
    // NEW: Admin-only policy
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
    
    // NEW: Staff policy (admin OR dispatcher)
    options.AddPolicy("StaffOnly", policy =>
        policy.RequireRole("admin", "dispatcher"));
    
    // OPTIONAL: Booker policy
    options.AddPolicy("BookerOnly", policy =>
        policy.RequireRole("booker"));
});
```

#### Step 2: Apply Policies to Endpoints

**Operational Endpoints** (Staff can access):
```csharp
// Both admin and dispatcher can access
app.MapGet("/bookings/list", ...)
   .RequireAuthorization("StaffOnly");

app.MapGet("/bookings/{id}", ...)
   .RequireAuthorization("StaffOnly");

app.MapPost("/bookings/{id}/assign-driver", ...)
   .RequireAuthorization("StaffOnly");

app.MapGet("/admin/locations", ...)
   .RequireAuthorization("StaffOnly");
```

**Admin-Only Endpoints** (Only admin can access):
```csharp
// Only admin can access
app.MapPost("/dev/seed-affiliates", ...)
   .RequireAuthorization("AdminOnly");

// Future billing endpoints
app.MapGet("/billing/reports", ...)
   .RequireAuthorization("AdminOnly");
```

#### Step 3: Field Masking for Dispatchers

**Helper Method**:
```csharp
// Services/UserAuthorizationHelper.cs
public static bool IsDispatcher(ClaimsPrincipal user)
{
    return user.FindFirst("role")?.Value == "dispatcher";
}

public static void MaskBillingData<T>(ClaimsPrincipal user, T dto)
    where T : class
{
    if (!IsDispatcher(user)) return;
    
    // Use reflection or specific properties
    var paymentProp = typeof(T).GetProperty("PaymentMethodId");
    paymentProp?.SetValue(dto, null);
    
    var cardProp = typeof(T).GetProperty("CardLast4");
    cardProp?.SetValue(dto, null);
    
    var amountProp = typeof(T).GetProperty("TotalAmount");
    amountProp?.SetValue(dto, null);
}
```

**Endpoint Usage**:
```csharp
app.MapGet("/bookings/{id}", async (
    string id,
    HttpContext context,
    IBookingRepository repo) =>
{
    var user = context.User;
    var booking = await repo.GetAsync(id);
    if (booking is null) return Results.NotFound();
    
    // Authorization check (staff or owner)
    if (!CanAccessBooking(user, booking.CreatedByUserId, booking.AssignedDriverUid))
    {
        return Results.Problem(statusCode: 403, title: "Forbidden");
    }
    
    // Create response DTO
    var response = new BookingDetailDto
    {
        Id = booking.Id,
        // ... populate fields
        PaymentMethodId = booking.Draft?.PaymentMethodId,
        CardLast4 = booking.Draft?.CardLast4,
        TotalAmount = booking.Draft?.TotalAmount
    };
    
    // Mask billing data for dispatchers
    if (IsDispatcher(user))
    {
        response.PaymentMethodId = null;
        response.CardLast4 = null;
        response.TotalAmount = null;
    }
    
    return Results.Ok(response);
})
.RequireAuthorization("StaffOnly");
```

#### Step 4: Create Billing DTOs

**File**: `Models/BillingDtos.cs`

```csharp
public class BookingDetailDto
{
    public string Id { get; set; }
    public string Status { get; set; }
    // ... operational fields
    
    // Billing fields (masked for dispatchers)
    public string? PaymentMethodId { get; set; }
    public string? CardLast4 { get; set; }
    public decimal? TotalAmount { get; set; }
}

public class QuoteDetailDto
{
    public string Id { get; set; }
    public string Status { get; set; }
    // ... operational fields
    
    // Billing fields (masked for dispatchers)
    public decimal? EstimatedCost { get; set; }
    public string? BillingNotes { get; set; }
}
```

---

### Role Comparison Matrix

| Feature | Admin | Dispatcher | Booker | Driver |
|---------|-------|-----------|--------|--------|
| **Data Access** |
| View all quotes | ? Yes | ? Yes | ? Own only | ? None |
| View all bookings | ? Yes | ? Yes | ? Own only | ? Assigned only |
| View billing data | ? Yes | ? **Masked** | ? No | ? No |
| View driver locations | ? Yes | ? Yes | ? No | ? Own only |
| **Operations** |
| Create quotes | ? Yes | ? Yes | ? Yes | ? No |
| Create bookings | ? Yes | ? Yes | ? Yes | ? No |
| Cancel bookings | ? All | ? All | ? Own only | ? No |
| Assign drivers | ? Yes | ? Yes | ? No | ? No |
| Update ride status | ? Yes | ? No | ? No | ? Yes |
| **Administration** |
| Manage users | ? Yes | ? No | ? No | ? No |
| Assign roles | ? Yes | ? No | ? No | ? No |
| View billing reports | ? Yes | ? No | ? No | ? No |
| Seed test data | ? Yes | ? No | ? No | ? No |

---

### Testing Plan (Phase 2)

#### Test Setup

1. **Authenticate as dispatcher** (`diana`):
```powershell
$dianaToken = (Invoke-RestMethod -Uri "https://localhost:5001/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"username":"diana","password":"password"}' `
    -UseBasicParsing).accessToken
```

2. **Decode JWT to verify role**:
```powershell
$payload = $dianaToken.Split('.')[1]
$payload = $payload.PadRight(($payload.Length + (4 - $payload.Length % 4) % 4), '=')
$claims = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
Write-Host "Role: $($claims.role)"  # Should be "dispatcher"
```

#### Test Cases

| Test | Description | Expected | Status |
|------|-------------|----------|--------|
| 1 | Diana lists quotes | See all quotes | ? Pending |
| 2 | Diana lists bookings | See all bookings | ? Pending |
| 3 | Diana views booking detail | Billing data masked | ? Pending |
| 4 | Diana assigns driver | Success | ? Pending |
| 5 | Diana tries admin endpoint | 403 Forbidden | ? Pending |
| 6 | Diana tries user management | 403 Forbidden | ? Pending |
| 7 | Admin views same booking | Billing data visible | ? Pending |
| 8 | Booker (Chris) views booking | See own only | ? Pass (Phase 1) |

#### Test Script (To Be Created)

**File**: `Scripts/Test-Phase2-Dispatcher.ps1`

Will test:
- ? Dispatcher authentication
- ? Access to operational endpoints
- ? Billing data masking
- ? Denial of admin endpoints
- ? Role assignment (admin only)

---

## ?? Authorization Decision Flow

```
???????????????????????????????????????????
? Request: GET /bookings/{id}             ?
? Authorization: Bearer <JWT>             ?
???????????????????????????????????????????
                 ?
                 ?
???????????????????????????????????????????
? 1. Extract user info from JWT           ?
?    - userId (for ownership)             ?
?    - role (for policy)                  ?
?    - uid (for drivers)                  ?
???????????????????????????????????????????
                 ?
                 ?
???????????????????????????????????????????
? 2. Check endpoint policy                ?
?    - StaffOnly? (admin OR dispatcher)   ?
?    - AdminOnly? (admin only)            ?
?    - DriverOnly? (driver only)          ?
???????????????????????????????????????????
                 ?
                 ?? Policy fails ??? 403 Forbidden
                 ?
                 ?
???????????????????????????????????????????
? 3. Fetch record from repository         ?
???????????????????????????????????????????
                 ?
                 ?? Not found ??? 404 Not Found
                 ?
                 ?
???????????????????????????????????????????
? 4. Check record-level authorization     ?
?    - Staff? ??? Allow                   ?
?    - Driver + assigned? ??? Allow       ?
?    - Booker + owner? ??? Allow          ?
?    - Else ??? Deny                      ?
???????????????????????????????????????????
                 ?
                 ?? Not authorized ??? 403 Forbidden
                 ?
                 ?
???????????????????????????????????????????
? 5. Build response DTO                   ?
???????????????????????????????????????????
                 ?
                 ?
???????????????????????????????????????????
? 6. Mask sensitive fields (if dispatcher)?
?    - PaymentMethodId ??? null           ?
?    - CardLast4 ??? null                 ?
?    - TotalAmount ??? null               ?
???????????????????????????????????????????
                 ?
                 ?
???????????????????????????????????????????
? 7. Return 200 OK with (maybe masked) DTO?
???????????????????????????????????????????
```

---

## ?? Implementation Checklist

### Phase 1 (? Complete)

- [x] Add `CreatedByUserId` to QuoteRecord
- [x] Add `CreatedByUserId` to BookingRecord
- [x] Add audit fields (`ModifiedByUserId`, `ModifiedOnUtc`)
- [x] Create `UserAuthorizationHelper` class
- [x] Implement `GetUserId()` helper
- [x] Implement `IsStaffOrAdmin()` helper
- [x] Implement `CanAccessRecord()` helper
- [x] Implement `CanAccessBooking()` helper
- [x] Add ownership capture on quote creation
- [x] Add ownership capture on booking creation
- [x] Filter quotes list by ownership
- [x] Filter bookings list by ownership
- [x] Add authorization to quote detail endpoint
- [x] Add authorization to booking detail endpoint
- [x] Add authorization to cancel endpoint
- [x] Create Phase 1 test script
- [x] Run and pass all 12 tests
- [x] Document Phase 1 implementation

### Phase 2 (?? In Progress)

- [ ] Add `AdminOnly` authorization policy
- [ ] Add `StaffOnly` authorization policy
- [ ] Add `BookerOnly` authorization policy (optional)
- [ ] Update helper: `IsDispatcher()` method
- [ ] Update helper: `MaskBillingData()` method
- [ ] Apply `StaffOnly` to operational endpoints
- [ ] Apply `AdminOnly` to admin endpoints
- [ ] Create billing DTOs with nullable fields
- [ ] Implement field masking in booking detail
- [ ] Implement field masking in quote detail
- [ ] Test dispatcher can access operational data
- [ ] Test dispatcher cannot see billing data
- [ ] Test dispatcher cannot access admin endpoints
- [ ] Create Phase 2 test script
- [ ] Run and pass all Phase 2 tests
- [ ] Document Phase 2 implementation
- [ ] Update API documentation with policies

---

## ?? Related Documents

- `00-INDEX.md` - Documentation navigation
- `01-System-Architecture.md` - JWT structure & user flow
- `02-Testing-Guide.md` - Testing workflows
- `Docs/AdminAPI-Phase2-Reference.md` - AuthServer Phase 2 changes
- `23-Security-Model.md` - Complete security documentation
- `ROADMAP.md` - Phase 3 and beyond planning

---

**Last Updated**: January 14, 2026  
**Phase 1 Status**: ? Complete  
**Phase 2 Status**: ?? In Progress  
**Next Phase**: Passenger user accounts (Phase 3)
