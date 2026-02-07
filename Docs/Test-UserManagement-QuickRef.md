# Quick Test Commands - User Management API

## Get Admin Token
```bash
ADMIN_TOKEN=$(curl -sk -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}' \
  | jq -r '.accessToken')

echo "Token: $ADMIN_TOKEN"
```

---

## 1. Create User (Dispatcher)
```bash
curl -sk -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "dispatcher@example.com",
    "firstName": "Jane",
    "lastName": "Smith",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }' | jq
```

**Expected**: `201 Created` - Save the `userId` from response

---

## 2. List Users
```bash
curl -sk -X GET "https://localhost:5206/users/list?take=50&skip=0" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq
```

**Expected**: `200 OK` with users array

---

## 3. Update User Roles
```bash
USER_ID="<paste-userId-here>"

curl -sk -X PUT "https://localhost:5206/users/$USER_ID/roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "roles": ["Admin", "Dispatcher"]
  }' | jq
```

**Expected**: `200 OK` with updated roles

---

## 4. Disable User
```bash
curl -sk -X PUT "https://localhost:5206/users/$USER_ID/disable" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": true
  }' | jq
```

**Expected**: `200 OK` with `isDisabled: true`

---

## 5. Enable User
```bash
curl -sk -X PUT "https://localhost:5206/users/$USER_ID/disable" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": false
  }' | jq
```

**Expected**: `200 OK` with `isDisabled: false`

---

## Error Scenario Tests

### Duplicate Email (409 Conflict)
```bash
curl -sk -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "dispatcher@example.com",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }' | jq
```

**Expected**: `409 Conflict` - "User already exists"

---

### Invalid Role (400 Bad Request)
```bash
curl -sk -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "tempPassword": "TempPass123!",
    "roles": ["InvalidRole"]
  }' | jq
```

**Expected**: `400 Bad Request` - Invalid role error message

---

### Password Too Short (400 Bad Request)
```bash
curl -sk -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "tempPassword": "short",
    "roles": ["Dispatcher"]
  }' | jq
```

**Expected**: `400 Bad Request` - "tempPassword must be at least 10 characters long"

---

## Full Test Suite (Copy-Paste Ready)

```bash
#!/bin/bash

# Get admin token
echo "==> Getting admin token..."
ADMIN_TOKEN=$(curl -sk -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}' \
  | jq -r '.accessToken')

if [ -z "$ADMIN_TOKEN" ] || [ "$ADMIN_TOKEN" == "null" ]; then
  echo "? Failed to get admin token"
  exit 1
fi

echo "? Admin token obtained"

# Test 1: Create user
echo ""
echo "==> Test 1: Create dispatcher user..."
CREATE_RESPONSE=$(curl -sk -X POST "https://localhost:5206/users" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test.dispatcher@example.com",
    "firstName": "Test",
    "lastName": "Dispatcher",
    "tempPassword": "TempPass123!",
    "roles": ["Dispatcher"]
  }')

USER_ID=$(echo "$CREATE_RESPONSE" | jq -r '.userId')

if [ -z "$USER_ID" ] || [ "$USER_ID" == "null" ]; then
  echo "? Failed to create user"
  echo "$CREATE_RESPONSE" | jq
  exit 1
fi

echo "? User created: $USER_ID"

# Test 2: List users
echo ""
echo "==> Test 2: List users..."
curl -sk -X GET "https://localhost:5206/users/list?take=5&skip=0" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq '.users | length'
echo "? List users works"

# Test 3: Update roles
echo ""
echo "==> Test 3: Update user roles..."
curl -sk -X PUT "https://localhost:5206/users/$USER_ID/roles" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "roles": ["Admin"]
  }' | jq '.roles'
echo "? Roles updated"

# Test 4: Disable user
echo ""
echo "==> Test 4: Disable user..."
curl -sk -X PUT "https://localhost:5206/users/$USER_ID/disable" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": true
  }' | jq '.isDisabled'
echo "? User disabled"

# Test 5: Enable user
echo ""
echo "==> Test 5: Enable user..."
curl -sk -X PUT "https://localhost:5206/users/$USER_ID/disable" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isDisabled": false
  }' | jq '.isDisabled'
echo "? User enabled"

echo ""
echo "??? All tests passed!"
```

Save as `test-user-management.sh` and run: `bash test-user-management.sh`
