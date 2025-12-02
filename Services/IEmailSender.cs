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
    }
}
