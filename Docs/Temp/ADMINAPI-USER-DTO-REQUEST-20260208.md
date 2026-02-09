# AdminAPI User DTO - Required Format to Match AuthServer

**Date**: February 8, 2026  
**For**: AdminAPI Team  
**From**: AdminPortal Team (via AuthServer Team)  
**Re**: User Management DTO Standardization - **REQUIRED FOR COMPATIBILITY**

---

## ?? **CRITICAL: Match AuthServer's Exact Format**

The AdminPortal is **already coded** to work with AuthServer's DTO format. AdminAPI **MUST** return the same structure for compatibility.

---

## ? **AuthServer's Current Response** (REFERENCE IMPLEMENTATION)

This is the **exact format** that AdminAPI must match:

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
    "email": "",
    "firstName": null,
    "lastName": null,
    "roles": ["driver"],
    "isDisabled": false,
    "createdAtUtc": null,
    "modifiedAtUtc": null
  }
]
```

**Key Points:**
- ? Returns **array directly** (not wrapped in `{users: [...]}` object)
- ? All field names are **camelCase** (lowercase first letter)
- ? `roles` is **lowercase plural** array with **lowercase role names**
- ? `null` used for missing/optional values (not empty strings)
- ? Dates are `null` if not available (not placeholder dates)

---

## ?? **Required Field Specifications**

| Field | Type | Required | Current AuthServer | AdminAPI Must |
|-------|------|----------|-------------------|---------------|
| `userId` | string (GUID) | ? Yes | Returns user.Id | Match exactly |
| `username` | string | ? Yes | Returns user.UserName | **ADD THIS** |
| `email` | string | ? Yes | Returns user.Email or `""` | Match exactly |
| `firstName` | string or null | ? No | Always `null` currently | Return `null` for now |
| `lastName` | string or null | ? No | Always `null` currently | Return `null` for now |
| `roles` | string[] | ? Yes | **lowercase** role names | **USE LOWERCASE** |
| `isDisabled` | boolean | ? Yes | true/false (not nullable) | Match exactly |
| `createdAtUtc` | string (ISO 8601) or null | ? No | `null` currently | Return `null` for alpha |
| `modifiedAtUtc` | string (ISO 8601) or null | ? No | `null` currently | Return `null` for alpha |

---

## ? **Current AdminAPI Issues**

### **Issue 1: Missing `username` Field**
```json
// ? AdminAPI current (BROKEN)
{
  "users": [{
    "userId": "...",
    "email": "alice@example.com",
    // username missing!
    "roles": ["Admin"]
  }]
}

// ? Required format
[{
  "userId": "...",
  "username": "alice",  // ? ADD THIS
  "email": "alice@example.com",
  "roles": ["admin"]
}]
```

### **Issue 2: Wrong Response Structure**
```json
// ? AdminAPI current (wrapped in object)
{
  "users": [...],
  "pagination": {...}
}

// ? Required format (direct array)
[
  {...user1...},
  {...user2...}
]
```

### **Issue 3: Wrong Case in `roles`**
```json
// ? AdminAPI current
"roles": ["Admin", "Dispatcher"]  // Capital letters

// ? Required format  
"roles": ["admin", "dispatcher"]  // All lowercase
```

### **Issue 4: Missing Optional Fields**
```json
// ? AdminAPI current (fields missing)
{
  "userId": "...",
  "email": "...",
  "roles": [...]
}

// ? Required format (all fields present)
{
  "userId": "...",
  "username": "...",
  "email": "...",
  "firstName": null,     // ? Include even if null
  "lastName": null,      // ? Include even if null
  "roles": [...],
  "isDisabled": false,
  "createdAtUtc": null,  // ? Include even if null
  "modifiedAtUtc": null  // ? Include even if null
}
```

---

## ?? **Implementation Guide for AdminAPI**

### **Step 1: Add `username` Field**

**Source:** `AspNetUsers.UserName` column

```csharp
// In your user query/mapping
var users = await _context.Users
    .Select(u => new UserDto
    {
        UserId = u.Id,
        Username = u.UserName ?? "",  // ? Add this
        Email = u.Email ?? "",
        FirstName = null,              // ? Add this
        LastName = null,               // ? Add this
        Roles = u.Roles.Select(r => r.Name.ToLowerInvariant()).ToList(),  // ? Lowercase!
        IsDisabled = u.LockoutEnabled && u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
        CreatedAtUtc = null,           // ? Add this (null for now)
        ModifiedAtUtc = null           // ? Add this (null for now)
    })
    .ToListAsync();
```

### **Step 2: Normalize Role Names to Lowercase**

**Critical:** AuthServer returns `["admin", "dispatcher"]` NOT `["Admin", "Dispatcher"]`

```csharp
// ? WRONG
Roles = userRoles.ToList()  // Returns ["Admin"]

// ? CORRECT
Roles = userRoles.Select(r => r.ToLowerInvariant()).ToList()  // Returns ["admin"]
```

### **Step 3: Return Direct Array (Not Wrapped Object)**

**For `/users/list` endpoint:**

```csharp
// ? WRONG - Don't wrap in object
return Ok(new {
    users = userList,
    pagination = {...}
});

// ? CORRECT - Return array directly
return Ok(userList);
```

**Note:** If pagination is needed, add it to response headers instead:
```csharp
Response.Headers.Add("X-Total-Count", totalCount.ToString());
Response.Headers.Add("X-Page-Size", pageSize.ToString());
return Ok(userList);
```

### **Step 4: Use JsonPropertyName Attributes**

**To ensure camelCase serialization:**

```csharp
using System.Text.Json.Serialization;

public class UserDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
    
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }  // ?? NOT nullable
    
    [JsonPropertyName("createdAtUtc")]
    public DateTime? CreatedAtUtc { get; set; }
    
    [JsonPropertyName("modifiedAtUtc")]
    public DateTime? ModifiedAtUtc { get; set; }
}
```

---

## ?? **Testing & Validation**

### **Test 1: Field Names (Case Sensitivity)**

```bash
# Test the response
curl -H "Authorization: Bearer {token}" \
     https://localhost:5206/users/list?take=1

# Should return (note the casing):
[{
  "userId": "...",     # ? lowercase u
  "username": "...",   # ? lowercase u
  "email": "...",      # ? lowercase e
  "firstName": null,   # ? camelCase
  "lastName": null,    # ? camelCase
  "roles": [...],      # ? lowercase r
  "isDisabled": false, # ? camelCase
  "createdAtUtc": null,   # ? camelCase
  "modifiedAtUtc": null   # ? camelCase
}]
```

### **Test 2: Role Name Casing**

```bash
# Check roles array
# ? WRONG: ["Admin", "Dispatcher"]
# ? CORRECT: ["admin", "dispatcher"]
```

### **Test 3: Response Structure**

```bash
# Check if array is returned directly
# ? WRONG: {"users": [...]}
# ? CORRECT: [...]
```

### **Test 4: AdminPortal Integration**

1. Update AdminAPI endpoint
2. Clear browser cache (Ctrl+Shift+R)
3. Open AdminPortal User Management
4. **Expected Results:**
   - ? Username column shows actual usernames (not emails)
   - ? Roles column shows role names (not "None")
   - ? Edit Roles modal shows current role selected
   - ? No console errors

---

## ?? **Comparison Table**

| Feature | AuthServer (? Working) | AdminAPI Current (? Broken) | AdminAPI Required (? Fix) |
|---------|------------------------|------------------------------|---------------------------|
| Response structure | Direct array `[...]` | Wrapped object `{users:[...]}` | **Match AuthServer** |
| `username` field | ? Present | ? Missing | **Add field** |
| `firstName` field | ? Present (null) | ? Missing | **Add as null** |
| `lastName` field | ? Present (null) | ? Missing | **Add as null** |
| `roles` casing | ? `["admin"]` | ? `["Admin"]` | **Use lowercase** |
| `createdAtUtc` field | ? Present (null) | ? Missing | **Add as null** |
| `modifiedAtUtc` field | ? Present (null) | ? Missing | **Add as null** |
| JSON property names | ? camelCase | ? Unknown | **Use camelCase** |

---

## ?? **Critical Notes**

### **1. Role Names MUST Be Lowercase**

AuthServer normalizes ALL role names to lowercase:
- `"admin"` not `"Admin"`
- `"dispatcher"` not `"Dispatcher"`
- `"booker"` not `"Booker"`
- `"driver"` not `"Driver"`

**Why:** AdminPortal's role selection logic is case-sensitive and expects lowercase.

### **2. `isDisabled` is NOT Nullable**

```csharp
// ? WRONG
public bool? IsDisabled { get; set; }

// ? CORRECT
public bool IsDisabled { get; set; }  // Always true or false, never null
```

### **3. Empty Email Returns Empty String, Not Null**

```csharp
// ? CORRECT
Email = user.Email ?? ""  // Empty string if no email
```

### **4. Timestamps Can Be Null (For Now)**

```csharp
// ? CORRECT (alpha release)
CreatedAtUtc = null
ModifiedAtUtc = null

// Future: Return actual timestamps when available
```

---

## ?? **Priority Fixes (Ranked)**

### **P0 - Critical (Breaks AdminPortal)**
1. ? Add `username` field
2. ? Lowercase role names (`"admin"` not `"Admin"`)
3. ? Return direct array (not wrapped object)

### **P1 - High (Portal expects these)**
4. ? Add `firstName` as null
5. ? Add `lastName` as null
6. ? Add `createdAtUtc` as null
7. ? Add `modifiedAtUtc` as null

### **P2 - Medium (Best practices)**
8. ? Use `[JsonPropertyName]` attributes
9. ? Ensure camelCase property names

---

## ?? **Sample Code - Complete DTO**

```csharp
using System.Text.Json.Serialization;

namespace AdminAPI.Models;

public class UserDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
    
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }
    
    [JsonPropertyName("createdAtUtc")]
    public DateTime? CreatedAtUtc { get; set; }
    
    [JsonPropertyName("modifiedAtUtc")]
    public DateTime? ModifiedAtUtc { get; set; }
}
```

---

## ?? **Implementation Checklist**

**For AdminAPI Team:**

- [ ] Copy `UserDto` class from sample above
- [ ] Update user query to include `username` from `AspNetUsers.UserName`
- [ ] Normalize role names to lowercase: `.Select(r => r.ToLowerInvariant())`
- [ ] Set `firstName`, `lastName`, `createdAtUtc`, `modifiedAtUtc` to `null`
- [ ] Return direct array (not `{users: [...]}`)
- [ ] Test with curl/Postman to verify exact JSON structure
- [ ] Coordinate with AdminPortal team to test integration

**Testing:**
```bash
# Quick test
curl https://localhost:5206/users/list?take=1 | jq '.[0]'

# Should match AuthServer format exactly
curl https://localhost:5001/api/admin/users?take=1 | jq '.[0]'
```

---

## ?? **Support**

**If you need:**
- Sample Entity Framework queries
- Help with role normalization
- Clarification on any field
- Testing assistance

**Contact**: AuthServer Team  
**Reference**: This document + `Models/AdminUserProvisioningDtos.cs` in AuthServer repo

---

## ? **Summary**

| Item | Status |
|------|--------|
| **AuthServer Format** | ? Reference implementation (working perfectly) |
| **AdminPortal Code** | ? Already coded to expect this format |
| **AdminAPI Current** | ? Missing fields, wrong structure |
| **AdminAPI Required** | ?? **Match AuthServer exactly** |
| **Breaking Changes** | ? None for AdminPortal (already compatible) |
| **Effort Estimate** | ?? 30-60 minutes to update AdminAPI |

---

**CRITICAL**: AdminPortal is **already coded** for this exact format. AdminAPI just needs to match what AuthServer is already returning.

**When AdminAPI matches this format**: Portal will work immediately with zero code changes! ??

---

**Status**: ?? **REQUIRED FOR ADMINAPI**  
**Priority**: ?? **HIGH** (blocks Portal user management)  
**Dependencies**: None (all fields can return null for alpha)  
**Risk**: Low (AuthServer proves format works)

---

*Copy the exact DTO structure from AuthServer's `Models/AdminUserProvisioningDtos.cs` for guaranteed compatibility!* ?
