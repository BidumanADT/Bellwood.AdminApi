# Driver Creation, Affiliate Assignment, and DriverApp Visibility Alignment

## Executive summary of current flow

Today, “drivers” exist in two different places and are only loosely connected:

- **Driver accounts (authentication)** live in **AuthServer** (created via AdminPortal → AdminAPI → AuthServer `/api/admin/users`). fileciteturn161file3L1-L1 fileciteturn143file0L1-L1  
- **Driver profiles (operations)** live in **AdminAPI** as `Driver` records stored in `App_Data/drivers.json`, each attached to an `Affiliate`. The operational link to the DriverApp is `Driver.UserUid`. fileciteturn148file0L1-L1 fileciteturn150file0L1-L1  
- The **DriverApp** receives rides from **AdminAPI** by calling `/driver/rides/*`, and AdminAPI filters rides by comparing the booking’s `AssignedDriverUid` to the **JWT `uid` claim**. fileciteturn159file3L1-L1 fileciteturn179file0L1-L1  

That means the system only works end-to-end when:

1) A driver user account exists in AuthServer  
2) A driver profile exists in AdminAPI  
3) The driver profile’s `UserUid` matches the JWT token’s **`uid` claim** value (coming from AuthServer)  
4) A booking is assigned to that driver profile, which copies `Driver.UserUid` into `Booking.AssignedDriverUid` fileciteturn148file8L1-L1 fileciteturn159file3L1-L1  

The specific issue behind your “disjointed” feeling is that **AdminPortal’s User Management creates the AuthServer user, but does not complete the “driver profile + affiliate assignment” step** (and the current Affiliate/Booking pages still lean on manually typing a “UserUid”, often shown as “driver-001”). fileciteturn147file2L1-L1 fileciteturn145file4L1-L1 fileciteturn145file6L1-L1  

So the platform can “look implemented” (affiliates exist, drivers exist, driver app exists), but the critical glue step is not enforced or streamlined.

## Breakdown of where things need improvement and alignment

### Driver provisioning is not a single coherent workflow  
**Severity: High (blocks driver assignment + driver app visibility if missed)**

- **What you want:** Create driver user → immediately pick affiliate → driver is listable under affiliate → driver sees assigned rides.  
- **What the code does now:**  
  - AdminPortal User Management creates users, and only performs a follow-up write for *booker* (booker profile), not for drivers. fileciteturn147file2L1-L1 fileciteturn143file0L1-L1  
  - The affiliate/booking pages contain “Add Driver” flows that still treat `UserUid` as optional/manual text input (and docs/scripts reinforce that). fileciteturn145file4L1-L1 fileciteturn145file6L1-L1 fileciteturn183file1L1-L1  

**Why this matters:** If you create a driver user in User Management but never create/link the AdminAPI driver record, **assignments won’t propagate a matching `AssignedDriverUid`** and the driver app will show “no rides”.

### Identity confusion: `uid` vs `userId` vs “driver-001”  
**Severity: High (easy to mis-link; causes “driver sees nothing”)**

AuthServer issues a JWT where:

- `uid` defaults to `user.Id`, and `userId` is also included as the identity GUID. fileciteturn184file5L1-L1  
- But if the user has a custom claim `uid`, AuthServer will **override** the `uid` claim while keeping `userId` as the GUID. fileciteturn184file5L1-L1  
- Dev seeding also explicitly creates a driver user (“charlie”) with custom `uid = driver-001` for backward compatibility. fileciteturn184file5L1-L1  

AdminAPI’s driver ride endpoints key off `uid`, not `userId`, so **whatever value ends up in `uid` must match what you stored in `Driver.UserUid`**, and the portal currently asks humans to type it. fileciteturn159file3L1-L1 fileciteturn148file0L1-L1  

This is the single biggest “footgun” in the driver suite.

### Affiliate creation path in AdminPortal is likely broken (response DTO mismatch)  
**Severity: High (blocks “add new affiliate” via UI, which you explicitly want)**

In AdminPortal `AffiliateService.CreateAffiliateAsync`, the response is deserialized as `Dictionary<string, string>`. But AdminAPI returns a full affiliate object that contains `drivers` (array), which is not a string and commonly breaks `Dictionary<string,string>` deserialization. fileciteturn145file2L1-L1 fileciteturn150file2L1-L1  

If you try to build “affiliate picker with create new affiliate” inside the driver setup workflow, this mismatch becomes an immediate blocker.

### AdminAPI affiliate/driver management endpoints are not strongly restricted  
**Severity: Medium–High (security / tenancy risk depending on deployment posture)**

Many affiliate/driver endpoints in AdminAPI are only `RequireAuthorization()` (authenticated) rather than staff-only, while driver assignment to bookings is `StaffOnly`. fileciteturn150file2L1-L1  

If a booker token can call “list affiliates with drivers,” that’s an information leak of internal partner structures. Even if the apps don’t call these, it’s exposed surface area.

### Portal driver management UX is incomplete and encourages manual linking  
**Severity: Medium (workable but brittle; high friction)**

AffiliateDetail has Add Driver, but edit/delete are not implemented and the UI still frames `UserUid` as a manual “e.g., driver-001” value. fileciteturn145file4L1-L1  

BookingDetail also includes an inline “Add Driver” form that asks for “User UID (e.g., driver-001)”. fileciteturn145file6L1-L1  

You can absolutely make this work, but it’s not the “single clean provisioning flow” you described.

## Proposed solution broken down by component

### AdminPortal

#### Make driver provisioning explicit, guided, and hard to skip  
**Goal:** Turn your intended flow into a single UI-driven procedure:

1) Create driver user in User Management  
2) Immediately complete “Driver Setup”:
   - choose existing affiliate OR create new affiliate  
   - create AdminAPI driver profile linked to the AuthServer identity  

**Concrete implementation plan (portal-side):**

- In `Components/Pages/Admin/UserManagement.razor`, after successful CreateUser:
  - If role == `driver` and `result.UserId` returned, open a **Driver Setup Modal** (immediately). fileciteturn147file2L1-L1  
- Driver Setup Modal should:
  - Load affiliates via `AffiliateService.GetAffiliatesAsync()`
  - Allow selecting an affiliate, or toggling to “Create new affiliate”
  - Require: `Driver Name`, `Driver Phone`
  - Auto-fill `Driver.UserUid = result.UserId` (AuthServer identity GUID), and hide the raw “UserUid” from the user (or show read-only as “Auth ID”).  
- On save:
  - Call `AddDriverToAffiliateAsync(selectedAffiliateId, driverDtoWithUserUidGuid)`  
  - Toast success and close  

**Important detail:** you already have the “booker post-create follow-up write” pattern (update booker profile phone after create). This is the same concept, but for drivers. fileciteturn147file2L1-L1  

#### Fix AffiliateService response parsing before you build the picker  
Update `Services/AffiliateService.cs`:

- `CreateAffiliateAsync` should deserialize to `AffiliateDto` (or a minimal response DTO), not `Dictionary<string,string>`. fileciteturn145file2L1-L1  
- Same idea for `AddDriverToAffiliateAsync`: deserialize to `DriverDto` (currently dictionary parsing “works by accident” only if no complex values are returned). fileciteturn145file2L1-L1  

This is foundational for the “affiliate picker where you can add a new affiliate and persist it.”

#### Reduce manual “UserUid” entry everywhere it appears  
After provisioning exists, you can keep legacy “UserUid text field” paths, but change the UX to prevent mismatch:

- On AffiliateDetail + BookingDetail “Add Driver,” label should read:
  - **“AuthServer UserId (GUID)”** rather than “e.g., driver-001”  
  - Or remove entirely and add a dropdown that pulls driver users from UserManagement list filtered by role driver (optional). fileciteturn145file4L1-L1 fileciteturn145file6L1-L1  

This aligns with your desire that “driver id is locked to that account forever.”

### AdminAPI

#### Keep the backend contract stable, but tighten correctness and safety  
The AdminAPI already supports your needs:

- Affiliate CRUD and nested driver listing exists. fileciteturn150file2L1-L1  
- Drivers are stored independently and link to AuthServer via `Driver.UserUid`. fileciteturn148file0L1-L1 fileciteturn150file0L1-L1  
- Booking assignment copies `Driver.UserUid` into booking assignment fields and is `StaffOnly`. fileciteturn150file2L1-L1  

Recommended alpha-safe improvement:

- Make affiliate/driver management endpoints **StaffOnly** (or at least staff-visible) rather than any authenticated user. This reduces the risk that booker tokens can enumerate internal affiliate/driver data. fileciteturn150file2L1-L1  

Do **not** change the meaning of `AssignedDriverUid` right now unless you’re ready to migrate existing seed/test data. The portal improvements alone can fix your workflow with minimal blast radius.

### AuthServer

#### Decide whether to keep or sunset the “custom uid claim” mechanism  
AuthServer currently supports:

- Default `uid = user.Id` and `userId = user.Id`
- Optional override of `uid` if a `uid` claim exists on the user fileciteturn184file5L1-L1  

This system is what enabled “driver-001” patterns, but it also **creates the mismatch risk** you’re experiencing.

For alpha, you can keep it and still succeed **if the portal uses `result.UserId` for `Driver.UserUid` and you avoid creating drivers with custom uid claims**.

For post-alpha cleanup, I would strongly consider:

- Deprecating/removing the “custom uid override” behavior (or stop using it for drivers)  
- Eliminating “driver-001” guidance in docs/scripts  
- Standardizing “driver linking key” == AuthServer identity GUID (`userId`) everywhere  

That gets you the “locked forever” stable ID you want, without hidden overrides.

### DriverApp

No required functional changes to meet your desired flow.

The DriverApp already consumes `/api/auth/login` and relies on AdminAPI `/driver/rides/*` endpoints to display assigned rides. fileciteturn184file5L1-L1 fileciteturn179file0L1-L1  

If your portal provisions drivers such that `Driver.UserUid` matches whatever ends up in the JWT `uid` claim, the app will “just work.”

## Prompts to get work done by best suited tool

Below are paste-ready, tightly scoped prompts with firm guardrails. I’m intentionally separating them so you can run them in parallel if needed.

### IDE Integrated GitHub Copilot prompt (AdminPortal focus)

**Prompt:**

You are working inside the repo `BidumanADT/Bellwood.AdminPortal` only.

Goal: Implement a clean “Driver Setup” workflow immediately after creating a driver user in the Admin portal, with minimal blast radius and no breaking changes to existing booker/profile/passenger/location features.

Constraints / guardrails:
- Do NOT refactor unrelated UI or services.
- Do NOT change routing.
- Do NOT remove existing functionality (booker profile modal, saved passengers/locations, role edit, disable/enable).
- Keep changes limited to these files unless absolutely required:
  - `Components/Pages/Admin/UserManagement.razor`
  - `Services/AffiliateService.cs`
  - (optional) `Models/AffiliateModels.cs` only if JSON annotations are needed
- The driver’s immutable linking identifier must be the AuthServer identity GUID returned from `UserService.CreateUserAsync(...)` as `UserActionResult.UserId`.

Tasks:
1) Fix `AffiliateService.CreateAffiliateAsync` to deserialize the AdminAPI response into `AffiliateDto` (not `Dictionary<string,string>`). Return the created affiliate’s `Id`.
2) Fix `AffiliateService.AddDriverToAffiliateAsync` to deserialize into `DriverDto` (not `Dictionary<string,string>`). Return the created driver’s `Id`.
3) Update `UserManagement.razor`:
   - Inject `IAffiliateService AffiliateService`.
   - When creating a user: if role `driver` is selected and creation succeeds with `result.UserId`, automatically open a new “Driver Setup” modal.
   - The Driver Setup modal must:
     - Load affiliates via `AffiliateService.GetAffiliatesAsync()`
     - Allow selecting an existing affiliate OR creating a new affiliate inline (name/email/phone required minimally to match current UI patterns).
     - Require driver name + driver phone (reuse existing First/Last and Phone fields if possible; avoid adding many new fields).
     - Create the AdminAPI driver record by calling `AffiliateService.AddDriverToAffiliateAsync(selectedAffiliateId, new DriverDto { Name=..., Phone=..., UserUid=result.UserId })`.
     - Never ask the admin to type a “UserUid” manually.
   - On success: toast “Driver created and assigned to affiliate.” Then close the modal.
   - On cancel: toast warning “Driver account created but not assigned to an affiliate yet.”

Acceptance checks:
- Creating a driver user results in an AdminAPI driver record with `UserUid == createdUserId` and the driver appears under the affiliate in `/affiliates`.
- Creating a booker still performs the booker-profile phone save step exactly as before.
- No compile errors; no existing UI regressions.

Output:
- Provide a short summary of code changes and any new state fields added.

### Claude Code CLI prompt (AdminAPI security + clarity)

**Prompt:**

Work only in `BidumanADT/Bellwood.AdminApi` on branch `<your-branch>`.

Objective: Reduce risk by restricting affiliate/driver management endpoints to staff roles while preserving existing DriverApp and booking flows.

Guardrails:
- Do NOT change endpoint URLs or response shapes.
- Do NOT touch quote/booking creation endpoints.
- Do NOT change driver filtering semantics in `/driver/rides/*`.
- Keep changes limited to `Program.cs` and, if unavoidable, policy configuration only.

Tasks:
1) Identify all affiliate and driver management endpoints:
   - `/affiliates/*` (list/create/get/update/delete)
   - `/drivers/*` (list/get/update/delete, by-uid)
2) Ensure they require `StaffOnly` (admin OR dispatcher) rather than only authenticated.
   - Exception: keep `/driver/*` endpoints as `DriverOnly`.
3) Verify booking assignment endpoint remains `StaffOnly`.
4) Update any relevant docs in `Docs/` only if a one-line clarification is needed; otherwise skip.

Deliver:
- A patch showing precisely which routes were modified and what authorization was added.
- A short justification for why DriverApp is unaffected.

### Codex prompt (AuthServer optional cleanup recommendation)

If you have time and want to reduce future confusion, this is a high-value cleanup, but it is optional for alpha if you enforce the portal provisioning workflow.

**Prompt:**

Work only in `BidumanADT/BellwoodAuthServer`.

Goal: Reduce driver identity confusion by de-emphasizing “custom uid claim overrides” and aligning with the standard `userId == uid == Identity GUID` approach for newly created users.

Guardrails:
- Do NOT break `/api/admin/users` or `/api/auth/login`.
- Do NOT change database schema.
- Keep changes minimal and focused on dev seeding + documentation.
- Do NOT remove endpoints unless they are clearly dev-only or already deprecated.

Tasks:
1) In `Program.cs`, adjust Development seeding so driver test users do NOT require “driver-001” style custom uid claims by default. Prefer default identity GUID for `uid`.
2) Update docs/scripts that instruct entering “driver-001” as the UID so that they instead instruct using the AuthServer `userId` GUID returned from user creation.
3) Clearly document that `userId` is the immutable identifier for driver linking, and custom `uid` is legacy/dev-only if retained.

Output:
- Summary of what was changed and what guidance is now authoritative.

## Ways to verify work was completed successfully and everything works as expected

### End-to-end “happy path” verification (your intended flow)

1) **Create affiliate** in AdminPortal  
   - `/affiliates` → “Create Affiliate”  
   - Confirm it appears in the list.  
   - This specifically validates `AffiliateService.CreateAffiliateAsync` is no longer broken by response parsing. fileciteturn145file2L1-L1  

2) **Create driver user** in AdminPortal User Management  
   - `/admin/users` → Create User  
   - Select role = `driver`  
   - Save  
   - Confirm the new **Driver Setup modal** opens immediately. fileciteturn147file2L1-L1  

3) **Assign driver to affiliate during setup**  
   - Choose affiliate or create a new one  
   - Enter driver name + phone  
   - Save  
   - Confirm success toast

4) **Confirm driver appears under affiliate**  
   - `/affiliates` → open that affiliate detail  
   - Confirm driver row exists and shows linked identity (even if you don’t display the GUID, the AdminAPI record should contain it). fileciteturn145file4L1-L1  

5) **Assign driver to a booking**  
   - Go to a booking detail page and assign the driver  
   - Confirm booking now shows assigned driver, and the booking includes AssignedDriverUid (if displayed) fileciteturn145file6L1-L1  

6) **DriverApp visibility test**  
   - Sign in with the driver account credentials in DriverApp (`/api/auth/login`). fileciteturn184file5L1-L1  
   - Confirm the ride appears in “today’s rides.”  
   - If no ride appears, the fastest diagnosis is:
     - Compare the JWT `uid` claim (from `/api/auth/me` or by decoding token) to the booking’s `AssignedDriverUid`. They must match.

### Negative testing (the “it’s disjointed again” regression checks)

- Create driver user but **cancel** driver setup modal  
  - Confirm portal shows a warning toast: “driver created but not assigned”  
  - Confirm booking assignment UI either:
    - doesn’t show that driver as selectable (ideal), or  
    - shows driver as “not linked/not provisioned” (acceptable)

- Attempt to assign an “unlinked driver” (driver profile missing `UserUid`)  
  - AdminAPI should reject with the “cannot assign driver without UserUid” style failure (current behavior). fileciteturn148file8L1-L1 fileciteturn150file2L1-L1  

### Security verification (if you apply the AdminAPI StaffOnly tightening)

- Log in as a booker and attempt to call `/affiliates/list`  
  - Should now be 403 (if tightened). fileciteturn150file2L1-L1  

This confirms affiliates/drivers are no longer enumerable by non-staff roles.

### Operational sanity checks (tracking continuity)

- After assigning a driver and moving ride status to “OnRoute” from DriverApp:
  - DriverApp starts posting location to `/driver/location/update` and Portal can see location updates through the tracking functions. fileciteturn159file3L1-L1 fileciteturn157file0L1-L1  

If tracking is working today, the driver provisioning improvements should not break it—this is mostly a linkage/identity correctness problem, not a tracking implementation problem.

