using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);
    Task<bool> CheckConnectivityAsync(CancellationToken ct = default);
}
