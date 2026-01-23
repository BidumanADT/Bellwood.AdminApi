# Bellwood AdminAPI Roadmap

**Last Updated**: January 14, 2026  
**Version**: 2.0.0

---

## ?? Current Status

**Version**: 2.0.0  
**Branch**: `feature/user-data-restriction` (Phase 1 complete)  
**Status**: ? Production Ready

###completed Features

- ? **Phase 0**: Real-Time GPS Tracking & Driver Integration
  - SignalR WebSockets for live location updates
  - Driver status persistence
  - Worldwide timezone support
  - Location privacy & authorization
  - Driver assignment system

- ? **Phase 1**: User Data Access Enforcement (COMPLETED Jan 14, 2026)
  - Ownership tracking (`CreatedByUserId` field)
  - Role-based access control (admin, booker, driver)
  - Authorization matrix implementation
  - List endpoint filtering
  - Detail endpoint ownership verification
  - 403 Forbidden for unauthorized access
  - Comprehensive testing suite

---

## ?? Phase 2: Enhanced RBAC & Dispatcher Role

**Status**: ?? Planning  
**Target**: Q1 2026  
**Priority**: ?? HIGH

### Goals

1. **Introduce Dispatcher Role**
   - New `dispatcher` role in AuthServer
   - Can see all operational data (bookings, quotes, drivers)
   - **Cannot** see billing information (payment methods, prices)
   - Cannot modify user accounts

2. **Billing Data Protection**
   - Remove billing fields from responses for dispatchers
   - Create `AdminOnly` endpoints for financial data
   - Audit logging for billing access

3. **Enhanced Authorization Policies**
   - `AdminOnly` policy (admin role required)
   - `StaffOnly` policy (admin OR dispatcher)
   - Field-level authorization (mask sensitive data)

### Implementation Plan

#### AuthServer Changes

```csharp
// Add dispatcher role to seeding
roleManager.CreateAsync(new IdentityRole("dispatcher"));

// JWT structure
{
  "sub": "david",
  "userId": "...",
  "role": "dispatcher",  // NEW
  "exp": ...
}
```

#### AdminAPI Changes

```csharp
// New authorization policies
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("admin"));
    
    options.AddPolicy("StaffOnly", policy => 
        policy.RequireRole("admin", "dispatcher"));
});

// Field masking for dispatchers
if (context.User.IsInRole("dispatcher"))
{
    response.PaymentMethodId = null;
    response.CardLast4 = null;
    response.TotalAmount = null;
}
```

#### Affected Endpoints

| Endpoint | Current | Phase 2 |
|----------|---------|---------|
| `GET /bookings/list` | RequireAuthorization | `StaffOnly` + field masking |
| `GET /bookings/{id}` | RequireAuthorization | `StaffOnly` + field masking |
| `POST /bookings/{id}/assign-driver` | RequireAuthorization | `StaffOnly` |
| `GET /admin/locations` | RequireAuthorization | `StaffOnly` |
| `/billing/*` (future) | N/A | `AdminOnly` |

### Testing

- [ ] Create dispatcher test user
- [ ] Verify dispatcher sees all bookings
- [ ] Verify dispatcher CANNOT see billing fields
- [ ] Verify dispatcher CANNOT access admin endpoints
- [ ] Verify admin still has full access

---

## ?? Phase 3: Passenger User Accounts

**Status**: ?? Planning  
**Target**: Q2 2026  
**Priority**: ?? MEDIUM

### Goals

1. **Passenger Identity**
   - Passengers create AuthServer accounts
   - Email-based user registration
   - Link passenger to bookings via `PassengerUserId`

2. **Concierge Bookings**
   - Concierge creates booking on behalf of passenger
   - Both concierge AND passenger can view booking
   - Access if `CreatedByUserId == user` OR `PassengerUserId == user`

3. **Enhanced Passenger Tracking**
   - Passenger location endpoint uses `PassengerId` claim
   - No longer relies on email matching
   - More secure and scalable

### Implementation Plan

#### Data Model Changes

```csharp
public class BookingRecord
{
    // Existing
    public string? CreatedByUserId { get; set; }  // Concierge who booked
    
    // NEW
    public string? PassengerUserId { get; set; }  // Actual passenger
}
```

#### Authorization Logic

```csharp
// Allow access if user is creator OR passenger
if (booking.CreatedByUserId == currentUserId || 
    booking.PassengerUserId == currentUserId)
{
    return Results.Ok(booking);
}
```

---

## ?? Phase 4: AdminPortal UI Enhancements

**Status**: ?? Backlog  
**Target**: Q3 2026  
**Priority**: ?? LOW

### Goals

1. **Role-Based UI**
   - Hide billing tabs for dispatchers
   - Show different dashboards per role
   - Conditional rendering based on permissions

2. **User Management UI**
   - Create/edit users in AdminPortal
   - Assign roles without database access
   - Audit user activities

3. **Real-Time Notifications**
   - Toast notifications for SignalR events
   - Driver status changes show in real-time
   - New booking alerts

---

## ?? Phase 5: Audit Logging

**Status**: ?? Backlog  
**Target**: Q4 2026  
**Priority**: ?? MEDIUM

### Goals

1. **Action Logging**
   - Log all create/update/delete operations
   - Capture user ID, timestamp, action type
   - Store in separate audit log repository

2. **Access Logging**
   - Log all data access attempts
   - Track unauthorized access attempts
   - Generate compliance reports

3. **Audit Trail UI**
   - View audit logs in AdminPortal
   - Filter by user, action, date range
   - Export for compliance

### Implementation Plan

```csharp
public interface IAuditLogRepository
{
    Task LogAsync(AuditEntry entry);
    Task<List<AuditEntry>> GetLogsAsync(AuditFilter filter);
}

public class AuditEntry
{
    public string UserId { get; set; }
    public string Action { get; set; }  // "Create", "Read", "Update", "Delete"
    public string EntityType { get; set; }  // "Booking", "Quote", etc.
    public string EntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
}
```

---

## ?? Phase 6: Multi-Region Support

**Status**: ?? Backlog  
**Target**: 2027  
**Priority**: ?? LOW

### Goals

1. **Geographic Expansion**
   - Support operations in multiple countries
   - Region-specific pricing
   - Localized content

2. **Data Residency**
   - Store data in customer's region
   - GDPR compliance
   - Data sovereignty

3. **Multi-Currency**
   - Support multiple currencies
   - Exchange rate handling
   - Regional tax calculations

---

## ?? Mobile App Enhancements

### PassengerApp

**Q1 2026**:
- [ ] Real-time status updates via SignalR
- [ ] Push notifications for ride events
- [ ] In-app chat with driver

**Q2 2026**:
- [ ] User account creation
- [ ] Saved payment methods
- [ ] Ride history

### DriverApp

**Q1 2026**:
- [ ] Offline mode support
- [ ] Turn-by-turn navigation integration
- [ ] Earnings dashboard

**Q2 2026**:
- [ ] Multi-language support
- [ ] Driver ratings & feedback
- [ ] Shift management

---

## ?? Technical Debt

### High Priority

- [ ] Move from file storage to Azure SQL
- [ ] Implement Redis for location caching
- [ ] Set up Azure SignalR Service
- [ ] Add comprehensive unit tests
- [ ] Set up CI/CD pipeline

### Medium Priority

- [ ] Implement request rate limiting
- [ ] Add API versioning
- [ ] Improve error messages
- [ ] Add health check endpoints
- [ ] Implement retry policies

### Low Priority

- [ ] Swagger documentation improvements
- [ ] Code coverage reporting
- [ ] Performance profiling
- [ ] Load testing automation

---

## ?? Success Metrics

### Phase 1 (Completed)

- ? Zero unauthorized data access
- ? 100% test coverage for ownership
- ? All endpoints have authorization
- ? No breaking changes

### Phase 2 (Target)

- ?? Dispatcher role in production
- ?? Zero billing leaks to dispatchers
- ?? Audit logs for all sensitive actions
- ?? Staff productivity up 20%

### Phase 3 (Target)

- ?? Passenger accounts active
- ?? Concierge bookings supported
- ?? Passenger satisfaction up 15%

---

## ??? Release Schedule

| Version | Target Date | Features |
|---------|-------------|----------|
| 2.0.0 | ? Jan 14, 2026 | Phase 1 complete |
| 2.1.0 | Feb 2026 | Phase 2 dispatcher role |
| 2.2.0 | Mar 2026 | Audit logging |
| 3.0.0 | May 2026 | Passenger accounts (Phase 3) |
| 3.1.0 | Jul 2026 | AdminPortal UI (Phase 4) |
| 4.0.0 | Q4 2026 | Azure SQL migration |

---

## ?? Future Feature Requests

### Community Requests

1. **ETA Calculations** (?? 15 votes)
   - Calculate arrival time based on speed + distance
   - Display in PassengerApp
   - Factor in traffic data

2. **Geofencing** (?? 12 votes)
   - Auto-detect arrival at pickup
   - Auto-complete at dropoff
   - Reduce manual updates

3. **Historical Location Tracking** (?? 8 votes)
   - Store location breadcrumbs
   - Display route traveled
   - Generate trip reports

4. **Automated Driver Assignment** (?? 6 votes)
   - AI-based driver matching
   - Optimize for distance/availability
   - Reduce manual dispatch work

5. **Multi-Stop Rides** (?? 5 votes)
   - Support multiple pickup/dropoff points
   - Calculate total distance
   - Dynamic pricing

---

## ?? Related Documents

- `11-User-Access-Control.md` - Phase 1 implementation details
- `CHANGELOG.md` - Version history
- `01-System-Architecture.md` - System overview
- `32-Troubleshooting.md` - Common issues

---

## ?? Feedback

Have ideas for the roadmap? Contact us:
- GitHub Issues: [Feature Requests](https://github.com/BidumanADT/Bellwood.AdminApi/issues)
- Email: product@bellwood.com
- Slack: #bellwood-feature-requests

---

**Last Updated**: January 14, 2026  
**Maintained By**: Bellwood Development Team  
**Review Schedule**: Monthly
