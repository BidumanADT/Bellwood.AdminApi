# Program.cs Modularization Plan

**Goal**: Break the 3,500+ line `Program.cs` into focused, independently editable modules — without changing any runtime behavior.

**Why**: At its current size, `Program.cs` is unmaintainable by humans or AI tooling alike. Every automated edit risks corrupting unrelated sections. Modularization enables:
- Targeted edits to individual feature areas
- Parallel development without merge conflicts
- AI tooling that can read/edit a single file without context overflow
- Code review scoped to the feature being changed

---

## Strategy: Minimal API Extension Methods

ASP.NET Core Minimal APIs support a clean modularization pattern using **extension methods on `WebApplication`** (for endpoints) and **`IServiceCollection`** (for service registration). Each feature area gets its own static class in its own file.

**Key constraint**: This is purely a code organization refactor. The HTTP contract, DI registrations, middleware order, and runtime behavior remain identical.

---

## Proposed File Structure

```
Program.cs                          (~80 lines — builder, middleware, app.Run())
Endpoints/
??? QuoteEndpoints.cs               (~250 lines)
??? QuoteLifecycleEndpoints.cs      (~350 lines)
??? BookingEndpoints.cs             (~400 lines)
??? BookingSeedEndpoints.cs         (~250 lines)
??? DriverEndpoints.cs              (~400 lines)
??? LocationEndpoints.cs            (~250 lines)
??? AffiliateEndpoints.cs           (~350 lines)
??? OAuthEndpoints.cs               (~150 lines)
??? AuditLogEndpoints.cs            (~250 lines)
??? DataRetentionEndpoints.cs       (~100 lines)
??? UserManagementEndpoints.cs      (~350 lines)
??? HealthCheckEndpoints.cs         (~60 lines)
Registration/
??? ServiceRegistration.cs          (~80 lines — all builder.Services calls)
??? AuthRegistration.cs             (~100 lines — JWT + policies)
Helpers/
??? SharedHelpers.cs                (~60 lines — GetBearerToken, GetDriverUid, GetRequestTimeZone, etc.)
Dtos/
??? RequestDtos.cs                  (~10 lines — ClearAuditLogsRequest, etc.)
```

---

## Pattern: Endpoint Extension Method

Each endpoint file follows this pattern:

```csharp
// File: Endpoints/QuoteEndpoints.cs
using static Bellwood.AdminApi.Services.UserAuthorizationHelper;

namespace Bellwood.AdminApi.Endpoints;

public static class QuoteEndpoints
{
    public static WebApplication MapQuoteEndpoints(this WebApplication app)
    {
        // POST /quotes/seed
        app.MapPost("/quotes/seed", async (...) =>
        {
            // exact same code as today
        })
        .WithName("SeedQuotes")
        .RequireAuthorization("AdminOnly");

        // POST /quotes
        app.MapPost("/quotes", async (...) =>
        {
            // exact same code as today
        })
        .WithName("SubmitQuote")
        .RequireAuthorization();

        // GET /quotes/list
        // GET /quotes/{id}
        // ... etc

        return app;
    }
}
```

And `Program.cs` calls it:

```csharp
app.MapQuoteEndpoints();
app.MapQuoteLifecycleEndpoints();
app.MapBookingEndpoints();
// etc.
```

---

## Pattern: Service Registration Extension Method

```csharp
// File: Registration/ServiceRegistration.cs
namespace Bellwood.AdminApi.Registration;

public static class ServiceRegistration
{
    public static IServiceCollection AddBellwoodServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Email
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        // Repositories
        services.AddSingleton<IQuoteRepository, FileQuoteRepository>();
        services.AddSingleton<IBookingRepository, FileBookingRepository>();
        // ... etc

        return services;
    }
}
```

```csharp
// File: Registration/AuthRegistration.cs
namespace Bellwood.AdminApi.Registration;

public static class AuthRegistration
{
    public static IServiceCollection AddBellwoodAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"] ?? "super-long-jwt-signing-secret-1234";
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        services.AddAuthentication(...)
            .AddJwtBearer(...);

        services.AddAuthorization(options => { ... });

        return services;
    }
}
```

---

## Pattern: Shared Helpers

The static local functions (`GetBearerToken`, `GetDriverUid`, `GetRequestTimeZone`, `GetCentralTimeZone`, `MaskSecret`) become regular static methods in a shared class:

```csharp
// File: Helpers/SharedHelpers.cs
namespace Bellwood.AdminApi.Helpers;

public static class SharedHelpers
{
    public static string? GetBearerToken(HttpContext context) { ... }
    public static string? GetDriverUid(HttpContext context) { ... }
    public static TimeZoneInfo GetRequestTimeZone(HttpContext context) { ... }
    public static TimeZoneInfo GetCentralTimeZone() { ... }
    public static string MaskSecret(string secret) { ... }
}
```

Endpoint files add `using static Bellwood.AdminApi.Helpers.SharedHelpers;` to keep call sites unchanged.

---

## Pattern: DTOs

```csharp
// File: Dtos/RequestDtos.cs
namespace Bellwood.AdminApi.Dtos;

/// <summary>
/// Request DTO for clearing all audit logs.
/// Alpha: Requires confirmation phrase for safety.
/// </summary>
/// <param name="Confirm">Must be exactly "CLEAR" (case-sensitive)</param>
public record ClearAuditLogsRequest(string Confirm);
```

---

## Resulting Program.cs (~80 lines)

```csharp
using Bellwood.AdminApi.Endpoints;
using Bellwood.AdminApi.Registration;

var builder = WebApplication.CreateBuilder(args);

// Service registration (single line per feature area)
builder.Services.AddBellwoodServices(builder.Configuration);
builder.Services.AddBellwoodAuth(builder.Configuration);
builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRouting();
builder.Services.ConfigureHttpJsonOptions(o => { /* same as today */ });

var app = builder.Build();

// Middleware (order matters)
app.UseCors();
app.UseMiddleware<ErrorTrackingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<LocationHub>("/hubs/location");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints (one line per feature area)
app.MapHealthCheckEndpoints();
app.MapQuoteEndpoints();
app.MapQuoteLifecycleEndpoints();
app.MapBookingEndpoints();
app.MapBookingSeedEndpoints();
app.MapDriverEndpoints();
app.MapLocationEndpoints();
app.MapAffiliateEndpoints();
app.MapOAuthEndpoints();
app.MapAuditLogEndpoints();
app.MapDataRetentionEndpoints();
app.MapUserManagementEndpoints();

// Startup validation
app.LogEmailConfiguration(builder.Configuration);

app.Run();
```

---

## Migration Steps (Recommended Order)

Each step is a single PR that can be reviewed independently. Every step must build and pass tests before the next one starts.

| Step | What moves out | Risk | Size |
|------|---------------|------|------|
| 1 | `Helpers/SharedHelpers.cs` — extract static helpers | Very low — no DI changes, just `using static` | ~60 lines |
| 2 | `Dtos/RequestDtos.cs` — extract `ClearAuditLogsRequest` | Very low — type moves to new namespace | ~10 lines |
| 3 | `Registration/ServiceRegistration.cs` | Low — only `builder.Services` calls | ~80 lines |
| 4 | `Registration/AuthRegistration.cs` | Low — only auth setup | ~100 lines |
| 5 | `Endpoints/HealthCheckEndpoints.cs` | Low — smallest endpoint group | ~60 lines |
| 6 | `Endpoints/OAuthEndpoints.cs` | Low — isolated feature | ~150 lines |
| 7 | `Endpoints/AuditLogEndpoints.cs` | Low — isolated feature | ~250 lines |
| 8 | `Endpoints/DataRetentionEndpoints.cs` | Low — isolated feature | ~100 lines |
| 9 | `Endpoints/AffiliateEndpoints.cs` | Medium — several related endpoints | ~350 lines |
| 10 | `Endpoints/LocationEndpoints.cs` | Medium — several endpoints + auth logic | ~250 lines |
| 11 | `Endpoints/DriverEndpoints.cs` | Medium — FSM logic | ~400 lines |
| 12 | `Endpoints/UserManagementEndpoints.cs` | Medium — external service calls | ~350 lines |
| 13 | `Endpoints/BookingSeedEndpoints.cs` | Medium — large seed data arrays | ~250 lines |
| 14 | `Endpoints/BookingEndpoints.cs` | Medium — core business logic | ~400 lines |
| 15 | `Endpoints/QuoteEndpoints.cs` | Medium — core business logic | ~250 lines |
| 16 | `Endpoints/QuoteLifecycleEndpoints.cs` | Medium — complex state machine | ~350 lines |

**Total**: 16 small, focused PRs. After all steps, `Program.cs` drops from ~3,500 lines to ~80.

---

## Verification Checklist (Per Step)

- [ ] `dotnet build` succeeds with zero errors
- [ ] All endpoints respond identically (same routes, same auth, same responses)
- [ ] No new `using` statements required in `Program.cs` beyond the extension method namespaces
- [ ] Git diff shows only code movement — no logic changes
- [ ] Swagger still shows all endpoints with correct names and tags

---

## Notes

- **No runtime behavior changes**: This is a pure structural refactor. Every HTTP endpoint, DI registration, middleware call, and auth policy remains identical.
- **No new packages required**: Extension methods are a built-in C# feature.
- **Backwards compatible**: The file structure change doesn't affect the compiled output.
- **AI-friendly**: Each endpoint file will be 60–400 lines — well within the range where automated tooling works reliably.
