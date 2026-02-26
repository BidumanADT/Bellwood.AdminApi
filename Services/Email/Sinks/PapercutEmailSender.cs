using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;
using Microsoft.Extensions.Options;

namespace Bellwood.AdminApi.Services
{
    public sealed class PapercutEmailSender : IEmailSender
    {
        private readonly SmtpEmailSender _inner;

        public PapercutEmailSender(
            IOptions<EmailOptions> opt,
            ILoggerFactory loggerFactory)
        {
            var source = opt.Value;
            var effective = new EmailOptions
            {
                Mode = "DevPapercut",
                To = source.To,
                SubjectPrefix = source.SubjectPrefix,
                IncludeOriginalRecipientInSubject = false,
                Smtp = new EmailSmtpOptions
                {
                    Host = string.IsNullOrWhiteSpace(source.Smtp.Host) ? "localhost" : source.Smtp.Host,
                    Port = source.Smtp.Port <= 0 ? 25 : source.Smtp.Port,
                    Username = source.Smtp.Username,
                    Password = source.Smtp.Password,
                    From = source.Smtp.From,
                    UseStartTls = source.Smtp.UseStartTls,
                    ThrottleMs = source.Smtp.ThrottleMs
                },
                OverrideRecipients = new EmailOverrideRecipientsOptions
                {
                    Enabled = false,
                    Address = string.Empty
                }
            };

            _inner = new SmtpEmailSender(
                Options.Create(effective),
                loggerFactory.CreateLogger<SmtpEmailSender>());
        }

        public Task SendQuoteAsync(QuoteDraft draft, string referenceId) => _inner.SendQuoteAsync(draft, referenceId);
        public Task SendBookingAsync(QuoteDraft draft, string referenceId) => _inner.SendBookingAsync(draft, referenceId);
        public Task SendBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName) => _inner.SendBookingCancellationAsync(draft, referenceId, bookerName);
        public Task SendDriverAssignmentAsync(BookingRecord booking, Driver driver, Affiliate affiliate) => _inner.SendDriverAssignmentAsync(booking, driver, affiliate);
        public Task SendQuoteResponseAsync(QuoteRecord quote) => _inner.SendQuoteResponseAsync(quote);
        public Task SendQuoteAcceptedAsync(QuoteRecord quote, string bookingId) => _inner.SendQuoteAcceptedAsync(quote, bookingId);
    }
}
