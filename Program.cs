using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;
using BellwoodGlobal.Mobile.Models;
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

// API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// API key validation helper
string? apiKey = builder.Configuration["Email:ApiKey"];
static IResult UnauthorizedIfKeyMissing(HttpRequest req, string? configuredKey)
{
    if (string.IsNullOrWhiteSpace(configuredKey)) return Results.Ok();
    if (!req.Headers.TryGetValue("X-Admin-ApiKey", out var provided) || provided != configuredKey)
        return Results.Unauthorized();
    return Results.Ok();
}

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
.WithName("SeedQuotes");

// POST /quotes - Submit a new quote request
app.MapPost("/quotes", async (
    [FromBody] QuoteDraft draft,
    HttpRequest req,
    IEmailSender email,
    IQuoteRepository repo,
    ILoggerFactory loggerFactory) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

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
.WithName("SubmitQuote");

// GET /quotes/list - List recent quotes (paginated)
app.MapGet("/quotes/list", async ([FromQuery] int take, HttpRequest req, IQuoteRepository repo) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

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
.WithName("ListQuotes");

// GET /quotes/{id} - Get detailed quote by ID
app.MapGet("/quotes/{id}", async (string id, HttpRequest req, IQuoteRepository repo) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

    var rec = await repo.GetAsync(id);
    return rec is null ? Results.NotFound() : Results.Ok(rec);
})
.WithName("GetQuote");

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
            Status = BookingStatus.Requested,
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
            Status = BookingStatus.Confirmed,
            BookerName = "Chris Bailey",
            PassengerName = "Jordan Chen",
            VehicleClass = "Sedan",
            PickupLocation = "Langham Hotel",
            DropoffLocation = "Midway Airport",
            PickupDateTime = now.AddDays(1),
            Draft = new BellwoodGlobal.Mobile.Models.QuoteDraft {
                Booker = new() { FirstName="Chris", LastName="Bailey" },
                Passenger = new() { FirstName="Jordan", LastName="Chen" },
                VehicleClass = "Sedan",
                PickupDateTime = now.AddDays(1),
                PickupLocation = "Langham Hotel",
                PickupStyle = BellwoodGlobal.Mobile.Models.PickupStyle.Curbside,
                DropoffLocation = "Midway Airport",
                PassengerCount = 1
            }
        },
        new BookingRecord {
            CreatedUtc = now.AddDays(-1),
            Status = BookingStatus.Completed,
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
.WithName("SeedBookings");

// POST /bookings - Submit a new booking request
app.MapPost("/bookings", async (
    [FromBody] QuoteDraft draft,
    HttpRequest req,
    IEmailSender email,
    IBookingRepository repo,
    ILoggerFactory loggerFactory) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

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
.WithName("SubmitBooking");

// GET /bookings/list - List recent bookings (paginated)
app.MapGet("/bookings/list", async ([FromQuery] int take, HttpRequest req, IBookingRepository repo) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

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
.WithName("ListBookings");

// GET /bookings/{id} - Get detailed booking by ID
app.MapGet("/bookings/{id}", async (string id, HttpRequest req, IBookingRepository repo) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

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
.WithName("GetBooking");

// ===================================================================
// CANCEL ENDPOINTS
// ===================================================================

// POST /bookings/{id}/cancel - Cancel a booking request
app.MapPost("/bookings/{id}/cancel", async (
    string id,
    HttpRequest req,
    IBookingRepository repo,
    IEmailSender email,
    ILoggerFactory loggerFactory) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

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
.WithName("CancelBooking");

// ===================================================================
// APPLICATION START
// ===================================================================

app.Run();