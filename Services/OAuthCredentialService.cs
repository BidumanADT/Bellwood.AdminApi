using Bellwood.AdminApi.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Phase 2: OAuth credential service with in-memory caching.
/// Wraps the repository to provide fast access to frequently-used credentials.
/// Cache is invalidated on credential updates.
/// Phase 3+ will use this service for all LimoAnywhere API calls.
/// </summary>
public class OAuthCredentialService
{
    private readonly IOAuthCredentialRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OAuthCredentialService> _logger;
    
    private const string CacheKey = "OAuthCredentials";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public OAuthCredentialService(
        IOAuthCredentialRepository repository,
        IMemoryCache cache,
        ILogger<OAuthCredentialService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get current OAuth credentials (cached).
    /// Returns null if not configured.
    /// </summary>
    public async Task<OAuthClientCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        // Try cache first
        if (_cache.TryGetValue(CacheKey, out OAuthClientCredentials? cached))
        {
            _logger.LogDebug("OAuth credentials retrieved from cache");
            return cached;
        }

        // Cache miss - load from repository
        var credentials = await _repository.GetCredentialsAsync(ct);
        
        if (credentials != null)
        {
            // Cache for future requests
            _cache.Set(CacheKey, credentials, CacheDuration);
            _logger.LogDebug("OAuth credentials loaded from repository and cached");
        }
        else
        {
            _logger.LogWarning("OAuth credentials not configured");
        }

        return credentials;
    }

    /// <summary>
    /// Update OAuth credentials and invalidate cache.
    /// </summary>
    public async Task UpdateCredentialsAsync(
        OAuthClientCredentials credentials,
        string updatedBy,
        CancellationToken ct = default)
    {
        // Update repository (encrypts before storage)
        await _repository.UpdateCredentialsAsync(credentials, updatedBy, ct);

        // Invalidate cache to force reload on next access
        _cache.Remove(CacheKey);
        
        _logger.LogInformation("OAuth credentials updated by {UpdatedBy} and cache invalidated", updatedBy);
    }

    /// <summary>
    /// Check if credentials are configured (bypasses cache for freshness).
    /// </summary>
    public async Task<bool> AreCredentialsConfiguredAsync(CancellationToken ct = default)
    {
        return await _repository.AreCredentialsConfiguredAsync(ct);
    }

    /// <summary>
    /// Force cache refresh (useful for testing or after manual file edits).
    /// </summary>
    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("OAuth credentials cache manually invalidated");
    }

    /// <summary>
    /// Phase 3+: Get access token for LimoAnywhere API calls.
    /// TODO: Implement OAuth2 token exchange using stored credentials.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // TODO: Phase 3 - Implement OAuth2 token exchange
        // 1. Get credentials from cache/repository
        // 2. Call LimoAnywhere OAuth2 token endpoint
        // 3. Cache the access token (with expiration)
        // 4. Return access token for API calls
        
        _logger.LogWarning("GetAccessTokenAsync not yet implemented (Phase 3)");
        return null;
    }
}
