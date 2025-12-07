using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Dedicated driver repository that stores drivers in a separate file for scalability.
/// This design supports hundreds of affiliates and thousands of drivers efficiently.
/// </summary>
public sealed class FileDriverRepository : IDriverRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileDriverRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "drivers.json");
        if (!File.Exists(_filePath)) File.WriteAllText(_filePath, "[]");
    }

    public async Task<List<Driver>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadAllAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task<List<Driver>> GetByAffiliateIdAsync(string affiliateId, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.Where(d => d.AffiliateId == affiliateId).ToList();
    }

    public async Task<Driver?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(d => d.Id == id);
    }

    public async Task<Driver?> GetByUserUidAsync(string userUid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userUid)) return null;

        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(d => d.UserUid == userUid);
    }

    public async Task<bool> IsUserUidUniqueAsync(string userUid, string? excludeDriverId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userUid)) return true; // Empty UserUid is allowed (though not for assignment)

        var all = await GetAllAsync(ct);
        var existing = all.FirstOrDefault(d => d.UserUid == userUid);
        
        if (existing is null) return true;
        if (excludeDriverId is not null && existing.Id == excludeDriverId) return true;
        
        return false;
    }

    public async Task AddAsync(Driver driver, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            
            // Ensure GUID-based ID for scalability
            if (string.IsNullOrWhiteSpace(driver.Id))
            {
                driver.Id = Guid.NewGuid().ToString("N");
            }

            list.Add(driver);
            await WriteAllAsync(list);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(Driver driver, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var existing = list.FirstOrDefault(d => d.Id == driver.Id);
            if (existing is not null)
            {
                list.Remove(existing);
                list.Add(driver);
                await WriteAllAsync(list);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var toRemove = list.FirstOrDefault(d => d.Id == id);
            if (toRemove is not null)
            {
                list.Remove(toRemove);
                await WriteAllAsync(list);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteByAffiliateIdAsync(string affiliateId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var toRemove = list.Where(d => d.AffiliateId == affiliateId).ToList();
            foreach (var driver in toRemove)
            {
                list.Remove(driver);
            }
            await WriteAllAsync(list);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<Driver>> ReadAllAsync()
    {
        using var fs = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<Driver>>(fs, _opts) ?? new();
    }

    private async Task WriteAllAsync(List<Driver> list)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, list, _opts);
    }
}
