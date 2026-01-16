namespace Bellwood.AdminApi.Models;

/// <summary>
/// Phase 2: OAuth client credentials for LimoAnywhere API integration.
/// Credentials are encrypted at rest using ASP.NET Core Data Protection API.
/// Phase 3+ will use these credentials to call LimoAnywhere Operator/Customer APIs.
/// </summary>
public class OAuthClientCredentials
{
    /// <summary>
    /// Unique identifier for this credential set.
    /// Currently only one set is stored, but designed for future multi-credential support.
    /// </summary>
    public string Id { get; set; } = "default";
    
    /// <summary>
    /// OAuth Client ID (public identifier).
    /// NOT encrypted - safe to display in logs/UI.
    /// </summary>
    public string ClientId { get; set; } = "";
    
    /// <summary>
    /// OAuth Client Secret (sensitive).
    /// Encrypted before storage using Data Protection API.
    /// Never returned unmasked to non-admin users.
    /// </summary>
    public string ClientSecret { get; set; } = "";
    
    /// <summary>
    /// When the credentials were last updated (UTC).
    /// Used for cache invalidation and audit logging.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who last updated the credentials (admin username).
    /// For audit trail purposes.
    /// </summary>
    public string? LastUpdatedBy { get; set; }
    
    /// <summary>
    /// Optional: Credential description/notes.
    /// Example: "Production LA credentials" or "Staging environment"
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Phase 2: DTO for retrieving OAuth credentials (with masked secret).
/// Client secret is never returned in full for security.
/// </summary>
public class OAuthCredentialsResponseDto
{
    public string ClientId { get; set; } = "";
    public string ClientSecretMasked { get; set; } = "********"; // Always masked
    public DateTime LastUpdatedUtc { get; set; }
    public string? LastUpdatedBy { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Phase 2: DTO for updating OAuth credentials.
/// Both fields required when updating.
/// </summary>
public class UpdateOAuthCredentialsRequest
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string? Description { get; set; }
}
