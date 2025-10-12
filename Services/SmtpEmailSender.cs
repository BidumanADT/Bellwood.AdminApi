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

            var builder = new BodyBuilder
            {
                HtmlBody = $@"
            <h3>Bellwood Elite — New Quote</h3>
            <p><b>Reference:</b> {H(referenceId)}</p>
            <p><b>Booker:</b> {H(bookerName)} &mdash; {H(bookerPhone)} &mdash; {EmailLink(bookerEmail)}</p>
            <p><b>Passenger:</b> {H(paxName)} &mdash; {H(paxPhone)} &mdash; {EmailLink(paxEmail)}</p>

            <p><b>Pickup:</b> {draft.PickupDateTime:G} — {H(draft.PickupLocation)}</p><p><b>Vehicle:</b> {H(draft.VehicleClass)}</p>
            <p><b>Passengers/Luggage:</b> {draft.PassengerCount} pax, {draft.CheckedBags ?? 0} checked, {draft.CarryOnBags ?? 0} carry-on</p>
            <p><b>As Directed:</b> {draft.AsDirected} {(draft.AsDirected ? $"({draft.Hours}h)" : "")}</p>
            <p><b>Round Trip:</b> {draft.RoundTrip} {(draft.RoundTrip ? $"(Return {draft.ReturnPickupTime:G})" : "")}</p>
            <p><b>Additional Request:</b> {H(draft.AdditionalRequest)} {(string.IsNullOrWhiteSpace(draft.AdditionalRequestOtherText) ? "" : $"— {H(draft.AdditionalRequestOtherText)}")}</p>
            <hr/>
            <pre>{WebUtility.HtmlEncode(json)}</pre>"
            };

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
