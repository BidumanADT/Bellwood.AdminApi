# Changelog

All notable changes to this API will be documented in this file.

## [v0.2.0-phase2-mvp-lock] - 2025-10-18

### Added
- **Minimal API endpoints**
  - `GET /health` for liveness checks.
  - `POST /quotes` accepts `QuoteDraft` and dispatches email (optional `X-Admin-ApiKey`).
- **SMTP email sender (Papercut-ready)**  
  - HTML + plaintext emails include:
    - Booker + Passenger (names, phone, mailto links)
    - Pickup (datetime, location), Dropoff
    - Pickup Style & Sign (outbound + return if round-trip)
    - Flight Details (commercial/return numbers, private tail(s), “same aircraft” note)
    - Pax/Luggage counts, As Directed (+hours), Round Trip (+return datetime)
    - Embedded JSON payload
- **Config binding (`EmailOptions`)** with host/port/TLS/from/to/subject prefix/api key/credentials.
- **Swagger in Development** and permissive CORS for local/mobile testing.

### Fixed
- Corrected `appsettings.Development.json` structure (single JSON object; Logging + Email).

### Files of note
- `Program.cs`
- `Services/EmailOptions.cs`, `IEmailSender.cs`, `SmtpEmailSender.cs`
- `appsettings.Development.json`, `appsettings.json`
- `Properties/launchSettings.json`

---
