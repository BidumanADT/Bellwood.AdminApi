# User Management API Reference

**Document Type**: Living Document - Technical Reference  
**Last Updated**: February 8, 2026  
**Status**: ? Production Ready

---

## ?? Overview

**Purpose**: Admin-only endpoints for managing users in AdminPortal. All requests are proxied to AuthServer for centralized user management.

**Authorization**: `AdminOnly` (requires `admin` role in JWT)

**Integration**: AdminAPI acts as a secure proxy to AuthServer's canonical user management API (`/api/admin/users`).

**Important Notes**:
- Responses return **direct arrays** (not wrapped in `{users:[...]}` objects)
- Role names are **lowercase** (`"admin"` not `"Admin"`)
- All property names are **camelCase** (`"userId"` not `"UserId"`)

---

## ?? DTO Format Specification

### AdminUserDto

**Complete DTO matching AuthServer reference implementation**:

```csharp
using System.Text.Json.Serialization;

public class AdminUserDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;
    
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }
    
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; init; }  // NOT nullable!
    
    [JsonPropertyName("createdAtUtc")]
    public DateTime? CreatedAtUtc { get; init; }
    
    [JsonPropertyName("modifiedAtUtc")]
    public DateTime? ModifiedAtUtc { get; init; }
}
```

**Key Points**:
- ? All 9 fields included (even if null)
- ? `[JsonPropertyName]` attributes ensure camelCase
- ? `isDisabled` is `bool` (not `bool?`)
- ? Roles are lowercase strings

---

## ?? API Endpoints

### GET /users/list

**Description**: List users with pagination

**Auth**: `AdminOnly`

**Query Parameters**:

| Parameter | Type | Default | Max | Description |
|-----------|------|---------|-----|-------------|
| `take` | integer | 50 | 200 | Number of users to return |
| `skip` | integer | 0 | - | Number of users to skip (pagination) |

**Request**:
```http
GET /users/list?take=50&skip=0 HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
```

**Response** (200 OK):
```json
[
  {
    "userId": "914562c8-f4d2-4bb8-ad7a-f59526356132",
    "username": "alice",
    "email": "alice.admin@bellwood.example",
    "firstName": null,
    "lastName": null,
    "roles": ["admin"],
    "isDisabled": false,
    "createdAtUtc": null,
    "modifiedAtUtc": null
  },
  {
    "userId": "66cdb99f-e309-4021-be81-a88b0eab5c4f",
    "username": "charlie",
    "email": "charlie.driver@bellwood.example",
    "firstName": null,
    "lastName": null,
    "roles": ["driver"],
    "isDisabled": false,
    "createdAtUtc": null,
    "modifiedAtUtc": null
  }
]
```

**Response Format**:
- ? **Direct array** (NOT `{users:[...], pagination:{...}}`)
- ? All property names are **camelCase**
- ? Role names are **lowercase**
- ? `isDisabled` is boolean (not nullable)

**Field Descriptions**:

| Field | Type | Description |
|-------|------|-------------|
| `userId` | string (GUID) | Unique user identifier from AuthServer |
| `username` | string | Login username (unique) |
| `email` | string | Email address (empty string if not set) |
| `firstName` | string? | First name (null for Phase Alpha) |
| `lastName` | string? | Last name (null for Phase Alpha) |
| `roles` | string[] | User roles (lowercase: `"admin"`, `"dispatcher"`, `"driver"`, `"booker"`) |
| `isDisabled` | boolean | Account disabled status (true = locked out) |
| `createdAtUtc` | DateTime? | Account creation timestamp (null for Phase Alpha) |
| `modifiedAtUtc` | DateTime? | Last modification timestamp (null for Phase Alpha) |

**Error Responses**:
- **503 Service Unavailable**: AuthServer unreachable (10s timeout)
- **401 Unauthorized**: Invalid or missing JWT token
- **403 Forbidden**: User lacks `admin` role

**Side Effects**:
- Audit log created (`User.Listed`)

---

### POST /users

**Description**: Create a new user with assigned roles

**Auth**: `AdminOnly`

**Request**:
```http
POST /users HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "email": "diana@bellwood.example",
  "firstName": "Diana",
  "lastName": "Prince",
  "tempPassword": "TempPass123!",
  "roles": ["Dispatcher"]
}
```

**Request Body**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `email` | string | Yes | Email address (unique) |
| `firstName` | string | No | First name |
| `lastName` | string | No | Last name |
| `tempPassword` | string | Yes | Temporary password (min 10 characters) |
| `roles` | string[] | Yes | User roles (case-insensitive, normalized to lowercase) |

**Response** (201 Created):
```json
{
  "userId": "new-user-guid-here",
  "username": "diana@bellwood.example",
  "email": "diana@bellwood.example",
  "firstName": "Diana",
  "lastName": "Prince",
  "roles": ["dispatcher"],
  "isDisabled": false,
  "createdAtUtc": "2026-02-08T12:00:00Z",
  "modifiedAtUtc": null
}
```

**Valid Roles** (case-insensitive input, normalized to lowercase):
- `Admin` ? `"admin"`
- `Dispatcher` ? `"dispatcher"`
- `Driver` ? `"driver"`
- `Booker` ? `"booker"` (passenger/customer)

**Validation Rules**:
- `email` is required and must be unique
- `tempPassword` must be at least 10 characters
- `roles` must contain at least one valid role
- Only admins can assign the `Admin` role (403 Forbidden for non-admins)

**Error Responses**:
- **400 Bad Request**: 
  - Missing `email` or `tempPassword`
  - Password < 10 characters
  - Invalid role name
  - Roles array empty
- **409 Conflict**: User with this email already exists
- **403 Forbidden**: Non-admin attempting to assign `Admin` role

**Side Effects**:
- User created in AuthServer
- Audit log created (`User.Created`)
- **Password is never logged** (security measure)

---

### PUT /users/{userId}/roles

**Description**: Replace user's roles (overwrites existing roles)

**Auth**: `AdminOnly`

**Request**:
```http
PUT /users/914562c8-f4d2-4bb8-ad7a-f59526356132/roles HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "roles": ["Admin", "Dispatcher"]
}
```

**Request Body**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `roles` | string[] | Yes | New roles (replaces all existing roles) |

**Response** (200 OK):
```json
{
  "userId": "914562c8-f4d2-4bb8-ad7a-f59526356132",
  "username": "alice",
  "email": "alice.admin@bellwood.example",
  "firstName": null,
  "lastName": null,
  "roles": ["admin", "dispatcher"],
  "isDisabled": false,
  "createdAtUtc": null,
  "modifiedAtUtc": "2026-02-08T12:30:00Z"
}
```

**Important**:
- This endpoint **replaces** all roles (not additive)
- To remove all roles except one, send only that role
- Only admins can assign the `Admin` role

**Error Responses**:
- **400 Bad Request**: Invalid role name or empty roles array
- **404 Not Found**: User not found
- **403 Forbidden**: Non-admin attempting to assign `Admin` role

**Side Effects**:
- User roles updated in AuthServer
- Audit log created (`User.Roles.Updated`)
- **User must re-login** to get new roles in JWT

---

### PUT /users/{userId}/disable

**Description**: Disable or enable a user account

**Auth**: `AdminOnly`

**Request (Disable)**:
```http
PUT /users/914562c8-f4d2-4bb8-ad7a-f59526356132/disable HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "isDisabled": true
}
```

**Request (Enable)**:
```http
PUT /users/914562c8-f4d2-4bb8-ad7a-f59526356132/disable HTTP/1.1
Host: localhost:5206
Authorization: Bearer {adminToken}
Content-Type: application/json

{
  "isDisabled": false
}
```

**Request Body**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `isDisabled` | boolean | Yes | `true` = disable account, `false` = enable account |

**Response** (200 OK):
```json
{
  "userId": "914562c8-f4d2-4bb8-ad7a-f59526356132",
  "username": "alice",
  "email": "alice.admin@bellwood.example",
  "firstName": null,
  "lastName": null,
  "roles": ["admin"],
  "isDisabled": true,
  "createdAtUtc": null,
  "modifiedAtUtc": "2026-02-08T12:45:00Z"
}
```

**Behavior**:
- `isDisabled: true` ? Calls `PUT /api/admin/users/{userId}/disable` on AuthServer
- `isDisabled: false` ? Calls `PUT /api/admin/users/{userId}/enable` on AuthServer

**Error Responses**:
- **400 Bad Request**: Missing `isDisabled` field
- **404 Not Found**: User not found

**Side Effects**:
- User account disabled/enabled in AuthServer
- Audit log created (`User.Disabled.Updated`)
- **Disabled users cannot log in** (401 on AuthServer login)

---

## ?? Security & Architecture

### Proxy Pattern

```
AdminPortal ? AdminAPI ? AuthServer
             (proxy)    (canonical)
```

**Why AdminAPI Proxies User Management**:
1. **Centralized Authentication**: AuthServer is the single source of truth for users
2. **Consistent UI**: AdminPortal uses AdminAPI for all operations
3. **Audit Trail**: AdminAPI logs all user management actions
4. **Security**: AdminAPI validates admin role before proxying
5. **Error Handling**: AdminAPI provides consistent error responses

**Data Flow**:
1. AdminPortal sends request to AdminAPI (`/users/list`)
2. AdminAPI validates admin role (403 if not admin)
3. AdminAPI forwards request to AuthServer (`/api/admin/users`)
4. AuthServer performs operation and returns result
5. AdminAPI maps response to standardized format
6. AdminAPI creates audit log
7. AdminAPI returns response to AdminPortal

---

### Timeout Configuration

**AdminAPI has a 10-second timeout** for AuthServer requests:

```csharp
builder.Services.AddHttpClient<AuthServerUserManagementService>()
    .ConfigureHttpClient(client =>
    {
        // Prevent hanging if AuthServer is slow (not down)
        client.Timeout = TimeSpan.FromSeconds(10);
    });
```

**Behavior**:
- ? Prevents infinite waiting if AuthServer is slow
- ? Returns clear timeout error instead of hanging
- ? Allows AdminPortal to show user-friendly error message

---

### Password Security

**Temporary passwords are never logged**:

```csharp
// ? NEVER DO THIS
logger.LogInformation("Creating user with password: {Password}", request.TempPassword);

// ? CORRECT
logger.LogInformation("Creating user with email: {Email}", request.Email);
```

**Security Properties**:
- Passwords are sent to AuthServer over HTTPS only
- Request logging explicitly excludes password fields
- Audit logs record user creation but not passwords
- Temporary passwords must be changed on first login (AuthServer enforces)

---

### Role Normalization

**Input roles are case-insensitive**:

```csharp
// All of these are valid and normalized to lowercase
"Admin" ? "admin"
"ADMIN" ? "admin"
"admin" ? "admin"
"Dispatcher" ? "dispatcher"
"Driver" ? "driver"
```

**Implementation**:
```csharp
public static bool TryNormalizeRoles(
    IEnumerable<string> roles, 
    out List<string> normalized, 
    out string? error)
{
    normalized = roles
        .Select(r => r.ToLowerInvariant())
        .Distinct()
        .ToList();
    
    var invalidRoles = normalized
        .Except(ValidRoles, StringComparer.OrdinalIgnoreCase)
        .ToList();
    
    if (invalidRoles.Any())
    {
        error = $"Invalid roles: {string.Join(", ", invalidRoles)}";
        return false;
    }
    
    error = null;
    return true;
}
```

---

## ?? Testing Examples

### Example 1: List Users

```bash
# Get admin token
ADMIN_TOKEN=$(curl -s -X POST https://localhost:5001/login \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}' \
  | jq -r '.accessToken')

# List users
curl -X GET "https://localhost:5206/users/list?take=10&skip=0" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

---

### Example 2: Create User (Dispatcher)

```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "diana@bellwood.example",
    "firstName": "Diana",
    "lastName": "Prince",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }'
```

**Expected**: 201 Created with user object

---

### Example 3: Update User Roles

```bash
curl -X PUT "https://localhost:5206/users/{userId}/roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "roles": ["Admin", "Dispatcher"]
  }'
```

**Expected**: 200 OK with updated user

---

### Example 4: Disable User

```bash
curl -X PUT "https://localhost:5206/users/{userId}/disable" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": true
  }'
```

**Expected**: 200 OK with updated user (`isDisabled: true`)

---

## ?? Error Scenarios

### Duplicate Email

```bash
# Create user twice with same email
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "duplicate@example.com",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }'
```

**Expected**: 409 Conflict with message "User already exists"

---

### Invalid Role

```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "tempPassword": "TempPass123!",
    "roles": ["InvalidRole"]
  }'
```

**Expected**: 400 Bad Request with message listing allowed roles

---

### Password Too Short

```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "tempPassword": "short",
    "roles": ["Dispatcher"]
  }'
```

**Expected**: 400 Bad Request with message "tempPassword must be at least 10 characters long"

---

## ?? Related Documents

- `20-API-Reference.md` - Complete API documentation
- `23-Security-Model.md` - JWT authentication & authorization
- `11-User-Access-Control.md` - RBAC implementation
- `Docs/Alpha-UserManagement-AdminApi.md` - Implementation notes

---

## ?? Summary

**What This API Provides**:
- ? Complete user CRUD operations
- ? Role assignment and management
- ? Account enable/disable
- ? Pagination support
- ? Audit logging
- ? AuthServer proxy pattern

**Key Features**:
- ?? Admin-only access (security)
- ?? Direct array responses (AdminPortal compatibility)
- ?? Role normalization (case-insensitive)
- ?? 10-second timeout (prevents hanging)
- ?? Audit trail (all actions logged)
- ?? Password security (never logged)

**Ready for AdminPortal integration!** ??

---

**Last Updated**: February 8, 2026  
**Status**: ? Production Ready  
**API Version**: 1.0 (Alpha)
