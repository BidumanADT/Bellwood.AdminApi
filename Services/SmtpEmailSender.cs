using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bellwood.AdminApi.Services
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;
        private readonly ILogger<SmtpEmailSender> _logger;
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        // AlphaSandbox throttle: serialize sends and enforce >= 1100ms between SMTP connections
        // so we stay under Mailtrap's 1 email/second limit during seed-all.
        private static readonly SemaphoreSlim _smtpGate = new(1, 1);
        private static DateTime _nextAllowedUtc = DateTime.MinValue;
        private static readonly int[] _retryDelaysMs = { 1200, 2000, 3000 };

        public SmtpEmailSender(IOptions<EmailOptions> opt, ILogger<SmtpEmailSender> logger)
        {
            _opt = opt.Value;
            _logger = logger;
        }

        // ===================================================================
        // ADDRESS RESOLUTION HELPERS
        // ===================================================================

        /// <summary>
        /// Resolve and validate the From address from config.
        /// Returns null (and logs an error) if the address is missing or invalid.
        /// </summary>
        private MailboxAddress? ResolveFrom(string eventType, string? referenceId)
        {
            var raw = _opt.Smtp.From?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogError("[Email] {EventType} {ReferenceId} skipped: missing From address. Set Email:Smtp:From in user-secrets or appsettings.",
                    eventType, referenceId ?? "(no ref)");
                return null;
            }
            if (!MailboxAddress.TryParse(ParserOptions.Default, raw, out var mailbox))
            {
                _logger.LogError("[Email] {EventType} {ReferenceId} skipped: '{Address}' is not a valid From email address.",
                    eventType, referenceId ?? "(no ref)", raw);
                return null;
            }
            return mailbox;
        }

        /// <summary>
        /// Resolve and validate the To address, applying override if enabled.
        /// <paramref name="intendedAddress"/> is the original intended recipient (e.g. affiliate email, passenger email).
        /// When null, falls back to _opt.To (the staff inbox).
        /// If OverrideRecipients.Enabled, the override address is used instead.
        /// Returns null (and logs an error) if the resolved address is missing or invalid.
        /// </summary>
        private MailboxAddress? ResolveTo(string eventType, string? referenceId, string? intendedAddress = null)
        {
            string? raw;
            if (_opt.OverrideRecipients.Enabled)
            {
                raw = _opt.OverrideRecipients.Address?.Trim();
            }
            else
            {
                raw = (intendedAddress ?? _opt.To)?.Trim();
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogError("[Email] {EventType} {ReferenceId} skipped: missing To address. Check Email:To or Email:OverrideRecipients:Address in config.",
                    eventType, referenceId ?? "(no ref)");
                return null;
            }
            if (!MailboxAddress.TryParse(ParserOptions.Default, raw, out var mailbox))
            {
                _logger.LogError("[Email] {EventType} {ReferenceId} skipped: '{Address}' is not a valid To email address.",
                    eventType, referenceId ?? "(no ref)", raw);
                return null;
            }
            return mailbox;
        }

        /// <summary>
        /// Build a MimeMessage with resolved From/To addresses.
        /// Returns null if either address cannot be resolved (error already logged).
        /// </summary>
        private MimeMessage? BuildMessage(string eventType, string? referenceId, string? intendedTo = null)
        {
            var from = ResolveFrom(eventType, referenceId);
            var to = ResolveTo(eventType, referenceId, intendedTo);
            if (from is null || to is null)
                return null;

            var msg = new MimeMessage();
            msg.From.Add(from);
            msg.To.Add(to);

            if (_opt.IsAlphaSandbox)
            {
                _logger.LogInformation("[Email/AlphaSandbox] {EventType} {ReferenceId} From={From} To={To} (override={Override})",
                    eventType, referenceId ?? "(no ref)", from.Address, to.Address, _opt.OverrideRecipients.Enabled);
            }

            return msg;
        }

        /// <summary>
        /// Build the subject line, optionally appending the original recipient when override is active.
        /// </summary>
        private string BuildSubject(string baseSubject, string? originalRecipient = null)
        {
            if (_opt.IncludeOriginalRecipientInSubject
                && _opt.OverrideRecipients.Enabled
                && !string.IsNullOrWhiteSpace(originalRecipient))
            {
                return $"{baseSubject} [orig: {originalRecipient}]";
            }
            return baseSubject;
        }

        // ===================================================================
        // PUBLIC METHODS
        // ===================================================================

        public async Task SendQuoteAsync(QuoteDraft draft, string referenceId)
        {
            var msg = BuildMessage("Quote.Submitted", referenceId);
            if (msg is null) return;

            msg.Subject = BuildSubject(
                $"{_opt.SubjectPrefix} {referenceId} - {draft.Passenger} / {draft.VehicleClass}");

            var context = new EmailContext(draft, referenceId);
            var builder = new BodyBuilder
            {
                HtmlBody = BuildQuoteHtmlBody(context),
                TextBody = BuildQuoteTextBody(context)
            };

            msg.Body = builder.ToMessageBody();
            await SendEmailAsync(msg);
        }

        public async Task SendBookingAsync(QuoteDraft draft, string referenceId)
        {
            var msg = BuildMessage("Booking.Submitted", referenceId);
            if (msg is null) return;

            msg.Subject = BuildSubject(
                $"Bellwood Elite - New Booking Request - {draft.PickupDateTime:MMM dd, yyyy @ h:mm tt} - {draft.Passenger}");

            var context = new EmailContext(draft, referenceId);
            var builder = new BodyBuilder
            {
                HtmlBody = BuildBookingHtmlBody(context),
                TextBody = BuildBookingTextBody(context)
            };

            msg.Body = builder.ToMessageBody();
            await SendEmailAsync(msg);
        }

        public async Task SendBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName)
        {
            var msg = BuildMessage("Booking.Cancelled", referenceId);
            if (msg is null) return;

            msg.Subject = BuildSubject(
                $"Bellwood Elite - BOOKING CANCELLED - {draft.PickupDateTime:MMM dd, yyyy @ h:mm tt} - {draft.Passenger}");

            var context = new EmailContext(draft, referenceId);
            var builder = new BodyBuilder
            {
                HtmlBody = BuildCancellationHtmlBody(context, bookerName),
                TextBody = BuildCancellationTextBody(context, bookerName)
            };

            msg.Body = builder.ToMessageBody();
            await SendEmailAsync(msg);
        }

        public async Task SendDriverAssignmentAsync(BookingRecord booking, Driver driver, Affiliate affiliate)
        {
            var msg = BuildMessage("Booking.DriverAssigned", booking.Id, affiliate.Email);
            if (msg is null) return;

            msg.Subject = BuildSubject(
                $"Bellwood Elite - Driver Assignment - {booking.PickupDateTime:MMM dd, yyyy @ h:mm tt}",
                affiliate.Email);

            string H(string? s) => WebUtility.HtmlEncode(s ?? "");

            var builder = new BodyBuilder
            {
                HtmlBody = $@"
                <h3 style=""color:#CBA135"">Bellwood Elite — Driver Assignment</h3>
                <p>Hello {H(affiliate.PointOfContact ?? affiliate.Name)},</p>
                <p>A driver from your affiliate has been assigned to a booking:</p>
                
                <hr/>
                <h4>Driver Information</h4>
                <p><b>Name:</b> {H(driver.Name)}</p>
                <p><b>Phone:</b> {H(driver.Phone)}</p>
                
                <hr/>
                <h4>Booking Details</h4>
                <p><b>Reference ID:</b> {H(booking.Id)}</p>
                <p><b>Passenger:</b> {H(booking.PassengerName)}</p>
                <p><b>Pickup Date/Time:</b> {booking.PickupDateTime:G}</p>
                <p><b>Pickup Location:</b> {H(booking.PickupLocation)}</p>
                <p><b>Dropoff Location:</b> {H(booking.DropoffLocation ?? "As Directed")}</p>
                <p><b>Vehicle Class:</b> {H(booking.VehicleClass)}</p>
                <p><b>Passenger Count:</b> {booking.Draft.PassengerCount}</p>
                
                <hr/>
                <p>Please ensure the driver is prepared and available for this assignment.</p>
                <p>Thank you,<br/>Bellwood Elite Team</p>",

                TextBody = $@"Bellwood Elite — Driver Assignment

Hello {affiliate.PointOfContact ?? affiliate.Name},

A driver from your affiliate has been assigned to a booking:

----------------------------------------
Driver Information
----------------------------------------
Name: {driver.Name}
Phone: {driver.Phone}

----------------------------------------
Booking Details
----------------------------------------
Reference ID: {booking.Id}
Passenger: {booking.PassengerName}
Pickup Date/Time: {booking.PickupDateTime:G}
Pickup Location: {booking.PickupLocation}
Dropoff Location: {booking.DropoffLocation ?? "As Directed"}
Vehicle Class: {booking.VehicleClass}
Passenger Count: {booking.Draft.PassengerCount}

Please ensure the driver is prepared and available for this assignment.

Thank you,
Bellwood Elite Team"
            };

            msg.Body = builder.ToMessageBody();
            await SendEmailAsync(msg);
        }

        // ===================================================================
        // EMAIL BODY BUILDERS - QUOTE
        // ===================================================================

        private static string BuildQuoteHtmlBody(EmailContext ctx)
        {
            return $@"
            <h3>Bellwood Elite — New Quote</h3>
            <p><b>Reference:</b> {ctx.H(ctx.ReferenceId)}</p>
            {ctx.BuildContactSectionHtml()}
            {ctx.BuildTripDetailsHtml()}
            {ctx.BuildVehicleAndCapacityHtml()}
            {ctx.BuildServiceOptionsHtml()}
            {ctx.BuildReturnTripHtml()}
            <p><b>Additional Request:</b> {ctx.H(ctx.Draft.AdditionalRequest)} {ctx.BuildAdditionalRequestOtherHtml()}</p>
            <hr/>
            <pre>{WebUtility.HtmlEncode(ctx.Json)}</pre>";
        }

        private static string BuildQuoteTextBody(EmailContext ctx)
        {
            return $@"Bellwood Elite — New Quote
            Reference: {ctx.ReferenceId}
            {ctx.BuildContactSectionText()}
            {ctx.BuildTripDetailsText()}
            {ctx.BuildVehicleAndCapacityText()}
            {ctx.BuildServiceOptionsText()}
            {ctx.BuildReturnTripText()}
            {ctx.BuildPaymentMethodText()}
            Additional Request: {ctx.Draft.AdditionalRequest}{ctx.BuildAdditionalRequestOtherText()}

            JSON:
            {ctx.Json}";
        }

        // ===================================================================
        // EMAIL BODY BUILDERS - BOOKING
        // ===================================================================

        private static string BuildBookingHtmlBody(EmailContext ctx)
        {
            return $@"
                <h3 style=""color:#CBA135"">Bellwood Elite — IMMEDIATE BOOKING REQUEST</h3>
                <p style=""background:#FFF3CD; padding:8px; border-left:4px solid #CBA135;""><b>⚠ ACTION REQUIRED:</b> Customer is requesting immediate booking confirmation.</p>
                <p><b>Reference:</b> {ctx.H(ctx.ReferenceId)}</p>
                {ctx.BuildContactSectionHtml()}
                {ctx.BuildTripDetailsHtml()}
                {ctx.BuildVehicleAndCapacityHtml()}
                {ctx.BuildServiceOptionsHtml()}
                {ctx.BuildReturnTripHtml()}
                {ctx.BuildPaymentMethodHtml()}
                <p><b>Additional Request:</b> {ctx.H(ctx.Draft.AdditionalRequest)} {ctx.BuildAdditionalRequestOtherHtml()}</p>
                <hr/>
                <pre>{WebUtility.HtmlEncode(ctx.Json)}</pre>";
        }

        private static string BuildBookingTextBody(EmailContext ctx)
        {
            return $@"Bellwood Elite — IMMEDIATE BOOKING REQUEST
                ⚠ ACTION REQUIRED: Customer is requesting immediate booking confirmation.

                Reference: {ctx.ReferenceId}
                {ctx.BuildContactSectionText()}
                {ctx.BuildTripDetailsText()}
                {ctx.BuildVehicleAndCapacityText()}
                {ctx.BuildServiceOptionsText()}
                {ctx.BuildReturnTripText()}
                {ctx.BuildPaymentMethodText()}
                Additional Request: {ctx.Draft.AdditionalRequest}{ctx.BuildAdditionalRequestOtherText()}

                JSON:
                {ctx.Json}";
        }

        // ===================================================================
        // EMAIL BODY BUILDERS - CANCELLATION
        // ===================================================================

        private static string BuildCancellationHtmlBody(EmailContext ctx, string bookerName)
        {
            return $@"
                <h3 style=""color:#e53e3e"">Bellwood Elite — BOOKING CANCELLATION</h3>
                <p style=""background:#FED7D7; padding:8px; border-left:4px solid #e53e3e;""><b>🚫 CANCELLED:</b> Customer has cancelled this booking request.</p>
                <p><b>Reference:</b> {ctx.H(ctx.ReferenceId)}</p>
                <p><b>Cancelled By:</b> {ctx.H(bookerName)}</p>
                <p><b>Cancelled At:</b> {DateTime.UtcNow:G} UTC</p>
                
                <hr/>
                <h4>Original Booking Details:</h4>
                {ctx.BuildContactSectionHtml()}
                {ctx.BuildTripDetailsHtml()}
                {ctx.BuildVehicleAndCapacityHtml()}
                {ctx.BuildServiceOptionsHtml()}
                {ctx.BuildReturnTripHtml()}
                {ctx.BuildPaymentMethodHtml()}
                <p><b>Additional Request:</b> {ctx.H(ctx.Draft.AdditionalRequest)} {ctx.BuildAdditionalRequestOtherHtml()}</p>
                <hr/>
                <pre>{WebUtility.HtmlEncode(ctx.Json)}</pre>";
        }

        private static string BuildCancellationTextBody(EmailContext ctx, string bookerName)
        {
            return $@"Bellwood Elite — BOOKING CANCELLATION
                🚫 CANCELLED: Customer has cancelled this booking request.

                Reference: {ctx.ReferenceId}
                Cancelled By: {bookerName}
                Cancelled At: {DateTime.UtcNow:G} UTC

                ========================================
                Original Booking Details:
                ========================================
                {ctx.BuildContactSectionText()}
                {ctx.BuildTripDetailsText()}
                {ctx.BuildVehicleAndCapacityText()}
                {ctx.BuildServiceOptionsText()}
                {ctx.BuildReturnTripText()}
                {ctx.BuildPaymentMethodText()}
                Additional Request: {ctx.Draft.AdditionalRequest}{ctx.BuildAdditionalRequestOtherText()}

                JSON:
                {ctx.Json}";
        }

        // ===================================================================
        // SMTP CONNECTION
        // ===================================================================

        private async Task SendEmailAsync(MimeMessage msg)
        {
            if (_opt.IsDisabled)
            {
                _logger.LogDebug("[Email] Mode=Disabled — skipping send.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_opt.Host))
            {
                _logger.LogWarning("[Email] SMTP host is not configured — skipping send.");
                return;
            }

            // AlphaSandbox: serialize all sends and enforce minimum spacing to avoid rate limits.
            if (_opt.IsAlphaSandbox)
                await _smtpGate.WaitAsync();

            try
            {
                for (int attempt = 0; attempt <= _retryDelaysMs.Length; attempt++)
                {
                    // Enforce minimum gap between sends (AlphaSandbox only).
                    if (_opt.IsAlphaSandbox)
                    {
                        var wait = _nextAllowedUtc - DateTime.UtcNow;
                        if (wait > TimeSpan.Zero)
                            await Task.Delay(wait);
                    }

                    try
                    {
                        using var smtp = new SmtpClient();
                        await smtp.ConnectAsync(_opt.Host, _opt.Port,
                            _opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

                        if (!string.IsNullOrWhiteSpace(_opt.Username))
                            await smtp.AuthenticateAsync(_opt.Username, _opt.Password);

                        await smtp.SendAsync(msg);
                        await smtp.DisconnectAsync(true);

                        // Update the gate timestamp after a successful send.
                        if (_opt.IsAlphaSandbox)
                            _nextAllowedUtc = DateTime.UtcNow.AddMilliseconds(1100);

                        return; // success — exit retry loop
                    }
                    catch (SmtpCommandException ex)
                        when (attempt < _retryDelaysMs.Length
                              && (ex.Message.Contains("Too many emails per second", StringComparison.OrdinalIgnoreCase)
                                  || ex.Message.Contains("5.7.0", StringComparison.Ordinal)))
                    {
                        // Mailtrap rate-limit hit — wait then retry.
                        var delay = _retryDelaysMs[attempt];
                        _logger.LogWarning("[Email] Rate limit on attempt {Attempt}/{Max} — retrying in {Delay}ms. ({Message})",
                            attempt + 1, _retryDelaysMs.Length, delay, ex.Message);
                        await Task.Delay(delay);

                        // Push the gate forward so the next attempt respects spacing too.
                        if (_opt.IsAlphaSandbox)
                            _nextAllowedUtc = DateTime.UtcNow.AddMilliseconds(delay);
                    }
                    // All other exceptions propagate normally.
                }
            }
            finally
            {
                if (_opt.IsAlphaSandbox)
                    _smtpGate.Release();
            }
        }

        // ===================================================================
        // PHASE ALPHA: QUOTE RESPONSE EMAIL NOTIFICATION
        // ===================================================================

        public async Task SendQuoteResponseAsync(QuoteRecord quote)
        {
            var intendedTo = quote.Draft?.Booker?.EmailAddress;
            var msg = BuildMessage("Quote.Responded", quote.Id, intendedTo ?? _opt.To);
            if (msg is null) return;

            msg.Subject = BuildSubject(
                $"Bellwood Elite - Quote Response - {quote.PassengerName} - {quote.PickupDateTime:MMM dd, yyyy}",
                intendedTo);

            string H(string? s) => WebUtility.HtmlEncode(s ?? "");

            var builder = new BodyBuilder
            {
                HtmlBody = $@"
                <h3 style=""color:#CBA135"">Bellwood Elite — Quote Response</h3>
                <p>Hello {H(quote.Draft?.Booker?.FirstName ?? "")},</p>
                <p>We have reviewed your quote request and are pleased to provide the following information:</p>
                
                <hr/>
                <h4>Quote Details</h4>
                <p><b>Reference ID:</b> {H(quote.Id)}</p>
                <p><b>Passenger:</b> {H(quote.PassengerName)}</p>
                <p><b>Vehicle Class:</b> {H(quote.VehicleClass)}</p>
                <p><b>Pickup Location:</b> {H(quote.PickupLocation)}</p>
                <p><b>Dropoff Location:</b> {H(quote.DropoffLocation ?? "As Directed")}</p>
                
                <hr/>
                <h4>Our Response</h4>
                <p><b>Estimated Price:</b> <span style=""font-size:24px; color:#CBA135; font-weight:bold;"">${quote.EstimatedPrice:F2}</span></p>
                <p><b>Estimated Pickup Time:</b> {quote.EstimatedPickupTime:F}</p>
                {(string.IsNullOrWhiteSpace(quote.Notes) ? "" : $"<p><b>Notes:</b> {H(quote.Notes)}</p>")}
                
                <hr/>
                <p><b>Next Steps:</b></p>
                <p>To accept this quote and create a booking, please use the Bellwood app or contact us directly.</p>
                <p>Thank you for choosing Bellwood Elite!</p>
                <p>Best regards,<br/>Bellwood Elite Team</p>",

            TextBody = $@"Bellwood Elite — Quote Response

Hello {quote.Draft?.Booker?.FirstName ?? ""},

We have reviewed your quote request and are pleased to provide the following information:

----------------------------------------
Quote Details
----------------------------------------
Reference ID: {quote.Id}
Passenger: {quote.PassengerName}
Vehicle Class: {quote.VehicleClass}
Pickup Location: {quote.PickupLocation}
Dropoff Location: {quote.DropoffLocation ?? "As Directed"}

----------------------------------------
Our Response
----------------------------------------
Estimated Price: ${quote.EstimatedPrice:F2}
Estimated Pickup Time: {quote.EstimatedPickupTime:F}
{(string.IsNullOrWhiteSpace(quote.Notes) ? "" : $"Notes: {quote.Notes}")}

----------------------------------------
Next Steps
----------------------------------------
To accept this quote and create a booking, please use the Bellwood app or contact us directly.

Thank you for choosing Bellwood Elite!

Best regards,
Bellwood Elite Team"
        };

        msg.Body = builder.ToMessageBody();
        await SendEmailAsync(msg);
    }

    // ===================================================================
    // PHASE ALPHA: QUOTE ACCEPTED EMAIL NOTIFICATION
    // ===================================================================

    public async Task SendQuoteAcceptedAsync(QuoteRecord quote, string bookingId)
    {
        var msg = BuildMessage("Quote.Accepted", quote.Id);
        if (msg is null) return;

        msg.Subject = BuildSubject(
            $"Bellwood Elite - Quote ACCEPTED - {quote.PassengerName} - Booking {bookingId}");

        string H(string? s) => WebUtility.HtmlEncode(s ?? "");

        var builder = new BodyBuilder
        {
            HtmlBody = $@"
                <h3 style=""color:#CBA135"">Bellwood Elite — Quote Accepted!</h3>
                <p style=""background:#D4EDDA; padding:8px; border-left:4px solid:#28A745;"">
                    <b>✓ QUOTE ACCEPTED:</b> Passenger has accepted the quote and created a booking.
                </p>
                
                <hr/>
                <h4>Quote Information</h4>
                <p><b>Quote ID:</b> {H(quote.Id)}</p>
                <p><b>Quote Status:</b> Accepted</p>
                <p><b>Estimated Price:</b> ${quote.EstimatedPrice:F2}</p>
                
                <hr/>
                <h4>New Booking Created</h4>
                <p><b>Booking ID:</b> {H(bookingId)}</p>
                <p><b>Status:</b> Requested (ready for confirmation)</p>
                
                <hr/>
                <h4>Passenger Details</h4>
                <p><b>Booker:</b> {H(quote.BookerName)}</p>
                <p><b>Passenger:</b> {H(quote.PassengerName)}</p>
                <p><b>Vehicle Class:</b> {H(quote.VehicleClass)}</p>
                <p><b>Pickup:</b> {quote.EstimatedPickupTime:F} at {H(quote.PickupLocation)}</p>
                <p><b>Dropoff:</b> {H(quote.DropoffLocation ?? "As Directed")}</p>
                
                <hr/>
                <p><b>Action Required:</b> Review the booking in the admin portal and assign a driver.</p>
                <p>Bellwood Elite Team</p>",

            TextBody = $@"Bellwood Elite — Quote Accepted!

✓ QUOTE ACCEPTED: Passenger has accepted the quote and created a booking.

----------------------------------------
Quote Information
----------------------------------------
Quote ID: {quote.Id}
Quote Status: Accepted
Estimated Price: ${quote.EstimatedPrice:F2}

----------------------------------------
New Booking Created
----------------------------------------
Booking ID: {bookingId}
Status: Requested (ready for confirmation)

----------------------------------------
Passenger Details
----------------------------------------
Booker: {quote.BookerName}
Passenger: {quote.PassengerName}
Vehicle Class: {quote.VehicleClass}
Pickup: {quote.EstimatedPickupTime:F} at {quote.PickupLocation}
Dropoff: {quote.DropoffLocation ?? "As Directed"}

----------------------------------------
Action Required: Review the booking in the admin portal and assign a driver.

Bellwood Elite Team"
        };

        msg.Body = builder.ToMessageBody();
        await SendEmailAsync(msg);
    }

    // ===================================================================
    // EMAIL CONTEXT HELPER CLASS
    // ===================================================================

    private sealed class EmailContext
    {
        public QuoteDraft Draft { get; }
        public string ReferenceId { get; }
        public string Json { get; }

        // Contact info
        public string BookerName { get; }
        public string BookerPhone { get; }
        public string BookerEmail { get; }
        public string PassengerName { get; }
        public string PassengerPhone { get; }
        public string PassengerEmail { get; }
        public List<string> AdditionalPassengers { get; }

        // Flight info
        public string? OutboundFlightNumber { get; }
        public string? OutboundTailNumber { get; }
        public string? ReturnFlightNumber { get; }
        public string? ReturnTailNumber { get; }
        public bool HasAnyFlight { get; }

        // Pickup/Dropoff info
        public string PickupStyleLabel { get; }
        public string PickupSign { get; }
        public string? ReturnStyleLabel { get; }
        public string? ReturnSign { get; }
        public string DropoffText { get; }
        public string? ReturnWhen { get; }
        public string ReturnPickupLocation { get; }

        // Payment info
        public string? PaymentMethodId { get; }
        public string? PaymentMethodLast4 { get; }
        public bool HasPaymentMethod { get; }

        public EmailContext(QuoteDraft draft, string referenceId)
        {
            Draft = draft;
            ReferenceId = referenceId;
            Json = JsonSerializer.Serialize(draft, _jsonOpts);

            // Extract contact info
            BookerName = draft.Booker?.ToString() ?? "Unknown";
            BookerPhone = string.IsNullOrWhiteSpace(draft.Booker?.PhoneNumber) ? "N/A" : draft.Booker!.PhoneNumber!;
            BookerEmail = string.IsNullOrWhiteSpace(draft.Booker?.EmailAddress) ? "N/A" : draft.Booker!.EmailAddress!;
            PassengerName = draft.Passenger?.ToString() ?? "Unknown";
            PassengerPhone = string.IsNullOrWhiteSpace(draft.Passenger?.PhoneNumber) ? "N/A" : draft.Passenger!.PhoneNumber!;
            PassengerEmail = string.IsNullOrWhiteSpace(draft.Passenger?.EmailAddress) ? "N/A" : draft.Passenger!.EmailAddress!;
            AdditionalPassengers = (draft.AdditionalPassengers ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            // Extract flight info
            OutboundFlightNumber = draft.OutboundFlight?.FlightNumber;
            OutboundTailNumber = draft.OutboundFlight?.TailNumber;
            ReturnFlightNumber = draft.ReturnFlight?.FlightNumber;
            ReturnTailNumber = draft.ReturnFlight?.TailNumber;
            HasAnyFlight = !string.IsNullOrWhiteSpace(OutboundFlightNumber) ||
                           !string.IsNullOrWhiteSpace(OutboundTailNumber) ||
                           !string.IsNullOrWhiteSpace(ReturnFlightNumber) ||
                           !string.IsNullOrWhiteSpace(ReturnTailNumber);

            // Extract pickup/dropoff info
            PickupStyleLabel = FormatPickupStyle(draft.PickupStyle);
            PickupSign = draft.PickupStyle == PickupStyle.MeetAndGreet ? (draft.PickupSignText ?? "").Trim() : "";
            ReturnStyleLabel = draft.ReturnPickupStyle.HasValue ? FormatPickupStyle(draft.ReturnPickupStyle) : null;
            ReturnSign = draft.ReturnPickupStyle == PickupStyle.MeetAndGreet ? (draft.ReturnPickupSignText ?? "").Trim() : null;
            DropoffText = draft.AsDirected ? "As Directed" : (string.IsNullOrWhiteSpace(draft.DropoffLocation) ? "N/A" : draft.DropoffLocation);
            ReturnWhen = draft.ReturnPickupTime?.ToString("G");
            ReturnPickupLocation = draft.DropoffLocation ?? draft.PickupLocation;

            // Extract payment info
            PaymentMethodId = draft.PaymentMethodId;
            PaymentMethodLast4 = draft.PaymentMethodLast4;
            HasPaymentMethod = !string.IsNullOrWhiteSpace(PaymentMethodId);
        }

        // Helper methods
        public string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        public string EmailLink(string s) => s == "N/A" ? s : $@"<a href=""mailto:{H(s)}"">{H(s)}</a>";

        private static string FormatPickupStyle(PickupStyle? style) => style switch
        {
            PickupStyle.MeetAndGreet => "Meet & Greet",
            PickupStyle.Curbside => "Curbside",
            _ => "Curbside"
        };

        // ===================================================================
        // SECTION BUILDERS - HTML
        // ===================================================================

        public string BuildContactSectionHtml()
        {
            var sb = new StringBuilder();
            sb.Append($"<p><b>Booker:</b> {H(BookerName)} &mdash; {H(BookerPhone)} &mdash; {EmailLink(BookerEmail)}</p>");
            sb.Append($"<p><b>Passenger:</b> {H(PassengerName)} &mdash; {H(PassengerPhone)} &mdash; {EmailLink(PassengerEmail)}</p>");
            if (AdditionalPassengers.Count > 0)
                sb.Append($"<p><b>Additional Passengers:</b> {H(string.Join(", ", AdditionalPassengers))}</p>");
            return sb.ToString();
        }

        public string BuildTripDetailsHtml()
        {
            var sb = new StringBuilder();
            sb.Append($"<p><b>Pickup:</b> {Draft.PickupDateTime:G} — {H(Draft.PickupLocation)}</p>");
            sb.Append($"<p><b>Pickup Style:</b> {H(PickupStyleLabel)}{(string.IsNullOrWhiteSpace(PickupSign) ? "" : $" — Sign: {H(PickupSign)}")}</p>");
            sb.Append($"<p><b>Dropoff:</b> {H(DropoffText)}</p>");
            sb.Append(BuildFlightDetailsHtml());
            return sb.ToString();
        }

        public string BuildVehicleAndCapacityHtml()
        {
            var sb = new StringBuilder();
            sb.Append($"<p><b>Vehicle:</b> {H(Draft.VehicleClass)}</p>");
            sb.Append($"<p><b>Passengers/Luggage:</b> {Draft.PassengerCount} pax, {Draft.CheckedBags ?? 0} checked, {Draft.CarryOnBags ?? 0} carry-on</p>");
            sb.Append(BuildCapacityWarningHtml());
            return sb.ToString();
        }

        public string BuildServiceOptionsHtml()
        {
            var sb = new StringBuilder();
            sb.Append($"<p><b>As Directed:</b> {Draft.AsDirected} {(Draft.AsDirected ? $"({Draft.Hours}h)" : "")}</p>");
            sb.Append($"<p><b>Round Trip:</b> {Draft.RoundTrip} {(Draft.RoundTrip ? $"(Return {H(ReturnWhen)})" : "")}</p>");
            return sb.ToString();
        }

        public string BuildReturnTripHtml()
        {
            if (!Draft.RoundTrip || Draft.ReturnPickupTime is null) return "";
            return $@"
            <p><b>Return Pickup:</b> {H(ReturnWhen)} — {H(ReturnPickupLocation)}</p>
            <p><b>Return Pickup Style:</b> {H(ReturnStyleLabel!)}{(string.IsNullOrWhiteSpace(ReturnSign) ? "" : $" — Sign: {H(ReturnSign)}")}</p>
            <p><b>Return Dropoff:</b> {H(Draft.PickupLocation)}</p>";
        }

        private string BuildFlightDetailsHtml()
        {
            if (!HasAnyFlight) return "";
            var sb = new StringBuilder("<p><b>Flight Details:</b><br/>");
            if (!string.IsNullOrWhiteSpace(OutboundFlightNumber)) sb.Append($"Outbound Flight #: {H(OutboundFlightNumber)}<br/>");
            if (!string.IsNullOrWhiteSpace(OutboundTailNumber)) sb.Append($"Outbound Tail #: {H(OutboundTailNumber)}<br/>");
            if (!string.IsNullOrWhiteSpace(ReturnFlightNumber)) sb.Append($"Return Flight #: {H(ReturnFlightNumber)}<br/>");
            if (!string.IsNullOrWhiteSpace(ReturnTailNumber)) sb.Append($"Return Tail #: {H(ReturnTailNumber)}<br/>");
            if (!string.IsNullOrWhiteSpace(OutboundTailNumber) && string.IsNullOrWhiteSpace(ReturnTailNumber) && Draft.RoundTrip)
                sb.Append("Return Aircraft: Same as outbound<br/>");
            sb.Append("</p>");
            return sb.ToString();
        }

        private string BuildCapacityWarningHtml()
        {
            if (Draft.CapacityWithinLimits) return "";
            var keepText = Draft.CapacityOverrideByUser
                ? "Booker chose to keep the current vehicle despite capacity limits."
                : "Please advise an upgrade with the booker.";
            var suggestion = string.IsNullOrWhiteSpace(Draft.SuggestedVehicle) ? "" : $" Suggested: {H(Draft.SuggestedVehicle)}.";
            var note = string.IsNullOrWhiteSpace(Draft.CapacityNote) ? "" : $" {H(Draft.CapacityNote)}";
            return $@"<p style=""color:#d97706""><b>Capacity Warning:</b>{note}{suggestion} {H(keepText)}</p>";
        }

        public string BuildAdditionalRequestOtherHtml()
        {
            return string.IsNullOrWhiteSpace(Draft.AdditionalRequestOtherText) ? "" : $"— {H(Draft.AdditionalRequestOtherText)}";
        }

        public string BuildPaymentMethodHtml()
        {
            if (!HasPaymentMethod)
            {
                return @"<p style=""color:#f56565; background:#fff5f5; padding:8px; border-left:3px solid #f56565;"">
                <b>⚠️ Payment Method:</b> Not provided — requires manual setup
             </p>";
            }

            var displayText = string.IsNullOrWhiteSpace(PaymentMethodLast4)
                ? $"Payment ID: {H(PaymentMethodId!)}"
                : $"Last 4: ••{H(PaymentMethodLast4)}";

            return $@"<p><b>💳 Selected Payment Method:</b> 
             <code style=""background:#2d3748; color:#d4af37; padding:2px 6px; border-radius:4px; font-family:monospace;"">{displayText}</code>
             <br/><small style=""color:#718096;"">Card to be used for invoice</small>
          </p>";
        }

        public string BuildPaymentMethodText()
        {
            if (!HasPaymentMethod)
                return "Payment Method: ⚠️ Not provided (requires manual setup)\n";

            var displayText = string.IsNullOrWhiteSpace(PaymentMethodLast4)
                ? $"{PaymentMethodId}"
                : $"••{PaymentMethodLast4}";

            return $"Last 4 of selected payment method: {displayText}\n  → Card to be used for invoice\n";
        }

        // ===================================================================
        // SECTION BUILDERS - TEXT
        // ===================================================================

        public string BuildContactSectionText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Booker: {BookerName} - {BookerPhone} - {BookerEmail}");
            sb.AppendLine($"Passenger: {PassengerName} - {PassengerPhone} - {PassengerEmail}");
            if (AdditionalPassengers.Count > 0)
                sb.AppendLine($"Additional Passengers: {string.Join(", ", AdditionalPassengers)}");
            return sb.ToString();
        }

        public string BuildTripDetailsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pickup: {Draft.PickupDateTime:G} — {Draft.PickupLocation}");
            sb.AppendLine($"Pickup Style: {PickupStyleLabel}{(string.IsNullOrWhiteSpace(PickupSign) ? "" : $" — Sign: {PickupSign}")}");
            sb.AppendLine($"Dropoff: {DropoffText}");
            sb.Append(BuildFlightDetailsText());
            return sb.ToString();
        }

        public string BuildVehicleAndCapacityText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Vehicle: {Draft.VehicleClass}");
            sb.AppendLine($"Passengers/Luggage: {Draft.PassengerCount} pax, {Draft.CheckedBags ?? 0} checked, {Draft.CarryOnBags ?? 0} carry-on");
            return sb.ToString();
        }

        public string BuildServiceOptionsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"As Directed: {Draft.AsDirected}{(Draft.AsDirected ? $" ({Draft.Hours}h)" : "")}");
            sb.AppendLine($"Round Trip: {Draft.RoundTrip}{(Draft.RoundTrip ? $" (Return {ReturnWhen})" : "")}");
            return sb.ToString();
        }

        public string BuildReturnTripText()
        {
            if (!Draft.RoundTrip || Draft.ReturnPickupTime is null) return "";
            var sb = new StringBuilder();
            sb.AppendLine($"Return Pickup: {ReturnWhen} — {ReturnPickupLocation}");
            sb.AppendLine($"Return Pickup Style: {ReturnStyleLabel}{(string.IsNullOrWhiteSpace(ReturnSign) ? "" : $" — Sign: {ReturnSign}")}");
            sb.AppendLine($"Return Dropoff: {Draft.PickupLocation}");
            return sb.ToString();
        }

        private string BuildFlightDetailsText()
        {
            if (!HasAnyFlight) return "";
            var lines = new List<string> { "Flight Details:" };
            if (!string.IsNullOrWhiteSpace(OutboundFlightNumber)) lines.Add($"  Outbound Flight #: {OutboundFlightNumber}");
            if (!string.IsNullOrWhiteSpace(OutboundTailNumber)) lines.Add($"  Outbound Tail #: {OutboundTailNumber}");
            if (!string.IsNullOrWhiteSpace(ReturnFlightNumber)) lines.Add($"  Return Flight #: {ReturnFlightNumber}");
            if (!string.IsNullOrWhiteSpace(ReturnTailNumber)) lines.Add($"  Return Tail #: {ReturnTailNumber}");
            if (!string.IsNullOrWhiteSpace(OutboundTailNumber) && string.IsNullOrWhiteSpace(ReturnTailNumber) && Draft.RoundTrip)
                lines.Add("  Return Aircraft: Same as outbound");
            return string.Join("\n", lines) + "\n";
        }

        public string BuildAdditionalRequestOtherText()
        {
            return string.IsNullOrWhiteSpace(Draft.AdditionalRequestOtherText) ? "" : $" — {Draft.AdditionalRequestOtherText}";
        }
    } // end EmailContext

    } // end SmtpEmailSender
} // end namespace
