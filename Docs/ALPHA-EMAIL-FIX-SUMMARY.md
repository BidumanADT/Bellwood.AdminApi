# Alpha Email Fix — Implementation Summary

**Branch**: `codex/add-email-configuration-for-adminapi`  
**Scope**: `Services/EmailOptions.cs`, `Services/SmtpEmailSender.cs`, `Program.cs`, `appsettings.Alpha.json`, `Properties/launchSettings.json`

---

## Root Cause

`MimeKit.ParseException: No address found` was thrown during seed-all because:

1. `Email:Smtp:From` was bound correctly (`Email:Smtp:From` ? `EmailSmtpOptions.From`) but was empty at runtime when user-secrets were not set.
2. `Email:OverrideRecipients` was never consulted. Every send method called `MailboxAddress.Parse(_opt.To)` directly with no override logic, so the sandbox intercept never fired.
3. No guard existed before `MailboxAddress.Parse()` — any empty or missing address produced an unhandled exception that crashed seed-all.

---

## Files Changed

### 1. `Services/EmailOptions.cs`

Added two read-only convenience properties:

```csharp
public bool IsAlphaSandbox => Mode.Equals("AlphaSandbox", StringComparison.OrdinalIgnoreCase);
public bool IsDisabled    => Mode.Equals("Disabled",      StringComparison.OrdinalIgnoreCase);
```

No other changes. All binding keys remain unchanged.

---

### 2. `Services/SmtpEmailSender.cs`

#### `ILogger<SmtpEmailSender>` injected via constructor

```csharp
public SmtpEmailSender(IOptions<EmailOptions> opt, ILogger<SmtpEmailSender> logger)
```

#### `ResolveFrom()` — private address helper

- Reads `_opt.Smtp.From` (bound from `Email:Smtp:From`)
- Trims whitespace
- Validates with `MailboxAddress.TryParse(ParserOptions.Default, raw, out var mailbox)`
- On blank or invalid: logs `LogError` with actionable message, returns `null`

#### `ResolveTo(intendedAddress?)` — private address helper

- If `OverrideRecipients.Enabled` ? uses `Email:OverrideRecipients:Address` (sandbox intercept)
- Otherwise ? uses `intendedAddress ?? _opt.To`
- Same `TryParse` validation and null-on-failure behaviour

#### `BuildMessage(intendedTo?)` — central message factory

- Calls both helpers; returns `null` if either address is unresolvable (error already logged)
- When `IsAlphaSandbox`: logs `LogInformation` showing resolved From/To and override state
- Only place that touches `msg.From` / `msg.To` — `MailboxAddress.Parse()` is never called directly anywhere in the sender

#### `BuildSubject(baseSubject, originalRecipient?)` — subject helper

- When `IncludeOriginalRecipientInSubject` is `true` **and** override is active **and** an original address is known, appends `[orig: original@address]` to the subject line

#### All six public `SendXxxAsync` methods updated

Each method now calls `BuildMessage(intendedTo?)`, checks `if (msg is null) return;`, and uses `BuildSubject(...)`.

| Method | `intendedTo` passed |
|---|---|
| `SendQuoteAsync` | none — staff inbox |
| `SendBookingAsync` | none — staff inbox |
| `SendBookingCancellationAsync` | none — staff inbox |
| `SendDriverAssignmentAsync` | `affiliate.Email` |
| `SendQuoteResponseAsync` | `quote.Draft?.Booker?.EmailAddress` |
| `SendQuoteAcceptedAsync` | none — staff inbox |

#### `SendEmailAsync` — early-exit guards added

```csharp
if (_opt.IsDisabled)
{
    _logger.LogDebug("[Email] Mode=Disabled — skipping send.");
    return;
}

if (string.IsNullOrWhiteSpace(_opt.Host))
{
    _logger.LogWarning("[Email] SMTP host is not configured — skipping send.");
    return;
}
```

---

### 3. `Program.cs`

Added a startup validation block immediately before `app.Run()`. Logs all relevant email config keys **without printing passwords or secrets**:

```
[Startup] Environment: Alpha
[Startup] Email Mode: AlphaSandbox
[Startup] Email:Smtp:From         = configured          ? or "*** NOT SET ***"
[Startup] Email:Smtp:Host         = sandbox.smtp.mailtrap.io
[Startup] Email:Smtp:Port         = 2525
[Startup] Email:Smtp:UseStartTls  = true
[Startup] Email:OverrideRecipients:Enabled = true
[Startup] Email:OverrideRecipients:Address = central-inbox@bellwood-alpha.test
```

Also corrected the audit action string in the `ManualDataRetentionCleanup` endpoint from the plain string `"DataRetention.ManualCleanup"` to the constant `AuditActions.DataRetentionCleanup`.

---

### 4. `appsettings.Alpha.json`

SMTP port set to `2525` with `UseStartTls: true` per Mailtrap sandbox recommendation:

```json
"Smtp": {
  "Port": 2525,
  "UseStartTls": true
}
```

---

### 5. `Properties/launchSettings.json`

Added `Alpha` launch profile:

```json
"Alpha": {
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": true,
  "launchUrl": "swagger",
  "applicationUrl": "https://localhost:5206;http://localhost:5205",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Alpha"
  }
}
```

When selected, ASP.NET Core automatically loads `appsettings.Alpha.json` on top of `appsettings.json`.

---

## Required Configuration Keys

| Key | Source | Example value | Notes |
|---|---|---|---|
| `Email:Smtp:From` | **user-secrets** | `alpha@bellwood.com` | Required — send skipped if missing |
| `Email:Smtp:Host` | **user-secrets** | `sandbox.smtp.mailtrap.io` | Required — send skipped if missing |
| `Email:Smtp:Username` | **user-secrets** | _(Mailtrap inbox username)_ | Required for auth |
| `Email:Smtp:Password` | **user-secrets** | _(Mailtrap inbox password)_ | Required for auth |
| `Email:Smtp:Port` | `appsettings.Alpha.json` | `2525` | ? Already set |
| `Email:Smtp:UseStartTls` | `appsettings.Alpha.json` | `true` | ? Already set |
| `Email:OverrideRecipients:Enabled` | `appsettings.Alpha.json` | `true` | ? Already set |
| `Email:OverrideRecipients:Address` | `appsettings.Alpha.json` | `central-inbox@bellwood-alpha.test` | ? Already set |
| `Email:Mode` | `appsettings.Alpha.json` | `AlphaSandbox` | ? Already set |

**Set secrets via CLI:**
```powershell
dotnet user-secrets set "Email:Smtp:From"     "alpha@bellwood.com"
dotnet user-secrets set "Email:Smtp:Host"     "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "Email:Smtp:Username" "<mailtrap-user>"
dotnet user-secrets set "Email:Smtp:Password" "<mailtrap-pass>"
```

---

## Failure Modes

| Scenario | Result |
|---|---|
| `Email:Smtp:From` not set | `LogError: Email skipped: missing From address` — seed-all completes normally |
| `Email:OverrideRecipients:Address` not set | `LogError: Email skipped: missing To address` — send skipped |
| Invalid email address in config | `LogError: Email skipped: '...' is not a valid email address` — send skipped |
| `Email:Smtp:Host` not set | `LogWarning: SMTP host is not configured — skipping send` |
| `Email:Mode = Disabled` | `LogDebug: Mode=Disabled — skipping send` |

In all cases: **no exception, no crash, seed-all completes**.

---

## Expected Log Output — Successful Alpha Run

```
[Startup] Environment: Alpha
[Startup] Email Mode: AlphaSandbox
[Startup] Email:Smtp:From         = configured
[Startup] Email:Smtp:Host         = sandbox.smtp.mailtrap.io
[Startup] Email:Smtp:Port         = 2525
[Startup] Email:Smtp:UseStartTls  = true
[Startup] Email:OverrideRecipients:Enabled = true
[Startup] Email:OverrideRecipients:Address = central-inbox@bellwood-alpha.test

... (seed-all runs) ...

info: [Email/AlphaSandbox] From=alpha@bellwood.com To=central-inbox@bellwood-alpha.test (override=True)
info: [Email/AlphaSandbox] From=alpha@bellwood.com To=central-inbox@bellwood-alpha.test (override=True)
info: [Email/AlphaSandbox] From=alpha@bellwood.com To=central-inbox@bellwood-alpha.test (override=True)
```

All emails are intercepted to the single override inbox regardless of the original intended recipient.

---

## Expected Log Output — Missing Secret

```
[Startup] Email:Smtp:From = *** NOT SET ***

... (seed-all runs) ...

fail: [Email] Email skipped: missing From address. Set Email:Smtp:From in user-secrets or appsettings.
fail: [Email] Email skipped: missing From address. Set Email:Smtp:From in user-secrets or appsettings.
```

No exception. No crash. Fix: set the secret and restart.
