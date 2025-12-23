using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public sealed class FileAffiliateRepository : IAffiliateRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized = false;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FileAffiliateRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "affiliates.json");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        await _gate.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_initialized) return;
            
            if (!File.Exists(_filePath))
            {
                await File.WriteAllTextAsync(_filePath, "[]");
            }
            
            _initialized = true;
        }
        finally { _gate.Release(); }
    }

    public async Task<List<Affiliate>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadAllAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task<Affiliate?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(a => a.Id == id);
    }

    public async Task AddAsync(Affiliate affiliate, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();

            // Ensure GUID-based ID for scalability
            if (string.IsNullOrWhiteSpace(affiliate.Id))
            {
                affiliate.Id = Guid.NewGuid().ToString("N");
            }

            list.Add(affiliate);
            await WriteAllAsync(list);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(Affiliate affiliate, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var existing = list.FirstOrDefault(a => a.Id == affiliate.Id);

            if (existing != null)
            {
                // Update all properties except ID
                existing.Name = affiliate.Name;
                existing.PointOfContact = affiliate.PointOfContact;
                existing.Phone = affiliate.Phone;
                existing.Email = affiliate.Email;
                existing.StreetAddress = affiliate.StreetAddress;
                existing.City = affiliate.City;
                existing.State = affiliate.State;

                await WriteAllAsync(list);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var toRemove = list.FirstOrDefault(a => a.Id == id);

            if (toRemove != null)
            {
                list.Remove(toRemove);
                await WriteAllAsync(list);
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<List<Affiliate>> ReadAllAsync()
    {
        // Check if file exists before trying to open it
        if (!File.Exists(_filePath))
        {
            return new List<Affiliate>();
        }
        
        using var fs = File.OpenRead(_filePath);
        var affiliates = await JsonSerializer.DeserializeAsync<List<Affiliate>>(fs, _opts) ?? new();
        
        // Ensure Drivers list is initialized (won't be in storage)
        foreach (var aff in affiliates)
        {
            aff.Drivers ??= new();
        }
        
        return affiliates;
    }

    private async Task WriteAllAsync(List<Affiliate> list)
    {
        // Don't persist drivers - they're stored separately
        foreach (var aff in list)
        {
            aff.Drivers = new();
        }

        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, list, _opts);
    }
}
