using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;
using Bellwood.AdminApi.Hubs;
using Bellwood.AdminApi.Middleware;
using BellwoodGlobal.Mobile.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

// Phase 1: Use static import for authorization helper methods
using static Bellwood.AdminApi.Services.UserAuthorizationHelper;

var builder = WebApplication.CreateBuilder(args);

// ===================================================================
// SERVICE REGISTRATION
// ===================================================================

// Phase 3: Application Insights for production monitoring
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    // Configuration will come from appsettings.json or environment variables
    // For local development, this can be disabled or use a development instrumentation key
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// Email configuration and sender
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// Repository services (file-backed storage)
builder.Services.AddSingleton<IQuoteRepository, FileQuoteRepository>();
builder.Services.AddSingleton<IBookingRepository, FileBookingRepository>();
builder.Services.AddSingleton<IAffiliateRepository, FileAffiliateRepository>();
builder.Services.AddSingleton<IDriverRepository, FileDriverRepository>();

// Phase 3: Audit log repository and logger service
builder.Services.AddSingleton<IAuditLogRepository, FileAuditLogRepository>();
builder.Services.AddSingleton<AuditLogger>();

// Phase 3C: Data protection services
builder.Services.AddSingleton<ISensitiveDataProtector, SensitiveDataProtector>();
builder.Services.AddSingleton<IDataRetentionService, DataRetentionService>();
builder.Services.AddHostedService<DataRetentionBackgroundService>();

// Phase 2: OAuth credential management
builder.Services.AddDataProtection(); // ASP.NET Core Data Protection API
builder.Services.AddMemoryCache(); // For credential caching
builder.Services.AddSingleton<IOAuthCredentialRepository, FileOAuthCredentialRepository>();
builder.Services.AddSingleton<OAuthCredentialService>();
builder.Services.AddHttpClient<AuthServerUserManagementService>()
    .ConfigureHttpClient(client =>
    {
        // Prevent hanging if AuthServer is slow (not down)
        client.Timeout = TimeSpan.FromSeconds(10);
    });

// Phase 4: LimoAnywhere integration (stub implementation)
builder.Services.AddSingleton<ILimoAnywhereService, LimoAnywhereServiceStub>();

// Location tracking service (in-memory)
builder.Services.AddSingleton<ILocationService, InMemoryLocationService>();

// SignalR for real-time location updates
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Background service for broadcasting location updates via SignalR
builder.Services.AddHostedService<LocationBroadcastService>();

// Phase 3: Enhanced health checks
builder.Services.AddHealthChecks()
    .AddCheck<AdminApiHealthCheck>("AdminAPI", tags: new[] { "ready", "live" });

// API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT auth using the same key as AuthServer:
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? "super-long-jwt-signing-secret-1234"; // fallback
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // CRITICAL: Disable default claim type mapping so "role" stays as "role"
    // instead of being mapped to "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    options.MapInboundClaims = false;
    
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role",      // Map "role" claim to roles
        NameClaimType = "sub"        // Map "sub" claim to username
    };
    
    // CRITICAL FIX: Manually set the claim types on the token validation parameters
    // This ensures User.IsInRole() works correctly
    options.TokenValidationParameters.RoleClaimType = "role";
    options.TokenValidationParameters.NameClaimType = "sub";
    
    // Add authentication event handlers for debugging and SignalR support
    options.Events = new JwtBearerEvents
    {
        // Handle SignalR WebSocket authentication (token in query string)
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            // If the request is for our SignalR hub, extract token from query string
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/location"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"?? Authentication FAILED: {context.Exception.GetType().Name}");
            Console.WriteLine($"   Message: {context.Exception.Message}");
            if (context.Exception.InnerException != null)
            {
                Console.WriteLine($"   Inner: {context.Exception.InnerException.Message}");
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var claims = context.Principal?.Claims
                .Select(c => $"{c.Type}={c.Value}")
                .ToList() ?? new List<string>();
            
            Console.WriteLine($"? Token VALIDATED successfully");
            Console.WriteLine($"   User: {context.Principal?.Identity?.Name ?? "N/A"}");
            Console.WriteLine($"   Claims: {string.Join(", ", claims)}");
            Console.WriteLine($"   IsAuthenticated: {context.Principal?.Identity?.IsAuthenticated}");
            
            // Check for role claim specifically
            var roleClaim = context.Principal?.FindFirst("role");
            if (roleClaim != null)
            {
                Console.WriteLine($"   ? Role found: {roleClaim.Value}");
            }
            else
            {
                Console.WriteLine($"   ??  NO ROLE CLAIM FOUND!");
            }
            
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"?? Authentication CHALLENGE");
            Console.WriteLine($"   Error: {context.Error}");
            Console.WriteLine($"   ErrorDescription: {context.ErrorDescription}");
            return Task.CompletedTask;
        },
        OnForbidden = context =>
        {
            Console.WriteLine($"?? Authorization FORBIDDEN (403)");
            Console.WriteLine($"   User: {context.Principal?.Identity?.Name ?? "Anonymous"}");
            Console.WriteLine($"   IsAuthenticated: {context.Principal?.Identity?.IsAuthenticated}");
            
            var roles = context.Principal?.FindAll("role").Select(c => c.Value).ToList();
            Console.WriteLine($"   Roles: {(roles?.Any() == true ? string.Join(", ", roles) : "NONE")}");

            return Task.CompletedTask;
        }
    };
});

// Register authorization with driver policy:
builder.Services.AddAuthorization(options =>
{
    // Phase 1: Driver policy
    options.AddPolicy("DriverOnly", policy =>
        policy.RequireRole("driver"));
    
    // Phase 2: Admin-only policy (sensitive operations)
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
    
    // Phase 2: Staff policy (admin OR dispatcher - operational access)
    options.AddPolicy("StaffOnly", policy =>
        policy.RequireRole("admin", "dispatcher"));
    
    // Phase 2: Booker policy (optional - for future use)
    options.AddPolicy("BookerOnly", policy =>
        policy.RequireRole("booker"));
});

// CORS for development
builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Routing
builder.Services.AddRouting();

// JSON serialization options (enums as strings, indented, null-ignore)
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.WriteIndented = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// ===================================================================
// MIDDLEWARE CONFIGURATION
// ===================================================================

app.UseCors();

// Phase 3: Error tracking middleware (before authentication to track all errors)
app.UseMiddleware<ErrorTrackingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hub for real-time location updates
app.MapHub<LocationHub>("/hubs/location");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===================================================================
// SHARED UTILITIES
// ===================================================================

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Phase 3: Enhanced health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
});

// Helper: Write detailed health check response as JSON
static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var result = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        timestamp = DateTime.UtcNow,
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds,
            exception = e.Value.Exception?.Message,
            data = e.Value.Data
        })
    }, new JsonSerializerOptions { WriteIndented = true });

    await context.Response.WriteAsync(result);
}

static string? GetBearerToken(HttpContext context)
{
    var authorizationHeader = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authorizationHeader))
    {
        return null;
    }

    return authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authorizationHeader["Bearer ".Length..].Trim()
        : null;
}

// ===================================================================
// QUOTE ENDPOINTS
// ===================================================================

// POST /quotes/seed - Seed sample quotes (DEV ONLY)
app.MapPost("/quotes/seed", async (HttpContext context, IQuoteRepository repo, AuditLogger auditLogger) =>
{
    var now = DateTime.UtcNow;
    
    // Phase 1: Capture the authenticated user's ID for seed data
    var createdByUserId = GetUserId(context.User);

    var samples = new[]
    {
        new QuoteRecord {
            CreatedUtc = now,
            Status = QuoteStatus.Pending,  // Changed from Submitted
            BookerName = "Alice Morgan",
            PassengerName = "Taylor Reed",
            VehicleClass = "Sedan",
            PickupLocation = "Langham Hotel, Chicago",
            DropoffLocation = "O'Hare International Airport",
            PickupDateTime = now.AddDays(1),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName = "Alice", LastName = "Morgan", PhoneNumber = "312-555-7777", EmailAddress = "alice.morgan@example.com" },
                Passenger = new() { FirstName = "Taylor", LastName = "Reed", PhoneNumber = "773-555-1122", EmailAddress = "taylor.reed@example.com" },
                VehicleClass = "Sedan",
                PickupDateTime = now.AddDays(1),
                PickupLocation = "Langham Hotel, Chicago",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                AsDirected = false,
                DropoffLocation = "O'Hare International Airport",
                RoundTrip = false,
                PassengerCount = 2, CheckedBags = 2, CarryOnBags = 1
            },
            // Phase 1: Set ownership on seeded data
            CreatedByUserId = createdByUserId
        },
        new QuoteRecord {
            CreatedUtc = now.AddMinutes(-10),
            Status = QuoteStatus.InReview,
            BookerName = "Chris Bailey",
            PassengerName = "Jordan Chen",
            VehicleClass = "SUV",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Downtown Chicago",
            PickupDateTime = now.AddDays(2),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Chris", LastName="Bailey" },
                Passenger = new() { FirstName="Jordan", LastName="Chen" },
                VehicleClass = "SUV",
                PickupDateTime = now.AddDays(2),
                PickupLocation = "O'Hare FBO",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.MeetAndGreet,
                PickupSignText = "CHEN / Bellwood",
                DropoffLocation = "Downtown Chicago",
                PassengerCount = 3, CheckedBags = 3, CarryOnBags = 2
            },
            CreatedByUserId = createdByUserId
        },
        new QuoteRecord {
            CreatedUtc = now.AddHours(-1),
            Status = QuoteStatus.Priced,
            BookerName = "Lisa Gomez",
            PassengerName = "Derek James",
            VehicleClass = "S-Class",
            PickupLocation = "Midway Airport",
            DropoffLocation = "The Langham Hotel",
            PickupDateTime = now.AddDays(3),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Lisa", LastName="Gomez" },
                Passenger = new() { FirstName="Derek", LastName="James" },
                VehicleClass = "S-Class",
                PickupDateTime = now.AddDays(3),
                PickupLocation = "Midway Airport",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "The Langham Hotel",
                PassengerCount = 2
            },
            CreatedByUserId = createdByUserId
        },
        new QuoteRecord {
            CreatedUtc = now.AddHours(-2),
            Status = QuoteStatus.Rejected,
            BookerName = "Evan Ross",
            PassengerName = "Mia Park",
            VehicleClass = "Sprinter",
            PickupLocation = "Signature FBO (ORD)",
            DropoffLocation = "Indiana Dunes State Park",
            PickupDateTime = now.AddDays(4),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Evan", LastName="Ross" },
                Passenger = new() { FirstName="Mia", LastName="Park" },
                VehicleClass = "Sprinter",
                PickupDateTime = now.AddDays(4),
                PickupLocation = "Signature FBO (ORD)",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.MeetAndGreet,
                PickupSignText = "PARK / Bellwood",
                DropoffLocation = "Indiana Dunes State Park",
                PassengerCount = 6, CheckedBags = 4, CarryOnBags = 6
            },
            CreatedByUserId = createdByUserId
        },
        new QuoteRecord {
            CreatedUtc = now.AddHours(-3),
            Status = QuoteStatus.Closed,
            BookerName = "Sarah Larkin",
            PassengerName = "James Miller",
            VehicleClass = "SUV",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Langham Hotel",
            PickupDateTime = now.AddDays(5),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Sarah", LastName="Larkin" },
                Passenger = new() { FirstName="James", LastName="Miller" },
                VehicleClass = "SUV",
                PickupDateTime = now.AddDays(5),
                PickupLocation = "O'Hare FBO",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Langham Hotel",
                PassengerCount = 4
            },
            CreatedByUserId = createdByUserId
        }
    };

    foreach (var r in samples)
        await repo.AddAsync(r);

    // Phase 3: Audit log the seed action
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.QuoteCreated,
        "Quote",
        details: new { count = samples.Length, action = "bulk_seed" },
        httpContext: context);

    return Results.Ok(new { 
        added = samples.Length,
        createdByUserId = createdByUserId ?? "(null - legacy data)"
    });
})
.WithName("SeedQuotes")
.RequireAuthorization("AdminOnly"); // Phase 2: Only admins can seed data

// POST /quotes - Submit a new quote request
app.MapPost("/quotes", async (
    [FromBody] QuoteDraft draft,
    HttpContext context,
    IEmailSender email,
    IQuoteRepository repo,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");

    if (draft is null || string.IsNullOrWhiteSpace(draft.PickupLocation))
        return Results.BadRequest(new { error = "Invalid payload" });

    // Phase 1: Capture the user who created this quote for ownership tracking
    var currentUserId = GetUserId(context.User);

    var rec = new QuoteRecord
    {
        BookerName = draft.Booker?.ToString() ?? "",
        PassengerName = draft.Passenger?.ToString() ?? "",
        VehicleClass = draft.VehicleClass,
        PickupLocation = draft.PickupLocation,
        DropoffLocation = draft.DropoffLocation,
        PickupDateTime = draft.PickupDateTime,
        Draft = draft,
        // Phase 1: Set ownership field (FIX: Use userId claim from JWT)
        CreatedByUserId = context.User.FindFirst("userId")?.Value ?? currentUserId
    };

    await repo.AddAsync(rec);

    // Phase 3: Audit log quote creation
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.QuoteCreated,
        "Quote",
        rec.Id,
        details: new { 
            passengerName = rec.PassengerName,
            vehicleClass = rec.VehicleClass,
            pickupLocation = rec.PickupLocation
        },
        httpContext: context);

    try
    {
        await email.SendQuoteAsync(draft, rec.Id);
        log.LogInformation("Quote {Id} submitted for {Passenger} by user {UserId}", 
            rec.Id, rec.PassengerName, rec.CreatedByUserId ?? "unknown");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Email send failed for {Id}", rec.Id);
        // Still accept the quote; email can be retried later if desired
    }

    return Results.Accepted($"/quotes/{rec.Id}", new { id = rec.Id });
})
.WithName("SubmitQuote")
.RequireAuthorization();

// GET /quotes/list - List recent quotes (paginated)
app.MapGet("/quotes/list", async ([FromQuery] int take, HttpContext context, IQuoteRepository repo) =>
{
    take = (take <= 0 || take > 200) ? 50 : take;
    var rows = await repo.ListAsync(take);

    // Phase 1: Filter quotes based on user role
    // - Staff (admin/dispatcher): See all quotes
    // - Bookers: Only see quotes they created
    // - Drivers: See no quotes (they don't use this endpoint)
    var user = context.User;
    var currentUserId = GetUserId(user);
    
    IEnumerable<QuoteRecord> filteredRows;
    if (IsStaffOrAdmin(user))
    {
        // Staff sees all quotes
        filteredRows = rows;
    }
    else
    {
        // Non-staff users only see their own quotes
        // Legacy records (null CreatedByUserId) are hidden from non-staff
        filteredRows = rows.Where(r => 
            !string.IsNullOrEmpty(r.CreatedByUserId) && 
            r.CreatedByUserId == currentUserId);
    }

    var list = filteredRows.Select(r => new {
        r.Id,
        r.CreatedUtc,
        r.Status,
        r.BookerName,
        r.PassengerName,
        r.VehicleClass,
        r.PickupLocation,
        r.DropoffLocation,
        r.PickupDateTime
    });

    return Results.Ok(list);
})
.WithName("ListQuotes")
.RequireAuthorization("StaffOnly"); // Phase 2: Changed from generic auth to StaffOnly

// GET /quotes/{id} - Get detailed quote by ID
app.MapGet("/quotes/{id}", async (string id, HttpContext context, IQuoteRepository repo, AuditLogger auditLogger) =>
{
    var rec = await repo.GetAsync(id);
    if (rec is null) 
        return Results.NotFound();
    
    // Phase 1: Verify user has access to this quote
    // - Staff (admin/dispatcher): Full access
    // - Bookers: Only their own quotes (CreatedByUserId match)
    // - Drivers: No access to quotes
    var user = context.User;
    
    // FIX: Staff can access all records (including legacy)
    if (!IsStaffOrAdmin(user))
    {
        // Non-staff: Must check ownership
        var currentUserId = GetUserId(user);
        
        // Legacy records (null CreatedByUserId) are not accessible to non-staff
        if (string.IsNullOrEmpty(rec.CreatedByUserId))
        {
            // Phase 3: Audit forbidden access attempt
            await auditLogger.LogForbiddenAsync(
                user,
                AuditActions.QuoteViewed,
                "Quote",
                id,
                httpContext: context);

            return Results.Problem(
                statusCode: 403,
                title: "Forbidden",
                detail: "You do not have permission to view this quote");
        }
        
        // Check if user owns this record
        if (rec.CreatedByUserId != currentUserId)
        {
            // Phase 3: Audit forbidden access attempt
            await auditLogger.LogForbiddenAsync(
                user,
                AuditActions.QuoteViewed,
                "Quote",
                id,
                httpContext: context);

            return Results.Problem(
                statusCode: 403,
                title: "Forbidden",
                detail: "You do not have permission to view this quote");
        }
    }
    
    // Phase 3: Audit successful quote view
    await auditLogger.LogSuccessAsync(
        user,
        AuditActions.QuoteViewed,
        "Quote",
        id,
        httpContext: context);

    // FIX: Return ALL lifecycle fields in response (not just EstimatedCost/BillingNotes)
    var response = new
    {
        rec.Id,
        Status = rec.Status.ToString(),
        rec.CreatedUtc,
        rec.BookerName,
        rec.PassengerName,
        rec.VehicleClass,
        rec.PickupLocation,
        rec.DropoffLocation,
        rec.PickupDateTime,
        rec.Draft,
        
        // FIX: Include ALL Phase Alpha lifecycle fields
        rec.CreatedByUserId,
        rec.ModifiedByUserId,
        rec.ModifiedOnUtc,
        rec.AcknowledgedAt,
        rec.AcknowledgedByUserId,
        rec.RespondedAt,
        rec.RespondedByUserId,
        rec.EstimatedPrice,
        rec.EstimatedPickupTime,
        rec.Notes
    };
    
    return Results.Ok(response);
})
.WithName("GetQuote")
.RequireAuthorization("StaffOnly"); // Phase 2: Changed from generic auth to StaffOnly

// ===================================================================
// PHASE ALPHA: QUOTE LIFECYCLE ENDPOINTS
// ===================================================================

// POST /quotes/{id}/acknowledge - Dispatcher acknowledges receipt of quote
app.MapPost("/quotes/{id}/acknowledge", async (
    string id,
    HttpContext context,
    IQuoteRepository repo,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");
    var user = context.User;
    var currentUserId = GetUserId(user);

    // Find quote
    var quote = await repo.GetAsync(id);
    if (quote is null)
        return Results.NotFound(new { error = "Quote not found" });

    // Verify quote is in Pending status
    if (quote.Status != QuoteStatus.Pending)
    {
        await auditLogger.LogFailureAsync(
            user,
            "Quote.Acknowledge",
            "Quote",
            id,
            errorMessage: $"Cannot acknowledge quote with status: {quote.Status}",
            httpContext: context);

        return Results.BadRequest(new { error = $"Can only acknowledge quotes with status 'Pending'. Current status: {quote.Status}" });
    }

    // Update quote
    quote.Status = QuoteStatus.Acknowledged;
    quote.AcknowledgedAt = DateTime.UtcNow;
    quote.AcknowledgedByUserId = currentUserId;
    quote.ModifiedByUserId = currentUserId;
    quote.ModifiedOnUtc = DateTime.UtcNow;

    await repo.UpdateAsync(quote);

    // Audit log acknowledgment
    await auditLogger.LogSuccessAsync(
        user,
        "Quote.Acknowledge",
        "Quote",
        id,
        details: new {
            acknowledgedAt = quote.AcknowledgedAt,
            acknowledgedBy = currentUserId,
            passengerName = quote.PassengerName
        },
        httpContext: context);

    log.LogInformation("Quote {Id} acknowledged by {UserId} for passenger {Passenger}",
        id, currentUserId, quote.PassengerName);

    return Results.Ok(new
    {
        message = "Quote acknowledged successfully",
        id = quote.Id,
        status = quote.Status.ToString(),
        acknowledgedAt = quote.AcknowledgedAt,
        acknowledgedBy = quote.AcknowledgedByUserId
    });
})
.WithName("AcknowledgeQuote")
.RequireAuthorization("StaffOnly"); // Phase Alpha: Dispatchers and admins only

// POST /quotes/{id}/respond - Dispatcher sends price/ETA response to passenger
app.MapPost("/quotes/{id}/respond", async (
    string id,
    [FromBody] QuoteResponseRequest request,
    HttpContext context,
    IQuoteRepository repo,
    IEmailSender email,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");
    var user = context.User;
    var currentUserId = GetUserId(user);

    // Validate request
    if (request.EstimatedPrice <= 0)
        return Results.BadRequest(new { error = "EstimatedPrice must be greater than 0" });

    // Simple validation: pickup time must be in the future (allow 1 minute grace period for clock skew)
    var now = DateTime.UtcNow;
    var gracePeriod = now.AddMinutes(-1); // 1 minute in the past is OK (clock skew tolerance)
    
    // Treat Unspecified as UTC for validation (simplest approach)
    var pickupTimeToValidate = request.EstimatedPickupTime.Kind == DateTimeKind.Utc 
        ? request.EstimatedPickupTime 
        : DateTime.SpecifyKind(request.EstimatedPickupTime, DateTimeKind.Utc);
    
    if (pickupTimeToValidate <= gracePeriod)
    {
        log.LogWarning("EstimatedPickupTime rejected: {PickupTime} is not after {GracePeriod}", 
            pickupTimeToValidate, gracePeriod);
        return Results.BadRequest(new { error = "EstimatedPickupTime must be in the future" });
    }

    // Find quote
    var quote = await repo.GetAsync(id);
    if (quote is null)
        return Results.NotFound(new { error = "Quote not found" });

    // Verify quote is in Acknowledged status
    if (quote.Status != QuoteStatus.Acknowledged)
    {
        await auditLogger.LogFailureAsync(
            user,
            "Quote.Respond",
            "Quote",
            id,
            errorMessage: $"Cannot respond to quote with status: {quote.Status}",
            httpContext: context);

        return Results.BadRequest(new { error = $"Can only respond to quotes with status 'Acknowledged'. Current status: {quote.Status}" });
    }

    // Update quote with response
    quote.Status = QuoteStatus.Responded;
    quote.RespondedAt = DateTime.UtcNow;
    quote.RespondedByUserId = currentUserId;
    quote.EstimatedPrice = request.EstimatedPrice;
    quote.EstimatedPickupTime = request.EstimatedPickupTime;
    quote.Notes = request.Notes;
    quote.ModifiedByUserId = currentUserId;
    quote.ModifiedOnUtc = DateTime.UtcNow;

    await repo.UpdateAsync(quote);

    // Audit log response
    await auditLogger.LogSuccessAsync(
        user,
        "Quote.Respond",
        "Quote",
        id,
        details: new {
            respondedAt = quote.RespondedAt,
            respondedBy = currentUserId,
            estimatedPrice = quote.EstimatedPrice,
            estimatedPickupTime = quote.EstimatedPickupTime,
            notes = quote.Notes,
            passengerName = quote.PassengerName
        },
        httpContext: context);

    // Phase Alpha: Send email notification to passenger
    try
    {
        await email.SendQuoteResponseAsync(quote);
        log.LogInformation("Quote response email sent to passenger for quote {Id}", id);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to send quote response email for quote {Id}", id);
        // Continue anyway - quote response is saved
    }

    log.LogInformation("Quote {Id} responded to by {UserId} with price ${Price} for passenger {Passenger}",
        id, currentUserId, quote.EstimatedPrice, quote.PassengerName);

    return Results.Ok(new
    {
        message = "Quote response sent successfully",
        id = quote.Id,
        status = quote.Status.ToString(),
        respondedAt = quote.RespondedAt,
        respondedBy = quote.RespondedByUserId,
        estimatedPrice = quote.EstimatedPrice,
        estimatedPickupTime = quote.EstimatedPickupTime,
        notes = quote.Notes
    });
})
.WithName("RespondToQuote")
.RequireAuthorization("StaffOnly"); // Phase Alpha: Dispatchers and admins only

// POST /quotes/{id}/accept - Passenger accepts quote and converts to booking
app.MapPost("/quotes/{id}/accept", async (
    string id,
    HttpContext context,
    IQuoteRepository quoteRepo,
    IBookingRepository bookingRepo,
    IEmailSender email,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");
    var user = context.User;
    var currentUserId = GetUserId(user);

    // Find quote
    var quote = await quoteRepo.GetAsync(id);
    if (quote is null)
        return Results.NotFound(new { error = "Quote not found" });

    // FIX: Verify ownership (booker ONLY - not staff!)
    // Only the booker who created the quote can accept it
    if (!IsStaffOrAdmin(user))
    {
        // Non-staff: Must own the quote
        if (string.IsNullOrEmpty(quote.CreatedByUserId) || quote.CreatedByUserId != currentUserId)
        {
            await auditLogger.LogForbiddenAsync(
                user,
                "Quote.Accept",
                "Quote",
                id,
                httpContext: context);

            return Results.Problem(
                statusCode: 403,
                title: "Forbidden",
                detail: "You do not have permission to accept this quote");
        }
    }
    else
    {
        // FIX: Staff (admin) should NOT be able to accept quotes on behalf of bookers
        // Only the actual booker can accept
        log.LogWarning("Admin {UserId} attempted to accept quote {QuoteId} on behalf of booker", currentUserId, id);
        
        await auditLogger.LogForbiddenAsync(
            user,
            "Quote.Accept",
            "Quote",
            id,
            httpContext: context);

        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "Only the booker who requested this quote can accept it");
    }

    // Verify quote is in Responded status
    if (quote.Status != QuoteStatus.Responded)
    {
        await auditLogger.LogFailureAsync(
            user,
            "Quote.Accept",
            "Quote",
            id,
            errorMessage: $"Cannot accept quote with status: {quote.Status}",
            httpContext: context);

        return Results.BadRequest(new { error = $"Can only accept quotes with status 'Responded'. Current status: {quote.Status}" });
    }

    // Create booking from quote (Status = Requested per recommendation)
    var booking = new BookingRecord
    {
        Status = BookingStatus.Requested, // Per alpha test plan - maintains existing workflow
        BookerName = quote.BookerName,
        PassengerName = quote.PassengerName,
        VehicleClass = quote.VehicleClass,
        PickupLocation = quote.PickupLocation,
        DropoffLocation = quote.DropoffLocation,
        PickupDateTime = quote.EstimatedPickupTime ?? quote.PickupDateTime, // Use estimated if available
        Draft = quote.Draft,
        CreatedByUserId = currentUserId,
        SourceQuoteId = quote.Id // FIX: Link back to originating quote
    };

    await bookingRepo.AddAsync(booking);

    // Update quote status to Accepted
    quote.Status = QuoteStatus.Accepted;
    quote.ModifiedByUserId = currentUserId;
    quote.ModifiedOnUtc = DateTime.UtcNow;

    await quoteRepo.UpdateAsync(quote);

    // Audit log acceptance and booking creation
    await auditLogger.LogSuccessAsync(
        user,
        "Quote.Accept",
        "Quote",
        id,
        details: new {
            acceptedBy = currentUserId,
            createdBookingId = booking.Id,
            passengerName = quote.PassengerName,
            estimatedPrice = quote.EstimatedPrice
        },
        httpContext: context);

    await auditLogger.LogSuccessAsync(
        user,
        AuditActions.BookingCreated,
        "Booking",
        booking.Id,
        details: new {
            sourceQuoteId = quote.Id,
            passengerName = booking.PassengerName,
            vehicleClass = booking.VehicleClass,
            pickupLocation = booking.PickupLocation,
            pickupDateTime = booking.PickupDateTime
        },
        httpContext: context);

    // Phase Alpha: Send email notification to Bellwood staff
    try
    {
        await email.SendQuoteAcceptedAsync(quote, booking.Id);
        log.LogInformation("Quote accepted email sent to staff for quote {QuoteId}, booking {BookingId}", 
            id, booking.Id);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to send quote accepted email for quote {QuoteId}", id);
        // Continue anyway - quote acceptance is saved
    }

    log.LogInformation("Quote {QuoteId} accepted by {UserId}, created booking {BookingId} for passenger {Passenger}",
        id, currentUserId, booking.Id, quote.PassengerName);

    return Results.Ok(new
    {
        message = "Quote accepted and booking created successfully",
        quoteId = quote.Id,
        quoteStatus = quote.Status.ToString(),
        bookingId = booking.Id,
        bookingStatus = booking.Status.ToString(),
        sourceQuoteId = booking.SourceQuoteId // FIX: Return SourceQuoteId in response
    });
})
.WithName("AcceptQuote")
.RequireAuthorization(); // Phase Alpha: Booker (owner) only can accept

// POST /quotes/{id}/cancel - Cancel a quote
app.MapPost("/quotes/{id}/cancel", async (
    string id,
    HttpContext context,
    IQuoteRepository repo,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");
    var user = context.User;
    var currentUserId = GetUserId(user);

    // Find quote
    var quote = await repo.GetAsync(id);
    if (quote is null)
        return Results.NotFound();

    // Verify permission (owner or staff)
    if (!CanAccessRecord(user, quote.CreatedByUserId))
    {
        await auditLogger.LogForbiddenAsync(
            user,
            "Quote.Cancel",
            "Quote",
            id,
            httpContext: context);

        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You do not have permission to cancel this quote");
    }

    // Verify quote can be cancelled (not already Accepted or Cancelled)
    if (quote.Status == QuoteStatus.Accepted || quote.Status == QuoteStatus.Cancelled)
    {
        await auditLogger.LogFailureAsync(
            user,
            "Quote.Cancel",
            "Quote",
            id,
            errorMessage: $"Cannot cancel quote with status: {quote.Status}",
            httpContext: context);

        return Results.BadRequest(new { error = $"Cannot cancel quote with status '{quote.Status}'" });
    }

    // Update quote
    quote.Status = QuoteStatus.Cancelled;
    quote.ModifiedByUserId = currentUserId;
    quote.ModifiedOnUtc = DateTime.UtcNow;

    await repo.UpdateAsync(quote);

    // Audit log cancellation
    await auditLogger.LogSuccessAsync(
        user,
        "Quote.Cancel",
        "Quote",
        id,
        details: new {
            cancelledBy = currentUserId,
            passengerName = quote.PassengerName,
            previousStatus = quote.Status.ToString()
        },
        httpContext: context);

    log.LogInformation("Quote {Id} cancelled by {UserId} (passenger: {Passenger})",
        id, currentUserId, quote.PassengerName);

    return Results.Ok(new
    {
        message = "Quote cancelled successfully",
        id = quote.Id,
        status = quote.Status.ToString()
    });
})
.WithName("CancelQuote")
.RequireAuthorization();
