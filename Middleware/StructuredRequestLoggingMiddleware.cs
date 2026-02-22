using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace Bellwood.AdminApi.Middleware;

public sealed class StructuredRequestLoggingMiddleware
{
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

                _logger.LogError(
                    ex,
                    "Unhandled exception for {Method} {RequestPath} with errorId {ErrorId} after {ElapsedMs}ms",
                    method,
                    path,
                    errorId,
                    sw.ElapsedMilliseconds);

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        title = "An unexpected error occurred.",
                        status = StatusCodes.Status500InternalServerError,
                        errorId
                    });
                }
            }
        }
    }
}
