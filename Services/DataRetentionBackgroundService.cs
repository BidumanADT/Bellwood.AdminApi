namespace Bellwood.AdminApi.Services;

/// <summary>
/// Background service that runs data retention cleanup tasks on a schedule.
/// Phase 3C: Automated data lifecycle management for GDPR compliance.
/// </summary>
public sealed class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRetentionBackgroundService> _logger;

    // Run cleanup daily at 2 AM UTC
    private readonly TimeSpan _runInterval = TimeSpan.FromHours(24);
    private readonly TimeSpan _targetTimeOfDay = TimeSpan.FromHours(2); // 2 AM UTC

    public DataRetentionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DataRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Retention Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate time until next 2 AM UTC
                var now = DateTime.UtcNow;
                var nextRun = CalculateNextRunTime(now);
                var delay = nextRun - now;

                _logger.LogInformation(
                    "Next data retention cleanup scheduled for {NextRun} UTC (in {Hours:F1} hours)",
                    nextRun, delay.TotalHours);

                // Wait until next scheduled run
                await Task.Delay(delay, stoppingToken);

                // Run cleanup tasks
                await RunDataRetentionTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                _logger.LogInformation("Data Retention Background Service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Data Retention Background Service");
                // Continue running - don't crash the service
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Retry in 30 minutes
            }
        }

        _logger.LogInformation("Data Retention Background Service stopped");
    }

    private DateTime CalculateNextRunTime(DateTime now)
    {
        var today = now.Date;
        var targetToday = today.Add(_targetTimeOfDay);

        if (now < targetToday)
        {
            // Target time hasn't passed today - run today
            return targetToday;
        }
        else
        {
            // Target time already passed - run tomorrow
            return targetToday.AddDays(1);
        }
    }

    private async Task RunDataRetentionTasksAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting scheduled data retention cleanup tasks");

        using var scope = _serviceProvider.CreateScope();
        var retentionService = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
        var auditLogger = scope.ServiceProvider.GetRequiredService<AuditLogger>();

        var results = new
        {
            startTime = DateTime.UtcNow,
            auditLogsDeleted = 0,
            bookingsAnonymized = 0,
            quotesDeleted = 0,
            errors = new List<string>()
        };

        try
        {
            // Task 1: Clean up old audit logs (90 days)
            _logger.LogInformation("Task 1/3: Cleaning up audit logs older than 90 days");
            results = results with 
            { 
                auditLogsDeleted = await retentionService.CleanupOldAuditLogsAsync(90, ct) 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup audit logs");
            results.errors.Add($"Audit log cleanup failed: {ex.Message}");
        }

        try
        {
            // Task 2: Anonymize old bookings (365 days)
            _logger.LogInformation("Task 2/3: Anonymizing bookings older than 365 days");
            results = results with 
            { 
                bookingsAnonymized = await retentionService.AnonymizeOldBookingsAsync(365, ct) 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to anonymize bookings");
            results.errors.Add($"Booking anonymization failed: {ex.Message}");
        }

        try
        {
            // Task 3: Delete old quotes (180 days)
            _logger.LogInformation("Task 3/3: Deleting quotes older than 180 days");
            results = results with 
            { 
                quotesDeleted = await retentionService.DeleteOldQuotesAsync(180, ct) 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete quotes");
            results.errors.Add($"Quote deletion failed: {ex.Message}");
        }

        var duration = DateTime.UtcNow - results.startTime;

        // Log completion summary
        _logger.LogInformation(
            "Data retention cleanup completed in {Duration:F1}s - " +
            "Audit logs deleted: {AuditLogs}, Bookings anonymized: {Bookings}, Quotes deleted: {Quotes}, Errors: {ErrorCount}",
            duration.TotalSeconds, results.auditLogsDeleted, results.bookingsAnonymized, 
            results.quotesDeleted, results.errors.Count);

        // Audit log the retention cleanup
        try
        {
            await auditLogger.LogSystemActionAsync(
                AuditActions.DataRetentionCleanup,
                "System",
                details: new
                {
                    auditLogsDeleted = results.auditLogsDeleted,
                    bookingsAnonymized = results.bookingsAnonymized,
                    quotesDeleted = results.quotesDeleted,
                    durationSeconds = duration.TotalSeconds,
                    errors = results.errors.Count > 0 ? results.errors : null
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for data retention cleanup");
        }
    }
}
