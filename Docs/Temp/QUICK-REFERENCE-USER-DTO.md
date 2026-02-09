# Quick Reference: AuthServer User DTO Format

**For**: AdminAPI & AdminPortal Teams  
**Date**: February 8, 2026  
**Status**: ? AuthServer Reference Implementation

---

## ?? **Exact JSON Format (Copy This)**

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
  }
]
```

---

## ? **Quick Facts**

| What | Value |
|------|-------|
| Response structure | **Direct array** `[...]` NOT `{users:[...]}` |
| Role casing | **Lowercase** `"admin"` NOT `"Admin"` |
| Property names | **camelCase** `"userId"` NOT `"UserId"` |
| Nullable fields | `firstName`, `lastName`, `createdAtUtc`, `modifiedAtUtc` |
| Required fields | `userId`, `username`, `email`, `roles`, `isDisabled` |

---

## ?? **Common Mistakes**

? **Wrong**: `"roles": ["Admin"]` ? ? **Right**: `"roles": ["admin"]`  
? **Wrong**: `{users: [...]}` ? ? **Right**: `[...]`  
? **Wrong**: Missing `username` ? ? **Right**: Always include  
? **Wrong**: `"UserId"` (capital U) ? ? **Right**: `"userId"` (lowercase u)

---

## ?? **C# DTO (Copy This)**

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
    public bool IsDisabled { get; set; }
    
    [JsonPropertyName("createdAtUtc")]
    public DateTime? CreatedAtUtc { get; set; }
    
    [JsonPropertyName("modifiedAtUtc")]
    public DateTime? ModifiedAtUtc { get; set; }
}
```

---

## ?? **Quick Test**

```bash
# Test AdminAPI response
curl https://localhost:5206/users/list?take=1 | jq

# Compare to AuthServer reference
curl https://localhost:5001/api/admin/users?take=1 | jq

# Should be identical structure
```

---

## ? **Checklist**

- [ ] Returns direct array (not wrapped)
- [ ] Includes `username` field
- [ ] Role names are lowercase
- [ ] All property names are camelCase
- [ ] Includes all 9 fields (even if null)
- [ ] `isDisabled` is boolean (not nullable)

---

**Source**: `BellwoodAuthServer/Models/AdminUserProvisioningDtos.cs`
