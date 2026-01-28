# Changelog

All notable changes to the Bellwood AdminAPI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - Phase Alpha - 2026-01-27

### Added - Quote Lifecycle Management
- **Quote Lifecycle Endpoints**:
  - `POST /quotes/{id}/acknowledge` - Dispatcher acknowledges quote receipt
  - `POST /quotes/{id}/respond` - Dispatcher sends price/ETA to passenger
  - `POST /quotes/{id}/accept` - Passenger accepts quote ? creates booking
  - `POST /quotes/{id}/cancel` - Cancel quote (owner or staff)

- **Quote Lifecycle Fields**:
  - `AcknowledgedAt`, `AcknowledgedByUserId` - Acknowledgment tracking
  - `RespondedAt`, `RespondedByUserId` - Response tracking
  - `EstimatedPrice`, `EstimatedPickupTime`, `Notes` - Quote response data

- **Email Notifications**:
  - Quote response email to passenger (with price/ETA)
  - Quote accepted email to Bellwood staff (with booking ID)

- **Booking Integration**:
  - `SourceQuoteId` field links bookings back to originating quotes
  - Quote acceptance automatically creates booking with `Requested` status

### Changed
- Updated `GET /quotes/{id}` to return all lifecycle fields
- Updated `GET /bookings/{id}` to return `SourceQuoteId`
- Quote status FSM enforced (Submitted ? Acknowledged ? Responded ? Accepted)

### Security
- Only quote owner can accept quotes (staff blocked from accepting on behalf of passengers)
- All lifecycle actions logged in audit trail

### Testing
- **30/30 tests passing (100%)**:
  - `Test-PhaseAlpha-QuoteLifecycle.ps1` - 12/12 tests (end-to-end workflow)
  - `Test-PhaseAlpha-ValidationEdgeCases.ps1` - 10/10 tests (validation & edge cases)
  - `Test-PhaseAlpha-Integration.ps1` - 8/8 tests (integration scenarios)

### Documentation
- Created `PhaseAlpha-QuoteLifecycle-Summary.md` - Complete implementation guide
- Updated `20-API-Reference.md` - Added 4 Phase Alpha endpoints

---

## [0.9.0] - Phase 4 - 2026-01-18

### Added - Testing & Finalization
- **Smoke Test Suite**: `Scripts/smoke-test.ps1` - Basic system health checks
- **LimoAnywhere Service Stub**: `Services/LimoAnywhereServiceStub.cs` - Placeholder for Phase 5
- **Test Connection Endpoint**: `GET /api/admin/limoanywhere/test-connection`
- **Data Status Script**: `Scripts/Get-TestDataStatus.ps1` - View test data statistics

### Testing
- Complete smoke test suite created
- Build verification successful
- All critical paths tested

### Documentation
- Created `Phase4-Summary.md` - Testing implementation summary
- Updated deployment guides with testing procedures

---

## [0.8.0] - Phase 3C - 2026-01-17

### Added - Data Protection & GDPR Compliance
- **Data Protection Services**:
  - `ISensitiveDataProtector` interface
  - `SensitiveDataProtector` - ASP.NET Core Data Protection wrapper
  - Encrypted fields: `PaymentMethodIdEncrypted`, `BillingNotesEncrypted`

- **Data Retention Services**:
  - `IDataRetentionService` interface
  - `DataRetentionService` - Automated cleanup implementation
  - `DataRetentionBackgroundService` - Runs daily at 2 AM UTC

- **Admin Endpoints**:
  - `GET /api/admin/data-retention/policy` - View retention policy
  - `POST /api/admin/data-retention/cleanup` - Manual cleanup trigger
  - `POST /api/admin/data-protection/test` - Test encryption/decryption

- **Retention Policies**:
  - Audit logs: 90 days ? delete
  - Bookings: 365 days ? anonymize PII
  - Quotes: 180 days ? delete
  - Payment data: 7 years (encrypted retention for PCI-DSS)

### Security
- PCI-DSS Level 1 compliant tokenization strategy
- GDPR Article 17 (Right to Erasure) framework
- Encryption at rest for sensitive billing data

### Testing
- Data protection encryption/decryption verified
- Retention policies tested
- Anonymization logic validated

### Documentation
- Created `34-Data-Protection-GDPR-Compliance.md` - Complete compliance guide
- Created `Phase3C-Summary.md` - Implementation summary

---

## [0.7.0] - Phase 3B - 2026-01-16

### Added - Monitoring & Health Checks
- **Application Insights Integration**:
  - Automatic request telemetry
  - Exception tracking with stack traces
  - Performance metrics (duration, throughput)
  - Custom events for security alerts

- **Enhanced Health Checks**:
  - `GET /health/live` - Liveness probe (Kubernetes-ready)
  - `GET /health/ready` - Readiness probe (Kubernetes-ready)
  - `AdminApiHealthCheck` - Custom health check implementation
  - Detailed JSON health reports

- **Error Tracking Middleware**:
  - `ErrorTrackingMiddleware` - Tracks all HTTP errors
  - Security alerts for excessive 403 attempts (>10/hour)
  - Slow request detection (>2 seconds)
  - Unhandled exception tracking

- **Custom Events**:
  - `SecurityAlert.ExcessiveForbiddenAttempts`
  - `Performance.SlowRequest`
  - `System.UnhandledException`
  - `System.HealthCheck`

### Configuration
- Added Application Insights connection string support
- Configurable alert thresholds
- Production-ready health check infrastructure

### Documentation
- Created `33-Application-Insights-Configuration.md` - Complete setup guide
- Created `Phase3B-Summary.md` - Implementation summary

---

## [0.6.0] - Phase 3A - 2026-01-15

### Added - Audit Logging
- **Audit Log System**:
  - `AuditLog` model - Complete audit record structure
  - `IAuditLogRepository` interface
  - `FileAuditLogRepository` - JSON file-based storage
  - `AuditLogger` service - Simplified audit logging API

- **Audit Log Endpoints**:
  - `GET /api/admin/audit-logs` - Query logs with filters
  - `GET /api/admin/audit-logs/{id}` - Get specific log entry
  - `DELETE /api/admin/audit-logs/cleanup` - Clean up old logs

- **Tracked Actions**:
  - All quote/booking CRUD operations
  - Driver assignment and updates
  - OAuth credential management
  - Security events (forbidden attempts, failures)
  - Data retention cleanup

- **Audit Log Fields**:
  - User ID, Username, Role
  - Action type, Entity type, Entity ID
  - Result (Success, Failed, Unauthorized, Forbidden)
  - IP address, HTTP method, endpoint path
  - Timestamp (UTC)

### Security
- Complete audit trail for all critical operations
- 90-day retention policy (configurable)
- AdminOnly access to audit log endpoints

### Documentation
- Updated `11-User-Access-Control.md` with audit logging details
- Created internal audit logging guide

---

## [0.5.0] - Phase 2 - 2026-01-14

### Added - Role-Based Access Control (RBAC)
- **Authorization Policies**:
  - `AdminOnly` - Admin-exclusive operations
  - `StaffOnly` - Admin OR dispatcher access
  - `DriverOnly` - Driver app access
  - `BookerOnly` - Future passenger operations

- **OAuth Credential Management**:
  - `OAuthClientCredentials` model
  - `IOAuthCredentialRepository` - Encrypted file storage interface
  - `FileOAuthCredentialRepository` - ASP.NET Core Data Protection implementation
  - `OAuthCredentialService` - In-memory caching (1-hour TTL)
  - `GET /api/admin/oauth` - View credentials (secret masked)
  - `PUT /api/admin/oauth` - Update credentials (AdminOnly)

- **Field Masking**:
  - `MaskBillingFields()` helper - Reflection-based field nulling
  - `BookingDetailResponseDto`, `QuoteDetailResponseDto` - Billing DTOs
  - Automatic masking for dispatcher role

- **Helper Methods**:
  - `IsDispatcher()` - Check for dispatcher role
  - `IsStaffOrAdmin()` - Check for operational access
  - `MaskSecret()` - Consistent secret masking (first 4 + last 4 chars)

### Changed
- Updated 10+ endpoints with appropriate authorization policies
- `/quotes/seed`, `/bookings/seed`, `/dev/seed-affiliates` ? `AdminOnly`
- `/quotes/list`, `/bookings/list`, `/admin/locations` ? `StaffOnly`
- GET endpoints ? `StaffOnly` (operational access)

### Security
- OAuth client secrets encrypted at rest (Data Protection API)
- Secrets never returned in full in API responses
- Billing fields masked for non-admin users
- Complete audit trail for OAuth operations

### Testing
- **10/10 tests passing**:
  - Dispatcher authentication
  - StaffOnly endpoint access
  - AdminOnly endpoint denial
  - OAuth credential management (admin only)
  - Field masking structure validation

### Documentation
- Updated `11-User-Access-Control.md` - Complete Phase 2 guide
- Created `Phase2-Summary.md` - Implementation summary
- Created `.ai-phase2-notes.md` - AI working notes (internal)

---

## [0.4.0] - Phase 1 - 2026-01-10

### Added - User Data Access Enforcement
- **Ownership Tracking**:
  - `CreatedByUserId` field on `QuoteRecord` and `BookingRecord`
  - Automatic population from JWT `userId` claim
  - Backward compatibility (nullable for legacy records)

- **Data Access Enforcement**:
  - Staff (admin/dispatcher): Full access to all records
  - Drivers: Only assigned bookings (via `AssignedDriverUid`)
  - Bookers: Only own records (via `CreatedByUserId`)
  - Passengers: Only own bookings (via email verification)

- **Audit Trail Fields**:
  - `ModifiedByUserId` - Who last modified
  - `ModifiedOnUtc` - When last modified
  - `CancelledByUserId` - Who cancelled (bookings)
  - `CancelledAt` - When cancelled

- **Authorization Helpers**:
  - `GetUserId()` - Extract user ID from claims
  - `CanAccessRecord()` - Check ownership or staff privilege
  - `CanAccessBooking()` - Check booking access (driver, owner, or staff)

### Changed
- Updated `/quotes/list` to filter by user role
- Updated `/bookings/list` to filter by user role
- Updated `/quotes/{id}`, `/bookings/{id}` with ownership checks
- Updated `/bookings/{id}/cancel` with ownership validation

### Security
- Per-user data isolation for bookers
- Driver access limited to assigned bookings only
- Staff maintain full operational visibility
- Audit trail for all modifications

### Testing
- **12/12 tests passing**:
  - Ownership tracking
  - Data isolation between users
  - Staff privilege verification
  - Driver assignment and access
  - Booking cancellation authorization

### Documentation
- Created `11-User-Access-Control.md` - Complete implementation guide
- Updated `20-API-Reference.md` with ownership details

---

## [v0.2.0-phase2-mvp-lock] - 2025-10-18

### Added
- **Minimal API endpoints**
  - `GET /health` for liveness checks
  - `POST /quotes` accepts `QuoteDraft` and dispatches email (optional `X-Admin-ApiKey`)

- **SMTP email sender (Papercut-ready)**  
  - HTML + plaintext emails include:
    - Booker + Passenger (names, phone, mailto links)
    - Pickup (datetime, location), Dropoff
    - Pickup Style & Sign (outbound + return if round-trip)
    - Flight Details (commercial/return numbers, private tail(s), "same aircraft" note)
    - Pax/Luggage counts, As Directed (+hours), Round Trip (+return datetime)
    - Embedded JSON payload

- **Config binding (`EmailOptions`)**
  - Host/port/TLS/from/to/subject prefix/api key/credentials

- **Swagger in Development**
  - Permissive CORS for local/mobile testing

### Fixed
- Corrected `appsettings.Development.json` structure (single JSON object; Logging + Email)

### Files
- `Program.cs`
- `Services/EmailOptions.cs`, `IEmailSender.cs`, `SmtpEmailSender.cs`
- `appsettings.Development.json`, `appsettings.json`
- `Properties/launchSettings.json`

---

## Future Releases

### [1.1.0] - Phase Beta (Planned)
- Push notifications for quote responses
- In-app quote acceptance (PassengerApp)
- Quote expiration (48-hour auto-expire)

### [1.2.0] - Phase 1 Production (Planned)
- LimoAnywhere OAuth 2.0 integration
- Automatic pricing from LA API
- Customer/Operator API integration
- Ride history import

### [2.0.0] - Major Enhancements (Future)
- Azure Key Vault integration
- Azure Blob Storage for backups
- Azure SignalR Service for scalability
- Redis Cache for distributed sessions
- Multi-language support
- Automated compliance reports

---

**Version Numbering**:
- **Major.Minor.Patch** (Semantic Versioning)
- **Phase Names**: Alpha, Beta, Production Release

**Documentation**:
- See `Docs/ROADMAP.md` for complete feature roadmap
- See `Docs/STATUS-REPORT.md` for current project status
