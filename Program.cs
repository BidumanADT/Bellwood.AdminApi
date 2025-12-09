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
builder.Services.AddSingleton<IAffiliateRepository, FileAffiliateRepository>();
builder.Services.AddSingleton<IDriverRepository, FileDriverRepository>();

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
            }
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
            }
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
            }
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
                Passenger = new() { FirstName="Emma", LastName="Watson", PhoneNumber = "312-555-8888", EmailAddress = "emma.watson@example.com" },
                VehicleClass = "S-Class",
                PickupDateTime = now.AddHours(48),
                PickupLocation = "O'Hare FBO",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.MeetAndGreet,
                PickupSignText = "WATSON / Bellwood",
                DropoffLocation = "Peninsula Hotel, Chicago",
                PassengerCount = 2, CheckedBags = 2, CarryOnBags = 1
            }
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
            }
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
            }
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
            }
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
        r.PickupDateTime,
        AssignedDriverId = r.AssignedDriverId,
        AssignedDriverUid = r.AssignedDriverUid,
        AssignedDriverName = r.AssignedDriverName ?? "Unassigned"
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
        rec.Draft,
        AssignedDriverId = rec.AssignedDriverId,
        AssignedDriverUid = rec.AssignedDriverUid,
        AssignedDriverName = rec.AssignedDriverName ?? "Unassigned"
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
    IAffiliateRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(affiliate.Name) || string.IsNullOrWhiteSpace(affiliate.Email))
        return Results.BadRequest(new { error = "Name and Email are required" });

    affiliate.Id = Guid.NewGuid().ToString("N");
    affiliate.Drivers = new List<Driver>(); // Initialize empty drivers list

    await repo.AddAsync(affiliate);
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
    IAffiliateRepository repo) =>
{
    var existing = await repo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    affiliate.Id = id; // Ensure ID matches
    await repo.UpdateAsync(affiliate);
    return Results.Ok(affiliate);
})
.WithName("UpdateAffiliate")
.RequireAuthorization();

// DELETE /affiliates/{id} - Delete affiliate (cascade delete drivers)
app.MapDelete("/affiliates/{id}", async (
    string id,
    IAffiliateRepository affiliateRepo,
    IDriverRepository driverRepo) =>
{
    var existing = await affiliateRepo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    // Cascade delete all drivers belonging to this affiliate
    await driverRepo.DeleteByAffiliateIdAsync(id);
    
    await affiliateRepo.DeleteAsync(id);
    return Results.Ok(new { message = "Affiliate and associated drivers deleted", id });
})
.WithName("DeleteAffiliate")
.RequireAuthorization();

// POST /affiliates/{affiliateId}/drivers - Create driver under affiliate
app.MapPost("/affiliates/{affiliateId}/drivers", async (
    string affiliateId,
    [FromBody] Driver driver,
    IAffiliateRepository affiliateRepo,
    IDriverRepository driverRepo) =>
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
            return Results.BadRequest(new { error = $"UserUid '{driver.UserUid}' is already assigned to another driver" });
    }

    driver.Id = Guid.NewGuid().ToString("N");
    driver.AffiliateId = affiliateId;

    await driverRepo.AddAsync(driver);
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
    IDriverRepository repo) =>
{
    var existing = await repo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    // Validate UserUid uniqueness if changed
    if (!string.IsNullOrWhiteSpace(driver.UserUid))
    {
        var isUnique = await repo.IsUserUidUniqueAsync(driver.UserUid, excludeDriverId: id);
        if (!isUnique)
            return Results.BadRequest(new { error = $"UserUid '{driver.UserUid}' is already assigned to another driver" });
    }

    driver.Id = id;
    driver.AffiliateId = existing.AffiliateId; // Preserve affiliate association
    await repo.UpdateAsync(driver);
    return Results.Ok(driver);
})
.WithName("UpdateDriver")
.RequireAuthorization();

// DELETE /drivers/{id} - Delete driver
app.MapDelete("/drivers/{id}", async (
    string id,
    IDriverRepository repo) =>
{
    var existing = await repo.GetByIdAsync(id);
    if (existing is null)
        return Results.NotFound();

    await repo.DeleteAsync(id);
    return Results.Ok(new { message = "Driver deleted", id });
})
.WithName("DeleteDriver")
.RequireAuthorization();

// POST /bookings/{bookingId}/assign-driver - Assign driver to booking
app.MapPost("/bookings/{bookingId}/assign-driver", async (
    string bookingId,
    [FromBody] DriverAssignmentRequest request,
    IBookingRepository bookingRepo,
    IDriverRepository driverRepo,
    IAffiliateRepository affiliateRepo,
    IEmailSender email,
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
.RequireAuthorization();

// POST /dev/seed-affiliates - Seed test affiliates and drivers (DEV ONLY)
app.MapPost("/dev/seed-affiliates", async (
    IAffiliateRepository affiliateRepo,
    IDriverRepository driverRepo) =>
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

    return Results.Ok(new 
    { 
        affiliatesAdded = affiliates.Length, 
        driversAdded = drivers.Length,
        message = "Affiliates and drivers seeded successfully",
        note = "Driver 'Charlie Johnson' has UserUid 'driver-001' matching AuthServer test user 'charlie'"
    });
})
.WithName("SeedAffiliates");

// ===================================================================
// APPLICATION START
// ===================================================================

app.Run();
