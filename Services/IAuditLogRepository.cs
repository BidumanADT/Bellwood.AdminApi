using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Repository interface for audit log storage.
/// Phase 3: Enterprise-grade audit logging for compliance and security.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Add a new audit log entry.
    /// </summary>
    Task AddAsync(AuditLog entry, CancellationToken ct = default);

    /// <summary>
    /// Get audit logs with filtering and pagination.
    /// </summary>
    /// <param name="userId">Filter by user ID (optional)</param>
    /// <param name="entityType">Filter by entity type (optional)</param>
    /// <param name="action">Filter by action (optional)</param>
    /// <param name="startDate">Filter by start date (optional)</param>
    /// <param name="endDate">Filter by end date (optional)</param>
    /// <param name="take">Number of records to return (default: 100, max: 1000)</param>
    /// <param name="skip">Number of records to skip (for pagination)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of audit log entries matching the filters</returns>
    Task<IReadOnlyList<AuditLog>> GetLogsAsync(
        string? userId = null,
        string? entityType = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int take = 100,
        int skip = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Get total count of audit logs (for pagination).
    /// </summary>
    Task<int> GetCountAsync(
        string? userId = null,
        string? entityType = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get audit log by ID.
    /// </summary>
    Task<AuditLog?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Delete audit logs older than the retention period.
    /// Phase 3: Compliance with data retention policies.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain logs (default: 90)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of logs deleted</returns>
    Task<int> DeleteOldLogsAsync(int retentionDays = 90, CancellationToken ct = default);

    /// <summary>
    /// Get audit log statistics.
    /// Alpha: Returns count and oldest/newest timestamps.
    /// </summary>
    Task<AuditLogStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Clear all audit logs (DANGEROUS - requires confirmation).
    /// Alpha: Used for testing/demo purposes only.
    /// Production: Should be heavily restricted or removed.
    /// </summary>
    Task<int> ClearAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Audit log statistics.
/// </summary>
public sealed class AuditLogStats
{
    public int Count { get; set; }
    public DateTime? OldestUtc { get; set; }
    public DateTime? NewestUtc { get; set; }
}
