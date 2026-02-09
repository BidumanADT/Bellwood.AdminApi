# AdminAPI vs AuthServer - Side-by-Side Comparison

**Date**: February 8, 2026  
**Purpose**: Show exact differences between current AdminAPI and required format

---

## ?? **Response Structure**

### **AdminAPI Current** ?
```json
{
  "users": [
    {
      "userId": "...",
      "email": "...",
      "roles": ["Admin"],
      "isDisabled": false
    }
  ],
  "pagination": {
    "skip": 0,
    "take": 50,
    "returned": 7
  }
}
```

### **AuthServer Reference** ?
```json
[
  {
    "userId": "...",
    "username": "alice",
    "email": "...",
    "firstName": null,
    "lastName": null,
    "roles": ["admin"],
    "isDisabled": false,
    "createdAtUtc": null,
    "modifiedAtUtc": null
  }
]
```

---

## ?? **Field-by-Field Differences**

| Field | AdminAPI Current | AuthServer Reference | Action Required |
|-------|------------------|---------------------|-----------------|
| Response wrapper | `{users:[...], pagination:{...}}` | Direct array `[...]` | **Remove wrapper** |
| `userId` | ? Present | ? Present | No change needed |
| `username` | ? **MISSING** | ? Present | **ADD THIS FIELD** |
| `email` | ? Present | ? Present | No change needed |
| `firstName` | ? **MISSING** | ? Present (null) | **ADD as null** |
| `lastName` | ? **MISSING** | ? Present (null) | **ADD as null** |
| `roles` | ? Present but **WRONG CASE** | ? lowercase | **Change to lowercase** |
| `isDisabled` | ? Present | ? Present | No change needed |
| `createdAtUtc` | ? **MISSING** | ? Present (null) | **ADD as null** |
| `modifiedAtUtc` | ? **MISSING** | ? Present (null) | **ADD as null** |

---

## ?? **Required Changes Summary**

### **Change 1: Remove Response Wrapper**

```csharp
// ? Current (WRONG)
return Ok(new {
    users = userList,
    pagination = new { skip, take, returned }
});

// ? Required (CORRECT)
return Ok(userList);  // Return array directly
```

### **Change 2: Add Missing Fields**

```csharp
var user = new UserDto
{
    UserId = dbUser.Id,
    Username = dbUser.UserName ?? "",        // ? ADD THIS
    Email = dbUser.Email ?? "",
    FirstName = null,                         // ? ADD THIS
    LastName = null,                          // ? ADD THIS
    Roles = dbUser.Roles.Select(r => r.ToLowerInvariant()).ToList(),
    IsDisabled = /* existing logic */,
    CreatedAtUtc = null,                      // ? ADD THIS
    ModifiedAtUtc = null                      // ? ADD THIS
};
```

### **Change 3: Normalize Role Names**

```csharp
// ? Current (WRONG)
Roles = userRoles.Select(r => r.Name).ToList()
// Returns: ["Admin", "Dispatcher"]

// ? Required (CORRECT)
Roles = userRoles.Select(r => r.Name.ToLowerInvariant()).ToList()
// Returns: ["admin", "dispatcher"]
```

---

## ?? **Before/After Example**

### **Before (AdminAPI Current)** ?

**Request**: `GET /users/list?take=2`

**Response**:
```json
{
  "users": [
    {
      "userId": "abc-123",
      "email": "alice@example.com",
      "roles": ["Admin"],
      "isDisabled": false
    },
    {
      "userId": "def-456",
      "email": "bob@example.com",
      "roles": ["Dispatcher"],
      "isDisabled": false
    }
  ],
  "pagination": {
    "skip": 0,
    "take": 2,
    "returned": 2
  }
}
```

**Portal Result**: ? Shows "No username", roles don't display

---

### **After (Matching AuthServer)** ?

**Request**: `GET /users/list?take=2`

**Response**:
```json
[
  {
    "userId": "abc-123",
    "username": "alice",
    "email": "alice@example.com",
    "firstName": null,
    "lastName": null,
    "roles": ["admin"],
    "isDisabled": false,
    "createdAtUtc": null,
    "modifiedAtUtc": null
  },
  {
    "userId": "def-456",
    "username": "bob",
    "email": "bob@example.com",
    "firstName": null,
    "lastName": null,
    "roles": ["dispatcher"],
    "isDisabled": false,
    "createdAtUtc": null,
    "modifiedAtUtc": null
  }
]
```

**Portal Result**: ? Shows usernames, roles display correctly

---

## ?? **Why These Changes Matter**

### **Missing `username` Field**
- Portal displays email instead of username
- Confusing for users (email might not match username)
- Inconsistent with AuthServer behavior

### **Wrong Role Casing**
- Portal's role selection logic is case-sensitive
- Expects `"admin"` but receives `"Admin"`
- Causes roles to not be recognized/displayed

### **Wrapped Response**
- Portal expects direct array `users = await fetch(...).then(r => r.json())`
- Wrapper breaks array iteration
- Would need Portal code changes to unwrap

### **Missing Timestamp Fields**
- Portal shows `1/1/0001` placeholder dates
- Can't track when users were created/modified
- Degrades user experience

---

## ?? **Estimated Effort**

| Change | Time | Difficulty |
|--------|------|-----------|
| Add `username` field | 5 min | Easy |
| Add null fields | 5 min | Easy |
| Lowercase roles | 5 min | Easy |
| Remove wrapper | 10 min | Easy |
| Test changes | 15 min | Easy |
| **Total** | **40 min** | **Low** |

---

## ? **Validation**

After making changes, verify:

```bash
# 1. Check response structure
curl https://localhost:5206/users/list?take=1 | jq 'type'
# Expected: "array" (not "object")

# 2. Check first user has all fields
curl https://localhost:5206/users/list?take=1 | jq '.[0] | keys'
# Expected: ["createdAtUtc", "email", "firstName", "isDisabled", 
#            "lastName", "modifiedAtUtc", "roles", "userId", "username"]

# 3. Check role casing
curl https://localhost:5206/users/list?take=1 | jq '.[0].roles'
# Expected: ["admin"] (lowercase)
# NOT: ["Admin"] (capital A)

# 4. Compare to AuthServer
diff <(curl -s https://localhost:5206/users/list?take=1 | jq '.[0] | keys | sort') \
     <(curl -s https://localhost:5001/api/admin/users?take=1 | jq '.[0] | keys | sort')
# Expected: No differences
```

---

## ?? **Questions?**

**For AdminAPI Team:**
- See full specification: `Docs/Temp/ADMINAPI-USER-DTO-REQUEST-20260208.md`
- Reference implementation: `BellwoodAuthServer/Models/AdminUserProvisioningDtos.cs`
- Contact: AuthServer Team

**For AdminPortal Team:**
- Portal is already coded correctly
- No changes needed once AdminAPI matches format
- Can test against AuthServer endpoint while waiting

---

**Status**: ?? **AdminAPI changes required**  
**Impact**: High (blocks Portal user management)  
**Complexity**: Low (straightforward mapping changes)  
**Timeline**: 40 minutes + testing

---

*Match the AuthServer format exactly and Portal will work immediately!* ?
