# AuthServer Phase 2 - Integration Reference

**Initiative:** Role-Based Access Control  
**Component:** AuthServer  
**Phase:** Phase 2 - Dispatcher Role & RBAC  
**Date:** January 13, 2026  
**Status:** ?? **INFORMATIONAL**

---

## ?? Purpose of This Document

This document describes the Phase 2 changes completed by the **AuthServer team** that affect other platform components. It is provided for **reference only** to help teams understand what changed in the authentication system.

**This document does NOT contain implementation instructions for other component teams.**

---

## ?? What Changed in Phase 2

### ? **New Role: Dispatcher**

**Summary:** AuthServer now supports a `dispatcher` role for operational staff.

**Test User Available:**
- Username: `diana`
- Password: `password`
- Role: `dispatcher`
- Email: `diana.dispatcher@bellwood.example`

**JWT Structure for Dispatcher:**
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

**What This Means:**
- Dispatchers are operational staff (not full admins)
- They should see all bookings/quotes but NOT billing data
- JWTs now may contain `"role": "dispatcher"`
- Dispatcher role is mutually exclusive with admin role

---

### ? **Authorization Policies Implemented**

**Summary:** AuthServer now enforces role-based authorization policies.

**Policies Available:**

#### **AdminOnly**
- Requires `admin` role
- Used for: User management, role assignment, sensitive operations
- **Impact:** AdminAPI should use this for financial endpoints

#### **StaffOnly**
- Requires `admin` OR `dispatcher` role
- Used for: Operational endpoints both staff types can access
- **Impact:** AdminAPI should use this for booking/quote management

#### **DriverOnly**
- Requires `driver` role
- Already in use (no change)

#### **BookerOnly**
- Requires `booker` role
- Available if needed

**Example Usage in AdminAPI:**
```csharp
// In AdminAPI Startup.cs or Program.cs
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
    
    options.AddPolicy("StaffOnly", policy =>
        policy.RequireRole("admin", "dispatcher"));
});

// On controllers/endpoints
[Authorize(Policy = "StaffOnly")]
public async Task<IActionResult> GetBookings()
{
    // Both admin and dispatcher can access
}

[Authorize(Policy = "AdminOnly")]
public async Task<IActionResult> GetBillingReport()
{
    // Only admin can access
}
```

---

### ? **Role Assignment Endpoint**

**Summary:** New endpoint allows admins to change user roles.

**Endpoint:** `PUT /api/admin/users/{username}/role`

**Authorization:** Requires `AdminOnly` policy (only admins can call)

**Request Example:**
```http
PUT /api/admin/users/bob/role
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "role": "dispatcher"
}
```

**Response Example:**
```json
{
  "message": "Successfully assigned role 'dispatcher' to user 'bob'.",
  "username": "bob",
  "previousRoles": ["admin"],
  "newRole": "dispatcher"
}
```

**What This Means:**
- Admins can promote users to dispatcher (or other roles)
- Roles are mutually exclusive (only one role per user)
- Users must re-login to get updated token with new role

**Valid Roles:**
- `admin`
- `dispatcher`
- `booker`
- `driver`

---

### ? **Protected Admin Endpoints**

**Summary:** All AuthServer admin endpoints now require admin role.

**Endpoints Now Protected:**
- `POST /api/admin/users/drivers` - Create driver user
- `PUT /api/admin/users/{username}/uid` - Update user UID
- `GET /api/admin/users/drivers` - List driver users
- `GET /api/admin/users/by-uid/{userUid}` - Get user by UID
- `DELETE /api/admin/users/drivers/{username}` - Delete driver user
- `PUT /api/admin/users/{username}/role` - Assign role (new)

**What This Means:**
- Dispatchers cannot create/modify users
- Dispatchers cannot assign roles
- Only admins can manage users
- Returns 403 Forbidden if non-admin tries to access

---

## ?? Impact on Other Components

### AdminAPI

**Phase 2 Requirements:**

1. **Implement Same Policies:**
   - Add `AdminOnly` and `StaffOnly` policies
   - Match AuthServer policy definitions

2. **Apply to Endpoints:**
   - **StaffOnly:** Booking/quote management, driver assignment
   - **AdminOnly:** Billing, financial reports, user management

3. **Field Masking for Dispatchers:**
   - When dispatcher accesses booking/quote, mask billing fields
   - Check `User.IsInRole("dispatcher")` and exclude sensitive data
   - Example:
     ```csharp
     if (User.IsInRole("dispatcher"))
     {
         booking.PaymentMethodId = null;
         booking.BillingAmount = null;
         // Mask other billing fields
     }
     ```

4. **Testing:**
   - Login as diana (dispatcher)
   - Verify can access bookings but billing data is masked
   - Verify cannot access admin-only endpoints

---

### Admin Portal

**Phase 2 Requirements:**

1. **Role-Based UI Hiding:**
   - Check user's role claim from JWT
   - Hide billing tabs/pages for dispatchers
   - Show operational tabs only for dispatchers

2. **Role Management UI (Optional):**
   - Allow admins to change user roles
   - Call `PUT /api/admin/users/{username}/role`
   - Display current role and allow selection

3. **Dispatcher Dashboard:**
   - Create dispatcher-specific views
   - Show bookings, quotes, driver assignment
   - Hide billing, user management, financial reports

4. **Testing:**
   - Login as diana
   - Verify billing sections not visible
   - Verify operational sections work

---

### Driver App

**No Changes Required:**
- Driver role unchanged
- JWT structure for drivers unchanged
- DriverOnly policy already in use

---

### Passenger App

**No Changes Required:**
- Booker role unchanged
- JWT structure for passengers unchanged
- BookerOnly policy available if needed

---

## ?? Role Comparison

| Feature | Admin | Dispatcher | Booker | Driver |
|---------|-------|-----------|--------|--------|
| View all bookings | ? Yes | ? Yes | ? No (own only) | ? No (assigned only) |
| View billing data | ? Yes | ? No (masked) | ? No | ? No |
| Assign drivers | ? Yes | ? Yes | ? No | ? No |
| Manage users | ? Yes | ? No | ? No | ? No |
| Assign roles | ? Yes | ? No | ? No | ? No |
| Create bookings | ? Yes | ? Yes | ? Yes | ? No |
| View driver locations | ? Yes | ? Yes | ? No | ? Own only |

---

## ?? JWT Claim Structure

### No Changes to Existing Users

**Admin (Phase 1 ? Phase 2):**
```json
{
  "sub": "alice",
  "uid": "guid-xxx...",
  "userId": "guid-xxx...",
  "role": "admin"
  // No change
}
```

**Driver (Phase 1 ? Phase 2):**
```json
{
  "sub": "charlie",
  "uid": "driver-001",
  "userId": "guid-xxx...",
  "role": "driver"
  // No change
}
```

**Booker (Phase 1 ? Phase 2):**
```json
{
  "sub": "chris",
  "uid": "guid-xxx...",
  "userId": "guid-xxx...",
  "role": "booker",
  "email": "chris.bailey@example.com"
  // No change
}
```

### New: Dispatcher

**Dispatcher (Phase 2):**
```json
{
  "sub": "diana",
  "uid": "guid-xxx...",
  "userId": "guid-xxx...",
  "role": "dispatcher",
  "email": "diana.dispatcher@bellwood.example"
  // NEW
}
```

---

## ?? Testing Reference

### Test Users

| Username | Password | Role | Access Level | Phase 2 Ready |
|----------|----------|------|--------------|---------------|
| alice | password | admin | Full access | ? |
| bob | password | admin* | Full access | ? |
| chris | password | booker | Own data only | ? |
| charlie | password | driver | Assigned only | ? |
| diana | password | dispatcher | Operational only | ? NEW |

*bob can be changed to dispatcher for testing using role assignment endpoint

---

### Quick Test Scenarios

#### **Test 1: Dispatcher Login**
```bash
curl -X POST https://localhost:5001/login \
  -H "Content-Type: application/json" \
  -d '{"username":"diana","password":"password"}'
```

**Expected:** JWT with `"role": "dispatcher"`

---

#### **Test 2: Dispatcher Denied Admin Access**
```bash
# Get diana's token
DIANA_TOKEN=$(curl -X POST https://localhost:5001/login \
  -H "Content-Type: application/json" \
  -d '{"username":"diana","password":"password"}' | jq -r '.token')

# Try admin endpoint
curl -X GET https://localhost:5001/api/admin/users/drivers \
  -H "Authorization: Bearer $DIANA_TOKEN"
```

**Expected:** 403 Forbidden

---

#### **Test 3: Admin Can Assign Roles**
```bash
# Get admin token
ALICE_TOKEN=$(curl -X POST https://localhost:5001/login \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}' | jq -r '.token')

# Change bob to dispatcher
curl -X PUT https://localhost:5001/api/admin/users/bob/role \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"role":"dispatcher"}'
```

**Expected:** Success response with role change confirmation

---

## ?? Questions & Answers

### Q: Do I need to change my JWT handling code?
**A:** No. JWT structure for existing roles unchanged. Just be aware of new `dispatcher` role.

---

### Q: What if I see a 403 error from AuthServer?
**A:** User's role doesn't match the required policy. Check the endpoint's authorization requirements.

---

### Q: How do I check if a user is a dispatcher?
**A:** Extract role claim from JWT: `User.IsInRole("dispatcher")` or `User.FindFirst("role")?.Value == "dispatcher"`

---

### Q: Can a user be both admin and dispatcher?
**A:** No. Phase 2 uses mutually exclusive roles. A user is EITHER admin OR dispatcher.

---

### Q: How do I know which policy to use?
**A:**
- **AdminOnly:** Sensitive operations (billing, user management)
- **StaffOnly:** Operational tasks (booking management, driver assignment)
- Use StaffOnly for most endpoints to support both admin and dispatcher

---

### Q: What about OAuth credentials for LimoAnywhere?
**A:** NOT in AuthServer. OAuth credential management should be in AdminAPI (the component that uses them).

---

### Q: Where can I find implementation details?
**A:** See `Docs/AuthServer-Phase2.md` for complete implementation guide (AuthServer team only). This document is just an integration reference.

---

## ? Summary

**What Changed:**
- New `dispatcher` role available ?
- Authorization policies implemented ?
- Role assignment endpoint created ?
- Admin endpoints protected ?

**Component Impact:**
- **AdminAPI:** Implement policies, mask fields for dispatchers
- **Admin Portal:** Hide UI elements based on role
- **Driver App:** No changes
- **Passenger App:** No changes

**Key Points:**
- JWT tokens now may contain `dispatcher` role
- Dispatchers should see operational data but NOT billing
- Use `StaffOnly` policy for endpoints both admin and dispatcher need
- Use `AdminOnly` policy for sensitive endpoints

---

**Status:** ?? **INFORMATIONAL REFERENCE**  
**Purpose:** Inform other teams of AuthServer Phase 2 changes  
**Audience:** AdminAPI, Admin Portal, Mobile App teams  
**Next Steps:** Implement Phase 2 in respective components  

---

*This document describes what the AuthServer team completed in Phase 2. Implementation instructions for other components will be provided separately by component leads.* ???
