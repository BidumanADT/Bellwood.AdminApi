namespace Bellwood.AdminApi.Services;

/// <summary>
/// Service interface for LimoAnywhere API integration.
/// Phase 4: Stub implementation - actual integration deferred to later phase.
/// </summary>
public interface ILimoAnywhereService
{
    /// <summary>
    /// Get customer details from LimoAnywhere.
    /// </summary>
    /// <param name="customerId">LimoAnywhere customer ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Customer details or null if not found</returns>
    Task<LimoAnywhereCustomer?> GetCustomerAsync(string customerId, CancellationToken ct = default);

    /// <summary>
    /// Get operator details from LimoAnywhere.
    /// </summary>
    /// <param name="operatorId">LimoAnywhere operator ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Operator details or null if not found</returns>
    Task<LimoAnywhereOperator?> GetOperatorAsync(string operatorId, CancellationToken ct = default);

    /// <summary>
    /// Import ride history from LimoAnywhere.
    /// </summary>
    /// <param name="customerId">LimoAnywhere customer ID</param>
    /// <param name="startDate">Start date for history import</param>
    /// <param name="endDate">End date for history import</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of rides imported</returns>
    Task<int> ImportRideHistoryAsync(
        string customerId, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken ct = default);

    /// <summary>
    /// Sync booking to LimoAnywhere.
    /// </summary>
    /// <param name="bookingId">Bellwood booking ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>LimoAnywhere reservation ID</returns>
    Task<string?> SyncBookingAsync(string bookingId, CancellationToken ct = default);

    /// <summary>
    /// Check API connectivity and credentials.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if API is accessible and credentials are valid</returns>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

/// <summary>
/// LimoAnywhere customer data model.
/// Phase 4: Stub model - will be refined when actual integration is implemented.
/// </summary>
public record LimoAnywhereCustomer
{
    public string CustomerId { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string CompanyName { get; init; } = "";
}

/// <summary>
/// LimoAnywhere operator data model.
/// Phase 4: Stub model - will be refined when actual integration is implemented.
/// </summary>
public record LimoAnywhereOperator
{
    public string OperatorId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public bool IsActive { get; init; } = true;
}
