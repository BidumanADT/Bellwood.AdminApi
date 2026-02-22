# A2 Audit Hardening + Logging Consistency (Alpha)

## Structured logging
- Serilog JSON console logging is enabled in `Program.cs`.
- Global request logging middleware captures: `service`, `environment`, `correlationId`, `requestPath`, `method`, `statusCode`, `elapsedMs`, `userId`, and `clientId`.
- Unhandled exceptions generate an `errorId` and return a generic payload without stack traces.

## Correlation ID propagation
- Incoming `X-Correlation-Id` is accepted; generated if missing.
- `X-Correlation-Id` is returned on all responses.
- Outbound HttpClient calls to AuthServer automatically include `X-Correlation-Id`.

## Hardened audit trail
- New DB-backed table: `AuditEvent` in `App_Data/audit-events.db`.
- Captured fields: `Id`, `TimestampUtc`, `ActorUserId`, `Action`, `TargetType`, `TargetId`, `Result`, `CorrelationId`, `IpAddress`, `UserAgent`, `MetadataJson`.
- Metadata is capped to 512 characters.
- Sensitive values are excluded; no auth headers, tokens, or SMTP credentials are logged.

### Audited endpoint families (mutating methods only)
- `/users/*`
- `/drivers/*`
- `/affiliates/*`
- `/quotes/*`
- `/bookings/*`
- `/oauth/*`

Methods: `POST`, `PUT`, `PATCH`, `DELETE`.

## Health endpoints
- `/health/live`: process liveness.
- `/health/ready`: readiness checks for Admin API internals, AuthServer, AuditEvent store connectivity, and SMTP configuration health.

## Rollback notes
- Revert middleware registrations in `Program.cs` to disable correlation/request/audit hardening.
- Remove `SqliteAuditEventRepository` registration and related health checks to roll back DB-backed audit events.
- Remove Serilog package references from `.csproj` and logging bootstrap in `Program.cs` to return to default logger.
