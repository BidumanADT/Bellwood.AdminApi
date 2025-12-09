# Port Configuration Update - Quick Reference

## Updated Port Configuration

The AdminAPI is running on **port 5206** (not 5003 as previously documented).

### Current Configuration

| Service | URL | Port |
|---------|-----|------|
| **AdminAPI** | `https://localhost:5206` | 5206 (HTTPS) |
| AdminAPI (HTTP) | `http://localhost:5205` | 5205 (HTTP) |
| **AuthServer** | `https://localhost:5001` | 5001 (HTTPS) |
| AuthServer (HTTP) | `http://localhost:5000` | 5000 (HTTP) |

---

## Files Updated

All references to port `5003` have been changed to `5206`:

### PowerShell Scripts

1. ? `Scripts/Seed-All.ps1`
   - Default `$ApiBaseUrl = "https://localhost:5206"`

2. ? `Scripts/Seed-Affiliates.ps1`
   - Default `$ApiBaseUrl = "https://localhost:5206"`

3. ? `Scripts/Seed-Quotes.ps1`
   - Default `$ApiBaseUrl = "https://localhost:5206"`

4. ? `Scripts/Seed-Bookings.ps1`
   - Default `$ApiBaseUrl = "https://localhost:5206"`

### Documentation

5. ? `Scripts/README.md`
   - Updated all port references to 5206
   - Updated examples and usage instructions

6. ? `BELLWOOD_SYSTEM_INTEGRATION.md`
   - Updated API URLs table

7. ? `Scripts/UPDATE_SUMMARY.md`
   - Updated code examples

---

## Usage

All scripts now use the correct port by default:

```powershell
# These now connect to https://localhost:5206 automatically
.\Scripts\Seed-All.ps1
.\Scripts\Seed-Affiliates.ps1
.\Scripts\Seed-Quotes.ps1
.\Scripts\Seed-Bookings.ps1
```

If you need to use a different port:

```powershell
.\Scripts\Seed-All.ps1 -ApiBaseUrl "https://localhost:XXXX"
```

---

## Verification

You can verify the AdminAPI is running on the correct port:

```powershell
# Check API health
Invoke-WebRequest -Uri "https://localhost:5206/health" -UseBasicParsing

# Expected response: { "status": "ok" }
```

Or check the console output when starting the API:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5206
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5205
```

---

## No Action Required

The scripts will automatically use the correct port. Just run them as usual:

```powershell
cd C:\Users\sgtad\source\repos\Bellwood.AdminApi
.\Scripts\Seed-All.ps1
```

? All set!
