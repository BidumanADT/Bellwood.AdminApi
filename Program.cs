using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;
using BellwoodGlobal.Mobile.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Email options + sender
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for dev
builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRouting();

// Make enums show up as strings in API responses
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.WriteIndented = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Repository (file-backed)
builder.Services.AddSingleton<IQuoteRepository, FileQuoteRepository>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Optional API key protection 
string? apiKey = builder.Configuration["Email:ApiKey"];
static IResult UnauthorizedIfKeyMissing(HttpRequest req, string? configuredKey)
{
    if (string.IsNullOrWhiteSpace(configuredKey)) return Results.Ok();
    if (!req.Headers.TryGetValue("X-Admin-ApiKey", out var provided) || provided != configuredKey)
        return Results.Unauthorized();
    return Results.Ok();
}

// --- DEV ONLY: seed a few quotes into the repository ---
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
            Status = QuoteStatus.InReview, // should show as "Pending"
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


// ---- Submit Quote: remove IQuoteStore; use repository only ----
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
        // Id/CreatedUtc default in model; OK if you already set those defaults there
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

// ---- List Quotes (unchanged, just repo) ----
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

// ---- Get Quote Detail (unchanged, just repo) ----
app.MapGet("/quotes/{id}", async (string id, HttpRequest req, IQuoteRepository repo) =>
{
    var auth = UnauthorizedIfKeyMissing(req, apiKey);
    if (auth is IStatusCodeHttpResult sc && sc.StatusCode == 401) return auth;

    var rec = await repo.GetAsync(id);
    return rec is null ? Results.NotFound() : Results.Ok(rec);
})
.WithName("GetQuote");

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
