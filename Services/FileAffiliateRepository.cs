using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public sealed class FileAffiliateRepository : IAffiliateRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

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
        if (!File.Exists(_filePath)) File.WriteAllText(_filePath, "[]");
    }

    public async Task<List<Affiliate>> GetAllAsync(CancellationToken ct = default)
    {
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
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();

            // Ensure GUID-based ID for scalability
            if (string.IsNullOrWhiteSpace(affiliate.Id))
            {
                affiliate.Id = Guid.NewGuid().ToString("N");
            }

            // Clear drivers list before persisting (drivers stored separately)
            var driversToAdd = affiliate.Drivers?.ToList() ?? new();
            affiliate.Drivers = new();

            list.Add(affiliate);
            await WriteAllAsync(list);

            // Restore drivers list for the returned object
            affiliate.Drivers = driversToAdd;
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(Affiliate affiliate, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var existing = list.FirstOrDefault(a => a.Id == affiliate.Id);
            if (existing is null) return;

            // Preserve any drivers reference but don't persist it
            var driversRef = affiliate.Drivers;
            affiliate.Drivers = new();

            list.Remove(existing);
            list.Add(affiliate);
            await WriteAllAsync(list);

            // Restore drivers list
            affiliate.Drivers = driversRef;
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var toRemove = list.FirstOrDefault(a => a.Id == id);
            if (toRemove is not null)
            {
                list.Remove(toRemove);
                await WriteAllAsync(list);
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<List<Affiliate>> ReadAllAsync()
    {
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
