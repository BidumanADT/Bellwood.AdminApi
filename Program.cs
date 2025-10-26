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

// Optional API key protection (same as you had)
string? apiKey = builder.Configuration["Email:ApiKey"];
static IResult UnauthorizedIfKeyMissing(HttpRequest req, string? configuredKey)
{
    if (string.IsNullOrWhiteSpace(configuredKey)) return Results.Ok();
    if (!req.Headers.TryGetValue("X-Admin-ApiKey", out var provided) || provided != configuredKey)
        return Results.Unauthorized();
    return Results.Ok();
}

// ---- Seed: create real, persistent records via repository ----
app.MapPost("/seed-test-quotes", async (IQuoteRepository repo) =>
{
    var now = DateTime.UtcNow;
    var samples = new[]
    {
        new QuoteRecord
        {
            BookerName = "Alice Morgan",
            PassengerName = "Taylor Reed",
            VehicleClass = "Sedan",
            PickupLocation = "Langham Hotel, Chicago",
            DropoffLocation = "O'Hare International Airport",
            PickupDateTime = now.AddDays(1),
            Status = QuoteStatus.Submitted
        },
        new QuoteRecord
        {
            BookerName = "Chris Bailey",
            PassengerName = "Jordan Chen",
            VehicleClass = "SUV",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Downtown Chicago",
            PickupDateTime = now.AddDays(2),
            Status = QuoteStatus.InReview   // maps to Pending in the app
        },
        new QuoteRecord
        {
            BookerName = "Lisa Gomez",
            PassengerName = "Derek James",
            VehicleClass = "S-Class",
            PickupLocation = "Midway Airport",
            DropoffLocation = "The Langham Hotel",
            PickupDateTime = now.AddDays(3),
            Status = QuoteStatus.Priced
        },
        new QuoteRecord
        {
            BookerName = "Evan Ross",
            PassengerName = "Mia Park",
            VehicleClass = "Sprinter",
            PickupLocation = "Signature FBO (ORD)",
            DropoffLocation = "Indiana Dunes State Park",
            PickupDateTime = now.AddDays(4),
            Status = QuoteStatus.Rejected
        },
        new QuoteRecord
        {
            BookerName = "Sarah Larkin",
            PassengerName = "James Miller",
            VehicleClass = "SUV",
            PickupLocation = "O'Hare FBO",
            DropoffLocation = "Langham Hotel",
            PickupDateTime = now.AddDays(5),
            Status = QuoteStatus.Closed
        }
    };

    // Persist each one (your interface doesn’t have AddMany)
    foreach (var s in samples)
        await repo.AddAsync(s);

    return Results.Ok(new { added = samples.Length });
});

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
