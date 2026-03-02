using System.Threading.Tasks;
using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Services
{
    public interface IEmailSender
    {
        Task SendQuoteAsync(QuoteDraft draft, string referenceId);
        Task SendBookingAsync(QuoteDraft draft, string referenceId);
        Task SendBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName);
        Task SendDriverAssignmentAsync(BookingRecord booking, Driver driver, Affiliate affiliate);
        
        // Phase Alpha: Quote lifecycle email notifications
        Task SendQuoteResponseAsync(QuoteRecord quote);
        Task SendQuoteAcceptedAsync(QuoteRecord quote, string bookingId);

        // Booking confirmation email to booker
        Task SendBookingConfirmationAsync(BookingRecord booking, string messageToPassenger);

        /// <summary>
        /// "We received your request" email — sent when staff acknowledges a booking.
        /// No commitment implied. Purely a receipt acknowledgment.
        /// </summary>
        Task SendBookingReceivedAsync(BookingRecord booking);
    }
}
