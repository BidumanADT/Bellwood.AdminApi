using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data fields.
/// Phase 3C: Production-grade data protection for billing and payment information.
/// </summary>
public interface ISensitiveDataProtector
{
    /// <summary>
    /// Encrypt sensitive string data.
    /// </summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypt sensitive string data.
    /// </summary>
    string Unprotect(string ciphertext);

    /// <summary>
    /// Encrypt sensitive object to JSON and protect.
    /// </summary>
    string ProtectObject<T>(T obj) where T : class;

    /// <summary>
    /// Unprotect and deserialize sensitive object from JSON.
    /// </summary>
    T? UnprotectObject<T>(string ciphertext) where T : class;

    /// <summary>
    /// Check if data is protected (encrypted).
    /// </summary>
    bool IsProtected(string? data);
}

/// <summary>
/// Implementation using ASP.NET Core Data Protection API.
/// </summary>
public sealed class SensitiveDataProtector : ISensitiveDataProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger<SensitiveDataProtector> _logger;

    // Prefix to identify protected data
    private const string ProtectedPrefix = "ENC:";

    public SensitiveDataProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SensitiveDataProtector> logger)
    {
        // Create a purpose-specific protector for sensitive data
        // This ensures keys are isolated from OAuth credential keys
        _protector = dataProtectionProvider.CreateProtector("Bellwood.SensitiveData.v1");
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
            var encrypted = _protector.Protect(plaintext);
            return $"{ProtectedPrefix}{encrypted}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect sensitive data");
            throw new InvalidOperationException("Data protection failed", ex);
        }
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        if (!ciphertext.StartsWith(ProtectedPrefix))
        {
            // Data not protected - return as-is (legacy data)
            _logger.LogWarning("Attempting to unprotect data without protection prefix");
            return ciphertext;
        }

        try
        {
            var encryptedData = ciphertext.Substring(ProtectedPrefix.Length);
            return _protector.Unprotect(encryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect sensitive data");
            throw new InvalidOperationException("Data unprotection failed", ex);
        }
    }

    public string ProtectObject<T>(T obj) where T : class
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        try
        {
            var json = JsonSerializer.Serialize(obj);
            return Protect(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect object of type {Type}", typeof(T).Name);
            throw new InvalidOperationException("Object protection failed", ex);
        }
    }

    public T? UnprotectObject<T>(string ciphertext) where T : class
    {
        if (string.IsNullOrEmpty(ciphertext))
            return null;

        try
        {
            var json = Unprotect(ciphertext);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect object of type {Type}", typeof(T).Name);
            throw new InvalidOperationException("Object unprotection failed", ex);
        }
    }

    public bool IsProtected(string? data)
    {
        return !string.IsNullOrEmpty(data) && data.StartsWith(ProtectedPrefix);
    }
}
