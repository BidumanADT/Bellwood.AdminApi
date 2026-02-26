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
using Serilog;
using Serilog.Formatting.Compact;

// Phase 1: Use static import for authorization helper methods
using static Bellwood.AdminApi.Services.UserAuthorizationHelper;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "AdminApi")
    .Enrich.WithProperty("environment", "Alpha")
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
// Load user-secrets in Development AND Alpha (local test environments)
if (builder.Environment.IsDevelopment() || builder.Environment.EnvironmentName.Equals("Alpha", StringComparison.OrdinalIgnoreCase))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

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
builder.Services.AddTransient<SmtpEmailSender>();

var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.Mode.Equals("DevPapercut", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddTransient<IEmailSender, PapercutEmailSender>();
}
else if (emailOptions.Mode.Equals("AlphaSandbox", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddTransient<IEmailSender, SmtpSandboxEmailSender>();
}
else
{
    builder.Services.AddTransient<IEmailSender, NoOpEmailSender>();
}

// Repository services (file-backed storage)
builder.Services.AddSingleton<IQuoteRepository, FileQuoteRepository>();
builder.Services.AddSingleton<IBookingRepository, FileBookingRepository>();
builder.Services.AddSingleton<IAffiliateRepository, FileAffiliateRepository>();
builder.Services.AddSingleton<IDriverRepository, FileDriverRepository>();

// Phase 3: Audit log repository and logger service
builder.Services.AddSingleton<IAuditLogRepository, FileAuditLogRepository>();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<IAuditEventRepository, SqliteAuditEventRepository>();

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
    .AddHttpMessageHandler<CorrelationIdHeaderHandler>()
    .ConfigureHttpClient(client =>
    {
        // Prevent hanging if AuthServer is slow (not down)
        client.Timeout = TimeSpan.FromSeconds(10);
    });
builder.Services.AddHttpClient("health-authserver");
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHeaderHandler>();

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
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<AdminApiHealthCheck>("admin-api", tags: new[] { "ready" })
    .AddCheck<AuthServerHealthCheck>("auth-server", tags: new[] { "ready" })
    .AddCheck<AuditEventStoreHealthCheck>("audit-event-store", tags: new[] { "ready" })
    .AddCheck<SmtpHealthCheck>("smtp", tags: new[] { "ready" });

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
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<StructuredRequestLoggingMiddleware>();

// Phase 3: Error tracking middleware (before authentication to track all errors)
app.UseMiddleware<ErrorTrackingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditEventMiddleware>();

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
            previousStatus = quote.Status.ToString()  // Record the actual previous status
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
.RequireAuthorization(); // Phase Alpha: Owner or staff can cancel

// ===================================================================
// BOOKING ENDPOINTS
// ===================================================================

// POST /bookings/seed - Seed sample bookings (DEV ONLY)
app.MapPost("/bookings/seed", async (HttpContext context, IBookingRepository repo, AuditLogger auditLogger) =>
{
    var now = DateTime.UtcNow;
    
    // Phase 1: Capture the authenticated user's ID for seed data
    var createdByUserId = GetUserId(context.User);

    var samples = new[]
    {
        // Requested - Initial booking request from passenger app
        new BookingRecord {
            CreatedUtc = now.AddMinutes(-10),
            Status = BookingStatus.Requested,
            BookerName = "Tom Anderson",
            PassengerName = "Maria Garcia",
            VehicleClass = "Sedan",
            PickupLocation = "Union Station, Chicago",
            DropoffLocation = "Willis Tower",
            PickupDateTime = now.AddHours(24),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName = "Tom", LastName = "Anderson", PhoneNumber = "312-555-1111", EmailAddress = "tom.anderson@example.com" },
                Passenger = new() { FirstName = "Maria", LastName = "Garcia", PhoneNumber = "312-555-2222", EmailAddress = "maria.garcia@example.com" },
                VehicleClass = "Sedan",
                PickupDateTime = now.AddHours(24),
                PickupLocation = "Union Station, Chicago",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Willis Tower",
                PassengerCount = 1, CheckedBags = 1, CarryOnBags = 1
            },
            CreatedByUserId = createdByUserId
        },
        // Confirmed - Staff approved booking
        new BookingRecord {
            CreatedUtc = now.AddMinutes(-20),
            Status = BookingStatus.Confirmed,
            BookerName = "James Wilson",
            PassengerName = "Patricia Brown",
            VehicleClass = "SUV",
            PickupLocation = "Navy Pier",
            DropoffLocation = "Midway Airport",
            PickupDateTime = now.AddHours(36),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName = "James", LastName = "Wilson", PhoneNumber = "312-555-3333", EmailAddress = "james.wilson@example.com" },
                Passenger = new() { FirstName = "Patricia", LastName = "Brown", PhoneNumber = "312-555-4444", EmailAddress = "patricia.brown@example.com" },
                VehicleClass = "SUV",
                PickupDateTime = now.AddHours(36),
                PickupLocation = "Navy Pier",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Midway Airport",
                PassengerCount = 3, CheckedBags = 3, CarryOnBags = 2
            },
            CreatedByUserId = createdByUserId
        },
        // Scheduled - Charlie's first ride (5 hours from now)
        new BookingRecord {
            CreatedUtc = now.AddMinutes(-5),
            Status = BookingStatus.Scheduled,
            AssignedDriverId = "TBD-driver-id",
            AssignedDriverUid = "driver-001", // Charlie
            AssignedDriverName = "Charlie Johnson",
            CurrentRideStatus = RideStatus.Scheduled,
            BookerName = "Chris Bailey",
            PassengerName = "Jordan Chen",
            VehicleClass = "Sedan",
            PickupLocation = "Langham Hotel",
            DropoffLocation = "Midway Airport",
            PickupDateTime = now.AddHours(5),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Chris", LastName="Bailey", PhoneNumber = "312-555-5555", EmailAddress = "chris.bailey@example.com" },
                Passenger = new() { FirstName="Jordan", LastName="Chen", PhoneNumber = "312-555-6666", EmailAddress = "jordan.chen@example.com" },
                VehicleClass = "Sedan",
                PickupDateTime = now.AddHours(5),
                PickupLocation = "Langham Hotel",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Midway Airport",
                PassengerCount = 1, CheckedBags = 1
            },
            CreatedByUserId = createdByUserId
        },
        // Scheduled - Charlie's second ride (48 hours from now)
        new BookingRecord {
            CreatedUtc = now.AddMinutes(-3),
            Status = BookingStatus.Scheduled,
            AssignedDriverId = "TBD-driver-id",
            AssignedDriverUid = "driver-001", // Charlie
            AssignedDriverName = "Charlie Johnson",
            CurrentRideStatus = RideStatus.Scheduled,
            BookerName = "David Miller",
            PassengerName = "Emma Watson",
            VehicleClass = "S-Class",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Peninsula Hotel, Chicago",
            PickupDateTime = now.AddHours(48),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="David", LastName="Miller", PhoneNumber = "312-555-7777", EmailAddress = "david.miller@example.com" },
                Passenger = new() { FirstName="Emma", LastName = "Watson", PhoneNumber = "312-555-8888", EmailAddress = "emma.watson@example.com" },
                VehicleClass = "S-Class",
                PickupDateTime = now.AddHours(48),
                PickupLocation = "O'Hare FBO",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.MeetAndGreet,
                PickupSignText = "WATSON / Bellwood",
                DropoffLocation = "Peninsula Hotel, Chicago",
                PassengerCount = 2, CheckedBags = 2, CarryOnBags = 1
            },
            CreatedByUserId = createdByUserId
        },
        // InProgress - Driver has picked up passenger
        new BookingRecord {
            CreatedUtc = now.AddHours(-1),
            Status = BookingStatus.InProgress,
            AssignedDriverId = "TBD-driver-id",
            AssignedDriverUid = "driver-002", // Sarah
            AssignedDriverName = "Sarah Lee",
            CurrentRideStatus = RideStatus.PassengerOnboard,
            BookerName = "Alice Morgan",
            PassengerName = "Taylor Reed",
            VehicleClass = "SUV",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Downtown Chicago",
            PickupDateTime = now.AddMinutes(-30),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName = "Alice", LastName = "Morgan", PhoneNumber = "312-555-7777", EmailAddress = "alice.morgan@example.com" },
                Passenger = new() { FirstName = "Taylor", LastName = "Reed", PhoneNumber = "773-555-1122", EmailAddress = "taylor.reed@example.com" },
                VehicleClass = "SUV",
                PickupDateTime = now.AddMinutes(-30),
                PickupLocation = "O'Hare FBO",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.MeetAndGreet,
                PickupSignText = "REED / Bellwood",
                DropoffLocation = "Downtown Chicago",
                PassengerCount = 2, CheckedBags = 2, CarryOnBags = 2
            },
            CreatedByUserId = createdByUserId
        },
        // Completed - Ride finished successfully
        new BookingRecord {
            CreatedUtc = now.AddDays(-1),
            Status = BookingStatus.Completed,
            AssignedDriverId = "TBD-driver-id",
            AssignedDriverUid = "driver-002", // Sarah
            AssignedDriverName = "Sarah Lee",
            CurrentRideStatus = RideStatus.Completed,
            BookerName = "Lisa Gomez",
            PassengerName = "Derek James",
            VehicleClass = "S-Class",
            PickupLocation = "O'Hare International",
            DropoffLocation = "Navy Pier",
            PickupDateTime = now.AddDays(-1).AddHours(2),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Lisa", LastName="Gomez", PhoneNumber = "312-555-9999", EmailAddress = "lisa.gomez@example.com" },
                Passenger = new() { FirstName="Derek", LastName="James", PhoneNumber = "312-555-0000", EmailAddress = "derek.james@example.com" },
                VehicleClass = "S-Class",
                PickupDateTime = now.AddDays(-1).AddHours(2),
                PickupLocation = "O'Hare International",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Navy Pier",
                PassengerCount = 2
            },
            CreatedByUserId = createdByUserId
        },
        // Cancelled - Booking was cancelled
        new BookingRecord {
            CreatedUtc = now.AddDays(-2),
            Status = BookingStatus.Cancelled,
            CancelledAt = now.AddDays(-2).AddHours(1),
            AssignedDriverId = "TBD-driver-id",
            AssignedDriverUid = "driver-003", // Robert
            AssignedDriverName = "Robert Brown",
            CurrentRideStatus = RideStatus.Cancelled,
            BookerName = "Michael Davis",
            PassengerName = "Jennifer Taylor",
            VehicleClass = "Sedan",
            PickupLocation = "Midway Airport",
            DropoffLocation = "Naperville",
            PickupDateTime = now.AddDays(-2).AddHours(3),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Michael", LastName="Davis", PhoneNumber = "847-555-1111", EmailAddress = "michael.davis@example.com" },
                Passenger = new() { FirstName="Jennifer", LastName="Taylor", PhoneNumber = "847-555-2222", EmailAddress = "jennifer.taylor@example.com" },
                VehicleClass = "Sedan",
                PickupDateTime = now.AddDays(-2).AddHours(3),
                PickupLocation = "Midway Airport",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Naperville",
                PassengerCount = 1
            },
            CreatedByUserId = createdByUserId
        },
        // NoShow - Passenger didn't show up
        new BookingRecord {
            CreatedUtc = now.AddDays(-3),
            Status = BookingStatus.NoShow,
            AssignedDriverId = "TBD-driver-id",
            AssignedDriverUid = "driver-003", // Robert
            AssignedDriverName = "Robert Brown",
            CurrentRideStatus = RideStatus.Cancelled,
            BookerName = "Robert Martinez",
            PassengerName = "Susan Clark",
            VehicleClass = "SUV",
            PickupLocation = "Union Station",
            DropoffLocation = "O'Hare Airport",
            PickupDateTime = now.AddDays(-3).AddHours(2),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Robert", LastName="Martinez", PhoneNumber = "847-555-3333", EmailAddress = "robert.martinez@example.com" },
                Passenger = new() { FirstName="Susan", LastName="Clark", PhoneNumber = "847-555-4444", EmailAddress = "susan.clark@example.com" },
                VehicleClass = "SUV",
                PickupDateTime = now.AddDays(-3).AddHours(2),
                PickupLocation = "Union Station",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "O'Hare Airport",
                PassengerCount = 2
            },
            CreatedByUserId = createdByUserId  // FIX: Add missing ownership field
        }
    };

    foreach (var r in samples)
        await repo.AddAsync(r);

    // Phase 3: Audit log the seed action
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.BookingCreated,
        "Booking",
        details: new { count = samples.Length, action = "bulk_seed" },
        httpContext: context);

    return Results.Ok(new { 
        added = samples.Length,
        createdByUserId = createdByUserId ?? "(null - legacy data)"  // FIX: Add to response
    });
})
.WithName("SeedBookings")
.RequireAuthorization("AdminOnly"); // Phase 2: Only admins can seed data

// POST /bookings - Submit a new booking request
app.MapPost("/bookings", async (
    [FromBody] QuoteDraft draft,
    HttpContext context,
    IEmailSender email,
    IBookingRepository repo,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("bookings");

    if (draft is null || string.IsNullOrWhiteSpace(draft.PickupLocation))
        return Results.BadRequest(new { error = "Invalid payload" });

    // Phase 1: Capture the user who created this booking for ownership tracking
    var currentUserId = GetUserId(context.User);

    var rec = new BookingRecord
    {
        BookerName = draft.Booker?.ToString() ?? "",
        PassengerName = draft.Passenger?.ToString() ?? "",
        VehicleClass = draft.VehicleClass,
        PickupLocation = draft.PickupLocation,
        DropoffLocation = draft.DropoffLocation,
        PickupDateTime = draft.PickupDateTime,
        Draft = draft,
        // Phase 1: Set ownership field
        CreatedByUserId = currentUserId
    };

    await repo.AddAsync(rec);

    // Phase 3: Audit log booking creation
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.BookingCreated,
        "Booking",
        rec.Id,
        details: new {
            passengerName = rec.PassengerName,
            vehicleClass = rec.VehicleClass,
            pickupLocation = rec.PickupLocation,
            pickupDateTime = rec.PickupDateTime
        },
        httpContext: context);

    try
    {
        await email.SendBookingAsync(draft, rec.Id);
        log.LogInformation("Booking {Id} submitted for {Passenger} by user {UserId}", 
            rec.Id, rec.PassengerName, currentUserId ?? "unknown");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Email send failed for booking {Id}", rec.Id);
        // Still accept the booking; email can be retried later
    }

    return Results.Accepted($"/bookings/{rec.Id}", new { id = rec.Id });
})
.WithName("SubmitBooking")
.RequireAuthorization();

// GET /bookings/list - List recent bookings (paginated)
app.MapGet("/bookings/list", async ([FromQuery] int take, HttpContext context, IBookingRepository repo) =>
{
    take = (take <= 0 || take > 200) ? 50 : take;
    var rows = await repo.ListAsync(take);
    
    // Phase 1: Filter bookings based on user role
    // - Staff (admin/dispatcher): See all bookings
    // - Drivers: See only bookings assigned to them
    // - Bookers: Only see their own bookings
    var user = context.User;
    var currentUserId = GetUserId(user);
    
    IEnumerable<BookingRecord> filteredRows;
    if (IsStaffOrAdmin(user))
    {
        // Staff sees all bookings
        filteredRows = rows;
    }
    else if (IsDriver(user))
    {
        // Drivers see only their assigned bookings
        // Note: Drivers should use /driver/rides/today instead, but this is a safety measure
        var driverUid = user.FindFirst("uid")?.Value;
        filteredRows = rows.Where(r => 
            !string.IsNullOrEmpty(r.AssignedDriverUid) && 
            r.AssignedDriverUid == driverUid);
    }
    else
    {
        // Bookers only see their own bookings
        // Legacy records (null CreatedByUserId) are hidden from non-staff
        filteredRows = rows.Where(r => 
            !string.IsNullOrEmpty(r.CreatedByUserId) && 
            r.CreatedByUserId == currentUserId);
    }
    
    // Get user's timezone for PickupDateTimeOffset conversion
    // Default to Central Time for admin users who don't send X-Timezone-Id header
    var userTz = GetRequestTimeZone(context);

    var list = filteredRows.Select(r =>
    {
        // FIX: Handle DateTime.Kind for PickupDateTimeOffset
        DateTimeOffset pickupOffset;
        if (r.PickupDateTime.Kind == DateTimeKind.Utc)
        {
            var pickupLocal = TimeZoneInfo.ConvertTimeFromUtc(r.PickupDateTime, userTz);
            pickupOffset = new DateTimeOffset(pickupLocal, userTz.GetUtcOffset(pickupLocal));
        }
        else
        {
            // Local or Unspecified - treat as already in userTz timezone
            // Must convert to Unspecified to avoid offset mismatch
            var unspecified = DateTime.SpecifyKind(r.PickupDateTime, DateTimeKind.Unspecified);
            pickupOffset = new DateTimeOffset(unspecified, userTz.GetUtcOffset(unspecified));
        }
        
        // FIX: Convert CreatedUtc to user's local timezone
        var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(r.CreatedUtc, userTz);
        var createdOffset = new DateTimeOffset(createdLocal, userTz.GetUtcOffset(createdLocal));
        
        return new
        {
            r.Id,
            r.CreatedUtc, // Keep for backward compatibility
            CreatedDateTimeOffset = createdOffset, // Add timezone-aware version
            Status = r.Status.ToString(),
            // FIX: Include CurrentRideStatus for real-time driver progress
            CurrentRideStatus = r.CurrentRideStatus?.ToString(),
            r.BookerName,
            r.PassengerName,
            r.VehicleClass,
            r.PickupLocation,
            r.DropoffLocation,
            // Keep old property for backward compatibility
            r.PickupDateTime,
            // FIX: Add PickupDateTimeOffset for correct timezone display
            PickupDateTimeOffset = pickupOffset,
            AssignedDriverId = r.AssignedDriverId,
            AssignedDriverUid = r.AssignedDriverUid,
            AssignedDriverName = r.AssignedDriverName ?? "Unassigned"
        };
    });

    return Results.Ok(list);
})
.WithName("ListBookings")
.RequireAuthorization("StaffOnly"); // Phase 2: Changed from generic auth to StaffOnly

// GET /bookings/{id} - Get detailed booking by ID
app.MapGet("/bookings/{id}", async (string id, HttpContext context, IBookingRepository repo, AuditLogger auditLogger) =>
{
    var rec = await repo.GetAsync(id);
    if (rec is null) return Results.NotFound();
    
    // Phase 1: Verify user has access to this booking
    // - Staff (admin/dispatcher): Full access
    // - Drivers: Only their assigned bookings
    // - Bookers: Only their own bookings (CreatedByUserId match)
    var user = context.User;
    
    if (!CanAccessBooking(user, rec.CreatedByUserId, rec.AssignedDriverUid))
    {
        // Phase 3: Audit forbidden access attempt
        await auditLogger.LogForbiddenAsync(
            user,
            AuditActions.BookingViewed,
            "Booking",
            id,
            httpContext: context);

        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You do not have permission to view this booking");
    }
    
    // Phase 3: Audit successful booking view
    await auditLogger.LogSuccessAsync(
        user,
        AuditActions.BookingViewed,
        "Booking",
        id,
        httpContext: context);

    // Get user's timezone for PickupDateTimeOffset conversion
    var userTz = GetRequestTimeZone(context);
    
    // Handle DateTime.Kind for PickupDateTimeOffset
    DateTimeOffset pickupOffset;
    if (rec.PickupDateTime.Kind == DateTimeKind.Utc)
    {
        var pickupLocal = TimeZoneInfo.ConvertTimeFromUtc(rec.PickupDateTime, userTz);
        pickupOffset = new DateTimeOffset(pickupLocal, userTz.GetUtcOffset(pickupLocal));
    }
    else
    {
        // Local or Unspecified - treat as already in userTz timezone
        // Must convert to Unspecified to avoid offset mismatch
        var unspecified = DateTime.SpecifyKind(rec.PickupDateTime, DateTimeKind.Unspecified);
        pickupOffset = new DateTimeOffset(unspecified, userTz.GetUtcOffset(unspecified));
    }

    // FIX: Convert CreatedUtc to user's local timezone
    var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(rec.CreatedUtc, userTz);
    var createdOffset = new DateTimeOffset(createdLocal, userTz.GetUtcOffset(createdLocal));

    // FIX: Return SourceQuoteId in response
    var response = new
    {
        rec.Id,
        Status = rec.Status.ToString(),
        CurrentRideStatus = rec.CurrentRideStatus?.ToString(),
        rec.CreatedUtc, // Keep for backward compatibility
        CreatedDateTimeOffset = createdOffset, // Timezone-aware version
        rec.BookerName,
        rec.PassengerName,
        rec.VehicleClass,
        rec.PickupLocation,
        rec.DropoffLocation,
        rec.PickupDateTime, // Keep for backward compatibility
        PickupDateTimeOffset = pickupOffset, // Timezone-aware version
        rec.Draft,
        rec.AssignedDriverId,
        rec.AssignedDriverUid,
        AssignedDriverName = rec.AssignedDriverName ?? "Unassigned",
        
        // FIX: Include SourceQuoteId in booking detail response
        rec.SourceQuoteId,
        
        // Phase 2: Billing fields (currently null - will be populated in Phase 3+)
        PaymentMethodId = (string?)null,      // TODO: Populate when Stripe/payment integration added
        PaymentMethodLast4 = (string?)null,   // TODO: Populate when Stripe/payment integration added
        PaymentAmount = (decimal?)null,        // TODO: Populate when final amount calculated
        TotalAmount = (decimal?)null,          // TODO: Populate when final amount calculated
        TotalFare = (decimal?)null             // TODO: Populate when final fare calculated
    };

    return Results.Ok(response);
})
.WithName("GetBooking")
.RequireAuthorization("StaffOnly"); // Phase 2: Changed from generic auth to StaffOnly

// ===================================================================
// CANCEL ENDPOINTS
// ===================================================================

// POST /bookings/{id}/cancel - Cancel a booking request
app.MapPost("/bookings/{id}/cancel", async (
    string id,
    HttpContext context,
    IBookingRepository repo,
    IEmailSender email,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("bookings");
    var user = context.User;
    var currentUserId = GetUserId(user);

    // Find booking
    var booking = await repo.GetAsync(id);
    if (booking is null)
        return Results.NotFound(new { error = "Booking not found" });

    // Phase 1: Verify user has permission to cancel this booking
    // - Staff (admin/dispatcher): Can cancel any booking
    // - Bookers: Can only cancel their own bookings
    // - Drivers: Cannot cancel bookings (they use status updates instead)
    if (!CanAccessRecord(user, booking.CreatedByUserId))
    {
        log.LogWarning("User {UserId} attempted to cancel booking {BookingId} they don't own", 
            currentUserId, id);

        // Phase 3: Audit forbidden cancellation attempt
        await auditLogger.LogForbiddenAsync(
            user,
            AuditActions.BookingCancelled,
            "Booking",
            id,
            httpContext: context);

        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You do not have permission to cancel this booking");
    }

    // Only allow cancellation if status is Requested or Confirmed
    if (booking.Status != BookingStatus.Requested && booking.Status != BookingStatus.Confirmed)
    {
        // Phase 3: Audit failed cancellation (invalid status)
        await auditLogger.LogFailureAsync(
            user,
            AuditActions.BookingCancelled,
            "Booking",
            id,
            errorMessage: $"Cannot cancel booking with status: {booking.Status}",
            httpContext: context);

        return Results.BadRequest(new { error = $"Cannot cancel booking with status: {booking.Status}" });
    }

    // Phase 1: Update status with audit trail (who cancelled and when)
    await repo.UpdateStatusAsync(id, BookingStatus.Cancelled, currentUserId);

    // Phase 3: Audit successful cancellation
    await auditLogger.LogSuccessAsync(
        user,
        AuditActions.BookingCancelled,
        "Booking",
        id,
        details: new {
            previousStatus = booking.Status.ToString(),
            passengerName = booking.PassengerName,
            cancelledBy = currentUserId
        },
        httpContext: context);

    // Send cancellation email
    try
    {
        await email.SendBookingCancellationAsync(booking.Draft, id, booking.BookerName);
        log.LogInformation("Booking {Id} cancelled by user {UserId} (booker: {Booker})", 
            id, currentUserId, booking.BookerName);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Cancellation email send failed for booking {Id}", id);
        // Continue anyway - cancellation is recorded
    }

    return Results.Ok(new { message = "Booking cancelled successfully", id, status = "Cancelled" });
})
.WithName("CancelBooking")
.RequireAuthorization();

// ===================================================================
// DRIVER ENDPOINTS
// ===================================================================

// Helper: Extract driver UID from claims
static string? GetDriverUid(HttpContext context) =>
    context.User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;

// Helper: Get timezone from request header or fallback to Central (for backward compatibility)
static TimeZoneInfo GetRequestTimeZone(HttpContext context)
{
    // Try to get timezone from header (e.g., "America/New_York", "Europe/London", "Asia/Tokyo")
    var timezoneHeader = context.Request.Headers["X-Timezone-Id"].FirstOrDefault();
    
    if (!string.IsNullOrWhiteSpace(timezoneHeader))
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneHeader);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? Invalid timezone ID in header '{timezoneHeader}': {ex.Message}");
            // Fall through to default
        }
    }
    
    // Fallback: Try Central Time (for backward compatibility with existing deployments)
    return GetCentralTimeZone();
}

// Helper: Get Central Standard Time (Bellwood's original operating timezone)
static TimeZoneInfo GetCentralTimeZone()
{
    try
    {
        // Try to get Central Standard Time
        return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    }
    catch
    {
        // Fallback for Linux/Mac (uses IANA timezone IDs)
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch
        {
            // Last resort: return local timezone (assumes server is in Central)
            Console.WriteLine("?? Warning: Could not load Central timezone, using server local time");
            return TimeZoneInfo.Local;
        }
    }
}

// Helper: Mask client secret for display (Phase 3: OAuth security)
static string MaskSecret(string secret)
{
    if (string.IsNullOrWhiteSpace(secret)) return "********";
    if (secret.Length <= 8) return "********";
    
    // Show first 4 and last 4 characters, mask the middle
    return $"{secret[..4]}...{secret[^4..]}";
}

// GET /driver/rides/today - Get driver's rides for the next 24 hours
app.MapGet("/driver/rides/today", async (HttpContext context, IBookingRepository repo) =>
{
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    // WORLDWIDE FIX: Use timezone from request header (driver's device timezone)
    // Mobile apps should send X-Timezone-Id header (e.g., "America/New_York", "Europe/London")
    // Falls back to Central Time for backward compatibility
    var driverTz = GetRequestTimeZone(context);
    var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, driverTz);
    var tomorrowLocal = nowLocal.AddHours(24);
    
    // Log timezone for debugging (remove in production)
    Console.WriteLine($"?? Driver {driverUid} timezone: {driverTz.Id}, current time: {nowLocal:yyyy-MM-dd HH:mm}");

    var allBookings = await repo.ListAsync(200); // Get enough to filter
    var driverRides = allBookings
        .Where(b => b.AssignedDriverUid == driverUid
                    && b.PickupDateTime >= nowLocal
                    && b.PickupDateTime <= tomorrowLocal
                    && b.CurrentRideStatus != RideStatus.Completed
                    && b.CurrentRideStatus != RideStatus.Cancelled)
        .OrderBy(b => b.PickupDateTime)
        .Select(b =>
        {
            // FIX: Handle both UTC and Unspecified DateTime.Kind
            // Seed data has Kind=Utc, mobile app data has Kind=Unspecified
            DateTimeOffset pickupOffset;
            
            if (b.PickupDateTime.Kind == DateTimeKind.Utc)
            {
                // Convert UTC to driver's local timezone first, then create offset
                var pickupLocal = TimeZoneInfo.ConvertTimeFromUtc(b.PickupDateTime, driverTz);
                pickupOffset = new DateTimeOffset(pickupLocal, driverTz.GetUtcOffset(pickupLocal));
            }
            else
            {
                // Local or Unspecified - treat as already in correct timezone
                // Must convert to Unspecified to avoid offset mismatch
                var unspecified = DateTime.SpecifyKind(b.PickupDateTime, DateTimeKind.Unspecified);
                pickupOffset = new DateTimeOffset(unspecified, driverTz.GetUtcOffset(unspecified));
            }
            
            return new DriverRideListItemDto
            {
                Id = b.Id,
                PickupDateTimeOffset = pickupOffset,
                PickupLocation = b.PickupLocation,
                DropoffLocation = b.DropoffLocation,
                PassengerName = b.PassengerName,
                PassengerPhone = b.Draft.Passenger?.PhoneNumber ?? "N/A",
                Status = b.CurrentRideStatus ?? RideStatus.Scheduled
            };
        })
        .ToList();

    return Results.Ok(driverRides);
})
.WithName("GetDriverRidesToday")
.RequireAuthorization("DriverOnly");

// GET /driver/rides/{id} - Get detailed ride info (ownership validated)
app.MapGet("/driver/rides/{id}", async (string id, HttpContext context, IBookingRepository repo) =>
{
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    var booking = await repo.GetAsync(id);
    if (booking is null)
        return Results.NotFound(new { error = "Ride not found" });

    // Verify driver owns this ride
    if (booking.AssignedDriverUid != driverUid)
        return Results.Forbid();

    // Get driver's timezone for correct pickup time display
    var driverTz = GetRequestTimeZone(context);
    
    // FIX: Handle both UTC and Unspecified DateTime.Kind
    DateTimeOffset pickupOffset;
    if (booking.PickupDateTime.Kind == DateTimeKind.Utc)
    {
        // Convert UTC to driver's local timezone first
        var pickupLocal = TimeZoneInfo.ConvertTimeFromUtc(booking.PickupDateTime, driverTz);
        pickupOffset = new DateTimeOffset(pickupLocal, driverTz.GetUtcOffset(pickupLocal));
    }
    else
    {
        // Local or Unspecified - treat as already in userTz timezone
        // Must convert to Unspecified to avoid offset mismatch
        var unspecified = DateTime.SpecifyKind(booking.PickupDateTime, DateTimeKind.Unspecified);
        pickupOffset = new DateTimeOffset(unspecified, driverTz.GetUtcOffset(unspecified));
    }

    var detail = new DriverRideDetailDto
    {
        Id = booking.Id,
        PickupDateTimeOffset = pickupOffset,
        PickupLocation = booking.PickupLocation,
        PickupStyle = booking.Draft.PickupStyle.ToString(),
        PickupSignText = booking.Draft.PickupSignText,
        DropoffLocation = booking.DropoffLocation,
        PassengerName = booking.PassengerName,
        PassengerPhone = booking.Draft.Passenger?.PhoneNumber ?? "N/A",
        PassengerCount = booking.Draft.PassengerCount,
        CheckedBags = booking.Draft.CheckedBags ?? 0,
        CarryOnBags = booking.Draft.CarryOnBags ?? 0,
        VehicleClass = booking.VehicleClass,
        OutboundFlight = booking.Draft.OutboundFlight,
        AdditionalRequest = booking.Draft.AdditionalRequest,
        Status = booking.CurrentRideStatus ?? RideStatus.Scheduled
    };

    return Results.Ok(detail);
})
.WithName("GetDriverRideDetail")
.RequireAuthorization("DriverOnly");

// POST /driver/rides/{id}/status - Update ride status with FSM validation
app.MapPost("/driver/rides/{id}/status", async (
    string id,
    [FromBody] RideStatusUpdateRequest request,
    HttpContext context,
    IBookingRepository repo,
    ILocationService locationService,
    IHubContext<LocationHub> hubContext,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    // Finite state machine for ride status transitions
    var allowedTransitions = new Dictionary<RideStatus, RideStatus[]>
    {
        [RideStatus.Scheduled] = new[] { RideStatus.OnRoute, RideStatus.Cancelled },
        [RideStatus.OnRoute] = new[] { RideStatus.Arrived, RideStatus.Cancelled },
        [RideStatus.Arrived] = new[] { RideStatus.PassengerOnboard, RideStatus.Cancelled },
        [RideStatus.PassengerOnboard] = new[] { RideStatus.Completed, RideStatus.Cancelled },
        [RideStatus.Completed] = Array.Empty<RideStatus>(),
        [RideStatus.Cancelled] = Array.Empty<RideStatus>()
    };

    var log = loggerFactory.CreateLogger("driver");
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    var booking = await repo.GetAsync(id);
    if (booking is null)
        return Results.NotFound(new { error = "Ride not found" });

    // Verify driver owns this ride
    if (booking.AssignedDriverUid != driverUid)
        return Results.Forbid();

    var currentStatus = booking.CurrentRideStatus ?? RideStatus.Scheduled;

    // Validate state transition
    if (!allowedTransitions.ContainsKey(currentStatus) ||
        !allowedTransitions[currentStatus].Contains(request.NewStatus))
    {
        // Phase 3: Audit failed status update (invalid transition)
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.BookingUpdated,
            "Booking",
            id,
            errorMessage: $"Invalid status transition from {currentStatus} to {request.NewStatus}",
            details: new { 
                currentStatus = currentStatus.ToString(),
                requestedStatus = request.NewStatus.ToString(),
                driverUid
            },
            httpContext: context);

        return Results.BadRequest(new
        {
            error = $"Invalid status transition from {currentStatus} to {request.NewStatus}"
        });
    }

    // Update status
    booking.CurrentRideStatus = request.NewStatus;

    // Sync with BookingStatus
    BookingStatus newBookingStatus = booking.Status;
    if (request.NewStatus == RideStatus.PassengerOnboard)
        newBookingStatus = BookingStatus.InProgress;
    else if (request.NewStatus == RideStatus.Completed)
        newBookingStatus = BookingStatus.Completed;
    else if (request.NewStatus == RideStatus.Cancelled)
        newBookingStatus = BookingStatus.Cancelled;

    // FIX: Use new method that persists BOTH CurrentRideStatus and Status
    await repo.UpdateRideStatusAsync(id, request.NewStatus, newBookingStatus);

    // Phase 3: Audit successful status update
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.BookingUpdated,
        "Booking",
        id,
        details: new {
            previousRideStatus = currentStatus.ToString(),
            newRideStatus = request.NewStatus.ToString(),
            newBookingStatus = newBookingStatus.ToString(),
            driverUid,
            passengerName = booking.PassengerName
        },
        httpContext: context);

    // Broadcast status change to AdminPortal and passengers via SignalR
    await hubContext.BroadcastRideStatusChangedAsync(
        id, 
        driverUid, 
        request.NewStatus,
        booking.AssignedDriverName,
        booking.PassengerName);

    // Clean up location data and notify clients when ride ends
    if (request.NewStatus == RideStatus.Completed || request.NewStatus == RideStatus.Cancelled)
    {
        locationService.RemoveLocation(id);
        var reason = request.NewStatus == RideStatus.Completed ? "Ride completed" : "Ride cancelled";
        await hubContext.NotifyTrackingStoppedAsync(id, reason);
        log.LogInformation("Location tracking stopped for ride {Id}: {Reason}", id, reason);
    }

    log.LogInformation("Driver {Uid} updated ride {Id} status to {Status}",
        driverUid, id, request.NewStatus);

    // Updated response contract for Phase 2
    return Results.Ok(new
    {
        success = true,
        rideId = id,
        newStatus = request.NewStatus.ToString(),
        bookingStatus = newBookingStatus.ToString(),
        timestamp = DateTime.UtcNow
    });
})
.WithName("UpdateRideStatus")
.RequireAuthorization("DriverOnly");

// POST /driver/location/update - Receive location updates (rate-limited)
app.MapPost("/driver/location/update", async (
    [FromBody] LocationUpdate update,
    HttpContext context,
    ILocationService locationService,
    IBookingRepository repo,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("driver");
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    // Verify ride exists and belongs to driver
    var booking = await repo.GetAsync(update.RideId);
    if (booking is null || booking.AssignedDriverUid != driverUid)
        return Results.NotFound(new { error = "Ride not found" });

    // Only accept updates for active rides
    var activeStatuses = new[] { RideStatus.OnRoute, RideStatus.Arrived, RideStatus.PassengerOnboard };
    if (!booking.CurrentRideStatus.HasValue || !activeStatuses.Contains(booking.CurrentRideStatus.Value))
    {
        return Results.BadRequest(new { error = "Location tracking not active for this ride" });
    }

    // Try to store location (rate-limited) - SignalR broadcast happens via event
    if (!locationService.TryUpdateLocation(driverUid, update))
    {
        return Results.StatusCode(429); // Too Many Requests
    }

    log.LogDebug("Location updated for ride {RideId} by driver {Uid}: ({Lat}, {Lon}), heading={Heading}, speed={Speed}", 
        update.RideId, driverUid, update.Latitude, update.Longitude, update.Heading, update.Speed);

    return Results.Ok(new { 
        message = "Location updated",
        rideId = update.RideId,
        timestamp = DateTime.UtcNow
    });
})
.WithName("UpdateDriverLocation")
.RequireAuthorization("DriverOnly");

// GET /driver/location/{rideId} - Get latest location for a ride (admin/driver/passenger use)
app.MapGet("/driver/location/{rideId}", async (
    string rideId,
    HttpContext context,
    ILocationService locationService,
    IBookingRepository repo) =>
{
    var booking = await repo.GetAsync(rideId);
    if (booking is null)
        return Results.NotFound();

    // SECURITY FIX: Verify caller has permission to view this ride's location
    var driverUid = GetDriverUid(context);
    var userRole = context.User.FindFirst("role")?.Value;
    var userSub = context.User.FindFirst("sub")?.Value;
    
    // Allow access if:
    // 1. User is the assigned driver
    // 2. User is an admin or dispatcher
    // 3. User is authenticated but has no role (backward compatibility for AdminPortal)
    bool isAuthorized = false;
    
    if (!string.IsNullOrEmpty(driverUid) && driverUid == booking.AssignedDriverUid)
    {
        isAuthorized = true; // Driver can see their own ride
    }
    else if (userRole == "admin" || userRole == "dispatcher")
    {
        isAuthorized = true; // Admins can see all rides
    }
    else if (string.IsNullOrEmpty(userRole) && context.User.Identity?.IsAuthenticated == true)
    {
        // FIX: AdminPortal users don't have role claim yet
        // Allow authenticated users without roles (backward compatibility)
        // TODO: Remove this once AdminPortal users have proper role claims
        isAuthorized = true;
    }
    
    if (!isAuthorized)
    {
        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You do not have permission to view this ride's location");
    }

    var location = locationService.GetLatestLocation(rideId);
    if (location is null)
        return Results.NotFound(new { message = "No recent location data" });

    return Results.Ok(new LocationResponse
    {
        RideId = location.RideId,
        Latitude = location.Latitude,
        Longitude = location.Longitude,
        Timestamp = location.Timestamp,
        Heading = location.Heading,
        Speed = location.Speed,
        Accuracy = location.Accuracy,
        AgeSeconds = (DateTime.UtcNow - location.Timestamp).TotalSeconds,
        DriverUid = booking.AssignedDriverUid,
        DriverName = booking.AssignedDriverName
    });
})
.WithName("GetRideLocation")
.RequireAuthorization(); // Authenticated users only, then check permissions

// GET /passenger/rides/{rideId}/location - Get location for passenger's own ride
app.MapGet("/passenger/rides/{rideId}/location", async (
    string rideId,
    HttpContext context,
    ILocationService locationService,
    IBookingRepository repo) =>
{
    var booking = await repo.GetAsync(rideId);
    if (booking is null)
        return Results.NotFound(new { error = "Ride not found" });

    // PASSENGER AUTHORIZATION: Verify caller owns this booking
    // Check if user's email or sub claim matches the booker/passenger email
    var userSub = context.User.FindFirst("sub")?.Value;
    var userEmail = context.User.FindFirst("email")?.Value;
    
    bool isPassengerAuthorized = false;
    
    // Check booker email
    if (!string.IsNullOrEmpty(userEmail) && 
        !string.IsNullOrEmpty(booking.Draft?.Booker?.EmailAddress) &&
        userEmail.Equals(booking.Draft.Booker.EmailAddress, StringComparison.OrdinalIgnoreCase))
    {
        isPassengerAuthorized = true;
    }
    
    // Check passenger email (if different from booker)
    if (!isPassengerAuthorized && 
        !string.IsNullOrEmpty(userEmail) &&
        !string.IsNullOrEmpty(booking.Draft?.Passenger?.EmailAddress) &&
        userEmail.Equals(booking.Draft.Passenger.EmailAddress, StringComparison.OrdinalIgnoreCase))
    {
        isPassengerAuthorized = true;
    }
    
    // Future: Check PassengerId claim when implemented
    // if (!isPassengerAuthorized && userSub == booking.PassengerId) ...
    
    if (!isPassengerAuthorized)
    {
        return Results.Problem(
            statusCode: 403,
            title: "Forbidden",
            detail: "You can only view location for your own bookings");
    }

    var location = locationService.GetLatestLocation(rideId);
    if (location is null)
    {
        // Return a "tracking not started" response instead of 404
        return Results.Ok(new
        {
            rideId,
            trackingActive = false,
            message = "Driver has not started tracking yet",
            currentStatus = booking.CurrentRideStatus?.ToString() ?? "Scheduled"
        });
    }

    // FIX: Return anonymous object with trackingActive = true for PassengerApp
    return Results.Ok(new
    {
        rideId = location.RideId,
        trackingActive = true,  // ? ADD THIS - PassengerApp expects this field!
        latitude = location.Latitude,
        longitude = location.Longitude,
        timestamp = location.Timestamp,
        heading = location.Heading,
        speed = location.Speed,
        accuracy = location.Accuracy,
        ageSeconds = (DateTime.UtcNow - location.Timestamp).TotalSeconds,
        driverUid = booking.AssignedDriverUid,
        driverName = booking.AssignedDriverName
    });
})
.WithName("GetPassengerRideLocation")
.RequireAuthorization(); // Passengers authenticate via PassengerApp

// GET /admin/locations - Get all active driver locations (admin dashboard)
app.MapGet("/admin/locations", async (
    ILocationService locationService,
    IBookingRepository bookingRepo) =>
{
    var activeLocations = locationService.GetAllActiveLocations();
    var result = new List<ActiveRideLocationDto>();
    
    foreach (var entry in activeLocations)
    {
        var booking = await bookingRepo.GetAsync(entry.Update.RideId);
        if (booking is null) continue;
        
        result.Add(new ActiveRideLocationDto
        {
            RideId = entry.Update.RideId,
            DriverUid = entry.DriverUid,
            DriverName = booking.AssignedDriverName,
            PassengerName = booking.PassengerName,
            PickupLocation = booking.PickupLocation,
            DropoffLocation = booking.DropoffLocation,
            CurrentStatus = booking.CurrentRideStatus,
            Latitude = entry.Update.Latitude,
            Longitude = entry.Update.Longitude,
            Timestamp = entry.Update.Timestamp,
            Heading = entry.Update.Heading,
            Speed = entry.Update.Speed,
            AgeSeconds = entry.AgeSeconds
        });
    }
    
    return Results.Ok(new
    {
        count = result.Count,
        locations = result,
        timestamp = DateTime.UtcNow
    });
})
.WithName("GetAllActiveLocations")
.RequireAuthorization("StaffOnly"); // Phase 2: Both admin and dispatcher can view locations

// GET /admin/locations/rides - Get locations for specific ride IDs (batch query)
app.MapGet("/admin/locations/rides", async (
    [FromQuery] string rideIds,
    ILocationService locationService,
    IBookingRepository bookingRepo) =>
{
    if (string.IsNullOrWhiteSpace(rideIds))
        return Results.BadRequest(new { error = "rideIds query parameter is required" });
    
    var ids = rideIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var entries = locationService.GetLocations(ids);
    var result = new List<ActiveRideLocationDto>();
    
    foreach (var entry in entries)
    {
        var booking = await bookingRepo.GetAsync(entry.Update.RideId);
        if (booking is null) continue;
        
        result.Add(new ActiveRideLocationDto
        {
            RideId = entry.Update.RideId,
            DriverUid = entry.DriverUid,
            DriverName = booking.AssignedDriverName,
            PassengerName = booking.PassengerName,
            PickupLocation = booking.PickupLocation,
            DropoffLocation = booking.DropoffLocation,
            CurrentStatus = booking.CurrentRideStatus,
            Latitude = entry.Update.Latitude,
            Longitude = entry.Update.Longitude,
            Timestamp = entry.Update.Timestamp,
            Heading = entry.Update.Heading,
            Speed = entry.Update.Speed,
            AgeSeconds = entry.AgeSeconds
        });
    }
    
    return Results.Ok(new
    {
        requested = ids.Length,
        found = result.Count,
        locations = result,
        timestamp = DateTime.UtcNow
    });
})
.WithName("GetRideLocations")
.RequireAuthorization("StaffOnly"); // Phase 2: Both admin and dispatcher can query locations

// ===================================================================
// AFFILIATE & DRIVER MANAGEMENT ENDPOINTS
// ===================================================================

// GET /affiliates/list - List all affiliates with nested drivers
app.MapGet("/affiliates/list", async (IAffiliateRepository affiliateRepo, IDriverRepository driverRepo) =>
{
    var affiliates = await affiliateRepo.GetAllAsync();
    
    // Populate drivers for each affiliate from separate storage
    foreach (var affiliate in affiliates)
    {
        affiliate.Drivers = await driverRepo.GetByAffiliateIdAsync(affiliate.Id);
    }
    
    return Results.Ok(affiliates);
})
.WithName("ListAffiliates")
.RequireAuthorization();

// POST /affiliates - Create a new affiliate
app.MapPost("/affiliates", async (
    [FromBody] Affiliate affiliate,
    HttpContext context,
    IAffiliateRepository repo,
    AuditLogger auditLogger) =>
{
    if (string.IsNullOrWhiteSpace(affiliate.Name) || string.IsNullOrWhiteSpace(affiliate.Email))
        return Results.BadRequest(new { error = "Name and Email are required" });

    affiliate.Id = Guid.NewGuid().ToString("N");
    affiliate.Drivers = new List<Driver>(); // Initialize empty drivers list

    await repo.AddAsync(affiliate);

    // Phase 3: Audit affiliate creation
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.AffiliateCreated,
        "Affiliate",
        affiliate.Id,
        details: new {
            name = affiliate.Name,
            email = affiliate.Email,
            city = affiliate.City
        },
        httpContext: context);

    return Results.Created($"/affiliates/{affiliate.Id}", affiliate);
})
.WithName("CreateAffiliate")
.RequireAuthorization();

// GET /affiliates/{id} - Get affiliate by ID
app.MapGet("/affiliates/{id}", async (string id, IAffiliateRepository affiliateRepo, IDriverRepository driverRepo) =>
{
    var affiliate = await affiliateRepo.GetByIdAsync(id);
    if (affiliate is null) return Results.NotFound();
    
    // Populate drivers from separate storage
    affiliate.Drivers = await driverRepo.GetByAffiliateIdAsync(id);
    
    return Results.Ok(affiliate);
})
.WithName("GetAffiliate")
.RequireAuthorization();

// PUT /affiliates/{id} - Update affiliate
app.MapPut("/affiliates/{id}", async (
    string id,
    [FromBody] Affiliate affiliate,
    HttpContext context,
    IAffiliateRepository repo,
    AuditLogger auditLogger) =>
{
    var existing = await repo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    affiliate.Id = id; // Ensure ID matches
    await repo.UpdateAsync(affiliate);

    // Phase 3: Audit affiliate update
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.AffiliateUpdated,
        "Affiliate",
        id,
        details: new {
            name = affiliate.Name,
            email = affiliate.Email,
            city = affiliate.City
        },
        httpContext: context);

    return Results.Ok(affiliate);
})
.WithName("UpdateAffiliate")
.RequireAuthorization();

// DELETE /affiliates/{id} - Delete affiliate (cascade delete drivers)
app.MapDelete("/affiliates/{id}", async (
    string id,
    HttpContext context,
    IAffiliateRepository affiliateRepo,
    IDriverRepository driverRepo,
    AuditLogger auditLogger) =>
{
    var existing = await affiliateRepo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    // Get driver count before deletion for audit
    var drivers = await driverRepo.GetByAffiliateIdAsync(id);

    // Cascade delete all drivers belonging to this affiliate
    await driverRepo.DeleteByAffiliateIdAsync(id);
    
    await affiliateRepo.DeleteAsync(id);

    // Phase 3: Audit driver deletion
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.AffiliateDeleted,
        "Affiliate",
        id,
        details: new {
            name = existing.Name,
            email = existing.Email,
            city = existing.City,
            cascadeDeletedDrivers = drivers.Count
        },
        httpContext: context);

    return Results.Ok(new { message = "Affiliate and associated drivers deleted", id });
})
.WithName("DeleteAffiliate")
.RequireAuthorization();

// POST /affiliates/{affiliateId}/drivers - Create driver under affiliate
app.MapPost("/affiliates/{affiliateId}/drivers", async (
    string affiliateId,
    [FromBody] Driver driver,
    HttpContext context,
    IAffiliateRepository affiliateRepo,
    IDriverRepository driverRepo,
    AuditLogger auditLogger) =>
{
    var affiliate = await affiliateRepo.GetByIdAsync(affiliateId);
    if (affiliate is null)
        return Results.NotFound(new { error = "Affiliate not found" });

    if (string.IsNullOrWhiteSpace(driver.Name) || string.IsNullOrWhiteSpace(driver.Phone))
        return Results.BadRequest(new { error = "Name and Phone are required" });

    // Validate UserUid uniqueness if provided
    if (!string.IsNullOrWhiteSpace(driver.UserUid))
    {
        var isUnique = await driverRepo.IsUserUidUniqueAsync(driver.UserUid);
        if (!isUnique)
        {
            // Phase 3: Audit failed driver creation (duplicate UserUid)
            await auditLogger.LogAsync(
                context.User,
                AuditActions.DriverCreated,
                "Driver",
                details: new { 
                    driverName = driver.Name,
                    userUid = driver.UserUid,
                    affiliateId
                },
                httpContext: context,
                result: AuditLogResult.ValidationError,
                errorMessage: $"UserUid '{driver.UserUid}' already assigned to another driver");

            return Results.BadRequest(new { error = $"UserUid '{driver.UserUid}' is already assigned to another driver" });
        }
    }

    driver.Id = Guid.NewGuid().ToString("N");
    driver.AffiliateId = affiliateId;

    await driverRepo.AddAsync(driver);

    // Phase 3: Audit driver creation
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.DriverCreated,
        "Driver",
        driver.Id,
        details: new {
            name = driver.Name,
            userUid = driver.UserUid,
            affiliateId = affiliate.Id,
            affiliateName = affiliate.Name
        },
        httpContext: context);

    return Results.Created($"/drivers/{driver.Id}", driver);
})
.WithName("CreateDriver")
.RequireAuthorization();

// GET /drivers/list - List all drivers
app.MapGet("/drivers/list", async (IDriverRepository repo) =>
{
    var drivers = await repo.GetAllAsync();
    return Results.Ok(drivers);
})
.WithName("ListDrivers")
.RequireAuthorization();

// GET /drivers/by-uid/{userUid} - Get driver by AuthServer UserUid
app.MapGet("/drivers/by-uid/{userUid}", async (string userUid, IDriverRepository repo) =>
{
    var driver = await repo.GetByUserUidAsync(userUid);
    return driver is null ? Results.NotFound(new { error = "No driver found with this UserUid" }) : Results.Ok(driver);
})
.WithName("GetDriverByUserUid")
.RequireAuthorization();

// GET /drivers/{id} - Get driver by ID
app.MapGet("/drivers/{id}", async (string id, IDriverRepository repo) =>
{
    var driver = await repo.GetByIdAsync(id);
    return driver is null ? Results.NotFound() : Results.Ok(driver);
})
.WithName("GetDriver")
.RequireAuthorization();

// PUT /drivers/{id} - Update driver
app.MapPut("/drivers/{id}", async (
    string id,
    [FromBody] Driver driver,
    HttpContext context,
    IDriverRepository repo,
    AuditLogger auditLogger) =>
{
    var existing = await repo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    // Validate UserUid uniqueness if changed
    if (!string.IsNullOrWhiteSpace(driver.UserUid))
    {
        var isUnique = await repo.IsUserUidUniqueAsync(driver.UserUid, excludeDriverId: id);
        if (!isUnique)
        {
            // Phase 3: Audit failed driver update (duplicate UserUid)
            await auditLogger.LogAsync(
                context.User,
                AuditActions.DriverUpdated,
                "Driver",
                id,
                details: new { 
                    driverName = driver.Name,
                    userUid = driver.UserUid
                },
                httpContext: context,
                result: AuditLogResult.ValidationError,
                errorMessage: $"UserUid '{driver.UserUid}' already assigned to another driver");

            return Results.BadRequest(new { error = $"UserUid '{driver.UserUid}' is already assigned to another driver" });
        }
    }

    driver.Id = id;
    driver.AffiliateId = existing.AffiliateId; // Preserve affiliate association
    await repo.UpdateAsync(driver);

    // Phase 3: Audit driver update
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.DriverUpdated,
        "Driver",
        id,
        details: new {
            name = driver.Name,
            userUid = driver.UserUid,
            previousUserUid = existing.UserUid
        },
        httpContext: context);

    return Results.Ok(driver);
})
.WithName("UpdateDriver")
.RequireAuthorization();

// DELETE /drivers/{id} - Delete driver
app.MapDelete("/drivers/{id}", async (
    string id,
    HttpContext context,
    IDriverRepository repo,
    AuditLogger auditLogger) =>
{
    var existing = await repo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    await repo.DeleteAsync(id);

    // Phase 3: Audit driver deletion
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.DriverDeleted,
        "Driver",
        id,
        details: new {
            name = existing.Name,
            userUid = existing.UserUid,
            affiliateId = existing.AffiliateId
        },
        httpContext: context);

    return Results.Ok(new { message = "Driver deleted", id });
})
.WithName("DeleteDriver")
.RequireAuthorization();

// POST /bookings/{bookingId}/assign-driver - Assign driver to booking
app.MapPost("/bookings/{bookingId}/assign-driver", async (
    string bookingId,
    [FromBody] DriverAssignmentRequest request,
    HttpContext context,
    IBookingRepository bookingRepo,
    IDriverRepository driverRepo,
    IAffiliateRepository affiliateRepo,
    IEmailSender email,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("bookings");

    // Validate booking exists
    var booking = await bookingRepo.GetAsync(bookingId);
    if (booking is null)
        return Results.NotFound(new { error = "Booking not found" });

    // Validate driver exists
    var driver = await driverRepo.GetByIdAsync(request.DriverId);
    if (driver is null)
        return Results.NotFound(new { error = "Driver not found" });

    // CRITICAL: Validate driver has UserUid for driver app authentication
    if (string.IsNullOrWhiteSpace(driver.UserUid))
    {
        // Phase 3: Audit failed assignment (missing UserUid)
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.DriverAssigned,
            "Booking",
            bookingId,
            errorMessage: "Driver missing UserUid",
            details: new { driverId = driver.Id, driverName = driver.Name },
            httpContext: context);

        return Results.BadRequest(new 
        { 
            error = "Cannot assign driver without a UserUid. Please link the driver to an AuthServer user first.",
            driverId = driver.Id,
            driverName = driver.Name
        });
    }

    // Get affiliate for email notification
    var affiliate = await affiliateRepo.GetByIdAsync(driver.AffiliateId);
    if (affiliate is null)
        return Results.NotFound(new { error = "Affiliate not found" });

    // Update booking with driver assignment
    await bookingRepo.UpdateDriverAssignmentAsync(
        bookingId,
        driver.Id,
        driver.UserUid,
        driver.Name);

    // Phase 3: Audit successful driver assignment
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.DriverAssigned,
        "Booking",
        bookingId,
        details: new {
            driverId = driver.Id,
            driverName = driver.Name,
            driverUid = driver.UserUid,
            affiliateId = affiliate.Id,
            affiliateName = affiliate.Name,
            passengerName = booking.PassengerName,
            pickupDateTime = booking.PickupDateTime
        },
        httpContext: context);

    // Send email notification to affiliate
    try
    {
        await email.SendDriverAssignmentAsync(booking, driver, affiliate);
        log.LogInformation("Driver {DriverName} (UserUid: {UserUid}) assigned to booking {BookingId}, email sent to {AffiliateEmail}",
            driver.Name, driver.UserUid, bookingId, affiliate.Email);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to send driver assignment email for booking {BookingId}", bookingId);
        // Continue anyway - assignment is saved
    }

    // Return updated booking info
    var updatedBooking = await bookingRepo.GetAsync(bookingId);
    return Results.Ok(new
    {
        bookingId,
        assignedDriverId = driver.Id,
        assignedDriverName = driver.Name,
        assignedDriverUid = driver.UserUid,
        status = updatedBooking?.Status.ToString(),
        message = "Driver assigned successfully"
    });
})
.WithName("AssignDriver")
.RequireAuthorization("StaffOnly"); // Phase 2: Changed from generic auth to StaffOnly (both admin and dispatcher can assign)

// POST /dev/seed-affiliates - Seed test affiliates and drivers (DEV ONLY)
app.MapPost("/dev/seed-affiliates", async (
    HttpContext context,
    IAffiliateRepository affiliateRepo,
    IDriverRepository driverRepo,
    AuditLogger auditLogger) =>
{
    // Define affiliates (without embedded drivers - they're stored separately)
    var affiliates = new[]
    {
        new Affiliate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Chicago Limo Service",
            PointOfContact = "John Smith",
            Phone = "312-555-1234",
            Email = "dispatch@chicagolimo.com",
            StreetAddress = "123 Main St",
            City = "Chicago",
            State = "IL"
        },
        new Affiliate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Suburban Chauffeurs",
            PointOfContact = "Emily Davis",
            Phone = "847-555-9876",
            Email = "emily@suburbanchauffeurs.com",
            City = "Naperville",
            State = "IL"
        }
    };

    // Save affiliates
    foreach (var aff in affiliates)
        await affiliateRepo.AddAsync(aff);

    // Define drivers with UserUid values matching AuthServer test users
    var drivers = new[]
    {
        // Chicago Limo Service drivers
        new Driver 
        { 
            Id = Guid.NewGuid().ToString("N"), 
            AffiliateId = affiliates[0].Id, 
            Name = "Charlie Johnson", 
            Phone = "312-555-0001", 
            UserUid = "driver-001"  // Matches AuthServer test user "charlie"
        },
        new Driver 
        { 
            Id = Guid.NewGuid().ToString("N"), 
            AffiliateId = affiliates[0].Id, 
            Name = "Sarah Lee", 
            Phone = "312-555-0002", 
            UserUid = "driver-002" 
        },
        // Suburban Chauffeurs drivers
        new Driver 
        { 
            Id = Guid.NewGuid().ToString("N"), 
            AffiliateId = affiliates[1].Id, 
            Name = "Robert Brown", 
            Phone = "847-555-1000", 
            UserUid = "driver-003" 
        }
    };

    // Save drivers separately
    foreach (var driver in drivers)
        await driverRepo.AddAsync(driver);

    // Phase 3: Audit seed action
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.AffiliateCreated,
        "Affiliate",
        details: new { 
            affiliatesCount = affiliates.Length,
            driversCount = drivers.Length,
            action = "bulk_seed"
        },
        httpContext: context);

    return Results.Ok(new 
    { 
        affiliatesAdded = affiliates.Length, 
        driversAdded = drivers.Length,
        message = "Affiliates and drivers seeded successfully",
        note = "Driver 'Charlie Johnson' has UserUid 'driver-001' matching AuthServer test user 'charlie'"
    });
})
.WithName("SeedAffiliates")
.RequireAuthorization("AdminOnly"); // Phase 2: Only admins can seed data (security best practice)

// ===================================================================
// PHASE 2: OAUTH CREDENTIAL MANAGEMENT ENDPOINTS
// ===================================================================

// GET /api/admin/oauth - Get current OAuth credentials (secret masked)
app.MapGet("/api/admin/oauth", async (
    OAuthCredentialService oauthService,
    AuditLogger auditLogger,
    HttpContext context) =>
{
    var credentials = await oauthService.GetCredentialsAsync();
    
    // Phase 3: Audit credential view (sensitive operation)
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.OAuthCredentialsViewed,
        "OAuth",
        "default",
        httpContext: context);

    if (credentials == null)
    {
        return Results.Ok(new
        {
            configured = false,
            message = "OAuth credentials not configured. Use PUT /api/admin/oauth to set them."
        });
    }

    // Return masked response (never expose full secret)
    var response = new OAuthCredentialsResponseDto
    {
        ClientId = credentials.ClientId,
        ClientSecretMasked = MaskSecret(credentials.ClientSecret),
        LastUpdatedUtc = credentials.LastUpdatedUtc,
        LastUpdatedBy = credentials.LastUpdatedBy,
        Description = credentials.Description
    };

    return Results.Ok(new
    {
        configured = true,
        credentials = response
    });
})
.WithName("GetOAuthCredentials")
.RequireAuthorization("AdminOnly") // Phase 2: Only admins can view credentials
.WithTags("Admin", "OAuth");


// PUT /api/admin/oauth - Update OAuth credentials
app.MapPut("/api/admin/oauth", async (
    [FromBody] UpdateOAuthCredentialsRequest request,
    HttpContext context,
    OAuthCredentialService oauthService,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("oauth");
    
    // Validate request
    if (string.IsNullOrWhiteSpace(request.ClientId) || 
        string.IsNullOrWhiteSpace(request.ClientSecret))
    {
        // Phase 3: Audit failed update (validation error)
        await auditLogger.LogAsync(
            context.User,
            AuditActions.OAuthCredentialsUpdated,
            "OAuth",
            "default",
            httpContext: context,
            result: AuditLogResult.ValidationError,
            errorMessage: "ClientId and ClientSecret are required");

        return Results.BadRequest(new 
        { 
            error = "Both ClientId and ClientSecret are required" 
        });
    }

    // Get admin username for audit trail
    var adminUsername = context.User.FindFirst("sub")?.Value ?? "unknown";

    // Update credentials (encrypts before storage, invalidates cache)
    var credentials = new OAuthClientCredentials
    {
        Id = "default",
        ClientId = request.ClientId,
        ClientSecret = request.ClientSecret,
        Description = request.Description
    };

    await oauthService.UpdateCredentialsAsync(credentials, adminUsername);

    // Phase 3: Audit successful credential update (CRITICAL - never log actual secret!)
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.OAuthCredentialsUpdated,
        "OAuth",
        "default",
        details: new {
            clientId = credentials.ClientId,
            clientSecretMasked = MaskSecret(credentials.ClientSecret),
            description = request.Description,
            updatedBy = adminUsername
        },
        httpContext: context);

    log.LogInformation("OAuth credentials updated by admin {AdminUsername}", adminUsername);

    // Return success with masked secret
    return Results.Ok(new
    {
        message = "OAuth credentials updated successfully",
        clientId = credentials.ClientId,
        clientSecretMasked = MaskSecret(credentials.ClientSecret),
        updatedBy = adminUsername,
        updatedAt = DateTime.UtcNow,
        note = "Cache invalidated. New credentials will be used for all future API calls."
    });
})
.WithName("UpdateOAuthCredentials")
.RequireAuthorization("AdminOnly") // Phase 2: Only admins can update credentials
.WithTags("Admin", "OAuth");


// ===================================================================
// PHASE 3: AUDIT LOG ENDPOINTS (Admin-Only)
// ===================================================================

// GET /api/admin/audit-logs - Query audit logs with filtering and pagination
app.MapGet("/api/admin/audit-logs", async (
    HttpContext context,
    [FromQuery] string? userId,
    [FromQuery] string? entityType,
    [FromQuery] string? action,
    [FromQuery] DateTime? startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] int take,
    [FromQuery] int skip,
    IAuditLogRepository auditRepo) =>
{
    // Validate and default pagination
    if (take <= 0 || take > 1000) take = 100;
    if (skip < 0) skip = 0;

    var logs = await auditRepo.GetLogsAsync(
        userId: userId,
        entityType: entityType,
        action: action,
        startDate: startDate,
        endDate: endDate,
        take: take,
        skip: skip);

    var totalCount = await auditRepo.GetCountAsync(
        userId: userId,
        entityType: entityType,
        action: action,
        startDate: startDate,
        endDate: endDate);

    return Results.Ok(new
    {
        logs,
        pagination = new
        {
            total = totalCount,
            skip,
            take,
            returned = logs.Count
        },
        filters = new
        {
            userId,
            entityType,
            action,
            startDate,
            endDate
        }
    });
})
.WithName("GetAuditLogs")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Audit");

// GET /api/admin/audit-logs/{id} - Get specific audit log by ID
app.MapGet("/api/admin/audit-logs/{id}", async (
    string id,
    HttpContext context,
    IAuditLogRepository auditRepo,
    AuditLogger auditLogger) =>
{
    var log = await auditRepo.GetByIdAsync(id);
    
    if (log is null)
    {
        return Results.NotFound(new { error = "Audit log not found" });
    }

    // Audit the individual log view (sensitive operation)
    await auditLogger.LogSuccessAsync(
        context.User,
        "AuditLog.Viewed",
        "AuditLog",
        id,
        httpContext: context);

    return Results.Ok(log);
})
.WithName("GetAuditLogById")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Audit");

// GET /api/admin/audit-logs/stats - Get audit log statistics
app.MapGet("/api/admin/audit-logs/stats", async (
    HttpContext context,
    IAuditLogRepository auditRepo,
    AuditLogger auditLogger,
    CancellationToken ct) =>
{
    var stats = await auditRepo.GetStatsAsync(ct);

    // Audit the stats view
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.AuditLogStatsViewed,
        "AuditLog",
        details: new
        {
            count = stats.Count,
            oldestUtc = stats.OldestUtc,
            newestUtc = stats.NewestUtc
        },
        httpContext: context);

    return Results.Ok(new
    {
        count = stats.Count,
        oldestUtc = stats.OldestUtc,
        newestUtc = stats.NewestUtc
    });
})
.WithName("GetAuditLogStats")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Audit");


// DELETE /api/admin/audit-logs/cleanup - Clean up old audit logs
app.MapDelete("/api/admin/audit-logs/cleanup", async (
    [FromQuery] int retentionDays,
    HttpContext context,
    IAuditLogRepository auditRepo,
    AuditLogger auditLogger) =>
{
    // Default retention: 90 days
    if (retentionDays <= 0 || retentionDays > 365) retentionDays = 90;

    var deletedCount = await auditRepo.DeleteOldLogsAsync(retentionDays);

    // Log the cleanup action
    await auditLogger.LogSystemActionAsync(
        AuditActions.DataRetentionCleanup,
        "AuditLog",
        details: new { retentionDays, deletedCount });

    return Results.Ok(new
    {
        message = "Audit log cleanup completed",
        deletedCount,
        retentionDays,
        cutoffDate = DateTime.UtcNow.AddDays(-retentionDays)
    });
})
.WithName("CleanupAuditLogs")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Audit");

// POST /api/admin/audit-logs/clear - Clear all audit logs (requires confirmation)
app.MapPost("/api/admin/audit-logs/clear", async (
    [FromBody] ClearAuditLogsRequest request,
    HttpContext context,
    IAuditLogRepository auditRepo,
    AuditLogger auditLogger,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    var log = loggerFactory.CreateLogger("audit");
    var currentUserId = GetUserId(context.User);
    var username = context.User.FindFirst("sub")?.Value ?? "unknown";

    // Safety check: require exact confirmation phrase
    if (request.Confirm != "CLEAR")
    {
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.AuditLogCleared,
            "AuditLog",
            errorMessage: "Invalid confirmation phrase",
            details: new {
                providedConfirm = request.Confirm,
                expectedConfirm = "CLEAR"
            },
            httpContext: context);

        return Results.BadRequest(new
        {
            error = "Confirmation phrase must be exactly 'CLEAR' (case-sensitive)"
        });
    }

    // Clear all audit logs
    var deletedCount = await auditRepo.ClearAllAsync(ct);
    var clearedAtUtc = DateTime.UtcNow;

    log.LogWarning("Audit logs cleared by {Username} ({UserId}). Deleted count: {DeletedCount}",
        username, currentUserId, deletedCount);

    // Record one final audit event AFTER clearing
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.AuditLogCleared,
        "AuditLog",
        details: new {
            deletedCount,
            clearedAtUtc,
            clearedByUserId = currentUserId,
            clearedByUsername = username
        },
        httpContext: context);

    return Results.Ok(new
    {
        deletedCount,
        clearedAtUtc,
        clearedByUserId = currentUserId,
        clearedByUsername = username,
        message = "All audit logs have been cleared successfully"
    });
})
.WithName("ClearAuditLogs")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Audit");

// ===================================================================
// PHASE 3C: DATA RETENTION ENDPOINTS (Admin-Only)
// ===================================================================

// GET /api/admin/data-retention/policy - Get data retention policy
app.MapGet("/api/admin/data-retention/policy", (
    IDataRetentionService retentionService) =>
{
    var policy = retentionService.GetRetentionPolicy();

    return Results.Ok(new
    {
        policy,
        description = "Data retention policy for GDPR compliance",
        note = "Automated cleanup runs daily at 2 AM UTC"
    });
})
.WithName("GetDataRetentionPolicy")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "DataRetention");

// POST /api/admin/data-retention/cleanup - Manual data retention cleanup
app.MapPost("/api/admin/data-retention/cleanup", async (
    HttpContext context,
    IDataRetentionService retentionService,
    AuditLogger auditLogger) =>
{
    var startTime = DateTime.UtcNow;

    // Run all cleanup tasks
    var auditLogsDeleted = await retentionService.CleanupOldAuditLogsAsync();
    var bookingsAnonymized = await retentionService.AnonymizeOldBookingsAsync();
    var quotesDeleted = await retentionService.DeleteOldQuotesAsync();

    var duration = DateTime.UtcNow - startTime;

    // Audit log manual cleanup
    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.DataRetentionCleanup,
        "System",
        details: new {
            auditLogsDeleted,
            bookingsAnonymized,
            quotesDeleted,
            durationSeconds = duration.TotalSeconds
        },
        httpContext: context);

    return Results.Ok(new
    {
        message = "Data retention cleanup completed successfully",
        auditLogsDeleted,
        bookingsAnonymized,
        quotesDeleted,
        durationSeconds = duration.TotalSeconds,
        policy = retentionService.GetRetentionPolicy()
    });
})
.WithName("ManualDataRetentionCleanup")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "DataRetention");


// POST /api/admin/data-protection/test - Test data encryption/decryption
app.MapPost("/api/admin/data-protection/test", (
    ISensitiveDataProtector dataProtector) =>
{
    var testData = "Sensitive test data 12345";

    try
    {
        // Test encryption
        var encrypted = dataProtector.Protect(testData);
        var isProtected = dataProtector.IsProtected(encrypted);

        // Test decryption
        var decrypted = dataProtector.Unprotect(encrypted);

        var success = decrypted == testData;

        return Results.Ok(new
        {
            success,
            message = success ? "Data protection is working correctly" : "Data protection test failed",
            test = new
            {
                original = testData,
                encrypted = encrypted.Substring(0, Math.Min(50, encrypted.Length)) + "...",
                decrypted,
                isProtected,
                encryptedLength = encrypted.Length
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            statusCode: 500,
            title: "Data protection test failed",
            detail: ex.Message);
    }
})
.WithName("TestDataProtection")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "DataProtection");

// ===================================================================
// PHASE 3C: ADMIN PORTAL USER MANAGEMENT ENDPOINTS (Admin-Only)
// ===================================================================

const int MinTempPasswordLength = 10;
const int MaxUsersPageSize = 200;

// GET /users/list - List users (paged)
app.MapGet("/users/list", async (
    HttpContext context,
    [FromQuery] int take,
    [FromQuery] int skip,
    AuthServerUserManagementService userService,
    AuditLogger auditLogger,
    CancellationToken ct) =>
{
    if (take <= 0) take = 50;
    if (take > MaxUsersPageSize) take = MaxUsersPageSize;
    if (skip < 0) skip = 0;

    var bearerToken = GetBearerToken(context);
    var result = await userService.ListUsersAsync(take, skip, bearerToken, ct);

    if (!result.Success)
    {
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.UserListed,
            "User",
            errorMessage: result.ErrorMessage,
            httpContext: context);

        return Results.Json(
            new { error = result.ErrorMessage ?? "AuthServer request failed." },
            statusCode: (int)result.StatusCode);
    }

    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.UserListed,
        "User",
        details: new
        {
            take,
            skip,
            returned = result.Items.Count,
            total = result.Total
        },
        httpContext: context);

    return Results.Ok(result.Items);
})
.WithName("ListUsers")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Users");

// POST /users - Create user and assign roles
app.MapPost("/users", async (
    [FromBody] CreateUserRequest request,
    HttpContext context,
    AuthServerUserManagementService userService,
    AuditLogger auditLogger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { error = "Email is required." });
    }

    if (string.IsNullOrWhiteSpace(request.TempPassword) ||
        request.TempPassword.Length < MinTempPasswordLength)
    {
        return Results.BadRequest(new
        {
            error = $"tempPassword must be at least {MinTempPasswordLength} characters long."
        });
    }

    if (!AdminUserRoleValidator.TryNormalizeRoles(request.Roles, out var normalizedRoles, out var roleError))
    {
        return Results.BadRequest(new { error = roleError });
    }

    if (normalizedRoles.Contains("admin") && !context.User.IsInRole("admin"))
    {
        await auditLogger.LogForbiddenAsync(
            context.User,
            AuditActions.UserCreated,
            "User",
            httpContext: context);

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Insufficient permissions",
            detail: "Only admins can assign the Admin role.");
    }

    var bearerToken = GetBearerToken(context);
    var result = await userService.CreateUserAsync(request, normalizedRoles, bearerToken, ct);

    if (!result.Success)
    {
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.UserCreated,
            "User",
            errorMessage: result.ErrorMessage,
            details: new { request.Email, Roles = normalizedRoles },
            httpContext: context);

        if (result.StatusCode == HttpStatusCode.Conflict)
        {
            return Results.Conflict(new { error = result.ErrorMessage ?? "User already exists." });
        }

        if (result.StatusCode == HttpStatusCode.BadRequest)
        {
            return Results.BadRequest(new { error = result.ErrorMessage ?? "Invalid user request." });
        }

        return Results.Json(
            new { error = result.ErrorMessage ?? "AuthServer request failed." },
            statusCode: (int)result.StatusCode);
    }

    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.UserCreated,
        "User",
        result.Data?.UserId,
        details: new { request.Email, Roles = normalizedRoles },
        httpContext: context);

    return Results.Created($"/users/{result.Data?.UserId}", result.Data);
})
.WithName("CreateUser")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Users");

// PUT /users/{userId}/roles - Replace user roles
app.MapPut("/users/{userId}/roles", async (
    string userId,
    [FromBody] UpdateUserRolesRequest request,
    HttpContext context,
    AuthServerUserManagementService userService,
    AuditLogger auditLogger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { error = "UserId is required." });
    }

    if (!AdminUserRoleValidator.TryNormalizeRoles(request.Roles, out var normalizedRoles, out var roleError))
    {
        return Results.BadRequest(new { error = roleError });
    }

    if (normalizedRoles.Contains("admin") && !context.User.IsInRole("admin"))
    {
        await auditLogger.LogForbiddenAsync(
            context.User,
            AuditActions.UserRolesUpdated,
            "User",
            userId,
            httpContext: context);

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Insufficient permissions",
            detail: "Only admins can assign the Admin role.");
    }

    var bearerToken = GetBearerToken(context);
    var result = await userService.UpdateRolesAsync(userId, normalizedRoles, bearerToken, ct);

    if (!result.Success)
    {
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.UserRolesUpdated,
            "User",
            userId,
            errorMessage: result.ErrorMessage,
            details: new { Roles = normalizedRoles },
            httpContext: context);

        if (result.StatusCode == HttpStatusCode.BadRequest)
        {
            return Results.BadRequest(new { error = result.ErrorMessage ?? "Invalid role request." });
        }

        return Results.Json(
            new { error = result.ErrorMessage ?? "AuthServer request failed." },
            statusCode: (int)result.StatusCode);
    }

    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.UserRolesUpdated,
        "User",
        userId,
        details: new { Roles = normalizedRoles },
        httpContext: context);

    return Results.Ok(result.Data);
})
.WithName("UpdateUserRoles")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Users");

// PUT /users/{userId}/disable - Disable or enable a user
app.MapPut("/users/{userId}/disable", async (
    string userId,
    [FromBody] UpdateUserDisabledRequest request,
    HttpContext context,
    AuthServerUserManagementService userService,
    AuditLogger auditLogger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { error = "UserId is required." });
    }

    var bearerToken = GetBearerToken(context);
    
    // Call appropriate AuthServer endpoint based on isDisabled flag
    var result = request.IsDisabled
        ? await userService.DisableUserAsync(userId, bearerToken, ct)
        : await userService.EnableUserAsync(userId, bearerToken, ct);

    if (!result.Success)
    {
        await auditLogger.LogFailureAsync(
            context.User,
            AuditActions.UserDisabledUpdated,
            "User",
            userId,
            errorMessage: result.ErrorMessage,
            details: new { request.IsDisabled },
            httpContext: context);

        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.NotFound(new { error = result.ErrorMessage ?? "User not found." });
        }

        if (result.StatusCode == HttpStatusCode.BadRequest)
        {
            return Results.BadRequest(new { error = result.ErrorMessage ?? "Invalid disable request." });
        }

        return Results.Json(
            new { error = result.ErrorMessage ?? "AuthServer request failed." },
            statusCode: (int)result.StatusCode);
    }

    await auditLogger.LogSuccessAsync(
        context.User,
        AuditActions.UserDisabledUpdated,
        "User",
        userId,
        details: new { IsDisabled = request.IsDisabled },
        httpContext: context);

    return Results.Ok(result.Data);
})
.WithName("DisableUser")
.RequireAuthorization("AdminOnly")
.WithTags("Admin", "Users");

// ===================================================================
// STARTUP: EMAIL CONFIGURATION VALIDATION
// ===================================================================
{
    var emailFrom = builder.Configuration["Email:Smtp:From"];
    var emailHost = builder.Configuration["Email:Smtp:Host"];
    var emailOverrideEnabled = builder.Configuration["Email:OverrideRecipients:Enabled"];
    var emailOverrideAddr = builder.Configuration["Email:OverrideRecipients:Address"];

    Console.WriteLine($"[Startup] Environment: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"[Startup] Email Mode: {builder.Configuration["Email:Mode"]}");
    Console.WriteLine($"[Startup] Email:Smtp:From         = {(string.IsNullOrWhiteSpace(emailFrom) ? "*** NOT SET ***" : "configured")}");
    Console.WriteLine($"[Startup] Email:Smtp:Host         = {(string.IsNullOrWhiteSpace(emailHost) ? "*** NOT SET ***" : emailHost)}");
    Console.WriteLine($"[Startup] Email:Smtp:Port         = {builder.Configuration["Email:Smtp:Port"] ?? "(default)"}");
    Console.WriteLine($"[Startup] Email:Smtp:UseStartTls  = {builder.Configuration["Email:Smtp:UseStartTls"] ?? "(default)"}");
    Console.WriteLine($"[Startup] Email:OverrideRecipients:Enabled = {emailOverrideEnabled ?? "false"}");
    Console.WriteLine($"[Startup] Email:OverrideRecipients:Address = {(string.IsNullOrWhiteSpace(emailOverrideAddr) ? "*** NOT SET ***" : emailOverrideAddr)}");
}

app.Run();

// ===================================================================
// REQUEST/RESPONSE DTOs
// ===================================================================

/// <summary>
/// Request DTO for clearing all audit logs.
/// Alpha: Requires confirmation phrase for safety.
/// </summary>
/// <param name="Confirm">Must be exactly "CLEAR" (case-sensitive)</param>
public record ClearAuditLogsRequest(string Confirm);

// ===================================================================
// END OF FILE
// ===================================================================
