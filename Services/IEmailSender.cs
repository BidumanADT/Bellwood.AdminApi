using System.Threading.Tasks;
using BellwoodGlobal.Mobile.Models;

namespace Bellwood.AdminApi.Services
{
    public interface IEmailSender
    {
        Task SendQuoteAsync(QuoteDraft draft, string referenceId);
    }
}
