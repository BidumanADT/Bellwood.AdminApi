using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bellwood.AdminApi.Services;

public sealed class AuditEventStoreHealthCheck : IHealthCheck
{
    private readonly IAuditEventRepository _repository;

    public AuditEventStoreHealthCheck(IAuditEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connected = await _repository.CheckConnectivityAsync(cancellationToken);
        return connected
            ? HealthCheckResult.Healthy("AuditEvent store reachable")
            : HealthCheckResult.Unhealthy("AuditEvent store unavailable");
    }
}
