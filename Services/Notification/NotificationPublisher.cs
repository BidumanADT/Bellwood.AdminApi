using System.Threading.Tasks;
using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;
using Microsoft.Extensions.Logging;

namespace Bellwood.AdminApi.Services
{
    public sealed class NotificationPublisher : INotificationPublisher
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<NotificationPublisher> _logger;

        public NotificationPublisher(IEmailSender emailSender, ILogger<NotificationPublisher> logger)
        {
            _emailSender = emailSender;
            _logger = logger;
        }

        public Task PublishQuoteSubmittedAsync(QuoteDraft draft, string referenceId)
        {
            _logger.LogInformation("Publishing notification event {EventName} for reference {ReferenceId}", "QuoteSubmitted", referenceId);
            return _emailSender.SendQuoteAsync(draft, referenceId);
        }

        public Task PublishBookingCreatedAsync(QuoteDraft draft, string referenceId)
        {
            _logger.LogInformation("Publishing notification event {EventName} for reference {ReferenceId}", "BookingCreated", referenceId);
            return _emailSender.SendBookingAsync(draft, referenceId);
        }

        public Task PublishBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName)
        {
            _logger.LogInformation("Publishing notification event {EventName} for reference {ReferenceId}", "BookingCancelled", referenceId);
            return _emailSender.SendBookingCancellationAsync(draft, referenceId, bookerName);
        }

        public Task PublishDriverAssignedAsync(BookingRecord booking, Driver driver, Affiliate affiliate)
        {
            _logger.LogInformation("Publishing notification event {EventName} for booking {BookingId}", "DriverAssigned", booking.Id);
            return _emailSender.SendDriverAssignmentAsync(booking, driver, affiliate);
        }

        public Task PublishQuoteResponseAsync(QuoteRecord quote)
        {
            _logger.LogInformation("Publishing notification event {EventName} for quote {QuoteId}", "QuoteResponse", quote.Id);
            return _emailSender.SendQuoteResponseAsync(quote);
        }

        public Task PublishQuoteAcceptedAsync(QuoteRecord quote, string bookingId)
        {
            _logger.LogInformation("Publishing notification event {EventName} for quote {QuoteId} and booking {BookingId}", "QuoteAccepted", quote.Id, bookingId);
            return _emailSender.SendQuoteAcceptedAsync(quote, bookingId);
        }
    }
}
