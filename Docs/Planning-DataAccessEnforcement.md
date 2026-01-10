# User-Specific Data Access Enforcement - Platform Overview

**Initiative:** Enforce user-specific data access across Bellwood Global platform  
**Status:** 📋 **PLANNING - OVERVIEW COMPLETE**  
**Priority:** 🔴 **CRITICAL - BLOCKING ALPHA TESTING**  
**Last Updated:** January 10, 2026

---

## 📋 Executive Summary

**Problem:** The Bellwood Global platform currently lacks robust role-based access control (RBAC) and per-user data isolation, creating significant security and privacy risks.

**Scope:** Five interconnected components:
- Passenger Mobile App
- Driver Mobile App  
- Admin Web Portal
- Admin API
- AuthServer

**Goal:** Implement comprehensive user-specific data access controls across all components using ownership metadata, expanded RBAC, and full auditing.

**Impact:** Critical security improvement required before alpha testing and production release.

---

## 🔍 Current State of Data Access Controls

Bellwood Global's platform currently consists of five main components – a Passenger mobile app, Driver mobile app, Admin web portal, Admin API, and an AuthServer – integrated to manage executive car service bookings. The backend uses ASP.NET Core Web API with Entity Framework Core (EF Core) on Azure SQL for data storage, and authentication is handled via ASP.NET Core Identity with JWT tokens.

### AuthServer (JWT Authentication)

The AuthServer issues JWTs containing the username (`sub`), a `uid` (user identifier), and user role claims. It uses ASP.NET Core Identity with basic role support (roles like "admin", "booker", "driver" are defined). However, role-based access control (RBAC) is minimal – currently only a Driver role is explicitly used for restricting certain endpoints.

**Current Implementation:**
- A "DriverOnly" policy is defined to require the driver role
- No similar policies exist yet for admin or dispatcher roles
- AuthServer creates an "admin" role in the database, but Admin APIs are not specifically locked down to it in code

---

### AdminAPI (Core Business API)

The AdminAPI exposes endpoints for quotes, bookings, drivers, etc. All endpoints require a valid JWT (users must be authenticated) but many endpoints are not restricted by role or ownership.

**Current Behavior:**
- Bookings endpoints allow any authenticated user to list or fetch bookings
- No filtering by user on general list/detail endpoints
- `GET /bookings/list` returns recent bookings without regard to who created them
- `GET /quotes/list` behaves similarly
- Endpoints like `GET /drivers/list` are open to any authenticated user by default

**Impact:** A passenger or driver with a valid token could theoretically retrieve data not associated with them (a clear violation of least-privilege access).

---

### Data Ownership Tracking

The system does not yet tag records with the creating user's identity. Booking and quote records store names/emails of the booker and passenger (e.g. `BookerName`, `PassengerName`, and email addresses in the draft data), but there is no explicit `CreatedByUserId` or similar field linking the record to an AuthServer user account.

**Current Implementation:**

| Entity Type | User ID Tracking | Method | Status |
|-------------|------------------|--------|--------|
| **Bookings** | ❌ No | Email only | Missing |
| **Quotes** | ❌ No | Email only | Missing |
| **Drivers** | ✅ Yes | `UserUid` field | Working |

**Driver Implementation (Only Working Example):**
- Driver accounts in AuthServer have a custom `uid` claim (like "driver-001")
- This matches a `UserUid` field on the Driver entity in the AdminAPI
- Used to link drivers to their assigned rides
- When a driver logs in, their JWT uid corresponds to the Driver's `UserUid`
- AdminAPI filters driver-specific endpoints by comparing `AssignedDriverUid` to the token's uid

**Missing:** Passenger and dispatcher roles are not explicitly linked to data records in the current design.

---

### Role Definitions

In practice, Admins and Dispatchers are not distinguished by the system yet – likely both are using admin-level accounts.

**Current Roles:**

| Role | Users | Purpose | Status |
|------|-------|---------|--------|
| `admin` | alice, bob (test) | Full system access | ✅ Exists, ❌ Not enforced |
| `booker` | Passengers, concierges | Create bookings/quotes | ✅ Exists |
| `driver` | Drivers | Accept rides, update location | ✅ Exists, ✅ Enforced |

**Notes:**
- Test seeds show users "alice" and "bob" assigned the admin role (presumably representing staff)
- Passengers (and concierges acting on passengers' behalf) use accounts with the "booker" role
- Drivers use accounts with the "driver" role
- AuthServer issues all users a default `uid` claim equal to their internal user ID (GUID) unless overridden
- Drivers override it with a custom code

**Missing:**
- No dispatcher role exists yet
- No fine-grained differentiation in the API between what an admin can do versus a dispatcher

---

### Data Access Behavior

As a result of the above, user-specific data access is only partially enforced:

#### Drivers: ✅ **Somewhat Constrained**

- Drivers are somewhat constrained to their data when using dedicated driver endpoints
- `GET /driver/rides/today` only returns rides where `AssignedDriverUid` matches the driver's `uid` claim (requires driver role)
- A driver attempting to fetch a ride not assigned to them is explicitly forbidden in the code
- **This is a positive example of ownership enforcement for one role**

#### Passengers: ❌ **NOT CONSTRAINED**

- Passengers (and concierges) currently use general booking and quote endpoints which do not enforce ownership checks
- **Only one ownership check implemented:** `GET /passenger/rides/{id}/location` verifies that the caller's email matches the booking's passenger or booker email before returning the driver's location
- This is a narrow check to ensure a passenger only sees live location for "their" ride
- Apart from that, a passenger with a valid token could call other endpoints (like listing all bookings) and the API would return data beyond their scope

#### Admin Portal Users: ❌ **TOO BROAD**

- Admins/dispatchers currently have broad access
- AdminAPI does not yet implement an "admin-only" policy (there's a TODO comment to add one)
- Any authenticated user token could technically call admin endpoints (like viewing all active driver locations, listing all affiliates, etc.)
- In practice the portal likely obtains an admin JWT, but the API itself isn't locking out non-admins from those routes yet
- Dispatchers currently would have to be given an admin token to use the portal at all
- They can see everything an admin can (including billing info), since no lesser-privileged role is in effect

---

## 🚨 Data Exposure Risks and Least-Privilege Violations

The lack of strict data isolation in the current implementation poses several security and privacy risks:

### Risk 1: Passengers or Concierges Viewing Others' Data

**Vulnerability:**
- Booking and quote APIs are not filtered by user
- A malicious or curious passenger could attempt to retrieve data that isn't theirs
- Calling `GET /bookings/list` with a passenger's token would return **all recent bookings in the system**
- Similarly, they could fetch any specific booking by ID (if known) since `GET /bookings/{id}` does not verify that the user is related to that booking

**Impact:** Violates the principle of least privilege – passengers should only see their own quotes, bookings, and ride history.

---

### Risk 2: Drivers Accessing Passenger Data

**Vulnerability:**
- A driver, who should only see rides assigned to them, could theoretically call general endpoints to see other rides or passenger info
- While drivers have their own locked-down endpoints for daily rides, nothing currently prevents a driver token from invoking `/bookings/list` or `/passenger/rides/{id}/location` for a ride they are not assigned
- Without additional checks, the API would return data as long as the JWT is valid (driver tokens still pass the generic `.RequireAuthorization()` check on those endpoints)

**Impact:** This cross-role data access is unintended and could expose sensitive customer information to drivers who should not have it.

---

### Risk 3: Dispatchers Seeing Billing Information

**Vulnerability:**
- By design, dispatchers are not supposed to see or modify billing details
- In the current setup, however, dispatchers have to use admin credentials (since no separate role exists)
- That means dispatchers logging into the Admin Portal effectively have full admin privileges and visibility
- They can see payment method IDs, last4 of credit cards, receipts, and any financial data present in booking records
- Booking draft data returned by the API includes payment method identifiers and last4 digits of the card, which would be visible to anyone with access to that booking record

**Impact:** Clear violation of the intended separation of duties – dispatch staff should be restricted from viewing payment or billing details, but currently there's no mechanism to do so.

---

### Risk 4: No Created-By Tracking

**Vulnerability:**
- The absence of a `CreatedBy` field on records is not only a logging issue but also a security issue
- Without it, the system cannot easily enforce ownership in queries (since it doesn't know which user owns a given record except by comparing email strings as a proxy in one case)

**Impact:**
- **No accountability:** We cannot tell from the data who actually created or changed a booking or quote
- **Complicates audits:** Could allow a user to manipulate data without trace
- **Example:** If an insider (say a dispatcher) created an unauthorized booking or altered a price, there's no audit trail field capturing their user ID
- **Compliance:** Lack of audit trails is recognized as an issue (it's on the backlog as a must-have)

---

### Risk 5: Shared Reference Data Over-Exposure

**Vulnerability:**
- While it's intended that certain reference data (like vehicle types, pricing structures, etc.) be globally readable, currently **all data is effectively globally readable** to authenticated users
- The system does not distinguish reference data endpoints from sensitive data endpoints in terms of access
- This means even things that should perhaps be limited (like driver personal info in `/drivers/list`) are accessible to any logged-in user today

**Conversely:**
- Reference data like vehicle lists should ideally be accessible broadly (since it's not sensitive)
- Must ensure that in securing other endpoints we don't inadvertently block necessary reads

---

### Risk 6: Least Privilege Principle Violations

**Summary of Violations:**
- Users are operating with far more privilege than needed due to the coarse security model
- A passenger or concierge has the same API read access as an admin in many cases (they're all just "authenticated users" to most of the endpoints)
- A dispatcher currently must be an admin, thus has the ability to perform actions beyond their intended scope (like adjusting billing or managing user accounts, which they shouldn't do)

**Impact:**
- Not only risk data leaks but also run afoul of privacy expectations
- One customer could potentially access another's trip details, which would be unacceptable in a production scenario

---

## 🎯 Proposed Solution: Per-User Data Isolation and Auditing

To ensure that only authorized users can access or manipulate the data they own or are associated with, we propose introducing explicit ownership metadata on records and applying filtering and role checks throughout the system.

### Key Elements of the Technical Solution

#### 1. Add "CreatedBy" (Owner) Field to Data Records

**Implementation:**
- Extend the data model for all key entities (Bookings, Quotes, Payments, etc.) to include a `CreatedBy` field
- Store the identifier of the user who created the record
- Use the user's unique ID (GUID) from AuthServer or another stable identifier
- Since the JWT already includes a `uid` claim (which for non-driver users defaults to the Identity GUID), we can use that as the stored reference

**Every Record Creation:**
- Every time a new booking or quote is created via the API, the backend will record the current user's ID in `CreatedBy`
- This creates an explicit link between data and the user account

**Audit Fields:**
- Maintain timestamps such as `CreatedOn` (many entities already have `CreatedUtc`)
- Introduce `ModifiedOn` and `ModifiedBy` fields for audit purposes
- These audit fields will capture when a record is updated and by which user

**Example Schema:**
```
Booking:
  - CreatedByUserId
  - CreatedOnUtc
  - ModifiedByUserId
  - ModifiedOnUtc
```

---

#### 2. Enforce Ownership Filtering in Queries

With ownership metadata in place, the AdminAPI can enforce that non-admin users only access their own records.

**For Passenger/Concierge Users (role: booker):**
- Any `GET /quotes/...` or `GET /bookings/...` endpoint will filter results to `WHERE CreatedBy = <current user>`
- Assumes the booker is always the creator
- A passenger only gets their own quotes or bookings
- Implementation: Repository layer (e.g., `ListBookings(userId)` returns bookings where `CreatedByUserId == userId`) or via LINQ filtering

**Interpretation:**
- "Data associated with them" = data they created
- Because the system distinguishes Booker vs Passenger in a booking, someone could create a booking for another passenger

**Future Enhancement:**
- If we want a passenger to see bookings that were created by their concierge on their behalf, we would extend this model
- Possibly by linking the actual Passenger's user ID to the record as well (see "Associated Users" below)

**For Drivers (role: driver):**
- Continue using the existing mechanism: drivers are associated with bookings via the `AssignedDriverUid`
- System already tags each booking with `AssignedDriverUid` when a driver is assigned
- Ensure all driver-facing endpoints query by this field (as is done in `GET /driver/rides/today` currently)
- **Rule:** Driver can only see or update a booking if `AssignedDriverUid` matches their own `uid`
- This check is already present for fetching ride details and status updates, and will be enforced universally on any new driver-related endpoints

**For Dispatchers (new role to be introduced):**
- Access limited to operational data but excluding billing-sensitive info
- Dispatchers will be allowed to see **all bookings and quotes** (to do their job)
- Similar to admin in breadth but with certain fields or actions disallowed
- Since dispatchers are company staff, we **do not filter out records by `CreatedBy`** for them – they may need to see all customer bookings
- Instead, enforce restrictions at the field/action level (see "Dispatcher restrictions" below)

**Dispatcher Field Masking:**
- Dispatcher can retrieve any booking to manage assignments
- If the booking object contains payment details or customer billing info, the API can omit or mask those fields unless an admin is requesting
- Implementation: Check the user's role in the result construction
- If `role == dispatcher`, set sensitive fields (like credit card token, billing amount, etc.) to null in the response
- This way, dispatchers get the data they need (pickup/dropoff, passenger name, etc.) but not the data they shouldn't see

**Shared Reference Data:**
- Reference data (e.g., list of vehicle types, standardized pricing tables, etc.) will remain accessible to all authenticated roles
- These could even be made public endpoints if they contain no sensitive info
- Key: We won't restrict these reads by user, since by design they are global
- However, ensure that modification of reference data (like adding vehicles) is limited to admins in the RBAC rules

---

#### 3. Implement Role-Based Checks (RBAC)

Expand the authorization policies in the AdminAPI to account for the new roles and restrictions:

**"AdminOnly" Policy:**
- Require the admin role
- Endpoints that should only be callable by administrators:
  - User management
  - Viewing financial reports
  - Full ride history export
- Example: `GET /admin/locations` (returns all active driver locations) should be admin-only
- Enforce with `.RequireAuthorization("AdminOnly")`

**"StaffOnly" or "DispatcherOrAdmin" Policy:**
- Allow either admin or dispatcher roles
- Use on endpoints that both admins and dispatchers are permitted to use (operational management endpoints)
- Ensures external roles like passengers/drivers cannot call these, while giving dispatchers needed access
- Example: `POST /bookings/{id}/assign-driver` (dispatching a ride) would fall under this policy
- Dispatchers should be able to assign drivers (but passengers should not)

**"BookerOnly" Policy (Optional):**
- For passenger-specific endpoints if needed
- In many cases, simply filtering by user ID is enough
- If we want to prevent drivers from ever hitting passenger endpoints (even if they wouldn't see data due to filtering), mark certain routes as BookerOnly
- Similarly, driver endpoints already have DriverOnly
- This further compartmentalizes the API surface

**JWT Support:**
- AuthServer already includes roles in JWTs (`"role": "driver"/"admin"/"booker"`)
- Ensure that new roles (like dispatcher) are also included when assigned
- AdminAPI's JWT validation is configured to map the role claim correctly, so it will honor these policies once defined

---

#### 4. Audit Trail and "CreatedBy" Usage

**Benefits of CreatedBy Field:**
- By using the `CreatedBy` field for enforcement, we also naturally improve auditing
- Every record will carry the ID of who created it
- Updates will carry who last modified it

**Action Logging:**
- Supplement with logging of sensitive actions
- Example: When a booking status is changed or a ride is canceled, the API should log an entry including:
  - Acting user's ID
  - Timestamp
- Some of this is already done (e.g., logging the booker's name on cancellation), but it should log the actual user account as well

**Audit Log Repository:**
- Implement an audit log repository that the AdminAPI writes to on each critical operation (create/modify/delete)
- Could be an Azure SQL table or an external logging service
- At minimum, capture:
  - **Who:** user id
  - **What:** action and record id  
  - **When:** timestamp
- Addresses the backlog item for API call audit logs and provides traceability

---

#### 5. Note on "Associated With Them"

The phrase "data they created or are associated with" covers cases like:
- A driver's assigned rides (not created by the driver, but associated via assignment)
- A passenger who is the traveler even if an assistant booked the ride

**Current Coverage:**
- **For drivers:** Our solution covers this via `AssignedDriverUid` matching
- **For passengers:** Currently the system doesn't have a concept of a passenger's user account linked to the ride if someone else booked it

**Future Implementation:**
- Might implement a `PassengerUserId` on bookings
- Have the AuthServer include a claim for user's customer ID
- Example: An executive could see trips their assistant booked for them
- The code even hints at this future plan: the passenger location endpoint has a comment about checking a `PassengerId` claim when implemented

**Implementation Details:**
- Would mean storing the passenger's user ID on the booking (if the passenger has an account and is the actual rider)
- Then allow access if either `CreatedBy == user` **OR** `PassengerUserId == user`
- This is an enhancement for down the road

**Current Assumption:**
- If a concierge books for an executive, the concierge's account is the one managing the booking
- The executive likely relies on the concierge (the exec might not log into the app themselves in such scenarios)

**Design Principle:**
- Design the system such that no matter what, data is only shown to the users who either:
  - Own it, **OR**
  - Have a defined role-based need for it
- Eliminates any chance of an unrelated customer or driver seeing someone else's private information

**Summary:** This solution introduces a clear notion of record ownership and role-based access. By adding a `CreatedBy` field and using it in authorization checks, and by extending RBAC policies for different user types, we lock down the data so each user only touches what they're meant to.

---

## 🔐 RBAC Improvements for AuthServer and Portal

Upgrading the AuthServer to handle richer role-based access is critical to enforcing the above rules. Currently, AuthServer uses ASP.NET Core Identity and already supports roles (admin, booker, driver). We will build on this by introducing any missing roles and providing administrative management of roles.

### Define New Roles

**New Role: Dispatcher**
- Add a Dispatcher role in the AuthServer's Identity setup
- Can be done via the RoleManager seeding (similar to how "admin" and "booker" are created on startup)

**Concierge Role (Optional):**
- If concierges are to be treated distinctly (though likely they can be lumped under the "booker" role since they use the passenger app)
- Could also introduce a "concierge" role
- However, it may be sufficient to use the existing "booker" role for both passengers and concierges
- Handle any nuanced differences through data (for instance, a concierge might have an affiliation with a specific passenger in our records, which can be managed without a separate role)

**Key Addition:** The dispatcher role, which fills the gap between admin and normal user.

---

### Role Assignment and Management

In the absence of a full admin UI for user management, we have a couple of options:

**Option 1: Extend AuthServer Admin Endpoints**
- AuthServer already has some admin endpoints for creating users (e.g. creating driver users with a UID claim via `/api/admin/users/drivers`)
- Add endpoints such as `PUT /api/admin/users/{username}/roles` to assign or change roles
- Ensure only an admin can call these
- Through the Admin Portal, an administrator could promote a user to dispatcher or change roles without direct database edits

**Option 2: Integrate into Admin Portal UI**
- Integrate basic user-role management into the Admin Portal's UI
- Example: When creating or editing an administrative user in the portal, include a dropdown for role (Admin vs Dispatcher)
- Since the Admin Portal communicates with AuthServer for user management, it can call the new endpoints to set roles accordingly

**Initial Seeding:**
- Ensure initial seeding covers at least one admin user (already done with "alice" and "bob" in dev)
- Possibly a sample dispatcher for testing

---

### Token Enhancements

**Current JWT Claims:**
```json
{
  "sub": "username",
  "uid": "user-guid",
  "role": "admin"
}
```

**What Works:**
- JWT tokens already include all roles assigned to the user (as multiple role claims)
- Must ensure that continues (the Identity system by default will include all roles)
- AuthServer mapping is preserving the "role" claim properly

**With New Roles:**
- Nothing special needed in the token format

**Future Consideration: OAuth2 Scopes**
- Consider using standard OAuth2 scopes in the future
- Example: Issue tokens with a `scope` claim indicating the client app and allowed operations
- AuthServer's `/connect/token` already supports a `scope` parameter (currently likely not enforcing scope beyond including it)
- Aligning with OAuth 2.0 best practices:
  - Passenger app tokens: scope like `"app.passenger"`
  - Driver app tokens: `"app.driver"`
  - In addition to roles
- Not strictly required for functionality, but forward-looking improvement
- Makes tokens more standards-compliant for integration with LimoAnywhere (which uses OAuth2 scopes)

---

### Strengthening AuthServer Security

While adjusting roles, should also consider improving the AuthServer in general:

**Password Policies:**
- Enforce stricter password policies **OR**
- Implement an option for MFA for admin accounts (high privilege)

**Token Storage:**
- Ensure refresh tokens and client secrets are stored and handled securely
- AuthServer currently uses an in-memory refresh token store
- In production, want a persistent, secure store or to leverage Identity's token features

**External Identity Providers:**
- Prepare the AuthServer to potentially integrate with external identity providers if needed
- Example: If Bellwood wants to allow SSO or integrate driver logins with LimoAnywhere's identity
- Having a modular IdentityServer setup could help
- At minimum, AuthServer should be ready to accept OAuth2 authorization code flows for the LimoAnywhere customer API (if we allow Bellwood users to log in to LA through our app)

---

### Admin Portal Role Awareness

The Blazor Admin Portal will be updated to be role-aware. Currently, it likely assumes an admin user.

**UI Changes Based on Role:**

**Display/Hide Elements:**
- If a dispatcher is logged in (their JWT `role` claim = `dispatcher`), the portal could hide menus related to:
  - Billing reports
  - User management (which they shouldn't use)
- Conversely, admins see everything

**Prevent Restricted Actions (Client-Side):**
- Extra layer of security
- Example: "Edit billing info" or "View invoice" buttons might simply not be rendered for dispatchers
- Backend will also block if somehow invoked, but good UI/UX dictates we don't even present options the user cannot do

**JWT Management:**
- Ensure that the portal obtains the JWT and sets it on API calls (this is already presumably happening)
- Might use a library to manage authorization in Blazor
- Or manually decode the token to check role claims in the portal code and adjust the UI

---

### Dispatcher Restrictions Implementation

Since dispatchers should not see billing data, how to enforce that? Combination of backend and frontend:

**Backend Enforcement:**
- For any API response that includes sensitive fields (like payment information, customer billing address, fare prices if that's considered sensitive), conditionally exclude or mask those fields for dispatchers
- Example: If the Booking detail DTO currently contains a payment method or price breakdown, remove those properties if the caller is in the dispatcher role
- If such data is separate (say an endpoint `/bookings/{id}/receipt` that returns cost details), simply restrict that endpoint to AdminOnly

**Frontend Enforcement:**
- Do not show billing screens to dispatchers
- Perhaps have separate views:
  - **Admins:** Full dashboard including financials
  - **Dispatchers:** Operations dashboard (rides, drivers, quotes approval) without finance tabs
- This delineation can be made clear in the UI

**Logging:**
- If a dispatcher attempts an unauthorized action (like somehow tries to hit an admin endpoint), the attempt should be logged and forbidden
- Mostly covered by the AuthServer not giving them the admin role and the AdminAPI enforcing policies

---

## 📝 Summary

By bolstering AuthServer with proper roles and making the AdminAPI aware of those roles in its authorization policies, we set the foundation for principle-of-least-privilege access.

**The combination of:**
1. **Role-based authorization** (who can call an endpoint at all)
2. **Per-user filtering** (what data they can see once authorized)

**Will fully partition the data:**
- ✅ Admins can do everything
- ✅ Dispatchers can do most things but not see certain fields
- ✅ Drivers only see their rides
- ✅ Passengers/concierges only see their bookings

---

## 🔗 Related Documentation (Within BellwoodGlobal.Mobile/Docs)

- `Planning-UserAccountIsolation.md` - Mobile app-specific isolation details
- `Next-Steps-UserAccountIsolation.md` - Mobile app implementation roadmap
- `Reference-BugFixes.md` - Bug #9 (Bookings access without authorization)
- `Feature-LocationTracking.md` - Example of proper authorization (email-based)

---

**Status:** 📋 **PLANNING - OVERVIEW COMPLETE**  
**Priority:** 🔴 **CRITICAL - BLOCKS ALPHA TESTING**  
**Version:** 1.0  
**Last Updated:** January 10, 2026

---

*This overview provides the foundation for detailed implementation planning across all platform components.* 🔐✨
