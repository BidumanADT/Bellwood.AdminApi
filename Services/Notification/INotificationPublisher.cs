using System.Threading.Tasks;
using Bellwood.AdminApi.Models;
using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Services
{
    public interface INotificationPublisher
    {
        Task PublishQuoteSubmittedAsync(QuoteDraft draft, string referenceId);
        Task PublishBookingCreatedAsync(QuoteDraft draft, string referenceId);
        Task PublishBookingCancellationAsync(QuoteDraft draft, string referenceId, string bookerName);
        Task PublishDriverAssignedAsync(BookingRecord booking, Driver driver, Affiliate affiliate);

        // Phase Alpha quote lifecycle events
        Task PublishQuoteResponseAsync(QuoteRecord quote);
        Task PublishQuoteAcceptedAsync(QuoteRecord quote, string bookingId);
    }
}
