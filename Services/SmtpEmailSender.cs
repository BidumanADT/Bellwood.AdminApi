using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BellwoodGlobal.Mobile.Models;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

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

        public async Task SendQuoteAsync(QuoteDraft draft, string referenceId)
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(_opt.From));
            msg.To.Add(MailboxAddress.Parse(_opt.To));
            msg.Subject = $"{_opt.SubjectPrefix} {referenceId} - {draft.Passenger} / {draft.VehicleClass}";

            var json = JsonSerializer.Serialize(draft, _jsonOpts);
            string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            string EmailLink(string s) => s == "N/A" ? s : $@"<a href=""mailto:{H(s)}"">{H(s)}</a>";


            var bookerName  = draft.Booker?.ToString() ?? "Unknown";
            var bookerPhone = string.IsNullOrWhiteSpace(draft.Booker?.PhoneNumber) ? "N/A" : draft.Booker!.PhoneNumber!;
            var bookerEmail = string.IsNullOrWhiteSpace(draft.Booker?.EmailAddress) ? "N/A" : draft.Booker!.EmailAddress!;

            var paxName  = draft.Passenger?.ToString() ?? "Unknown";
            var paxPhone = string.IsNullOrWhiteSpace(draft.Passenger?.PhoneNumber) ? "N/A" : draft.Passenger!.PhoneNumber!;
            var paxEmail = string.IsNullOrWhiteSpace(draft.Passenger?.EmailAddress) ? "N/A" : draft.Passenger!.EmailAddress!;

            var addlPax = (draft.AdditionalPassengers ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

            string AddlPaxLineHtml() => 
                addlPax.Count == 0 ? ""
                : $"<p><b>Additional Passengers:</b> {H(string.Join(", ", addlPax))}</p>";

            string AddlPaxLineText() =>
                addlPax.Count == 0 ? ""
                : $"Additional Passengers: {string.Join(", ", addlPax)}";

            // Flight summary (Commercial vs Private) from the built QuoteDraft
            string? outboundNumber = draft.OutboundFlight?.FlightNumber;
            string? outboundTail = draft.OutboundFlight?.TailNumber;
            string? returnNumber = draft.ReturnFlight?.FlightNumber;
            string? returnTail = draft.ReturnFlight?.TailNumber;

            bool hasAnyFlight = !string.IsNullOrWhiteSpace(outboundNumber) ||
                                !string.IsNullOrWhiteSpace(outboundTail) ||
                                !string.IsNullOrWhiteSpace(returnNumber) ||
                                !string.IsNullOrWhiteSpace(returnTail);

            string FlightHtml()
            {
                if (!hasAnyFlight) return "";
                var sb = new System.Text.StringBuilder("<p><b>Flight Details:</b><br/>");
                if (!string.IsNullOrWhiteSpace(outboundNumber)) sb.Append($"Outbound Flight #: {H(outboundNumber)}<br/>");
                if (!string.IsNullOrWhiteSpace(outboundTail)) sb.Append($"Outbound Tail #: {H(outboundTail)}<br/>");
                if (!string.IsNullOrWhiteSpace(returnNumber)) sb.Append($"Return Flight #: {H(returnNumber)}<br/>");
                if (!string.IsNullOrWhiteSpace(returnTail)) sb.Append($"Return Tail #: {H(returnTail)}<br/>");

                // If private tail is unchanged on return, make that explicit.
                if (!string.IsNullOrWhiteSpace(outboundTail) && string.IsNullOrWhiteSpace(returnTail) && draft.RoundTrip)
                    sb.Append("Return Aircraft: Same as outbound<br/>");

                sb.Append("</p>");
                return sb.ToString();
            }

            string FlightText()
            {
                if (!hasAnyFlight) return "";
                var lines = new List<string> { "Flight Details:" };
                if (!string.IsNullOrWhiteSpace(outboundNumber)) lines.Add($"  Outbound Flight #: {outboundNumber}");
                if (!string.IsNullOrWhiteSpace(outboundTail)) lines.Add($"  Outbound Tail #: {outboundTail}");
                if (!string.IsNullOrWhiteSpace(returnNumber)) lines.Add($"  Return Flight #: {returnNumber}");
                if (!string.IsNullOrWhiteSpace(returnTail)) lines.Add($"  Return Tail #: {returnTail}");
                if (!string.IsNullOrWhiteSpace(outboundTail) && string.IsNullOrWhiteSpace(returnTail) && draft.RoundTrip)
                    lines.Add("  Return Aircraft: Same as outbound");
                return string.Join("\n", lines) + "\n";
            }

            // Map enum -> label
            static string StyleLabel(PickupStyle? s) => s switch
            {
                PickupStyle.MeetAndGreet => "Meet & Greet",
                PickupStyle.Curbside => "Curbside",
                _ => "Curbside"
            };

            var pickupStyleLabel = StyleLabel(draft.PickupStyle);
            var pickupSign = draft.PickupStyle == PickupStyle.MeetAndGreet
                ? (draft.PickupSignText ?? "").Trim()
                : "";

            var returnStyleLabel = draft.ReturnPickupStyle.HasValue ? StyleLabel(draft.ReturnPickupStyle) : null;
            var returnSign = draft.ReturnPickupStyle == PickupStyle.MeetAndGreet
                ? (draft.ReturnPickupSignText ?? "").Trim()
                : null;

            var dropoffText = draft.AsDirected
                ? "As Directed"
                : (string.IsNullOrWhiteSpace(draft.DropoffLocation) ? "N/A" : draft.DropoffLocation);

            var returnWhen = draft.ReturnPickupTime?.ToString("G"); 
            var returnPickupLoc = draft.DropoffLocation ?? draft.PickupLocation; // return pickup = outbound dropoff (or fallback)

            var builder = new BodyBuilder
            {
            HtmlBody = $@"
            <h3>Bellwood Elite — New Quote</h3>
            <p><b>Reference:</b> {H(referenceId)}</p>
            <p><b>Booker:</b> {H(bookerName)} &mdash; {H(bookerPhone)} &mdash; {EmailLink(bookerEmail)}</p>
            <p><b>Passenger:</b> {H(paxName)} &mdash; {H(paxPhone)} &mdash; {EmailLink(paxEmail)}</p>
            {AddlPaxLineHtml()}

            <p><b>Pickup:</b> {draft.PickupDateTime:G} — {H(draft.PickupLocation)}</p>
            <p><b>Pickup Style:</b> {H(pickupStyleLabel)}{(string.IsNullOrWhiteSpace(pickupSign) ? "" : $" — Sign: {H(pickupSign)}")}</p>
            <p><b>Dropoff:</b> {H(dropoffText)}</p>
            {FlightHtml()}

            <p><b>Vehicle:</b> {H(draft.VehicleClass)}</p>
            <p><b>Passengers/Luggage:</b> {draft.PassengerCount} pax, {draft.CheckedBags ?? 0} checked, {draft.CarryOnBags ?? 0} carry-on</p>
            <p><b>As Directed:</b> {draft.AsDirected} {(draft.AsDirected ? $"({draft.Hours}h)" : "")}</p>
            <p><b>Round Trip:</b> {draft.RoundTrip} {(draft.RoundTrip ? $"(Return {H(returnWhen)})" : "")}</p>

            {(draft.RoundTrip && draft.ReturnPickupTime is not null ? $@"
            <p><b>Return Pickup:</b> {H(returnWhen)} — {H(returnPickupLoc)}</p>
            <p><b>Return Pickup Style:</b> {H(returnStyleLabel!)}{(string.IsNullOrWhiteSpace(returnSign) ? "" : $" — Sign: {H(returnSign)}")}</p>
            <p><b>Return Dropoff:</b> {H(draft.PickupLocation)}</p>" : "")}

            <p><b>Additional Request:</b> {H(draft.AdditionalRequest)} {(string.IsNullOrWhiteSpace(draft.AdditionalRequestOtherText) ? "" : $"— {H(draft.AdditionalRequestOtherText)}")}</p>
            <hr/>
            <pre>{WebUtility.HtmlEncode(json)}</pre>"
            };


            builder.TextBody =
            $@"Bellwood Elite — New Quote
            Reference: {referenceId}
            Booker: {bookerName} - {bookerPhone} - {bookerEmail}
            Passenger: {paxName} - {paxPhone} - {paxEmail}
            {AddlPaxLineText()}

            Pickup: {draft.PickupDateTime:G} — {draft.PickupLocation}
            Pickup Style: {pickupStyleLabel}{(string.IsNullOrWhiteSpace(pickupSign) ? "" : $" — Sign: {pickupSign}")}
            Dropoff: {dropoffText}
            {FlightText()}

            Vehicle: {draft.VehicleClass}
            Passengers/Luggage: {draft.PassengerCount} pax, {draft.CheckedBags ?? 0} checked, {draft.CarryOnBags ?? 0} carry-on
            As Directed: {draft.AsDirected}{(draft.AsDirected ? $" ({draft.Hours}h)" : "")}
            Round Trip: {draft.RoundTrip}{(draft.RoundTrip ? $" (Return {returnWhen})" : "")}
            {(draft.RoundTrip && draft.ReturnPickupTime is not null ? $@"Return Pickup: {returnWhen} — {returnPickupLoc}
            Return Pickup Style: {returnStyleLabel}{(string.IsNullOrWhiteSpace(returnSign) ? "" : $" — Sign: {returnSign}")}<br/>
            Return Dropoff: {draft.PickupLocation}
            " : "")}Additional Request: {draft.AdditionalRequest}{(string.IsNullOrWhiteSpace(draft.AdditionalRequestOtherText) ? "" : $" — {draft.AdditionalRequestOtherText}")}

            JSON:
            {json}";

            msg.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_opt.Host, _opt.Port,
                _opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

            if (!string.IsNullOrWhiteSpace(_opt.Username))
                await smtp.AuthenticateAsync(_opt.Username, _opt.Password);

            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);
        }
    }
}
