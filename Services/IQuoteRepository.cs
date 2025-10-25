using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services
{
    public interface IQuoteRepository
    {
        Task<QuoteRecord> AddAsync(QuoteRecord rec, CancellationToken ct = default);
        Task<QuoteRecord?> GetAsync(string id, CancellationToken ct = default);
        Task<IReadOnlyList<QuoteRecord>> ListAsync(int take = 50, CancellationToken ct = default);
        Task UpdateStatusAsync(string id, QuoteStatus status, CancellationToken ct = default);
    }
}
