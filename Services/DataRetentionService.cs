using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Service for enforcing data retention policies.
/// Phase 3C: GDPR compliance and data lifecycle management.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Clean up old audit logs based on retention policy.
    /// Default: 90 days for audit logs.
    /// </summary>
    Task<int> CleanupOldAuditLogsAsync(int retentionDays = 90, CancellationToken ct = default);

    /// <summary>
    /// Anonymize old bookings (keep for analytics, remove PII).
    /// Default: 365 days before anonymization.
    /// </summary>
    Task<int> AnonymizeOldBookingsAsync(int retentionDays = 365, CancellationToken ct = default);

    /// <summary>
    /// Delete old quotes based on retention policy.
    /// Default: 180 days for quotes.
    /// </summary>
    Task<int> DeleteOldQuotesAsync(int retentionDays = 180, CancellationToken ct = default);

    /// <summary>
    /// Get retention policy summary.
    /// </summary>
    DataRetentionPolicy GetRetentionPolicy();
}

/// <summary>
/// Data retention policy configuration.
/// </summary>
public record DataRetentionPolicy
{
    /// <summary>
    /// Audit logs retention in days.
    /// Requirement: Retain for compliance (90 days minimum).
    /// </summary>
    public int AuditLogRetentionDays { get; init; } = 90;

    /// <summary>
    /// Booking data retention before anonymization.
    /// Requirement: GDPR compliance (keep analytics, remove PII).
    /// </summary>
    public int BookingRetentionDays { get; init; } = 365;

    /// <summary>
    /// Quote data retention before deletion.
    /// Requirement: Clean up old quotes (180 days).
    /// </summary>
    public int QuoteRetentionDays { get; init; } = 180;

    /// <summary>
    /// Payment data retention (must match financial regulations).
    /// Requirement: PCI-DSS compliance (7 years for card data metadata).
    /// </summary>
    public int PaymentDataRetentionDays { get; init; } = 2555; // ~7 years
}

/// <summary>
/// Implementation of data retention service.
/// </summary>
public sealed class DataRetentionService : IDataRetentionService
{
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IBookingRepository _bookingRepo;
    private readonly IQuoteRepository _quoteRepo;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly DataRetentionPolicy _policy;

    public DataRetentionService(
        IAuditLogRepository auditLogRepo,
        IBookingRepository bookingRepo,
        IQuoteRepository quoteRepo,
        ILogger<DataRetentionService> logger,
        IConfiguration configuration)
    {
        _auditLogRepo = auditLogRepo;
        _bookingRepo = bookingRepo;
        _quoteRepo = quoteRepo;
        _logger = logger;

        // Load retention policy from configuration (with defaults)
        _policy = configuration.GetSection("DataRetention").Get<DataRetentionPolicy>()
                  ?? new DataRetentionPolicy();
    }

    public async Task<int> CleanupOldAuditLogsAsync(int retentionDays = 90, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting audit log cleanup with {RetentionDays} day retention", retentionDays);

        try
        {
            var deletedCount = await _auditLogRepo.DeleteOldLogsAsync(retentionDays, ct);

            _logger.LogInformation("Deleted {Count} audit logs older than {Days} days", 
                deletedCount, retentionDays);

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old audit logs");
            throw;
        }
    }

    public async Task<int> AnonymizeOldBookingsAsync(int retentionDays = 365, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting booking anonymization with {RetentionDays} day retention", retentionDays);

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var bookings = await _bookingRepo.ListAsync(10000, ct); // Get all bookings

            var anonymizedCount = 0;

            foreach (var booking in bookings.Where(b => b.CreatedUtc < cutoffDate))
            {
                // For Phase 3C alpha: Log anonymization candidates
                // Future: Implement repository UpdateAsync to persist anonymized data
                _logger.LogInformation(
                    "Booking {BookingId} created {CreatedUtc} would be anonymized (older than {Days} days)",
                    booking.Id, booking.CreatedUtc, retentionDays);
                
                anonymizedCount++;
            }

            if (anonymizedCount > 0)
            {
                _logger.LogWarning(
                    "Phase 3C Alpha: {Count} bookings identified for anonymization. " +
                    "Repository UpdateAsync not yet implemented - anonymization logged only.",
                    anonymizedCount);
            }

            return anonymizedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to anonymize old bookings");
            throw;
        }
    }

    public async Task<int> DeleteOldQuotesAsync(int retentionDays = 180, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting quote deletion with {RetentionDays} day retention", retentionDays);

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var quotes = await _quoteRepo.ListAsync(10000, ct); // Get all quotes

            var deletedCount = 0;

            foreach (var quote in quotes.Where(q => q.CreatedUtc < cutoffDate))
            {
                // For Phase 3C alpha: Log deletion candidates
                // Future: Implement repository DeleteAsync to persist deletions
                _logger.LogInformation(
                    "Quote {QuoteId} created {CreatedUtc} would be deleted (older than {Days} days)",
                    quote.Id, quote.CreatedUtc, retentionDays);
                
                deletedCount++;
            }

            if (deletedCount > 0)
            {
                _logger.LogWarning(
                    "Phase 3C Alpha: {Count} quotes identified for deletion. " +
                    "Repository DeleteAsync not yet implemented - deletion logged only.",
                    deletedCount);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old quotes");
            throw;
        }
    }

    public DataRetentionPolicy GetRetentionPolicy()
    {
        return _policy;
    }
}
