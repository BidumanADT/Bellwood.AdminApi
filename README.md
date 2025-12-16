# Bellwood AdminAPI

**Version**: 1.0.0  
**Framework**: .NET 8  
**Architecture**: ASP.NET Core Minimal APIs + SignalR

---

## 📋 Overview

The **Bellwood AdminAPI** is the central backend service for the Bellwood chauffeur and limousine management system. It provides:

- 🚗 **Booking & Quote Management** - Create, track, and manage ride bookings and quote requests
- 👥 **Affiliate & Driver Management** - Organize affiliate companies and their drivers
- 📍 **Real-Time Location Tracking** - Live GPS tracking via SignalR WebSockets
- 🌍 **Worldwide Timezone Support** - Automatic timezone handling for global operations
- 🔐 **JWT Authentication** - Secure API access with role-based authorization
- 📧 **Email Notifications** - SMTP-based notifications for bookings and assignments
- 📱 **Mobile-First Design** - Optimized for PassengerApp and DriverApp integration

---

## 🏗️ System Architecture

The Bellwood system consists of five interconnected components:

```
┌─────────────────┐    JWT Auth      ┌──────────────┐
│   AuthServer    │ ◄──────────────► │  AdminAPI    │
│  (Identity)     │                  │ (This Repo)  │
└─────────────────┘                  └───────┬──────┘
                                             │
                     ┌───────────────────────┼───────────────────────┐
                     │                       │                       │
              ┌──────▼──────┐        ┌──────▼──────┐        ┌──────▼──────┐
              │ AdminPortal │        │ PassengerApp│        │  DriverApp  │
              │  (Blazor)   │        │   (MAUI)    │        │   (MAUI)    │
              └─────────────┘        └─────────────┘        └─────────────┘
```

### Key Integration Points

- **AuthServer**: Issues JWT tokens with `uid` claims for driver identification
- **AdminPortal**: Staff interface for managing bookings and assignments
- **PassengerApp**: Customer interface for booking rides and tracking drivers
- **DriverApp**: Driver interface for viewing assignments and updating ride status

---

## 🚀 Quick Start

### Prerequisites

- .NET 8 SDK
- PowerShell 5.1+ (for test data scripts)
- **AuthServer** running at `https://localhost:5001`
- SMTP server (Papercut SMTP recommended for development)

### Running the API

```bash
# Clone the repository
git clone https://github.com/BidumanADT/Bellwood.AdminApi.git
cd Bellwood.AdminApi

# Restore dependencies
dotnet restore

# Run the API
dotnet run
```

The API will start at:
- **HTTPS**: `https://localhost:5206`
- **HTTP**: `http://localhost:5207`
- **Swagger**: `https://localhost:5206/swagger`

### Initial Setup

```powershell
# Seed test data (affiliates, drivers, quotes, bookings)
.\Scripts\Seed-All.ps1

# Or seed individually
.\Scripts\Seed-Affiliates.ps1  # Affiliates and drivers
.\Scripts\Seed-Quotes.ps1       # Sample quotes
.\Scripts\Seed-Bookings.ps1     # Sample bookings
```

---

## 📡 API Endpoints

### Authentication

All endpoints require JWT authentication via `Authorization: Bearer {token}` header.

**Getting a Token**:
```bash
curl -X POST https://localhost:5001/login \
  -H "Content-Type: application/json" \
  -d '{"username": "alice", "password": "password"}'
```

### Core Endpoints

#### 🏥 Health Check
```
GET /health
```
Returns API health status (no auth required).

#### 📋 Quote Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/quotes` | POST | Submit new quote request |
| `/quotes/list?take=50` | GET | List recent quotes (paginated) |
| `/quotes/{id}` | GET | Get quote details |
| `/quotes/seed` | POST | Seed sample quotes (dev only) |

#### 📅 Booking Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/bookings` | POST | Submit new booking request |
| `/bookings/list?take=50` | GET | List recent bookings (paginated) |
| `/bookings/{id}` | GET | Get booking details |
| `/bookings/{id}/cancel` | POST | Cancel a booking |
| `/bookings/{id}/assign-driver` | POST | Assign driver to booking |
| `/bookings/seed` | POST | Seed sample bookings (dev only) |

#### 👥 Affiliate & Driver Management

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/affiliates/list` | GET | List all affiliates with drivers |
| `/affiliates` | POST | Create new affiliate |
| `/affiliates/{id}` | GET | Get affiliate details |
| `/affiliates/{id}` | PUT | Update affiliate |
| `/affiliates/{id}` | DELETE | Delete affiliate (cascade deletes drivers) |
| `/affiliates/{id}/drivers` | POST | Create driver under affiliate |
| `/drivers/list` | GET | List all drivers |
| `/drivers/{id}` | GET | Get driver details |
| `/drivers/{id}` | PUT | Update driver |
| `/drivers/{id}` | DELETE | Delete driver |
| `/drivers/by-uid/{userUid}` | GET | Find driver by AuthServer UserUid |

#### 🚗 Driver Endpoints (DriverOnly Role)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/driver/rides/today` | GET | Get driver's rides for next 24 hours |
| `/driver/rides/{id}` | GET | Get detailed ride information |
| `/driver/rides/{id}/status` | POST | Update ride status (FSM-validated) |
| `/driver/location/update` | POST | Submit GPS location update |

**Headers Required**: `X-Timezone-Id` (e.g., `America/New_York`)

#### 📍 Location Tracking

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/driver/location/{rideId}` | GET | Get latest location for a ride |
| `/admin/locations` | GET | Get all active driver locations |
| `/admin/locations/rides?rideIds=a,b,c` | GET | Batch query specific rides |
| `/hubs/location` | WebSocket | SignalR hub for real-time updates |

---

## 🌐 Real-Time Tracking (SignalR)

The API provides **real-time GPS tracking** via SignalR WebSockets.

### Connecting to the Hub

```javascript
// JavaScript example
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5206/hubs/location?access_token=" + jwtToken)
    .build();

await connection.start();
```

### Hub Methods

**Subscribe to a Ride** (Passengers):
```javascript
await connection.invoke("SubscribeToRide", "ride123");
```

**Subscribe to Admin View** (Auto-joined for admin/dispatcher roles):
```javascript
// Admins automatically receive all location updates
```

**Subscribe to a Driver** (Admin only):
```javascript
await connection.invoke("SubscribeToDriver", "driver-001");
```

### Hub Events

**LocationUpdate** - Received when driver sends GPS update:
```json
{
  "rideId": "abc123",
  "driverUid": "driver-001",
  "driverName": "Charlie Johnson",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "heading": 45.5,
  "speed": 12.3,
  "accuracy": 8.5,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**TrackingStopped** - Received when ride completes/cancels:
```json
{
  "rideId": "abc123",
  "reason": "Ride completed",
  "timestamp": "2024-01-15T11:00:00Z"
}
```

**SubscriptionConfirmed** - Acknowledgment of successful subscription:
```json
{
  "rideId": "abc123",
  "status": "subscribed"
}
```

---

## 🌍 Worldwide Timezone Support

The API automatically handles **worldwide timezone operations** via request headers.

### How It Works

1. **Mobile apps** detect device timezone using `TimeZoneInfo.Local.Id`
2. Apps send timezone in **`X-Timezone-Id` header** with every request
3. Server converts times to driver's local timezone for comparison
4. Falls back to Central Time if header not provided (backward compatibility)

### Supported Timezones

All IANA/Windows timezone IDs are supported:

| Region | Timezone ID (IANA) | Timezone ID (Windows) |
|--------|-------------------|----------------------|
| USA East | America/New_York | Eastern Standard Time |
| USA Central | America/Chicago | Central Standard Time |
| USA Pacific | America/Los_Angeles | Pacific Standard Time |
| UK | Europe/London | GMT Standard Time |
| Japan | Asia/Tokyo | Tokyo Standard Time |
| Australia | Australia/Sydney | AUS Eastern Standard Time |
| ...400+ more | | |

### Example Request

```bash
curl -X GET https://localhost:5206/driver/rides/today \
  -H "Authorization: Bearer {jwt}" \
  -H "X-Timezone-Id: Asia/Tokyo"
```

**Without header**: Defaults to Central Time (America/Chicago)

---

## 🔐 Security & Authentication

### JWT Authentication

The API uses **JWT Bearer tokens** issued by AuthServer.

**Token Structure**:
```json
{
  "sub": "charlie",          // Username
  "uid": "driver-001",       // Driver UserUid (links to Driver.UserUid)
  "role": "driver",          // Role (driver, admin, dispatcher)
  "exp": 1234567890          // Expiration timestamp
}
```

### Authorization Policies

- **DriverOnly**: Requires `role: driver` claim
- **Default**: Requires any authenticated user

### The UserUid Link

The entire driver assignment system hinges on the **`uid` claim**:

```
AuthServer             AdminAPI               DriverApp
──────────             ────────               ─────────
User: charlie    →     Driver:          ←     JWT Token:
uid: "driver-001"      UserUid: "driver-001"  uid: "driver-001"
                             ↓
                       Booking:
                       AssignedDriverUid: "driver-001"
```

**Critical**: Drivers **must have a UserUid** matching their AuthServer account to:
- Log into DriverApp
- See assigned rides
- Update ride status
- Send location updates

---

## 📧 Email Notifications

The API sends automated email notifications via SMTP.

### Email Types

1. **Quote Submission** - Sent when new quote requested
2. **Booking Confirmation** - Sent when booking created
3. **Booking Cancellation** - Sent when booking cancelled
4. **Driver Assignment** - Sent to affiliate when driver assigned

### Configuration

Update `appsettings.json`:

```json
{
  "Email": {
    "Host": "localhost",
    "Port": 25,
    "UseTls": false,
    "FromAddress": "noreply@bellwood.com",
    "FromDisplayName": "Bellwood Elite",
    "ToAddress": "reservations@bellwood.com",
    "SubjectPrefix": "[Bellwood]",
    "EnableAuth": false,
    "Username": "",
    "Password": ""
  }
}
```

### Development Setup (Papercut SMTP)

```bash
# Install Papercut SMTP (Windows)
choco install papercut

# Run Papercut on port 25
# All emails will be captured locally for testing
```

---

## 💾 Data Storage

### File-Based Repositories

Data is stored in **JSON files** in the `App_Data` directory:

| File | Contains | Repository |
|------|----------|------------|
| `bookings.json` | Booking records | `FileBookingRepository` |
| `quotes.json` | Quote requests | `FileQuoteRepository` |
| `affiliates.json` | Affiliate companies | `FileAffiliateRepository` |
| `drivers.json` | Driver profiles | `FileDriverRepository` |

### Location Tracking

GPS location data is stored **in-memory** with 1-hour TTL:

- **Service**: `InMemoryLocationService`
- **Rate Limiting**: 10-second minimum between updates
- **Auto-Expiration**: Locations older than 1 hour are automatically cleaned up
- **Scalability**: Can be replaced with Redis for distributed deployments

### Thread Safety

All file repositories use **lazy async initialization** with:
- `SemaphoreSlim` for thread-safe access
- Double-check locking for performance
- Defensive file existence checks

---

## 🧪 Test Data Scripts

### PowerShell Scripts

Located in `Scripts/` directory:

| Script | Purpose |
|--------|---------|
| `Seed-All.ps1` | Seed all test data (affiliates → quotes → bookings) |
| `Seed-Affiliates.ps1` | Seed 2 affiliates with 3 drivers |
| `Seed-Quotes.ps1` | Seed 5 sample quotes |
| `Seed-Bookings.ps1` | Seed 8 sample bookings |
| `Clear-TestData.ps1` | Delete all JSON data files |
| `Get-TestDataStatus.ps1` | Show current data status |
| `Test-Repository-Fix.ps1` | Verify file repository initialization |

### Example Usage

```powershell
# Complete reset and re-seed
.\Scripts\Clear-TestData.ps1 -Confirm
.\Scripts\Seed-All.ps1

# Check data status
.\Scripts\Get-TestDataStatus.ps1

# Test with custom URLs
.\Scripts\Seed-All.ps1 -ApiBaseUrl "https://api.bellwood.com" -AuthServerUrl "https://auth.bellwood.com"
```

### Test Drivers

Seeded drivers can log into DriverApp:

| Driver | UserUid | AuthServer Username | Password |
|--------|---------|---------------------|----------|
| Charlie Johnson | driver-001 | charlie | password |
| Sarah Lee | driver-002 | sarah | password |
| Robert Brown | driver-003 | robert | password |

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Jwt": {
    "Key": "super-long-jwt-signing-secret-1234"
  },
  "Email": {
    "Host": "localhost",
    "Port": 25,
    "UseTls": false,
    "FromAddress": "noreply@bellwood.com",
    "ToAddress": "reservations@bellwood.com"
  }
}
```

### Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Development |
| `ASPNETCORE_URLS` | Listening URLs | https://localhost:5206 |
| `Jwt__Key` | JWT signing key | (see appsettings.json) |

---

## 📊 Monitoring & Logging

### Console Logging

The API logs key events to console:

```
🌍 Driver driver-001 timezone: America/New_York, current time: 2025-12-14 20:04
📍 Location updated for ride abc123: (41.8781, -87.6298), heading=45.5, speed=12.3
✅ Driver Charlie Johnson assigned to booking def456
🔴 Authentication FAILED: SecurityTokenExpiredException
```

### Health Check

```bash
curl https://localhost:5206/health
```

Returns:
```json
{
  "status": "ok"
}
```

---

## 🚧 Roadmap & Future Improvements

### Short-Term
- [ ] Add timezone field to `BookingRecord` for per-booking timezone storage
- [ ] Implement Redis-backed location service for distributed deployments
- [ ] Add Azure SignalR Service support for scalability
- [ ] Implement ETA calculations based on speed data

### Long-Term
- [ ] Integrate with Limo Anywhere API
- [ ] Add historical location tracking (breadcrumbs)
- [ ] Implement geofencing for automatic status updates
- [ ] Add payment processing integration
- [ ] Support multiple language/culture settings

---

## 🐛 Troubleshooting

### Common Issues

**1. FileNotFoundException on First Run**

```
System.IO.FileNotFoundException: Could not find file 'App_Data\bookings.json'
```

**Solution**: The latest version includes lazy initialization - this should not occur. If it does:
```powershell
# Restart the API
dotnet run
```

**2. Authentication Failures**

```
❌ Authentication FAILED: SecurityTokenSignatureKeyNotFoundException
```

**Solution**: Ensure JWT signing key matches between AuthServer and AdminAPI:
```json
// Both appsettings.json must match
"Jwt": {
  "Key": "super-long-jwt-signing-secret-1234"
}
```

**3. Driver Rides Not Appearing**

**Solution**: Ensure DriverApp sends `X-Timezone-Id` header:
```csharp
_httpClient.DefaultRequestHeaders.Add("X-Timezone-Id", TimeZoneInfo.Local.Id);
```

**4. SignalR Connection Failures**

**Solution**: Pass JWT token in query string:
```javascript
.withUrl("/hubs/location?access_token=" + jwtToken)
```

---

## 📚 Documentation

Comprehensive documentation is available in the `Docs/` directory:

| Document | Description |
|----------|-------------|
| `BELLWOOD_SYSTEM_INTEGRATION.md` | Complete system architecture and integration guide |
| `REALTIME_TRACKING_BACKEND_SUMMARY.md` | Real-time location tracking implementation |
| `TIMEZONE_FIX_DRIVER_RIDES_SUMMARY.md` | Worldwide timezone support details |
| `DRIVER_APP_TIMEZONE_INTEGRATION.md` | Driver app integration guide for timezones |
| `FILE_REPOSITORY_RACE_CONDITION_FIX.md` | File repository thread safety fix |
| `DRIVER_API_SUMMARY.md` | Driver-specific API endpoint documentation |
| `AFFILIATE_DRIVER_SUMMARY.md` | Affiliate and driver management guide |
| `SCRIPTS_SUMMARY.md` | Test data script documentation |

---

## 🤝 Contributing

### Code Standards

- **Language**: C# 12
- **Framework**: .NET 8
- **Style**: Follow existing patterns (Minimal APIs, async/await)
- **Naming**: PascalCase for public members, camelCase for private
- **Documentation**: XML comments for all public APIs

### Pull Request Process

1. Create a feature branch from `main`
2. Implement changes with tests (if applicable)
3. Update documentation (README, Docs/)
4. Run `dotnet build` to ensure no errors
5. Submit PR with clear description

---

## 📄 License

Copyright © 2024 Biduman ADT / Bellwood Global

---

## 📞 Support

For issues or questions:
- **GitHub Issues**: [https://github.com/BidumanADT/Bellwood.AdminApi/issues](https://github.com/BidumanADT/Bellwood.AdminApi/issues)
- **Documentation**: See `Docs/` directory
- **Email**: support@bellwood.com

---

## 🎯 Key Features Summary

✅ **Real-Time GPS Tracking** via SignalR WebSockets  
✅ **Worldwide Timezone Support** with automatic detection  
✅ **JWT Authentication** with role-based authorization  
✅ **File-Based Storage** with thread-safe lazy initialization  
✅ **Email Notifications** via SMTP (MailKit)  
✅ **Driver Assignment System** with UserUid linking  
✅ **Complete API Documentation** with Swagger  
✅ **Test Data Scripts** for rapid development  
✅ **Mobile-Optimized** for PassengerApp and DriverApp  
✅ **Production-Ready** with proper error handling and logging  

---