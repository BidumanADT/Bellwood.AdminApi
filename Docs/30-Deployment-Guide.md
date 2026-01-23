# Deployment Guide

**Document Type**: Living Document - Deployment & Operations  
**Last Updated**: January 14, 2026  
**Status**: ? Production Ready

---

## ?? Overview

This document provides complete deployment instructions for the Bellwood AdminAPI, including local development setup, staging deployment, production deployment, and environment configuration.

**Target Framework**: .NET 8.0  
**Runtime**: ASP.NET Core 8.0  
**Deployment Models**: Self-hosted, IIS, Azure App Service, Docker

---

## ??? Prerequisites

### Development Environment

**Required Software**:
- ? .NET 8.0 SDK or later ([Download](https://dotnet.microsoft.com/download))
- ? Visual Studio 2022 (17.8+) or VS Code
- ? Git for version control

**Optional Tools**:
- PowerShell 7+ (for test scripts)
- Postman or curl (for API testing)
- Azure CLI (for Azure deployments)
- Docker Desktop (for containerization)

**Verification**:
```bash
dotnet --version
# Expected: 8.0.x or higher
```

---

### AuthServer Dependency

**Critical**: AdminAPI requires **AuthServer** for JWT token issuance.

**AuthServer Endpoints**:
- Development: `https://localhost:5001`
- Staging: TBD
- Production: TBD

**Required AuthServer Features**:
- User registration (`POST /api/admin/users`)
- User authentication (`POST /api/auth/login`)
- JWT token issuance with claims (`sub`, `uid`, `role`, `email`)

**Test Users** (AuthServer):
```csharp
// Admin user
Username: alice
Password: password
Role: admin

// Dispatcher user
Username: diana
Password: password
Role: dispatcher

// Driver user
Username: charlie
Password: password
Role: driver
UID: driver-001
```

---

## ??? Local Development Setup

### Step 1: Clone Repository

```bash
git clone https://github.com/BidumanADT/Bellwood.AdminApi.git
cd Bellwood.AdminApi
```

### Step 2: Restore Dependencies

```bash
dotnet restore
```

**Expected Output**:
```
Restore completed in 2.5 sec for Bellwood.AdminApi.csproj
```

### Step 3: Configure Settings

**File**: `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "Key": "super-long-jwt-signing-secret-1234"
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromAddress": "noreply@bellwood.com",
    "FromName": "Bellwood AdminAPI"
  }
}
```

**Critical**: `Jwt.Key` must match AuthServer's signing key!

---

### Step 4: Build Project

```bash
dotnet build
```

**Expected Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

### Step 5: Run Locally

**Terminal 1** (AuthServer):
```bash
cd ../AuthServer
dotnet run
# Listening on https://localhost:5001
```

**Terminal 2** (AdminAPI):
```bash
cd ../Bellwood.AdminApi
dotnet run
# Listening on https://localhost:5206
```

**Verify**:
```bash
curl https://localhost:5206/health
# Expected: {"status":"ok"}
```

---

### Step 6: Seed Test Data

**Option 1: PowerShell Script** (Recommended):
```powershell
cd Scripts
.\Seed-All.ps1
```

**Option 2: Manual Seeding**:
```bash
# Get admin token
$adminToken = (curl -X POST https://localhost:5001/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{"username":"alice","password":"password"}' | ConvertFrom-Json).accessToken

# Seed affiliates & drivers
curl -X POST https://localhost:5206/dev/seed-affiliates `
  -H "Authorization: Bearer $adminToken"

# Seed quotes
curl -X POST https://localhost:5206/quotes/seed `
  -H "Authorization: Bearer $adminToken"

# Seed bookings
curl -X POST https://localhost:5206/bookings/seed `
  -H "Authorization: Bearer $adminToken"
```

---

## ?? Build for Production

### Step 1: Clean Build

```bash
dotnet clean
dotnet build --configuration Release
```

### Step 2: Publish

**Self-Contained** (includes .NET runtime):
```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained true -o ./publish
```

**Framework-Dependent** (requires .NET runtime on server):
```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained false -o ./publish
```

**Output Directory**: `./publish/`

**Published Files**:
```
publish/
??? Bellwood.AdminApi.dll
??? Bellwood.AdminApi.exe (Windows only)
??? appsettings.json
??? appsettings.Production.json
??? web.config (for IIS)
??? ... (dependencies)
```

---

## ?? Environment Configuration

### Environment Variables

**Required**:

| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development`, `Staging`, `Production` |
| `Jwt__Key` | JWT signing key | `super-long-jwt-signing-secret-1234` |
| `Email__SmtpServer` | SMTP server address | `smtp.gmail.com` |
| `Email__SmtpPort` | SMTP port | `587` |
| `Email__SmtpUsername` | SMTP username | `your-email@gmail.com` |
| `Email__SmtpPassword` | SMTP password | `your-app-password` |

**Optional**:

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_URLS` | Listening URLs | `https://localhost:5206` |
| `ConnectionStrings__DefaultConnection` | Database connection (future) | N/A |

**Windows**:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Jwt__Key = "production-jwt-key-goes-here"
```

**Linux/macOS**:
```bash
export ASPNETCORE_ENVIRONMENT=Production
export Jwt__Key=production-jwt-key-goes-here
```

---

### appsettings.Production.json

**Create**: `appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "Key": "${JWT_KEY}"  // Replace or use environment variable
  },
  "Email": {
    "SmtpServer": "smtp.office365.com",
    "SmtpPort": 587,
    "SmtpUsername": "${EMAIL_USERNAME}",
    "SmtpPassword": "${EMAIL_PASSWORD}",
    "FromAddress": "noreply@bellwood.com",
    "FromName": "Bellwood Global"
  },
  "AllowedHosts": "bellwood.com,*.bellwood.com"
}
```

**Security Best Practices**:
- ? **Never** commit secrets to Git
- ? Use environment variables or Azure Key Vault
- ? Rotate JWT keys periodically
- ? Use app-specific passwords for SMTP

---

## ??? IIS Deployment

### Prerequisites

- Windows Server 2016+ or Windows 10+
- IIS 10.0+
- .NET 8.0 Hosting Bundle ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))

### Step 1: Install Hosting Bundle

```powershell
# Download and install .NET 8.0 Hosting Bundle
# Restart IIS after installation
iisreset
```

### Step 2: Create Application Pool

**IIS Manager**:
1. Right-click **Application Pools** ? **Add Application Pool**
2. **Name**: `BellwoodAdminAPI`
3. **.NET CLR Version**: **No Managed Code**
4. **Managed Pipeline Mode**: **Integrated**
5. **Start application pool immediately**: ?

**Advanced Settings**:
- **Identity**: `ApplicationPoolIdentity` (recommended)
- **Load User Profile**: `True`
- **Enable 32-Bit Applications**: `False`

### Step 3: Create Website

**IIS Manager**:
1. Right-click **Sites** ? **Add Website**
2. **Site name**: `BellwoodAdminAPI`
3. **Application pool**: `BellwoodAdminAPI`
4. **Physical path**: `C:\inetpub\wwwroot\BellwoodAdminAPI`
5. **Binding**:
   - **Type**: `https`
   - **Port**: `443`
   - **Host name**: `api.bellwood.com`
   - **SSL certificate**: Select your certificate
6. Click **OK**

### Step 4: Deploy Files

**Copy Published Files**:
```powershell
# Copy all files from ./publish to IIS directory
Copy-Item -Path .\publish\* -Destination C:\inetpub\wwwroot\BellwoodAdminAPI\ -Recurse -Force
```

### Step 5: Configure web.config

**File**: `C:\inetpub\wwwroot\BellwoodAdminAPI\web.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\Bellwood.AdminApi.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

### Step 6: Set Permissions

```powershell
# Grant read/write permissions to App_Data folder
icacls "C:\inetpub\wwwroot\BellwoodAdminAPI\App_Data" /grant "IIS AppPool\BellwoodAdminAPI:(OI)(CI)F" /T
```

### Step 7: Restart Application Pool

```powershell
Restart-WebAppPool -Name BellwoodAdminAPI
```

### Step 8: Verify

```bash
curl https://api.bellwood.com/health
# Expected: {"status":"ok"}
```

---

## ?? Azure App Service Deployment

### Prerequisites

- Azure subscription
- Azure CLI installed ([Download](https://docs.microsoft.com/cli/azure/install-azure-cli))

### Step 1: Login to Azure

```bash
az login
```

### Step 2: Create Resource Group

```bash
az group create --name BellwoodAPI --location eastus
```

### Step 3: Create App Service Plan

```bash
az appservice plan create \
  --name BellwoodAPIPlan \
  --resource-group BellwoodAPI \
  --sku B1 \
  --is-linux
```

### Step 4: Create Web App

```bash
az webapp create \
  --name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --plan BellwoodAPIPlan \
  --runtime "DOTNET|8.0"
```

### Step 5: Configure Environment Variables

```bash
az webapp config appsettings set \
  --name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    Jwt__Key="your-production-jwt-key" \
    Email__SmtpServer="smtp.office365.com" \
    Email__SmtpPort=587 \
    Email__SmtpUsername="noreply@bellwood.com" \
    Email__SmtpPassword="your-app-password"
```

### Step 6: Configure HTTPS

```bash
# Enable HTTPS only
az webapp update \
  --name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --https-only true
```

### Step 7: Deploy Code

**Option 1: ZIP Deploy**:
```bash
# Create ZIP of publish folder
Compress-Archive -Path .\publish\* -DestinationPath .\bellwood-api.zip

# Deploy ZIP
az webapp deploy \
  --name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --src-path .\bellwood-api.zip \
  --type zip
```

**Option 2: GitHub Actions**:

**Create**: `.github/workflows/azure-deploy.yml`

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Publish
      run: dotnet publish --configuration Release --output ./publish
    
    - name: Deploy to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: bellwood-adminapi
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ./publish
```

### Step 8: Configure Custom Domain

```bash
# Add custom domain
az webapp config hostname add \
  --webapp-name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --hostname api.bellwood.com

# Enable SSL
az webapp config ssl bind \
  --name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --certificate-thumbprint {thumbprint} \
  --ssl-type SNI
```

### Step 9: Verify

```bash
curl https://bellwood-adminapi.azurewebsites.net/health
# Or custom domain:
curl https://api.bellwood.com/health
```

---

## ?? Docker Deployment

### Dockerfile

**Create**: `Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Bellwood.AdminApi.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create App_Data directory
RUN mkdir -p /app/App_Data

# Expose ports
EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "Bellwood.AdminApi.dll"]
```

### Build Image

```bash
docker build -t bellwood-adminapi:latest .
```

### Run Container

```bash
docker run -d \
  --name bellwood-api \
  -p 5206:80 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Jwt__Key="your-jwt-key" \
  -e Email__SmtpServer="smtp.gmail.com" \
  -e Email__SmtpPort=587 \
  -e Email__SmtpUsername="your-email@gmail.com" \
  -e Email__SmtpPassword="your-app-password" \
  -v /data/bellwood:/app/App_Data \
  bellwood-adminapi:latest
```

**Volume Mapping**:
- `-v /data/bellwood:/app/App_Data` - Persists data files outside container

### Docker Compose

**Create**: `docker-compose.yml`

```yaml
version: '3.8'

services:
  adminapi:
    build: .
    container_name: bellwood-adminapi
    ports:
      - "5206:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Jwt__Key=${JWT_KEY}
      - Email__SmtpServer=${SMTP_SERVER}
      - Email__SmtpPort=${SMTP_PORT}
      - Email__SmtpUsername=${SMTP_USERNAME}
      - Email__SmtpPassword=${SMTP_PASSWORD}
    volumes:
      - bellwood-data:/app/App_Data
    restart: unless-stopped

volumes:
  bellwood-data:
```

**Run**:
```bash
docker-compose up -d
```

---

## ?? Data Protection Keys

### Development

**Location**: `%LocalAppData%\ASP.NET\DataProtection-Keys\`

**Automatic**: Keys created on first run.

### Production

**Challenge**: Keys must be shared across multiple instances.

**Solution 1: Azure Blob Storage** (Recommended):

```csharp
// Program.cs
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(
        new Uri("https://bellwood.blob.core.windows.net/keys/dataprotection-keys"),
        new DefaultAzureCredential());
```

**Solution 2: File System** (Single Server):

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\keys"));
```

**Solution 3: Redis** (Distributed):

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
```

---

## ? Production Checklist

### Pre-Deployment

- [ ] All tests passing (`dotnet test`)
- [ ] Build succeeds with `--configuration Release`
- [ ] No hardcoded secrets in code
- [ ] `appsettings.Production.json` configured
- [ ] Environment variables documented
- [ ] SSL certificate acquired
- [ ] Custom domain DNS configured
- [ ] AuthServer production endpoint configured

### Deployment

- [ ] Published to target environment
- [ ] Data Protection keys configured (if multi-instance)
- [ ] App_Data directory permissions set
- [ ] Health check endpoint accessible (`/health`)
- [ ] Test authentication (login to AuthServer)
- [ ] Seed initial affiliates and drivers (optional)
- [ ] Verify SMTP email sending works

### Post-Deployment

- [ ] Monitor application logs
- [ ] Test all critical endpoints
- [ ] Verify SignalR WebSocket connections work
- [ ] Test driver GPS location updates
- [ ] Verify passenger tracking endpoint
- [ ] Check OAuth credential encryption/decryption
- [ ] Run full regression test suite
- [ ] Document deployment timestamp and version
- [ ] Notify team of deployment

---

## ?? Monitoring & Logging

### Application Insights (Azure)

**Install Package**:
```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

**Configure**:
```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

**appsettings.Production.json**:
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key"
  }
}
```

### Logging Configuration

**Production Logging**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Bellwood.AdminApi": "Information"
    }
  }
}
```

**File Logging** (Serilog):

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
```

```csharp
// Program.cs
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/bellwood-.log", rollingInterval: RollingInterval.Day);
});
```

---

## ?? Troubleshooting Deployment

### Issue 1: "HTTP Error 500.30 - ASP.NET Core app failed to start"

**Cause**: Missing .NET 8.0 Hosting Bundle (IIS only)

**Fix**:
```powershell
# Install Hosting Bundle
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Restart IIS
iisreset
```

---

### Issue 2: "Unable to access App_Data folder"

**Cause**: Insufficient permissions

**Fix** (IIS):
```powershell
icacls "C:\inetpub\wwwroot\BellwoodAdminAPI\App_Data" /grant "IIS AppPool\BellwoodAdminAPI:(OI)(CI)F" /T
```

**Fix** (Docker):
```bash
# Ensure volume mounted correctly
docker run -v /data/bellwood:/app/App_Data ...
```

---

### Issue 3: "JWT token validation failed"

**Cause**: Mismatched JWT signing keys between AuthServer and AdminAPI

**Fix**:
```bash
# Verify keys match
# AuthServer: appsettings.json -> Jwt.Key
# AdminAPI: appsettings.json -> Jwt.Key
# Must be IDENTICAL
```

---

### Issue 4: SignalR WebSocket Connection Fails

**Cause**: Missing WebSocket support (IIS)

**Fix**:
```powershell
# Enable WebSockets feature
Install-WindowsFeature -Name Web-WebSockets
# Restart IIS
iisreset
```

---

## ?? Related Documentation

- `01-System-Architecture.md` - Overall system design
- `02-Testing-Guide.md` - Testing workflows
- `31-Scripts-Reference.md` - Deployment scripts
- `32-Troubleshooting.md` - Common issues & solutions

---

## ?? Future Deployment Enhancements

### Phase 3+ Roadmap

1. **Kubernetes Deployment**:
   - Helm charts for multi-instance deployments
   - Auto-scaling based on CPU/memory
   - Rolling updates with zero downtime

2. **CI/CD Pipeline**:
   - Automated builds on commit
   - Automated testing before deployment
   - Blue/green deployments

3. **Database Migration**:
   - Move from file-based storage to SQL Server
   - Entity Framework Core migrations
   - Backup/restore strategies

4. **Performance Monitoring**:
   - Real-time performance metrics
   - Alerting on failures
   - Distributed tracing

---

**Last Updated**: January 14, 2026  
**Status**: ? Production Ready  
**Deployment Version**: 2.0
