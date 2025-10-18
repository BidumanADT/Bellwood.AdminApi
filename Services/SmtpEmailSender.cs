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

            <p><b>Pickup:</b> {draft.PickupDateTime:G} — {H(draft.PickupLocation)}</p>
            <p><b>Pickup Style:</b> {H(pickupStyleLabel)}{(string.IsNullOrWhiteSpace(pickupSign) ? "" : $" — Sign: {H(pickupSign)}")}</p>
            <p><b>Dropoff:</b> {H(dropoffText)}</p>

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

            Pickup: {draft.PickupDateTime:G} — {draft.PickupLocation}
            Pickup Style: {pickupStyleLabel}{(string.IsNullOrWhiteSpace(pickupSign) ? "" : $" — Sign: {pickupSign}")}
            Dropoff: {dropoffText}
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
