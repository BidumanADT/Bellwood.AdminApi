# AdminPortal User Management (Alpha)

**Purpose**: Admin-only endpoints in AdminAPI that proxy to AuthServer for user creation and role management.

**Integration**: AdminAPI proxies user management requests to AuthServer canonical routes (`/api/admin/users`).

## Roles & Authorization
- **Required role**: `admin` (JWT role claim).
- Requests must include a valid bearer token from AuthServer.
- AdminAPI validates admin role before proxying requests to AuthServer.

## AuthServer Integration

AdminAPI acts as a secure proxy to AuthServer for user management operations. All requests are forwarded to AuthServer's canonical user management API:

- **List Users**: `GET /api/admin/users?take=50&skip=0`
- **Create User**: `POST /api/admin/users`
- **Update Roles**: `PUT /api/admin/users/{userId}/roles`
- **Disable User**: `PUT /api/admin/users/{userId}/disable`
- **Enable User**: `PUT /api/admin/users/{userId}/enable`

**Error Handling**:
- 400 Bad Request ? Clear validation error messages
- 409 Conflict ? User already exists
- 404 Not Found ? User not found
- 503 Service Unavailable ? AuthServer unreachable (10s timeout)
- No stack traces exposed to clients

## Endpoints

### 1) GET `/users/list?take=50&skip=0`
**Description**: List users with paging.

**Query Parameters**:
- `take` (optional): Number of users to return (default: 50, max: 200)
- `skip` (optional): Number of users to skip for pagination (default: 0)

**Response fields**:
- `userId`
- `email`
- `firstName` / `lastName`
- `roles[]`
- `isDisabled` (if available)
- `createdAtUtc` / `modifiedAtUtc`
- `createdByUserId` / `modifiedByUserId`

**Example response**:
```json
{
  "users": [
    {
      "userId": "4d7f2d66...",
      "email": "dispatch@example.com",
      "firstName": "Sam",
      "lastName": "Lee",
      "roles": ["Dispatcher"],
      "isDisabled": false,
      "createdAtUtc": "2025-02-01T18:05:12Z",
      "modifiedAtUtc": "2025-02-03T12:00:00Z",
      "createdByUserId": "admin-uid",
      "modifiedByUserId": "admin-uid"
    }
  ],
  "pagination": {
    "total": 42,
    "skip": 0,
    "take": 50,
    "returned": 1
  }
}
```

---

### 2) POST `/users`
**Description**: Create a user and assign roles.

**Request body**:
```json
{
  "email": "new.user@example.com",
  "firstName": "New",
  "lastName": "User",
  "tempPassword": "TemporaryPass123!",
  "roles": ["Dispatcher"]
}
```

**Validation**:
- `email` is required
- `tempPassword` must be at least 10 characters
- `roles` must contain at least one valid role: Passenger, Driver, Dispatcher, Admin

**Response**: Created user (no password returned).

**Error Responses**:
- 400 Bad Request: Validation error (email required, password too short, invalid role)
- 409 Conflict: User with this email already exists
- 403 Forbidden: Only admins can assign the Admin role

---

### 3) PUT `/users/{userId}/roles`
**Description**: Replace user roles with the provided list.

**Path Parameters**:
- `userId`: The user ID (GUID) to update

**Request body**:
```json
{
  "roles": ["Admin", "Dispatcher"]
}
```

**Validation**:
- `userId` is required
- `roles` must contain at least one valid role
- Only admins can assign the Admin role

**Response**: Updated user (no password returned).

**Error Responses**:
- 400 Bad Request: Validation error
- 404 Not Found: User not found
- 403 Forbidden: Insufficient permissions to assign Admin role

---

### 4) PUT `/users/{userId}/disable`
**Description**: Enable or disable a user account.

**Path Parameters**:
- `userId`: The user ID (GUID) to update

**Request body**:
```json
{
  "isDisabled": true
}
```

**Behavior**:
- `isDisabled: true` ? Calls `PUT /api/admin/users/{userId}/disable` on AuthServer
- `isDisabled: false` ? Calls `PUT /api/admin/users/{userId}/enable` on AuthServer

**Response**: Updated user with `isDisabled` status.

**Error Responses**:
- 400 Bad Request: Invalid request
- 404 Not Found: User not found
- 500 Internal Server Error: AuthServer integration issue

---

## Manual Testing (No automated tests in repo)

> Replace `{adminToken}` with a valid AuthServer JWT that includes the `admin` role.

### 1) List users
```bash
curl -X GET "https://localhost:5206/users/list?take=50&skip=0" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json"
```

### 2) Create user (dispatcher)
```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "dispatcher@example.com",
    "firstName": "Jane",
    "lastName": "Smith",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }'
```

**Expected**: 201 Created with user object (no 405 error)

### 3) Create user (admin) - save userId from response
```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "firstName": "John",
    "lastName": "Admin",
    "tempPassword": "AdminPass123!",
    "roles": ["Admin"]
  }'
```

### 4) Update user roles
```bash
curl -X PUT "https://localhost:5206/users/{userId}/roles" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "roles": ["Admin", "Dispatcher"]
  }'
```

**Expected**: 200 OK with updated user

### 5) Disable user
```bash
curl -X PUT "https://localhost:5206/users/{userId}/disable" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": true
  }'
```

**Expected**: 200 OK with updated user (`isDisabled: true`)

### 6) Enable user
```bash
curl -X PUT "https://localhost:5206/users/{userId}/disable" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": false
  }'
```

**Expected**: 200 OK with updated user (`isDisabled: false`)

---

## Error Scenarios

### Duplicate Email
```bash
# Create user twice with same email
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "duplicate@example.com",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }'
```

**Expected**: 409 Conflict with message "User already exists"

### Invalid Role
```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "tempPassword": "TempPass123!",
    "roles": ["InvalidRole"]
  }'
```

**Expected**: 400 Bad Request with message listing allowed roles

### Password Too Short
```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "tempPassword": "short",
    "roles": ["Dispatcher"]
  }'
```

**Expected**: 400 Bad Request with message "tempPassword must be at least 10 characters long"

---

## Implementation Notes

### Password Security
- Temporary passwords are **never logged** in audit logs or error messages
- Passwords are sent to AuthServer over HTTPS only
- Request logging explicitly excludes password fields

### Timeout Configuration
- AdminAPI has a **10-second timeout** for AuthServer requests
- Prevents hanging if AuthServer is slow (not down)
- Clear timeout error returned to client instead of infinite wait

### Pagination Safety
- `take` is clamped to max 200 users
- `skip` must be >= 0
- Default `take` is 50 if not specified

### Role Normalization
- Role names are case-insensitive (Admin, admin, ADMIN all work)
- Display names (Admin, Dispatcher) are normalized to lowercase for AuthServer
- Invalid roles return clear error messages with list of allowed roles
