using System.Text;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;
using BellwoodGlobal.Mobile.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ===================================================================
// SERVICE REGISTRATION
// ===================================================================

// Email configuration and sender
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// Repository services (file-backed storage)
builder.Services.AddSingleton<IQuoteRepository, FileQuoteRepository>();
builder.Services.AddSingleton<IBookingRepository, FileBookingRepository>();

// Location tracking service (in-memory)
builder.Services.AddSingleton<ILocationService, InMemoryLocationService>();

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
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Register authorization with driver policy:
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DriverOnly", policy =>
        policy.RequireClaim("role", "driver"));
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

app.UseAuthentication();
app.UseAuthorization();

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

// ===================================================================
// QUOTE ENDPOINTS
// ===================================================================

// POST /quotes/seed - Seed sample quotes (DEV ONLY)
app.MapPost("/quotes/seed", async (IQuoteRepository repo) =>
{
    var now = DateTime.UtcNow;

    var samples = new[]
    {
        new QuoteRecord {
            CreatedUtc = now,
            Status = QuoteStatus.Submitted,
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
            }
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
            }
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
            }
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
            }
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
            }
        }
    };

    foreach (var r in samples)
        await repo.AddAsync(r);

    return Results.Ok(new { added = samples.Length });
})
.WithName("SeedQuotes")
.RequireAuthorization();

// POST /quotes - Submit a new quote request
app.MapPost("/quotes", async (
    [FromBody] QuoteDraft draft,
    IEmailSender email,
    IQuoteRepository repo,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");

    if (draft is null || string.IsNullOrWhiteSpace(draft.PickupLocation))
        return Results.BadRequest(new { error = "Invalid payload" });

    var rec = new QuoteRecord
    {
        BookerName = draft.Booker?.ToString() ?? "",
        PassengerName = draft.Passenger?.ToString() ?? "",
        VehicleClass = draft.VehicleClass,
        PickupLocation = draft.PickupLocation,
        DropoffLocation = draft.DropoffLocation,
        PickupDateTime = draft.PickupDateTime,
        Draft = draft
    };

    await repo.AddAsync(rec);

    try
    {
        await email.SendQuoteAsync(draft, rec.Id);
        log.LogInformation("Quote {Id} submitted for {Passenger}", rec.Id, rec.PassengerName);
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
app.MapGet("/quotes/list", async ([FromQuery] int take, IQuoteRepository repo) =>
{
    take = (take <= 0 || take > 200) ? 50 : take;
    var rows = await repo.ListAsync(take);

    var list = rows.Select(r => new {
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
.RequireAuthorization();

// GET /quotes/{id} - Get detailed quote by ID
app.MapGet("/quotes/{id}", async (string id, IQuoteRepository repo) =>
{
    var rec = await repo.GetAsync(id);
    return rec is null ? Results.NotFound() : Results.Ok(rec);
})
.WithName("GetQuote")
.RequireAuthorization();

// ===================================================================
// BOOKING ENDPOINTS
// ===================================================================

// POST /bookings/seed - Seed sample bookings (DEV ONLY)
app.MapPost("/bookings/seed", async (IBookingRepository repo) =>
{
    var now = DateTime.UtcNow;

    var samples = new[]
    {
        new BookingRecord {
            CreatedUtc = now.AddMinutes(-5),
            Status = BookingStatus.Scheduled,
            AssignedDriverUid = "driver-001", // Test driver UID
            CurrentRideStatus = RideStatus.Scheduled,
            BookerName = "Alice Morgan",
            PassengerName = "Taylor Reed",
            VehicleClass = "SUV",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Downtown Chicago",
            PickupDateTime = now.AddHours(3),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName = "Alice", LastName = "Morgan", PhoneNumber = "312-555-7777", EmailAddress = "alice.morgan@example.com" },
                Passenger = new() { FirstName = "Taylor", LastName = "Reed", PhoneNumber = "773-555-1122", EmailAddress = "taylor.reed@example.com" },
                VehicleClass = "SUV",
                PickupDateTime = now.AddHours(3),
                PickupLocation = "O'Hare FBO",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.MeetAndGreet,
                PickupSignText = "REED / Bellwood",
                DropoffLocation = "Downtown Chicago",
                PassengerCount = 2, CheckedBags = 2, CarryOnBags = 2
            }
        },
        new BookingRecord {
            CreatedUtc = now.AddHours(-2),
            Status = BookingStatus.Scheduled,
            AssignedDriverUid = "driver-001", // Same driver
            CurrentRideStatus = RideStatus.Scheduled,
            BookerName = "Chris Bailey",
            PassengerName = "Jordan Chen",
            VehicleClass = "Sedan",
            PickupLocation = "Langham Hotel",
            DropoffLocation = "Midway Airport",
            PickupDateTime = now.AddHours(5),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Chris", LastName="Bailey" },
                Passenger = new() { FirstName="Jordan", LastName="Chen" },
                VehicleClass = "Sedan",
                PickupDateTime = now.AddHours(5),
                PickupLocation = "Langham Hotel",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Midway Airport",
                PassengerCount = 1
            }
        },
        new BookingRecord {
            CreatedUtc = now.AddDays(-1),
            Status = BookingStatus.Completed,
            AssignedDriverUid = "driver-002", // Different driver
            CurrentRideStatus = RideStatus.Completed,
            BookerName = "Lisa Gomez",
            PassengerName = "Derek James",
            VehicleClass = "S-Class",
            PickupLocation = "O'Hare International",
            DropoffLocation = "Navy Pier",
            PickupDateTime = now.AddDays(-1).AddHours(2),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Lisa", LastName="Gomez" },
                Passenger = new() { FirstName="Derek", LastName="James" },
                VehicleClass = "S-Class",
                PickupDateTime = now.AddDays(-1).AddHours(2),
                PickupLocation = "O'Hare International",
                DropoffLocation = "Navy Pier",
                PassengerCount = 2
            }
        }
    };

    foreach (var r in samples)
        await repo.AddAsync(r);

    return Results.Ok(new { added = samples.Length });
})
.WithName("SeedBookings")
.RequireAuthorization();

// POST /bookings - Submit a new booking request
app.MapPost("/bookings", async (
    [FromBody] QuoteDraft draft,
    IEmailSender email,
    IBookingRepository repo,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("bookings");

    if (draft is null || string.IsNullOrWhiteSpace(draft.PickupLocation))
        return Results.BadRequest(new { error = "Invalid payload" });

    var rec = new BookingRecord
    {
        BookerName = draft.Booker?.ToString() ?? "",
        PassengerName = draft.Passenger?.ToString() ?? "",
        VehicleClass = draft.VehicleClass,
        PickupLocation = draft.PickupLocation,
        DropoffLocation = draft.DropoffLocation,
        PickupDateTime = draft.PickupDateTime,
        Draft = draft
    };

    await repo.AddAsync(rec);

    try
    {
        await email.SendBookingAsync(draft, rec.Id);
        log.LogInformation("Booking {Id} submitted for {Passenger}", rec.Id, rec.PassengerName);
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
app.MapGet("/bookings/list", async ([FromQuery] int take, IBookingRepository repo) =>
{
    take = (take <= 0 || take > 200) ? 50 : take;
    var rows = await repo.ListAsync(take);

    var list = rows.Select(r => new {
        r.Id,
        r.CreatedUtc,
        Status = r.Status.ToString(),
        r.BookerName,
        r.PassengerName,
        r.VehicleClass,
        r.PickupLocation,
        r.DropoffLocation,
        r.PickupDateTime
    });

    return Results.Ok(list);
})
.WithName("ListBookings")
.RequireAuthorization();

// GET /bookings/{id} - Get detailed booking by ID
app.MapGet("/bookings/{id}", async (string id, IBookingRepository repo) =>
{
    var rec = await repo.GetAsync(id);
    if (rec is null) return Results.NotFound();

    return Results.Ok(new
    {
        rec.Id,
        rec.CreatedUtc,
        Status = rec.Status.ToString(),
        rec.BookerName,
        rec.PassengerName,
        rec.VehicleClass,
        rec.PickupLocation,
        rec.DropoffLocation,
        rec.PickupDateTime,
        rec.Draft
    });
})
.WithName("GetBooking")
.RequireAuthorization();

// ===================================================================
// CANCEL ENDPOINTS
// ===================================================================

// POST /bookings/{id}/cancel - Cancel a booking request
app.MapPost("/bookings/{id}/cancel", async (
    string id,
    IBookingRepository repo,
    IEmailSender email,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("bookings");

    // Find booking
    var booking = await repo.GetAsync(id);
    if (booking is null)
        return Results.NotFound(new { error = "Booking not found" });

    // Only allow cancellation if status is Requested or Confirmed
    if (booking.Status != BookingStatus.Requested && booking.Status != BookingStatus.Confirmed)
    {
        return Results.BadRequest(new { error = $"Cannot cancel booking with status: {booking.Status}" });
    }

    // Update status
    booking.Status = BookingStatus.Cancelled;
    booking.CancelledAt = DateTime.UtcNow;
    await repo.UpdateStatusAsync(id, BookingStatus.Cancelled);

    // Send cancellation email
    try
    {
        await email.SendBookingCancellationAsync(booking.Draft, id, booking.BookerName);
        log.LogInformation("Booking {Id} cancelled by {Booker}", id, booking.BookerName);
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

// GET /driver/rides/today - Get driver's rides for the next 24 hours
app.MapGet("/driver/rides/today", async (HttpContext context, IBookingRepository repo) =>
{
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Results.Unauthorized();

    var now = DateTime.UtcNow;
    var tomorrow = now.AddHours(24);

    var allBookings = await repo.ListAsync(200); // Get enough to filter
    var driverRides = allBookings
        .Where(b => b.AssignedDriverUid == driverUid
                    && b.PickupDateTime >= now
                    && b.PickupDateTime <= tomorrow
                    && b.CurrentRideStatus != RideStatus.Completed
                    && b.CurrentRideStatus != RideStatus.Cancelled)
        .OrderBy(b => b.PickupDateTime)
        .Select(b => new DriverRideListItemDto
        {
            Id = b.Id,
            PickupDateTime = b.PickupDateTime,
            PickupLocation = b.PickupLocation,
            DropoffLocation = b.DropoffLocation,
            PassengerName = b.PassengerName,
            PassengerPhone = b.Draft.Passenger?.PhoneNumber ?? "N/A",
            Status = b.CurrentRideStatus ?? RideStatus.Scheduled
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

    var detail = new DriverRideDetailDto
    {
        Id = booking.Id,
        PickupDateTime = booking.PickupDateTime,
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
        return Results.BadRequest(new
        {
            error = $"Invalid status transition from {currentStatus} to {request.NewStatus}"
        });
    }

    // Update status
    booking.CurrentRideStatus = request.NewStatus;

    // Sync with BookingStatus
    if (request.NewStatus == RideStatus.PassengerOnboard)
        booking.Status = BookingStatus.InProgress;
    else if (request.NewStatus == RideStatus.Completed)
        booking.Status = BookingStatus.Completed;
    else if (request.NewStatus == RideStatus.Cancelled)
        booking.Status = BookingStatus.Cancelled;

    await repo.UpdateStatusAsync(id, booking.Status);

    log.LogInformation("Driver {Uid} updated ride {Id} status to {Status}",
        driverUid, id, request.NewStatus);

    return Results.Ok(new
    {
        message = "Status updated successfully",
        rideId = id,
        newStatus = request.NewStatus.ToString()
    });
})
.WithName("UpdateRideStatus")
.RequireAuthorization("DriverOnly");

// POST /driver/location/update - Receive location updates (rate-limited)
app.MapPost("/driver/location/update", (
    [FromBody] LocationUpdate update,
    HttpContext context,
    ILocationService locationService,
    IBookingRepository repo,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("driver");
    var driverUid = GetDriverUid(context);
    if (string.IsNullOrEmpty(driverUid))
        return Task.FromResult(Results.Unauthorized());

    // Verify ride exists and belongs to driver
    return repo.GetAsync(update.RideId).ContinueWith<IResult>(task =>
    {
        var booking = task.Result;
        if (booking is null || booking.AssignedDriverUid != driverUid)
            return Results.NotFound(new { error = "Ride not found" });

        // Only accept updates for active rides
        var activeStatuses = new[] { RideStatus.OnRoute, RideStatus.Arrived, RideStatus.PassengerOnboard };
        if (!booking.CurrentRideStatus.HasValue || !activeStatuses.Contains(booking.CurrentRideStatus.Value))
        {
            return Results.BadRequest(new { error = "Location tracking not active for this ride" });
        }

        // Try to store location (rate-limited)
        if (!locationService.TryUpdateLocation(driverUid, update))
        {
            return Results.StatusCode(429); // Too Many Requests
        }

        log.LogDebug("Location updated for ride {RideId} by driver {Uid}", update.RideId, driverUid);

        return Results.Ok(new { message = "Location updated" });
    });
})
.WithName("UpdateDriverLocation")
.RequireAuthorization("DriverOnly");

// GET /driver/location/{rideId} - Get latest location for a ride (admin/passenger use)
app.MapGet("/driver/location/{rideId}", async (
    string rideId,
    ILocationService locationService,
    IBookingRepository repo) =>
{
    var booking = await repo.GetAsync(rideId);
    if (booking is null)
        return Results.NotFound();

    var location = locationService.GetLatestLocation(rideId);
    if (location is null)
        return Results.NotFound(new { message = "No recent location data" });

    return Results.Ok(new
    {
        rideId = location.RideId,
        latitude = location.Latitude,
        longitude = location.Longitude,
        timestamp = location.Timestamp,
        ageSeconds = (DateTime.UtcNow - location.Timestamp).TotalSeconds
    });
})
.WithName("GetRideLocation")
.RequireAuthorization(); // Any authenticated user can track

// ===================================================================
// APPLICATION START
// ===================================================================

app.Run();
