using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Services
{
    public sealed class NoOpEmailSender : IEmailSender
    {
        private readonly ILogger<NoOpEmailSender> _logger;

        public NoOpEmailSender(ILogger<NoOpEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendQuoteAsync(QuoteDraft draft, string referenceId)
        {
            _logger.LogDebug("[Email/NoOp] Skipping quote email for {ReferenceId}.", referenceId);
            return Task.CompletedTask;
        }

        public Task SendBookingAsync(QuoteDraft draft, string referenceId)
        {
            _logger.LogDebug("[Email/NoOp] Skipping booking email for {ReferenceId}.", referenceId);
            return Task.CompletedTask;
        }

        public Task SendBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName)
        {
            _logger.LogDebug("[Email/NoOp] Skipping booking cancellation email for {ReferenceId}.", referenceId);
            return Task.CompletedTask;
        }

        public Task SendDriverAssignmentAsync(BookingRecord booking, Driver driver, Affiliate affiliate)
        {
            _logger.LogDebug("[Email/NoOp] Skipping driver assignment email for booking {BookingId}.", booking.Id);
            return Task.CompletedTask;
        }

        public Task SendQuoteResponseAsync(QuoteRecord quote)
        {
            _logger.LogDebug("[Email/NoOp] Skipping quote response email for {QuoteId}.", quote.Id);
            return Task.CompletedTask;
        }

        public Task SendQuoteAcceptedAsync(QuoteRecord quote, string bookingId)
        {
            _logger.LogDebug("[Email/NoOp] Skipping quote accepted email for {QuoteId} (booking {BookingId}).", quote.Id, bookingId);
            return Task.CompletedTask;
        }
    }
}
