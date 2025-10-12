using System.Text.Json.Serialization;
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

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Optional API key protection (set Email:ApiKey in config and pass X-Admin-ApiKey from the app)
string? apiKey = builder.Configuration["Email:ApiKey"];

app.MapPost("/quotes", async (
    [FromBody] QuoteDraft draft,
    HttpRequest req,
    IEmailSender email,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("quotes");

    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        if (!req.Headers.TryGetValue("X-Admin-ApiKey", out var provided) || provided != apiKey)
            return Results.Unauthorized();
    }

    // trivial validation
    if (draft is null || string.IsNullOrWhiteSpace(draft.PickupLocation))
        return Results.BadRequest(new { error = "Invalid payload" });

    var id = Guid.NewGuid().ToString("N");

    try
    {
        await email.SendQuoteAsync(draft, id);
        log.LogInformation("Quote {Id} submitted for {Passenger}", id, draft.Passenger?.ToString());
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Email send failed for {Id}", id);
        return Results.Problem($"Email send failed: {ex.Message}");
    }

    // 202 Accepted with a Location to (future) resource
    return Results.Accepted($"/quotes/{id}", new { id });
})
.WithName("SubmitQuote")
.Produces(202)
.ProducesProblem(500)
.WithOpenApi();

app.Run();
