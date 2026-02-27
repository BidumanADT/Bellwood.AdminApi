# A5 DI Update: Email Options + Sink Resolution

## What changed

Step A5 moves email configuration from ad-hoc section binding to the ASP.NET Core Options pattern, and resolves `IEmailSender` through DI using `EmailOptions.Mode` at runtime.

## `EmailOptions` configuration

`Program.cs` now registers strongly typed email settings via:

```csharp
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
```

This binds `appsettings.json` (and environment-specific overrides) under `Email` to `EmailOptions`.

## Sink registration and runtime selection

All concrete sink implementations are explicitly registered:

- `PapercutEmailSender`
- `SmtpSandboxEmailSender`
- `NoOpEmailSender`

`IEmailSender` is resolved with a factory that reads `IOptions<EmailOptions>` and selects by `Mode`:

- `DevPapercut` → `PapercutEmailSender`
- `AlphaSandbox` → `SmtpSandboxEmailSender`
- any other mode (including `Disabled`) → `NoOpEmailSender`

This keeps sink behavior unchanged while making registration consistent and testable.

## Notification publisher wiring

`INotificationPublisher` remains registered as:

```csharp
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
```

`NotificationPublisher` continues to depend on `IEmailSender`, so sink selection automatically flows through the new factory.

## Using `IOptions<EmailOptions>` in new services

When a service needs email configuration, inject `IOptions<EmailOptions>`:

```csharp
public sealed class ExampleService
{
    private readonly EmailOptions _email;

    public ExampleService(IOptions<EmailOptions> options)
    {
        _email = options.Value;
    }
}
```

Avoid direct calls like:

```csharp
configuration.GetSection("Email").Get<EmailOptions>()
```

inside services or runtime logic.
