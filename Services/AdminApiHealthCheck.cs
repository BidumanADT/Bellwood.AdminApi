using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Enhanced health check for monitoring system status.
/// Phase 3: Production monitoring for alpha test readiness.
/// </summary>
public class AdminApiHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminApiHealthCheck> _logger;

    public AdminApiHealthCheck(
        IServiceProvider serviceProvider,
        ILogger<AdminApiHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var issues = new List<string>();

        try
        {
            // Check 1: Verify repositories are accessible
            await CheckRepositoriesAsync(data, issues, cancellationToken);

            // Check 2: Verify data protection is available
            CheckDataProtection(data, issues);

            // Check 3: Check audit log system
            await CheckAuditLogSystemAsync(data, issues, cancellationToken);

            // Check 4: Check SignalR hub
            CheckSignalRHub(data, issues);

            // Check 5: System resources
            CheckSystemResources(data, issues);

            // Determine overall health status
            if (issues.Count == 0)
            {
                return HealthCheckResult.Healthy(
                    "All systems operational",
                    data);
            }
            else if (issues.Count <= 2)
            {
                return HealthCheckResult.Degraded(
                    $"System degraded: {string.Join(", ", issues)}",
                    data: data);
            }
            else
            {
                return HealthCheckResult.Unhealthy(
                    $"System unhealthy: {string.Join(", ", issues)}",
                    data: data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            return HealthCheckResult.Unhealthy(
                "Health check failed",
                exception: ex,
                data: data);
        }
    }

    private async Task CheckRepositoriesAsync(
        Dictionary<string, object> data,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Test booking repository
            var bookingRepo = scope.ServiceProvider.GetService<IBookingRepository>();
            if (bookingRepo != null)
            {
                var bookings = await bookingRepo.ListAsync(1, cancellationToken);
                data["BookingRepository"] = "OK";
                data["BookingCount"] = bookings.Count;
            }
            else
            {
                issues.Add("BookingRepository not available");
                data["BookingRepository"] = "FAIL";
            }

            // Test quote repository
            var quoteRepo = scope.ServiceProvider.GetService<IQuoteRepository>();
            if (quoteRepo != null)
            {
                var quotes = await quoteRepo.ListAsync(1, cancellationToken);
                data["QuoteRepository"] = "OK";
                data["QuoteCount"] = quotes.Count;
            }
            else
            {
                issues.Add("QuoteRepository not available");
                data["QuoteRepository"] = "FAIL";
            }

            // Test audit log repository
            var auditRepo = scope.ServiceProvider.GetService<IAuditLogRepository>();
            if (auditRepo != null)
            {
                var auditLogs = await auditRepo.GetLogsAsync(take: 1, ct: cancellationToken);
                data["AuditLogRepository"] = "OK";
                data["AuditLogCount"] = auditLogs.Count;
            }
            else
            {
                issues.Add("AuditLogRepository not available");
                data["AuditLogRepository"] = "FAIL";
            }
        }
        catch (Exception ex)
        {
            issues.Add($"Repository check failed: {ex.Message}");
            data["RepositoryError"] = ex.Message;
        }
    }

    private void CheckDataProtection(Dictionary<string, object> data, List<string> issues)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dataProtectionProvider = scope.ServiceProvider.GetService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            
            if (dataProtectionProvider != null)
            {
                data["DataProtection"] = "OK";
            }
            else
            {
                issues.Add("DataProtection not configured");
                data["DataProtection"] = "FAIL";
            }
        }
        catch (Exception ex)
        {
            issues.Add($"DataProtection check failed: {ex.Message}");
            data["DataProtectionError"] = ex.Message;
        }
    }

    private async Task CheckAuditLogSystemAsync(
        Dictionary<string, object> data,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auditLogger = scope.ServiceProvider.GetService<AuditLogger>();
            
            if (auditLogger != null)
            {
                // Verify audit logger can write (system action - no user)
                await auditLogger.LogSystemActionAsync(
                    "System.HealthCheck",
                    "System",
                    details: new { timestamp = DateTime.UtcNow });
                
                data["AuditLogger"] = "OK";
            }
            else
            {
                issues.Add("AuditLogger not available");
                data["AuditLogger"] = "FAIL";
            }
        }
        catch (Exception ex)
        {
            issues.Add($"AuditLogger check failed: {ex.Message}");
            data["AuditLoggerError"] = ex.Message;
        }
    }

    private void CheckSignalRHub(Dictionary<string, object> data, List<string> issues)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<Hubs.LocationHub>>();
            
            if (hubContext != null)
            {
                data["SignalR"] = "OK";
            }
            else
            {
                issues.Add("SignalR hub not configured");
                data["SignalR"] = "FAIL";
            }
        }
        catch (Exception ex)
        {
            issues.Add($"SignalR check failed: {ex.Message}");
            data["SignalRError"] = ex.Message;
        }
    }

    private void CheckSystemResources(Dictionary<string, object> data, List<string> issues)
    {
        try
        {
            // Check available memory
            var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            data["MemoryUsedMB"] = gcMemory;

            // Warn if using more than 500MB (configurable threshold)
            if (gcMemory > 500)
            {
                issues.Add($"High memory usage: {gcMemory}MB");
            }

            // Check thread pool
            ThreadPool.GetAvailableThreads(out var workerThreads, out var ioThreads);
            data["AvailableWorkerThreads"] = workerThreads;
            data["AvailableIOThreads"] = ioThreads;

            if (workerThreads < 10)
            {
                issues.Add($"Low worker threads: {workerThreads}");
            }

            // System uptime
            var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            data["UptimeMinutes"] = (int)uptime.TotalMinutes;
        }
        catch (Exception ex)
        {
            issues.Add($"Resource check failed: {ex.Message}");
            data["ResourceError"] = ex.Message;
        }
    }
}
