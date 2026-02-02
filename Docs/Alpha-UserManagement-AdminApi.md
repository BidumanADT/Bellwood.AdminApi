# AdminPortal User Management (Alpha)

**Purpose**: Admin-only endpoints in AdminAPI that proxy to AuthServer for user creation and role management.

## Roles & Authorization
- **Required role**: `admin` (JWT role claim).
- Requests must include a valid bearer token from AuthServer.

## Endpoints

### 1) GET `/users/list?take=50&skip=0`
**Description**: List users with paging.

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
    "take": 50,
    "skip": 0,
    "returned": 1,
    "total": 42
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
  "tempPassword": "TemporaryPass123",
  "roles": ["Dispatcher"]
}
```

**Response**: Created user (no password returned).

---

### 3) PUT `/users/{userId}/roles`
**Description**: Replace user roles with the provided list.

**Request body**:
```json
{
  "roles": ["Admin", "Dispatcher"]
}
```

**Response**: Updated user (no password returned).

---

### 4) PUT `/users/{userId}/disable`
**Description**: Enable/disable a user.

**Request body**:
```json
{
  "isDisabled": true
}
```

**Status**: **Not implemented** yet. This endpoint returns `501 Not Implemented` until the AuthServer supports user disabling.

---

## Manual Testing (No automated tests in repo)

> Replace `{adminToken}` with a valid AuthServer JWT that includes the `admin` role.

### 1) List users
```bash
curl -X GET "https://localhost:5206/users/list?take=50&skip=0" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json"
```

### 2) Create user (admin)
```bash
curl -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "new.user@example.com",
    "firstName": "New",
    "lastName": "User",
    "tempPassword": "TemporaryPass123",
    "roles": ["Dispatcher"]
  }'
```

### 3) Update roles
```bash
curl -X PUT "https://localhost:5206/users/{userId}/roles" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "roles": ["Admin"]
  }'
```

### 4) Disable user (stub)
```bash
curl -X PUT "https://localhost:5206/users/{userId}/disable" \
  -H "Authorization: Bearer {adminToken}" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": true
  }'
```

Expected response: `501 Not Implemented` with a message indicating the feature is not yet supported.
