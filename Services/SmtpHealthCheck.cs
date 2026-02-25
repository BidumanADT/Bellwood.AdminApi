using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bellwood.AdminApi.Services;

public sealed class SmtpHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public SmtpHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:Smtp:Host"];
        var hasCreds = !string.IsNullOrWhiteSpace(_configuration["Email:Smtp:Username"])
                       && !string.IsNullOrWhiteSpace(_configuration["Email:Smtp:Password"]);

        if (string.IsNullOrWhiteSpace(host))
        {
            return Task.FromResult(HealthCheckResult.Degraded("SMTP host not configured"));
        }

        return Task.FromResult(hasCreds
            ? HealthCheckResult.Healthy("SMTP configured")
            : HealthCheckResult.Degraded("SMTP credentials incomplete"));
    }
}
