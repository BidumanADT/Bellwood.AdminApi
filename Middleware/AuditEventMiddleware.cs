using System.Text.Json;
using Bellwood.AdminApi.Models;
using Bellwood.AdminApi.Services;

namespace Bellwood.AdminApi.Middleware;

public sealed class AuditEventMiddleware
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditEventMiddleware> _logger;

    public AuditEventMiddleware(RequestDelegate next, ILogger<AuditEventMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditEventRepository auditEventRepository)
    {
        await _next(context);

        if (!ShouldAudit(context))
        {
            return;
        }

        try
        {
            var segments = context.Request.Path.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
            var targetType = segments.Length > 0 ? segments[0] : "unknown";
            var targetId = segments.Length > 1 ? segments[1] : null;
            var metadata = JsonSerializer.Serialize(new
            {
                method = context.Request.Method,
                path = context.Request.Path.Value,
                statusCode = context.Response.StatusCode
            });

            if (metadata.Length > 512)
            {
                metadata = metadata[..512];
            }

            var auditEvent = new AuditEvent
            {
                ActorUserId = context.User.FindFirst("userId")?.Value ?? context.User.FindFirst("uid")?.Value,
                Action = $"{context.Request.Method}.{targetType}".ToUpperInvariant(),
                TargetType = targetType,
                TargetId = targetId,
                Result = context.Response.StatusCode < 400 ? "Success" : "Failed",
                CorrelationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString(),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = RedactUserAgent(context.Request.Headers.UserAgent.ToString()),
                MetadataJson = metadata
            };

            await auditEventRepository.AddAsync(auditEvent, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hardened audit event");
        }
    }

    private static bool ShouldAudit(HttpContext context)
    {
        if (!MutatingMethods.Contains(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/drivers", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/affiliates", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/quotes", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/bookings", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/oauth", StringComparison.OrdinalIgnoreCase);
    }

    private static string? RedactUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        return userAgent.Length > 200 ? userAgent[..200] : userAgent;
    }
}
