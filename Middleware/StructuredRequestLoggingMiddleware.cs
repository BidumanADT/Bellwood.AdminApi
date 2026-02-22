using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace Bellwood.AdminApi.Middleware;

public sealed class StructuredRequestLoggingMiddleware
{
    // Key used to store the generated errorId in HttpContext.Items for downstream correlation.
    public const string ErrorIdItemKey = "errorId";

    private readonly RequestDelegate _next;
    private readonly ILogger<StructuredRequestLoggingMiddleware> _logger;

    public StructuredRequestLoggingMiddleware(RequestDelegate next, ILogger<StructuredRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.ToString();
        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? context.TraceIdentifier;
        var userId = context.User.FindFirst("userId")?.Value ?? context.User.FindFirst("uid")?.Value;
        var clientId = context.User.FindFirst("client_id")?.Value;

        using (LogContext.PushProperty("requestPath", path))
        using (LogContext.PushProperty("method", method))
        using (LogContext.PushProperty("correlationId", correlationId))
        using (LogContext.PushProperty("userId", userId ?? "anonymous"))
        using (LogContext.PushProperty("clientId", clientId ?? "n/a"))
        {
            try
            {
                await _next(context);
                sw.Stop();
                _logger.LogInformation(
                    "Request completed {Method} {RequestPath} with {StatusCode} in {ElapsedMs}ms",
                    method,
                    path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorId = Guid.NewGuid().ToString("N");

                // Store errorId for downstream middleware (e.g. ErrorTrackingMiddleware) to correlate.
                context.Items[ErrorIdItemKey] = errorId;

                _logger.LogError(
                    ex,
                    "Unhandled exception for {Method} {RequestPath} with errorId {ErrorId} after {ElapsedMs}ms",
                    method,
                    path,
                    errorId,
                    sw.ElapsedMilliseconds);

                // Do NOT write a response here. Rethrow so ErrorTrackingMiddleware and
                // ASP.NET's configured exception handler (UseExceptionHandler / developer
                // exception page) can observe and handle the exception normally.
                throw;
            }
        }
    }
}
