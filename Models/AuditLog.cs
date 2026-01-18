namespace Bellwood.AdminApi.Models;

/// <summary>
/// Audit log entry for tracking all critical system operations.
/// Phase 3: Enterprise-grade compliance and security monitoring.
/// </summary>
public sealed class AuditLog
{
    /// <summary>
    /// Unique identifier for this audit log entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Timestamp when the action occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID (uid claim from JWT) who performed the action.
    /// Nullable for system-initiated actions.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Username (sub claim from JWT) who performed the action.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// User's role at the time of the action.
    /// </summary>
    public string? UserRole { get; set; }

    /// <summary>
    /// Type of action performed.
    /// Examples: "Quote.Created", "Booking.Updated", "Driver.Assigned", "OAuth.Updated"
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// Type of entity affected (e.g., "Quote", "Booking", "Driver", "OAuth").
    /// </summary>
    public string EntityType { get; set; } = "";

    /// <summary>
    /// ID of the entity affected.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Additional details about the action (JSON serialized).
    /// Examples: Previous/new values, driver assignment details, etc.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// IP address of the client (for security monitoring).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// HTTP method used (GET, POST, PUT, DELETE).
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Endpoint path that was called.
    /// </summary>
    public string? EndpointPath { get; set; }

    /// <summary>
    /// Result of the action (Success, Failed, Unauthorized, etc.).
    /// </summary>
    public AuditLogResult Result { get; set; } = AuditLogResult.Success;

    /// <summary>
    /// Error message if the action failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of an audited action.
/// </summary>
public enum AuditLogResult
{
    Success = 0,        // Action completed successfully
    Failed = 1,         // Action failed due to error
    Unauthorized = 2,   // User not authenticated
    Forbidden = 3,      // User lacks permissions
    ValidationError = 4 // Input validation failed
}
