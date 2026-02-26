# A4 - NotificationPublisher

## Purpose

`INotificationPublisher` introduces a business-event abstraction for outbound notifications.
Instead of application endpoints calling `IEmailSender` directly, they now publish domain events through a single service.

This keeps business logic focused on what happened (event) instead of how notifications are delivered (sink implementation).

## Event-to-sink mapping (Phase Alpha)

`NotificationPublisher` currently acts as a pass-through adapter to the existing `IEmailSender` sink implementations:

- `PublishQuoteSubmittedAsync(QuoteDraft, referenceId)` → `IEmailSender.SendQuoteAsync(...)`
- `PublishBookingCreatedAsync(QuoteDraft, referenceId)` → `IEmailSender.SendBookingAsync(...)`
- `PublishBookingCancellationAsync(QuoteDraft, referenceId, bookerName)` → `IEmailSender.SendBookingCancellationAsync(...)`
- `PublishDriverAssignedAsync(BookingRecord, Driver, Affiliate)` → `IEmailSender.SendDriverAssignmentAsync(...)`
- `PublishQuoteResponseAsync(QuoteRecord)` → `IEmailSender.SendQuoteResponseAsync(...)`
- `PublishQuoteAcceptedAsync(QuoteRecord, bookingId)` → `IEmailSender.SendQuoteAcceptedAsync(...)`

## Dependency injection

`Program.cs` now registers the publisher with scoped lifetime:

```csharp
services.AddScoped<INotificationPublisher, NotificationPublisher>();
```

Existing `IEmailSender` mode-based registrations remain unchanged. The publisher composes those sinks rather than replacing them.

## How to publish new events in the future

1. Add a new method to `INotificationPublisher` named for the business event (for example, `PublishRideStartedAsync`).
2. Implement the method in `NotificationPublisher` and map it to the current sink behavior.
3. Inject `INotificationPublisher` into endpoint/service handlers and call the new publish method.
4. Keep sink and template changes independent unless the new event requires new content.

This extension pattern keeps the event contract stable while allowing underlying delivery mechanisms to evolve later (for example queueing, retries, or an event bus).
