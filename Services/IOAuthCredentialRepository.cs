using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Phase 2: Repository for managing OAuth client credentials.
/// Credentials are encrypted at rest and cached in memory.
/// Phase 3+ will use these credentials for LimoAnywhere API calls.
/// </summary>
public interface IOAuthCredentialRepository
{
    /// <summary>
    /// Get current OAuth credentials (decrypted).
    /// Returns null if no credentials have been configured.
    /// </summary>
    Task<OAuthClientCredentials?> GetCredentialsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Update OAuth credentials (encrypts before storage).
    /// Triggers cache invalidation.
    /// </summary>
    /// <param name="credentials">New credentials to store</param>
    /// <param name="updatedBy">Admin username who made the change</param>
    Task UpdateCredentialsAsync(
        OAuthClientCredentials credentials, 
        string updatedBy,
        CancellationToken ct = default);
    
    /// <summary>
    /// Check if credentials are configured.
    /// Useful for startup validation.
    /// </summary>
    Task<bool> AreCredentialsConfiguredAsync(CancellationToken ct = default);
}
