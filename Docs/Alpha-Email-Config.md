# Alpha Email Configuration

This project uses environment-specific email configuration via the `Email` settings section.

## Environment behavior

- **Development** uses local Papercut SMTP (`Mode=DevPapercut`) for safe testing.
- **Alpha** uses sandbox SMTP (`Mode=AlphaSandbox`) and forces all outbound mail to a central inbox by enabling `OverrideRecipients`.
- **Beta** disables email sending (`Mode=Disabled`).

## Secrets handling

Do **not** commit real SMTP credentials.

Provide sensitive values through:
- `dotnet user-secrets` for local development, and/or
- environment variables in deployment.

Keep committed appsettings files limited to non-secret placeholders/defaults.
