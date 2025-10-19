using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;
using BellwoodGlobal.Mobile.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Bind Email options from configuration
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow dev clients
builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRouting();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton<IQuoteRepository, FileQuoteRepository>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Optional API key protection (set Email:ApiKey in config and pass X-Admin-ApiKey from the app)
string? apiKey = builder.Configuration["Email:ApiKey"];

static IResult UnauthorizedIfKeyMissing(HttpRequest req, string? configuredKey)
{
    if (string.IsNullOrWhiteSpace(configuredKey)) return Results.Ok();
    if (!req.Headers.TryGetValue("X-Admin-ApiKey", out var provided) || provided != configuredKey)
        return Results.Unauthorized();
    return Results.Ok();
}

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
        // auto Id/CreatedUtc
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
        return Results.Problem($"Email send failed: {ex.Message}");
    }

    return Results.Accepted($"/quotes/{rec.Id}", new { id = rec.Id });
})
.WithName("SubmitQuote");

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
