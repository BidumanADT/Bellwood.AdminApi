using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;
using Microsoft.Extensions.Options;

namespace Bellwood.AdminApi.Services
{
    public sealed class SmtpSandboxEmailSender : IEmailSender
    {
        private readonly SmtpEmailSender _inner;

        public SmtpSandboxEmailSender(
            IOptions<EmailOptions> opt,
            ILoggerFactory loggerFactory)
        {
            // SmtpEmailSender handles all AlphaSandbox-specific behavior when
            // opt.Value.Mode == "AlphaSandbox": throttling, [ALPHA-OVERRIDE]
            // subject prefix, and recipient-override logging.
            _inner = new SmtpEmailSender(
                opt,
                loggerFactory.CreateLogger<SmtpEmailSender>());
        }

        public Task SendQuoteAsync(QuoteDraft draft, string referenceId)
            => _inner.SendQuoteAsync(draft, referenceId);

        public Task SendBookingAsync(QuoteDraft draft, string referenceId)
            => _inner.SendBookingAsync(draft, referenceId);

        public Task SendBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName)
            => _inner.SendBookingCancellationAsync(draft, referenceId, bookerName);

        public Task SendDriverAssignmentAsync(BookingRecord booking, Driver driver, Affiliate affiliate)
            => _inner.SendDriverAssignmentAsync(booking, driver, affiliate);

        public Task SendQuoteResponseAsync(QuoteRecord quote)
            => _inner.SendQuoteResponseAsync(quote);

        public Task SendQuoteAcceptedAsync(QuoteRecord quote, string bookingId)
            => _inner.SendQuoteAcceptedAsync(quote, bookingId);
    }
}
