using System.Threading.Tasks;
using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bellwood.AdminApi.Services
{
    public sealed class NotificationPublisher : INotificationPublisher
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<NotificationPublisher> _logger;
        private readonly EmailOptions _emailOpts;

        public NotificationPublisher(
            IEmailSender emailSender,
            ILogger<NotificationPublisher> logger,
            IOptions<EmailOptions> emailOptions)
        {
            _emailSender = emailSender;
            _logger = logger;
            _emailOpts = emailOptions.Value;
        }

        /// <summary>
        /// Returns true and logs a debug message when the event is listed in
        /// Email:SuppressedEvents config. Callers receive a completed Task with no error.
        /// </summary>
        private bool IsSuppressed(string eventName)
        {
            if (_emailOpts.SuppressedEvents.Contains(eventName, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[Notifications] Event {EventName} is suppressed via Email:SuppressedEvents config — skipping send.", eventName);
                return true;
            }
            return false;
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
            if (IsSuppressed("BookingCancelled")) return Task.CompletedTask;
            _logger.LogInformation("Publishing notification event {EventName} for reference {ReferenceId}", "BookingCancelled", referenceId);
            return _emailSender.SendBookingCancellationAsync(draft, referenceId, bookerName);
        }

        public Task PublishDriverAssignedAsync(BookingRecord booking, Driver driver, Affiliate affiliate)
        {
            if (IsSuppressed("DriverAssigned")) return Task.CompletedTask;
            _logger.LogInformation("Publishing notification event {EventName} for booking {BookingId}", "DriverAssigned", booking.Id);
            return _emailSender.SendDriverAssignmentAsync(booking, driver, affiliate);
        }

        public Task PublishQuoteResponseAsync(QuoteRecord quote)
        {
            if (IsSuppressed("QuoteResponse")) return Task.CompletedTask;
            _logger.LogInformation("Publishing notification event {EventName} for quote {QuoteId}", "QuoteResponse", quote.Id);
            return _emailSender.SendQuoteResponseAsync(quote);
        }

        public Task PublishQuoteAcceptedAsync(QuoteRecord quote, string bookingId)
        {
            if (IsSuppressed("QuoteAccepted")) return Task.CompletedTask;
            _logger.LogInformation("Publishing notification event {EventName} for quote {QuoteId} and booking {BookingId}", "QuoteAccepted", quote.Id, bookingId);
            return _emailSender.SendQuoteAcceptedAsync(quote, bookingId);
        }

        public Task PublishBookingConfirmedAsync(BookingRecord booking, string messageToPassenger)
        {
            if (IsSuppressed("BookingConfirmed")) return Task.CompletedTask;
            _logger.LogInformation("Publishing notification event {EventName} for booking {BookingId}", "BookingConfirmed", booking.Id);
            return _emailSender.SendBookingConfirmationAsync(booking, messageToPassenger);
        }

        public Task PublishBookingReceivedAsync(BookingRecord booking)
        {
            if (IsSuppressed("BookingReceived")) return Task.CompletedTask;
            _logger.LogInformation("Publishing notification event {EventName} for booking {BookingId}", "BookingReceived", booking.Id);
            return _emailSender.SendBookingReceivedAsync(booking);
        }
    }
}
