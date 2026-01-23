# Data Protection & GDPR Compliance

**Phase 3C**: Production-Grade Data Protection  
**Last Updated**: January 18, 2026  
**Status**: ? Alpha Test Ready

---

## ?? Overview

This document describes the Bellwood AdminAPI's data protection strategy, including encryption of sensitive fields, data retention policies, and GDPR compliance measures for alpha testing.

**Compliance Framework**:
- ? **GDPR** (General Data Protection Regulation) - EU data privacy
- ? **PCI-DSS** (Payment Card Industry Data Security Standard) - Payment data
- ? **CCPA** (California Consumer Privacy Act) - US data privacy

---

## ?? Sensitive Data Encryption

### Encrypted Fields

The following sensitive fields are encrypted using ASP.NET Core Data Protection API:

| Entity | Field | Encryption | Purpose |
|--------|-------|------------|---------|
| `BookingRecord` | `PaymentMethodIdEncrypted` | ? Yes | Stripe payment method ID |
| `BookingRecord` | `BillingNotesEncrypted` | ? Yes | Internal billing notes |
| `OAuthClientCredentials` | `ClientSecret` | ? Yes | OAuth API credentials |

### Unencrypted Fields (Safe for Display)

| Entity | Field | Encryption | Purpose |
|--------|-------|------------|---------|
| `BookingRecord` | `PaymentMethodLast4` | ? No | Last 4 digits for display (PCI-compliant) |
| `BookingRecord` | `PaymentMethodType` | ? No | Type (e.g., "card", "bank_account") |
| `BookingRecord` | `TotalAmountCents` | ? No | Total charge amount |
| `BookingRecord` | `TotalFareCents` | ? No | Fare amount (before fees) |
| `BookingRecord` | `CurrencyCode` | ? No | Currency (ISO 4217) |

---

## ??? Data Protection API

### Service: `ISensitiveDataProtector`

**Implementation**: `SensitiveDataProtector`  
**Encryption**: AES-256 via ASP.NET Core Data Protection  
**Key Management**: Automatic rotation, isolated keys per purpose

**Usage**:

```csharp
// Inject service
private readonly ISensitiveDataProtector _dataProtector;

// Encrypt sensitive data
var encrypted = _dataProtector.Protect("pm_1234567890abcdef");
// Result: "ENC:CfDJ8K..."

// Decrypt sensitive data
var decrypted = _dataProtector.Unprotect(encrypted);
// Result: "pm_1234567890abcdef"

// Check if data is protected
var isProtected = _dataProtector.IsProtected("ENC:CfDJ8K...");
// Result: true

// Encrypt/decrypt objects
var encryptedObj = _dataProtector.ProtectObject(billingDetails);
var decryptedObj = _dataProtector.UnprotectObject<BillingDetails>(encryptedObj);
```

**Key Features**:
- ? Purpose-specific keys (`Bellwood.SensitiveData.v1`)
- ? Protected data prefix (`ENC:`) for identification
- ? Automatic key rotation
- ? Exception handling with detailed logging

---

## ?? Data Retention Policies

### Retention Periods

| Data Type | Retention Period | Reason | Action After Period |
|-----------|------------------|--------|---------------------|
| **Audit Logs** | 90 days | Compliance | Delete |
| **Bookings** | 365 days | GDPR compliance | Anonymize PII, keep analytics |
| **Quotes** | 180 days | Business requirement | Delete |
| **Payment Data** | 7 years (2555 days) | PCI-DSS/Financial regulations | Encrypted retention |

### Configuration

**appsettings.json**:

```json
{
  "DataRetention": {
    "AuditLogRetentionDays": 90,
    "BookingRetentionDays": 365,
    "QuoteRetentionDays": 180,
    "PaymentDataRetentionDays": 2555
  }
}
```

---

## ?? Automated Data Retention

### Background Service

**Service**: `DataRetentionBackgroundService`  
**Schedule**: Daily at 2:00 AM UTC  
**Tasks**:
1. ? Delete audit logs older than 90 days
2. ? Anonymize bookings older than 365 days (Phase 3C Alpha: logs candidates)
3. ? Delete quotes older than 180 days (Phase 3C Alpha: logs candidates)

**Logs**:
```
[2026-01-18 02:00:00] Starting scheduled data retention cleanup tasks
[2026-01-18 02:00:01] Task 1/3: Cleaning up audit logs older than 90 days
[2026-01-18 02:00:02] Deleted 145 audit logs older than 90 days
[2026-01-18 02:00:02] Task 2/3: Anonymizing bookings older than 365 days
[2026-01-18 02:00:03] Phase 3C Alpha: 3 bookings identified for anonymization
[2026-01-18 02:00:03] Task 3/3: Deleting quotes older than 180 days
[2026-01-18 02:00:04] Phase 3C Alpha: 12 quotes identified for deletion
[2026-01-18 02:00:04] Data retention cleanup completed in 4.2s
```

**Audit Trail**:
- All cleanup operations are logged to audit log
- System action: `System.DataRetentionCleanup`
- Details include counts and duration

---

## ?? GDPR Compliance

### User Rights

#### Right to Access (Article 15)

**Endpoint**: Manual process (admin-only)  
**Action**: Admin retrieves user's data via audit logs and booking records  
**Timeline**: 30 days

**Example Query**:
```bash
# Get all audit logs for user
GET /api/admin/audit-logs?userId=user-123

# Get all bookings created by user
GET /bookings/list
# Filter: CreatedByUserId == "user-123"
```

---

#### Right to Rectification (Article 16)

**Endpoint**: Manual process (admin updates booking)  
**Action**: Admin corrects inaccurate personal data  
**Timeline**: 30 days

**Example**:
```bash
# Update booking with corrected passenger name
PUT /bookings/{id}
{
  "passengerName": "Corrected Name"
}
```

---

#### Right to Erasure / "Right to be Forgotten" (Article 17)

**Endpoint**: `POST /api/admin/data-retention/anonymize-user` (Future)  
**Action**: Anonymize all user's personal data  
**Timeline**: 30 days

**Phase 3C Alpha**: Manual process via data retention service

**Anonymization Strategy**:
- Replace PII with `"ANONYMIZED"` / `"anonymized@example.com"`
- Retain booking ID, dates, amounts for analytics
- Clear encrypted payment data
- Remove user ownership links (`CreatedByUserId = null`)

---

#### Right to Data Portability (Article 20)

**Endpoint**: `GET /api/admin/users/{userId}/export` (Future)  
**Action**: Export user's data in JSON/CSV format  
**Timeline**: 30 days

**Phase 3C Alpha**: Manual export via audit logs + booking data

---

#### Right to Restrict Processing (Article 18)

**Endpoint**: Manual flag in user profile (Future)  
**Action**: Mark user as "restricted" - no marketing, minimal processing  
**Timeline**: Immediate

**Phase 3C Alpha**: Not implemented - alpha testing only

---

#### Right to Object (Article 21)

**Endpoint**: Manual process (Future)  
**Action**: User can object to processing (e.g., automated decisions)  
**Timeline**: Immediate

**Phase 3C Alpha**: Not applicable - no automated decision-making

---

## ?? PCI-DSS Compliance

### Payment Data Handling

**Stripe Integration** (Phase 4+):
- ? Tokenization: Card details never touch our servers
- ? Payment Method ID stored encrypted
- ? Last 4 digits stored unencrypted (PCI-compliant)
- ? Full card number **NEVER** stored
- ? CVV **NEVER** stored
- ? 7-year retention for payment metadata

**PCI-DSS Requirements**:
| Requirement | Status | Implementation |
|-------------|--------|----------------|
| Build and maintain secure network | ? | HTTPS only, JWT authentication |
| Protect cardholder data | ? | Tokenization (Stripe), encryption at rest |
| Maintain vulnerability management | ? | Regular updates, security patches |
| Implement strong access control | ? | RBAC, audit logging |
| Regularly monitor and test networks | ? | Application Insights, health checks |
| Maintain information security policy | ? | This document |

---

## ?? Testing Data Protection

### Test Encryption

**Endpoint**: `POST /api/admin/data-protection/test`  
**Auth**: Admin Only

**Request**:
```bash
curl -X POST https://localhost:5206/api/admin/data-protection/test \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Response**:
```json
{
  "success": true,
  "message": "Data protection is working correctly",
  "test": {
    "original": "Sensitive test data 12345",
    "encrypted": "ENC:CfDJ8KrYz3m9...",
    "decrypted": "Sensitive test data 12345",
    "isProtected": true,
    "encryptedLength": 256
  }
}
```

---

### Test Data Retention

**Endpoint**: `POST /api/admin/data-retention/cleanup`  
**Auth**: Admin Only

**Request**:
```bash
curl -X POST https://localhost:5206/api/admin/data-retention/cleanup \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Response**:
```json
{
  "message": "Data retention cleanup completed successfully",
  "auditLogsDeleted": 0,
  "bookingsAnonymized": 0,
  "quotesDeleted": 0,
  "durationSeconds": 0.5,
  "policy": {
    "auditLogRetentionDays": 90,
    "bookingRetentionDays": 365,
    "quoteRetentionDays": 180,
    "paymentDataRetentionDays": 2555
  }
}
```

---

## ?? Admin API Endpoints

### Data Retention

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/admin/data-retention/policy` | GET | AdminOnly | Get retention policy |
| `/api/admin/data-retention/cleanup` | POST | AdminOnly | Manual cleanup trigger |
| `/api/admin/data-protection/test` | POST | AdminOnly | Test encryption/decryption |

---

## ?? Configuration

### Production Settings

**appsettings.Production.json**:

```json
{
  "DataRetention": {
    "AuditLogRetentionDays": 90,
    "BookingRetentionDays": 365,
    "QuoteRetentionDays": 180,
    "PaymentDataRetentionDays": 2555
  },
  "DataProtection": {
    "ApplicationName": "BellwoodAdminAPI",
    "KeyLifetime": 90
  }
}
```

### Environment Variables

```bash
# Data Protection key storage (Azure)
export DataProtection__Azure__KeyVaultUri="https://bellwood-keys.vault.azure.net/"

# Data Retention override
export DataRetention__AuditLogRetentionDays=90
```

---

## ?? Security Best Practices

### DO ?

- ? Use `ISensitiveDataProtector` for all payment data
- ? Store only payment method ID (encrypted) + last 4 digits
- ? Run data retention cleanup regularly (automated)
- ? Audit all data access (automatic via `AuditLogger`)
- ? Use HTTPS only
- ? Encrypt data at rest (ASP.NET Core Data Protection)
- ? Implement least privilege access (RBAC)

### DON'T ?

- ? **NEVER** store full credit card numbers
- ? **NEVER** store CVV codes
- ? **NEVER** log sensitive data (payment IDs, secrets)
- ? **NEVER** display encrypted fields in responses
- ? Don't skip data retention (GDPR violation)
- ? Don't share encryption keys across environments
- ? Don't disable audit logging

---

## ?? Compliance Checklist

### Alpha Test Readiness

- [x] Sensitive field encryption implemented
- [x] Data retention policies documented
- [x] Automated data retention service running
- [x] Audit logging for all sensitive operations
- [x] Admin endpoints for manual data management
- [x] OAuth credentials encrypted
- [x] Test endpoints for verification
- [ ] Full booking anonymization (Phase 4 - requires repository updates)
- [ ] Full quote deletion (Phase 4 - requires repository updates)
- [ ] User data export API (Phase 4)
- [ ] GDPR consent tracking (Phase 4)

### Production Readiness (Future)

- [ ] Azure Key Vault integration for key storage
- [ ] Implement full anonymization (update BookingRepository)
- [ ] Implement full deletion (update QuoteRepository)
- [ ] GDPR consent UI
- [ ] Privacy policy documentation
- [ ] User data export automation
- [ ] Data breach notification procedures
- [ ] Regular security audits

---

## ?? Future Enhancements

### Phase 4: Full Data Lifecycle

1. **Repository Updates**:
   - Add `UpdateAsync` to `IBookingRepository`
   - Add `DeleteAsync` to `IQuoteRepository`
   - Implement physical anonymization/deletion

2. **User Self-Service**:
   - User data export API
   - User data deletion request
   - GDPR consent management

3. **Advanced Encryption**:
   - Azure Key Vault integration
   - Hardware Security Module (HSM) support
   - Key rotation automation

4. **Compliance Automation**:
   - Automated compliance reports
   - Data breach detection
   - Privacy impact assessments

---

## ?? Support

### Data Protection Questions

- **Encryption issues**: Check Data Protection API logs
- **Retention failures**: Check background service logs
- **GDPR requests**: Manual process via admin endpoints

### Related Documentation

- `23-Security-Model.md` - Authentication & authorization
- `33-Application-Insights-Configuration.md` - Monitoring setup
- `32-Troubleshooting.md` - Common issues

---

**Last Updated**: January 18, 2026  
**Phase**: 3C - Data Protection  
**Status**: ? Alpha Test Ready  
**Compliance**: GDPR, PCI-DSS (Tokenization), CCPA
