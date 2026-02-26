# A3 - Email Sinks

This document describes how AdminApi routes outbound email through environment-specific `IEmailSender` sinks.

## Sink selection

`Program.cs` reads `Email:Mode` and registers one `IEmailSender` implementation:

- `DevPapercut` → `PapercutEmailSender`
- `AlphaSandbox` → `SmtpSandboxEmailSender`
- Any other mode (including `Disabled`/`Beta`) → `NoOpEmailSender`

## Sink behaviors

### 1) `PapercutEmailSender` (Development)

- Used for local development when `Email:Mode` is `DevPapercut`.
- Reuses existing SMTP message builders and send logic through `SmtpEmailSender`.
- Forces override recipients off.
- Defaults SMTP host to `localhost` and port to `25` when values are missing.
- Intended for local Papercut SMTP capture.

### 2) `SmtpSandboxEmailSender` (Alpha)

- Used when `Email:Mode` is `AlphaSandbox`.
- Uses MailKit SMTP with configured host/port/credentials in `Email:Smtp`.
- Preserves throttling and retry behavior used for sandbox rate limits.
- Honors `Email:OverrideRecipients` and routes all messages to the override address when enabled.
- Adds `[ALPHA-OVERRIDE]` to subjects when override routing is active.

### 3) `NoOpEmailSender` (Beta/Production)

- Used for non-sandbox modes such as `Disabled` and `Beta`.
- Implements all `IEmailSender` methods but intentionally does not send any email.
- Emits debug logs so skipped sends are observable.

## Configuration by environment

### Development (`appsettings.Development.json`)

```json
"Email": {
  "Mode": "DevPapercut",
  "Smtp": {
    "Host": "localhost",
    "Port": 25
  }
}
```

### Alpha (`appsettings.Alpha.json`)

```json
"Email": {
  "Mode": "AlphaSandbox",
  "Smtp": {
    "Host": "<sandbox-host>",
    "Port": 2525,
    "Username": "<username>",
    "Password": "<password>",
    "From": "<from-address>",
    "UseStartTls": true
  },
  "OverrideRecipients": {
    "Enabled": true,
    "Address": "central-inbox@bellwood-alpha.test"
  },
  "IncludeOriginalRecipientInSubject": true
}
```

### Production/Beta (`appsettings.json` / `appsettings.Beta.json`)

Use `"Mode": "Disabled"` (or any non-DevPapercut/non-AlphaSandbox value) to route to `NoOpEmailSender`.
