using System.Diagnostics;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Bellwood.AdminApi.Middleware;

/// <summary>
/// Middleware for tracking HTTP errors and security events.
/// Phase 3: Production monitoring for alpha test readiness.
/// </summary>
public class ErrorTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorTrackingMiddleware> _logger;
    private readonly TelemetryClient? _telemetryClient;

    // Track repeated 403 failures for potential security threats
    private static readonly Dictionary<string, List<DateTime>> _forbiddenAttempts = new();
    private static readonly SemaphoreSlim _forbiddenLock = new(1, 1);
    private const int MaxForbiddenAttemptsPerHour = 10;

    public ErrorTrackingMiddleware(
        RequestDelegate next,
        ILogger<ErrorTrackingMiddleware> logger,
        TelemetryClient? telemetryClient = null)
    {
        _next = next;
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    public async Task InvokeAsync(HttpContext context, AuditLogger? auditLogger = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            // Continue to next middleware
            await _next(context);

            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;

            // Track response based on status code
            var statusCode = context.Response.StatusCode;

            if (statusCode >= 400)
            {
                await TrackErrorResponseAsync(context, statusCode, duration, auditLogger);
            }
            else if (statusCode >= 200 && statusCode < 300)
            {
                TrackSuccessMetrics(context, duration);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await TrackUnhandledExceptionAsync(context, ex, stopwatch.ElapsedMilliseconds, auditLogger);
            throw; // Re-throw to let ASP.NET Core handle it
        }
    }

    /// <summary>
    /// Track error responses (4xx, 5xx).
    /// </summary>
    private async Task TrackErrorResponseAsync(
        HttpContext context, 
        int statusCode, 
        long durationMs,
        AuditLogger? auditLogger)
    {
        var user = context.User;
        var userId = user.FindFirst("uid")?.Value ?? user.FindFirst("sub")?.Value;
        var username = user.FindFirst("sub")?.Value ?? user.Identity?.Name;
        var role = user.FindFirst("role")?.Value;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var method = context.Request.Method;
        var path = context.Request.Path;

        // Log to Application Insights
        if (_telemetryClient != null)
        {
            var requestTelemetry = new RequestTelemetry
            {
                Name = $"{method} {path}",
                Duration = TimeSpan.FromMilliseconds(durationMs),
                ResponseCode = statusCode.ToString(),
                Success = false,
                Url = new Uri($"{context.Request.Scheme}://{context.Request.Host}{path}")
            };

            requestTelemetry.Properties["UserId"] = userId ?? "anonymous";
            requestTelemetry.Properties["Username"] = username ?? "anonymous";
            requestTelemetry.Properties["Role"] = role ?? "none";
            requestTelemetry.Properties["IpAddress"] = ipAddress ?? "unknown";

            _telemetryClient.TrackRequest(requestTelemetry);
        }

        // Handle specific status codes
        if (statusCode == 401)
        {
            _logger.LogWarning(
                "Unauthorized access attempt: {Method} {Path} from {IpAddress} - User: {Username}",
                method, path, ipAddress, username ?? "anonymous");
        }
        else if (statusCode == 403)
        {
            // Track forbidden attempts for security monitoring
            await TrackForbiddenAttemptAsync(context, userId, username, role, ipAddress, auditLogger);
        }
        else if (statusCode >= 500)
        {
            _logger.LogError(
                "Server error ({StatusCode}): {Method} {Path} - Duration: {Duration}ms - User: {Username}",
                statusCode, method, path, durationMs, username ?? "anonymous");

            // Track server errors in Application Insights as exceptions
            if (_telemetryClient != null)
            {
                var exceptionTelemetry = new ExceptionTelemetry
                {
                    Message = $"HTTP {statusCode} error on {method} {path}",
                    SeverityLevel = SeverityLevel.Error
                };
                exceptionTelemetry.Properties["StatusCode"] = statusCode.ToString();
                exceptionTelemetry.Properties["Duration"] = $"{durationMs}ms";
                _telemetryClient.TrackException(exceptionTelemetry);
            }
        }
    }

    /// <summary>
    /// Track forbidden (403) attempts and detect potential security threats.
    /// Phase 3: Alert on repeated authorization failures.
    /// </summary>
    private async Task TrackForbiddenAttemptAsync(
        HttpContext context,
        string? userId,
        string? username,
        string? role,
        string? ipAddress,
        AuditLogger? auditLogger)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";
        var key = $"{userId ?? ipAddress ?? "unknown"}";

        await _forbiddenLock.WaitAsync();
        try
        {
            if (!_forbiddenAttempts.ContainsKey(key))
            {
                _forbiddenAttempts[key] = new List<DateTime>();
            }

            var now = DateTime.UtcNow;
            var attempts = _forbiddenAttempts[key];

            // Remove attempts older than 1 hour
            attempts.RemoveAll(a => (now - a).TotalHours > 1);

            // Add current attempt
            attempts.Add(now);

            var recentAttempts = attempts.Count;

            _logger.LogWarning(
                "Forbidden (403) access: {Method} {Path} from {IpAddress} - User: {Username} ({Role}) - Recent attempts: {Attempts}/hour",
                method, path, ipAddress, username ?? "anonymous", role ?? "none", recentAttempts);

            // Alert if excessive attempts detected
            if (recentAttempts >= MaxForbiddenAttemptsPerHour)
            {
                _logger.LogCritical(
                    "??  SECURITY ALERT: Excessive forbidden (403) attempts detected! " +
                    "User: {Username} (ID: {UserId}, Role: {Role}) from IP: {IpAddress} - " +
                    "{Attempts} attempts in the last hour on {Path}",
                    username ?? "anonymous", userId ?? "unknown", role ?? "none", 
                    ipAddress ?? "unknown", recentAttempts, path);

                // Track as high-severity event in Application Insights
                if (_telemetryClient != null)
                {
                    var eventTelemetry = new EventTelemetry("SecurityAlert.ExcessiveForbiddenAttempts");
                    eventTelemetry.Properties["UserId"] = userId ?? "unknown";
                    eventTelemetry.Properties["Username"] = username ?? "anonymous";
                    eventTelemetry.Properties["Role"] = role ?? "none";
                    eventTelemetry.Properties["IpAddress"] = ipAddress ?? "unknown";
                    eventTelemetry.Properties["Path"] = path;
                    eventTelemetry.Properties["Attempts"] = recentAttempts.ToString();
                    eventTelemetry.Metrics["AttemptsPerHour"] = recentAttempts;

                    _telemetryClient.TrackEvent(eventTelemetry);
                }

                // Phase 3: Audit log security alert
                if (auditLogger != null)
                {
                    await auditLogger.LogAsync(
                        context.User,
                        "Security.Alert.ExcessiveForbiddenAttempts",
                        "Security",
                        entityId: userId ?? ipAddress,
                        details: new
                        {
                            attemptsPerHour = recentAttempts,
                            threshold = MaxForbiddenAttemptsPerHour,
                            path,
                            ipAddress
                        },
                        httpContext: context,
                        result: AuditLogResult.Forbidden);
                }
            }
        }
        finally
        {
            _forbiddenLock.Release();
        }
    }

    /// <summary>
    /// Track successful requests for performance metrics.
    /// </summary>
    private void TrackSuccessMetrics(HttpContext context, long durationMs)
    {
        if (_telemetryClient == null) return;

        var method = context.Request.Method;
        var path = context.Request.Path;

        var requestTelemetry = new RequestTelemetry
        {
            Name = $"{method} {path}",
            Duration = TimeSpan.FromMilliseconds(durationMs),
            ResponseCode = context.Response.StatusCode.ToString(),
            Success = true,
            Url = new Uri($"{context.Request.Scheme}://{context.Request.Host}{path}")
        };

        var userId = context.User.FindFirst("uid")?.Value ?? context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            requestTelemetry.Properties["UserId"] = userId;
        }

        _telemetryClient.TrackRequest(requestTelemetry);

        // Track slow requests (>2 seconds)
        if (durationMs > 2000)
        {
            _logger.LogWarning(
                "Slow request detected: {Method} {Path} - Duration: {Duration}ms",
                method, path, durationMs);

            var eventTelemetry = new EventTelemetry("Performance.SlowRequest");
            eventTelemetry.Metrics["DurationMs"] = durationMs;
            eventTelemetry.Properties["Endpoint"] = $"{method} {path}";
            _telemetryClient.TrackEvent(eventTelemetry);
        }
    }

    /// <summary>
    /// Track unhandled exceptions.
    /// </summary>
    private async Task TrackUnhandledExceptionAsync(
        HttpContext context,
        Exception exception,
        long durationMs,
        AuditLogger? auditLogger)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var userId = context.User.FindFirst("uid")?.Value ?? context.User.FindFirst("sub")?.Value;
        var username = context.User.FindFirst("sub")?.Value ?? context.User.Identity?.Name;

        // Correlate with the errorId generated by StructuredRequestLoggingMiddleware (if present).
        var errorId = context.Items[StructuredRequestLoggingMiddleware.ErrorIdItemKey]?.ToString();

        _logger.LogError(exception,
            "Unhandled exception in {Method} {Path} - User: {Username} - Duration: {Duration}ms - ErrorId: {ErrorId}",
            method, path, username ?? "anonymous", durationMs, errorId ?? "n/a");

        // Track in Application Insights
        if (_telemetryClient != null)
        {
            var exceptionTelemetry = new ExceptionTelemetry(exception)
            {
                SeverityLevel = SeverityLevel.Critical
            };
            exceptionTelemetry.Properties["Method"] = method;
            exceptionTelemetry.Properties["Path"] = path;
            exceptionTelemetry.Properties["UserId"] = userId ?? "unknown";
            exceptionTelemetry.Properties["Duration"] = $"{durationMs}ms";

            _telemetryClient.TrackException(exceptionTelemetry);
        }

        // Phase 3: Audit log unhandled exception
        if (auditLogger != null)
        {
            var stackTrace = exception.StackTrace;
            var truncatedStackTrace = string.IsNullOrEmpty(stackTrace)
                ? null
                : stackTrace.Substring(0, Math.Min(500, stackTrace.Length));

            await auditLogger.LogFailureAsync(
                context.User,
                "System.UnhandledException",
                "System",
                errorMessage: exception.Message,
                details: new
                {
                    exceptionType = exception.GetType().Name,
                    stackTrace = truncatedStackTrace,
                    endpoint = $"{method} {path}"
                },
                httpContext: context);
        }
    }
}
