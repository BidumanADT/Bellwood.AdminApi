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
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        public SmtpEmailSender(IOptions<EmailOptions> opt) => _opt = opt.Value;

        // ===================================================================
        // PUBLIC METHODS
        // ===================================================================

        public async Task SendQuoteAsync(QuoteDraft draft, string referenceId)
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(_opt.From));
            msg.To.Add(MailboxAddress.Parse(_opt.To));
            msg.Subject = $"{_opt.SubjectPrefix} {referenceId} - {draft.Passenger} / {draft.VehicleClass}";

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
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(_opt.From));
            msg.To.Add(MailboxAddress.Parse(_opt.To));
            msg.Subject = $"Bellwood Elite - New Booking Request - {draft.PickupDateTime:MMM dd, yyyy @ h:mm tt} - {draft.Passenger}";

            var context = new EmailContext(draft, referenceId);
            var builder = new BodyBuilder
            {
                HtmlBody = BuildBookingHtmlBody(context),
                TextBody = BuildBookingTextBody(context)
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
            {ctx.BuildPaymentMethodHtml()}
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
                {ctx.BuildPaymentMethodText()}

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
                Additional Request: {ctx.Draft.AdditionalRequest}{ctx.BuildAdditionalRequestOtherText()}

                JSON:
                {ctx.Json}";
        }

        // ===================================================================
        // SMTP CONNECTION
        // ===================================================================

        private async Task SendEmailAsync(MimeMessage msg)
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_opt.Host, _opt.Port,
                _opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

            if (!string.IsNullOrWhiteSpace(_opt.Username))
                await smtp.AuthenticateAsync(_opt.Username, _opt.Password);

            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);
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
                    ? $"Payment ID: {H(PaymentMethodId!)}"  // Fallback to ID if last4 missing
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
                    ? $"{PaymentMethodId}"  // Fallback to ID
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
        }
    }
}
