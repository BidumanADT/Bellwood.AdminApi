using System.Security.Claims;
using System.Text.Json;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Helper service for creating and logging audit entries.
/// Phase 3: Simplifies audit logging across the application.
/// </summary>
public sealed class AuditLogger
{
    private readonly IAuditLogRepository _repository;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IAuditLogRepository repository, ILogger<AuditLogger> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Log an action performed by a user.
    /// </summary>
    /// <param name="user">ClaimsPrincipal of the user performing the action</param>
    /// <param name="action">Action performed (e.g., "Quote.Created", "Booking.Updated")</param>
    /// <param name="entityType">Type of entity (e.g., "Quote", "Booking", "Driver")</param>
    /// <param name="entityId">ID of the entity affected</param>
    /// <param name="details">Additional details (will be JSON serialized)</param>
    /// <param name="httpContext">HTTP context for IP address and endpoint info</param>
    /// <param name="result">Result of the action (default: Success)</param>
    /// <param name="errorMessage">Error message if action failed</param>
    public async Task LogAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        HttpContext? httpContext = null,
        AuditLogResult result = AuditLogResult.Success,
        string? errorMessage = null)
    {
        try
        {
            var entry = new AuditLog
            {
                UserId = user.FindFirst("uid")?.Value,
                Username = user.FindFirst("sub")?.Value ?? user.Identity?.Name,
                UserRole = user.FindFirst("role")?.Value,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                HttpMethod = httpContext?.Request.Method,
                EndpointPath = httpContext?.Request.Path,
                Result = result,
                ErrorMessage = errorMessage
            };

            await _repository.AddAsync(entry);

            // Also log to console for debugging
            _logger.LogInformation(
                "Audit: {Action} by {Username} ({Role}) on {EntityType} {EntityId} - {Result}",
                action, entry.Username, entry.UserRole, entityType, entityId ?? "N/A", result);
        }
        catch (Exception ex)
        {
            // Audit logging should never break the application
            _logger.LogError(ex, "Failed to write audit log for action {Action}", action);
        }
    }

    /// <summary>
    /// Log a successful action.
    /// </summary>
    public async Task LogSuccessAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        HttpContext? httpContext = null)
    {
        await LogAsync(user, action, entityType, entityId, details, httpContext, AuditLogResult.Success);
    }

    /// <summary>
    /// Log a failed action.
    /// </summary>
    public async Task LogFailureAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        string? errorMessage = null,
        object? details = null,
        HttpContext? httpContext = null)
    {
        await LogAsync(user, action, entityType, entityId, details, httpContext, AuditLogResult.Failed, errorMessage);
    }

    /// <summary>
    /// Log an unauthorized access attempt.
    /// </summary>
    public async Task LogUnauthorizedAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        HttpContext? httpContext = null)
    {
        await LogAsync(user, action, entityType, entityId, null, httpContext, AuditLogResult.Unauthorized);
    }

    /// <summary>
    /// Log a forbidden access attempt (authenticated but insufficient permissions).
    /// </summary>
    public async Task LogForbiddenAsync(
        ClaimsPrincipal user,
        string action,
        string entityType,
        string? entityId = null,
        HttpContext? httpContext = null)
    {
        await LogAsync(user, action, entityType, entityId, null, httpContext, AuditLogResult.Forbidden);
    }

    /// <summary>
    /// Log a system action (no user).
    /// </summary>
    public async Task LogSystemActionAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? details = null)
    {
        try
        {
            var entry = new AuditLog
            {
                UserId = "system",
                Username = "system",
                UserRole = "system",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                Result = AuditLogResult.Success
            };

            await _repository.AddAsync(entry);

            _logger.LogInformation(
                "System Audit: {Action} on {EntityType} {EntityId}",
                action, entityType, entityId ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write system audit log for action {Action}", action);
        }
    }
}

/// <summary>
/// Standard audit action names for consistency.
/// Phase 3: Centralized action naming convention.
/// </summary>
public static class AuditActions
{
    // Quote actions
    public const string QuoteCreated = "Quote.Created";
    public const string QuoteViewed = "Quote.Viewed";
    public const string QuoteUpdated = "Quote.Updated";
    public const string QuoteDeleted = "Quote.Deleted";

    // Booking actions
    public const string BookingCreated = "Booking.Created";
    public const string BookingViewed = "Booking.Viewed";
    public const string BookingUpdated = "Booking.Updated";
    public const string BookingCancelled = "Booking.Cancelled";
    public const string BookingDeleted = "Booking.Deleted";

    // Driver actions
    public const string DriverAssigned = "Driver.Assigned";
    public const string DriverUnassigned = "Driver.Unassigned";
    public const string DriverCreated = "Driver.Created";
    public const string DriverUpdated = "Driver.Updated";
    public const string DriverDeleted = "Driver.Deleted";

    // Affiliate actions
    public const string AffiliateCreated = "Affiliate.Created";
    public const string AffiliateUpdated = "Affiliate.Updated";
    public const string AffiliateDeleted = "Affiliate.Deleted";

    // OAuth actions
    public const string OAuthCredentialsViewed = "OAuth.Credentials.Viewed";
    public const string OAuthCredentialsUpdated = "OAuth.Credentials.Updated";

    // Dispatcher actions
    public const string DispatcherAccessGranted = "Dispatcher.Access.Granted";
    public const string DispatcherAccessDenied = "Dispatcher.Access.Denied";

    // System actions
    public const string SystemStartup = "System.Startup";
    public const string SystemShutdown = "System.Shutdown";
    public const string DataRetentionCleanup = "System.DataRetention.Cleanup";

    // User management actions
    public const string UserCreated = "User.Created";
    public const string UserListed = "User.Listed";
    public const string UserRolesUpdated = "User.Roles.Updated";
    public const string UserDisabledUpdated = "User.Disabled.Updated";

    // Alpha: Audit log management actions
    public const string AuditLogStatsViewed = "AuditLog.Stats.Viewed";
    public const string AuditLogCleared = "AuditLog.Cleared";
}
