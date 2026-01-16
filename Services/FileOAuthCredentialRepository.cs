using System.Text.Json;
using Bellwood.AdminApi.Models;
using Microsoft.AspNetCore.DataProtection;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Phase 2: File-based OAuth credential repository with encryption.
/// Uses ASP.NET Core Data Protection API for encryption at rest.
/// Consistent with other file-based repositories (bookings, quotes, etc.).
/// </summary>
public class FileOAuthCredentialRepository : IOAuthCredentialRepository
{
    private readonly string _filePath = Path.Combine("App_Data", "oauth-credentials.json");
    private readonly IDataProtector _dataProtector;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized = false;

    // Data Protection purpose string - unique identifier for this protection scope
    private const string ProtectionPurpose = "Bellwood.OAuthCredentials.v1";

    public FileOAuthCredentialRepository(IDataProtectionProvider dataProtectionProvider)
    {
        // Create a data protector with a specific purpose string
        // This ensures keys are scoped to OAuth credentials only
        _dataProtector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
    }

    /// <summary>
    /// Ensure file and directory exist (lazy initialization).
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _gate.WaitAsync();
        try
        {
            if (_initialized) return;

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(_filePath))
            {
                // Create empty credentials file (null state)
                await File.WriteAllTextAsync(_filePath, "null");
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Get current OAuth credentials (decrypted).
    /// Returns null if no credentials configured.
    /// </summary>
    public async Task<OAuthClientCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        await _gate.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            
            // Handle null/empty file
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                return null;

            var encrypted = JsonSerializer.Deserialize<EncryptedCredentials>(json);
            if (encrypted == null) return null;

            // Decrypt the client secret
            var decryptedSecret = _dataProtector.Unprotect(encrypted.EncryptedClientSecret);

            return new OAuthClientCredentials
            {
                Id = encrypted.Id,
                ClientId = encrypted.ClientId,
                ClientSecret = decryptedSecret,
                LastUpdatedUtc = encrypted.LastUpdatedUtc,
                LastUpdatedBy = encrypted.LastUpdatedBy,
                Description = encrypted.Description
            };
        }
        catch (Exception ex)
        {
            // Log error but don't expose encryption details
            Console.WriteLine($"?? Error reading OAuth credentials: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Update OAuth credentials (encrypts before storage).
    /// </summary>
    public async Task UpdateCredentialsAsync(
        OAuthClientCredentials credentials, 
        string updatedBy,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        await _gate.WaitAsync(ct);
        try
        {
            // Encrypt the client secret before storage
            var encryptedSecret = _dataProtector.Protect(credentials.ClientSecret);

            var encrypted = new EncryptedCredentials
            {
                Id = credentials.Id,
                ClientId = credentials.ClientId,
                EncryptedClientSecret = encryptedSecret,
                LastUpdatedUtc = DateTime.UtcNow,
                LastUpdatedBy = updatedBy,
                Description = credentials.Description
            };

            var json = JsonSerializer.Serialize(encrypted, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, json, ct);

            Console.WriteLine($"? OAuth credentials updated by {updatedBy}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Check if credentials are configured.
    /// </summary>
    public async Task<bool> AreCredentialsConfiguredAsync(CancellationToken ct = default)
    {
        var creds = await GetCredentialsAsync(ct);
        return creds != null && 
               !string.IsNullOrWhiteSpace(creds.ClientId) && 
               !string.IsNullOrWhiteSpace(creds.ClientSecret);
    }

    /// <summary>
    /// Internal storage model with encrypted secret.
    /// Never exposed outside this class.
    /// </summary>
    private class EncryptedCredentials
    {
        public string Id { get; set; } = "default";
        public string ClientId { get; set; } = "";
        public string EncryptedClientSecret { get; set; } = ""; // Encrypted by Data Protection API
        public DateTime LastUpdatedUtc { get; set; }
        public string? LastUpdatedBy { get; set; }
        public string? Description { get; set; }
    }
}
