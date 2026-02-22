using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bellwood.AdminApi.Services;

public sealed class AuthServerHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthServerHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["AuthServer:Url"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return HealthCheckResult.Degraded("AuthServer URL not configured");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("health-authserver");
            client.Timeout = TimeSpan.FromSeconds(5);
            using var request = new HttpRequestMessage(HttpMethod.Head, baseUrl);
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("AuthServer reachable")
                : HealthCheckResult.Degraded($"AuthServer returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("AuthServer unreachable", ex);
        }
    }
}
