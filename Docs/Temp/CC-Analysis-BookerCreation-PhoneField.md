# Claude Code Analysis: Booker Creation — Phone Field & Name Fields

**Date**: 2026-01-28  
**Branch**: `codex/implement-booker-profile-endpoints`  
**Prepared by**: GitHub Copilot (AdminAPI workspace review)

---

## Original Prompt Given to Claude Code

> Do not change auth semantics or RBAC.  
> Do not change DB providers/migrations unless explicitly required.  
> Do not add registration flows to MAUI (user creation is Admin Portal only).  
> Do not commit automatically. Provide a diff-style summary and wait.  
> DO use the document library present to answer any questions you may have and raise any questions whose answers are not in the docs prior to making any changes based on assumptions.
>
> OBJECTIVE (AdminPortal):  
> Ensure AdminPortal user creation captures and persists the required booker fields (first name, last name, email (should already exist), and phone) and does not default the UI or outbound payloads to "Alice Morgan" except in explicit test/demo scaffolding.
>
> SCOPE:  
> - Search for: "Alice Morgan" and any default values in user creation forms, DTO builders, request payloads, or initial state for create-booker flows.  
> - Confirm that booker creation sends required fields to AdminAPI/AuthServer as appropriate.
>
> REQUIRED OUTCOME:  
> - Alice may remain as an optional test user (seed/test utility) but must not populate new users by default.  
> - Admin-created booker accounts have enough info to book rides immediately.

---

## Claude Code's Response (Summarised)

CC correctly found:

- **Zero** "Alice Morgan" runtime defaults — all occurrences are in `Docs/` or test scripts only. ?
- **Real gaps** in the AdminPortal `UserManagement.razor` create-user modal: `firstName` and `lastName` are absent from the form and from `CreateUserRequest` in `UserModels.cs`.
- CC proposed adding those two fields to both the DTO and the form, with conditional required validation when the "booker" role is selected.
- CC **correctly blocked on phone** and asked before proceeding: *"Does POST /users on AdminAPI accept and persist a phone field?"*

CC's question was well-formed and appropriately cautious. The answer it needed was not resolvable from AdminPortal's own docs alone — it required knowledge of the AdminAPI workspace, specifically the shape of `CreateUserRequest` and the existence of `BookerProfile`.

---

## GitHub Copilot's Analysis

### 1. CC's findings are accurate

The inventory is correct. There are no "Alice Morgan" runtime defaults to remove. The genuine problem is a missing UI + DTO gap for `firstName` and `lastName` in the AdminPortal create-user flow. CC identified it cleanly.

### 2. The phone question is the right question — and the answer is "not via POST /users, but via a separate endpoint"

This is the most important thing to resolve before CC proceeds. The architecture has two separate identity layers:

**Layer 1 — AuthServer user identity** (`POST /users`)  
Handled by `CreateUserRequest` ? `AuthServerCreateUserRequest` ? forwarded to AuthServer's `/api/admin/users`.

```csharp
// Models/AdminUserDtos.cs
public record CreateUserRequest
{
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string TempPassword { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
    // ? No phone here — and should not be added
}
```

`CreateUserRequest` does **not** have a `phone` field, and the internal `AuthServerCreateUserRequest` it maps to does not either. Adding `phone` here would either be silently dropped by AuthServer or cause a `400`. **This layer is not where phone lives.**

**Layer 2 — BookerProfile** (`PUT /api/bookers/me`)  
This is where phone is persisted, and it already fully supports it:

```csharp
// Models/BookerProfile.cs
public sealed class BookerProfile
{
    public string UserId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhoneNumber { get; set; }    // ? Phone is here
    public string? EmailAddress { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}
```

`BookerProfileService.UpsertAsync` persists all four fields. The endpoint `PUT /api/bookers/me` is already live and requires `firstName`, `lastName`, `emailAddress`, and `phoneNumber`.

### 3. The intended flow for admin-created booker accounts

When an admin creates a booker account, achieving "enough info to book rides immediately" requires **two sequential calls**, not one:

```
Step 1:  POST /users
         { email, firstName, lastName, tempPassword, roles: ["booker"] }
         ? Creates the identity account in AuthServer
         ? Returns { userId, email, firstName, lastName, roles, ... }

Step 2:  PUT /api/bookers/me  (called by the booker themselves after first login)
         { firstName, lastName, emailAddress, phoneNumber }
         ? Creates/updates the BookerProfile in AdminAPI
         ? This is what gets used at booking time to pre-fill the quote draft
```

The `SyncFromDraftAsync` path (called on `POST /quotes` and `POST /bookings`) also upserts the `BookerProfile` automatically from the draft — so a booker who has never explicitly saved a profile still gets one created the first time they submit a quote.

This means:

- **Phone cannot and should not be collected during admin user creation** — it belongs to `BookerProfile`, which the booker owns and sets themselves.
- **First name and last name** are correctly in scope for the `POST /users` form — they are AuthServer identity fields and CC's proposed fix for those is sound.

### 4. What "enough info to book rides immediately" actually means

The objective says "Admin-created booker accounts have enough info to book rides immediately." This is already satisfied by the existing architecture once `firstName` and `lastName` are added to the form:

- The booker logs in with their email + temp password.
- On first quote/booking submission, `HasRequiredBookerDetails(draft)` enforces that all four fields (firstName, lastName, emailAddress, phoneNumber) are supplied in the draft payload.
- `SyncFromDraftAsync` then persists that into `BookerProfile` automatically.

Phone is therefore collected at **first booking**, not at account creation, which is the correct UX pattern for this system. An admin does not need to know the booker's phone number to create their account.

### 5. Summary of what CC should do (and not do)

| Action | Do it? | Reason |
|---|---|---|
| Remove "Alice Morgan" runtime defaults | ? Confirmed already done / not needed | Zero runtime defaults found |
| Add `firstName` + `lastName` to `CreateUserRequest` in AdminPortal's `UserModels.cs` | ? Yes | These fields exist on AdminAPI's `CreateUserRequest` and are forwarded to AuthServer |
| Add First Name + Last Name fields to AdminPortal create-user modal | ? Yes | Required for booker role; optional for others |
| Add `phone` to `CreateUserRequest` | ? No | AdminAPI's `CreateUserRequest` has no phone field; phone belongs to `BookerProfile` |
| Add `phone` field to the AdminPortal create-user modal | ? No | Same reason — wrong layer, wrong endpoint |
| Document the two-step flow in a tooltip or form helper text | ? Recommended | Admins creating booker accounts should see a note: "The booker can add their phone number after first login" |

### 6. One additional gap CC did not raise

`PUT /api/bookers/me` is currently restricted to `BookerOrStaff` policy, meaning the booker themselves or a staff member can call it. An admin **can** pre-populate a `BookerProfile` on behalf of a newly created booker using this endpoint if they know the phone number at creation time — but the AdminPortal has no UI surface for this today. This is not in scope for the current objective but is worth noting for a future "Admin creates full booker profile" flow.

---

## Recommended Response to CC

> **On phone:** Do not add a phone field to `POST /users` or the create-user form. Phone is persisted in `BookerProfile` via `PUT /api/bookers/me`, which is a separate endpoint the booker populates after first login. Adding phone to `CreateUserRequest` would be silently dropped or rejected by AuthServer. The existing architecture handles phone correctly at booking time via `SyncFromDraftAsync`.
>
> **On firstName + lastName:** Your proposed changes are correct. Proceed with:
> 1. Add `FirstName` and `LastName` to `UserModels.cs` `CreateUserRequest`.
> 2. Add the two form fields to `UserManagement.razor`, required when the booker role is selected.
> 3. Update `ValidateCreateForm`, `ClearCreateForm`, and `CreateUserAsync` as you described.
> 4. Optionally add a helper note in the form: *"Phone number can be added by the booker after first login."*
>
> Do not change anything else. The "Alice Morgan" cleanup is a non-issue — no runtime defaults exist.

---

## ?? Correction to Previous Analysis — Real Blocker Identified

The original analysis stated: *"This is already satisfied by the existing architecture once firstName and lastName are added to the form."* **That statement was wrong.** Here is the correction.

### What the original analysis got right

- Phone does not belong on `POST /users` and should not be added there. That part stands.
- `BookerProfile` is the correct storage layer for phone. That is still true.

### What it missed

The original analysis assumed the booker would get a chance to call `PUT /api/bookers/me` before their first booking. **There is no such step in the actual user journey.** The flow is:

1. Admin creates the account via `POST /users` ? user gets `email`, `firstName`, `lastName`, `tempPassword`, `roles: ["booker"]`.
2. Booker logs in for the first time.
3. Booker navigates to the booking form and submits.
4. AdminAPI's `POST /quotes` or `POST /bookings` calls `HasRequiredBookerDetails(draft)`, where `draft.Booker.PhoneNumber` is required to be non-empty.
5. **The booking is rejected with a 400 because no phone number exists anywhere in the system yet.**
6. There is no onboarding screen, no "complete your profile" step, no first-login wizard that calls `PUT /api/bookers/me`. The booker account is stuck: they cannot book, and there is no in-app path to fix it. This is the alpha blocker.

---

## Fix Resolution

### Root cause (one sentence)

Phone number is required to submit a booking but there is no UI surface that collects it from a newly admin-created booker before their first booking attempt.

### The scoped fix: add `PUT /api/bookers/{userId}` (staff-callable)

The smallest possible fix is a new **staff-side endpoint** on AdminAPI that lets an admin set a booker's profile including phone at the time of account creation, without giving admins access to `PUT /api/bookers/me` (which is the booker's own endpoint).

**New endpoint — AdminAPI:**
```
PUT /api/bookers/{userId}
Authorization: StaffOnly
Body: { firstName, lastName, emailAddress, phoneNumber }
```

This calls `BookerProfileService.UpsertAsync` for the given `userId`. It is identical to the existing `PUT /api/bookers/me` logic but takes the `userId` from the route rather than from the caller's JWT. `BookerProfileService` and `FileBookerRepository` need **no changes** — `UpsertAsync` already accepts any `userId`.

**Impact table:**

| File | Change | Risk |
|---|---|---|
| `Program.cs` | Add one new `app.MapPut("/api/bookers/{userId}", ...)` endpoint (~15 lines, pattern-identical to `PUT /api/bookers/me`) | Very low |
| `Models/AdminUserDtos.cs` | No change | — |
| `Services/BookerProfileService.cs` | No change | — |
| `Services/FileBookerRepository.cs` | No change | — |
| AdminPortal `UserManagement.razor` | After `POST /users` succeeds, if role includes `"booker"`, show an inline phone + name form and call `PUT /api/bookers/{userId}` | Low — additive UI only |
| AdminPortal `UserModels.cs` | Add `FirstName`, `LastName` to `CreateUserRequest` (CC's planned change — still correct) | Low |

### What the AdminPortal flow looks like after the fix

```
Step 1:  Admin fills: Email, First Name, Last Name, Temp Password, Roles = ["booker"], Phone Number
         ?
         POST /users
         ? Creates AuthServer identity with firstName + lastName
         ? Returns { userId, ... }
         ?
         If role includes "booker":
         PUT /api/bookers/{userId}
         ? Creates BookerProfile with firstName, lastName, emailAddress, phoneNumber
         ?
         Show success: "Booker account created and ready to book."

Step 2:  Booker logs in, navigates to booking form, submits.
         HasRequiredBookerDetails(draft) passes ?
         SyncFromDraftAsync updates BookerProfile from draft ?
         Booking created ?
```

### What CC should do — revised instructions

| Action | Do it? |
|---|---|
| Add `firstName` + `lastName` to AdminPortal `CreateUserRequest` in `UserModels.cs` | ? Yes — unchanged from CC's plan |
| Add First Name + Last Name fields to create-user modal | ? Yes — unchanged from CC's plan |
| Add Phone Number field to create-user modal (shown when role includes `"booker"`) | ? Yes — this is the new addition |
| After `POST /users` succeeds and role includes `"booker"`, call `PUT /api/bookers/{userId}` with name + email + phone | ? Yes — this is the fix |
| Add `PUT /api/bookers/{userId}` endpoint to AdminAPI `Program.cs` | ? Yes — ~15 lines, `StaffOnly`, delegates to existing `BookerProfileService.UpsertAsync` |
| Add `phone` to `CreateUserRequest` (forwarded to AuthServer) | ? No — wrong layer, still applies |
| Change `BookerProfileService` or `FileBookerRepository` | ? No — they already work correctly |
| Change `PUT /api/bookers/me` | ? No — leave the booker's own endpoint untouched |

### Why this is the right scope

- Zero new models, zero new services, zero storage changes.
- No auth/RBAC changes — `StaffOnly` is already established and used on multiple endpoints.
- The new endpoint reuses `BookerProfileService.UpsertAsync` which is already tested via the `/api/bookers/me` path.
- The AdminPortal change is purely additive: one new field in the form, one additional API call after user creation.
- Phone remains optional for non-booker roles (admin, dispatcher, driver) — the new field is only shown and required when `"booker"` is in the selected roles.
