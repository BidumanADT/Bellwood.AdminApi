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
        Converters = { new JsonStringEnumConverter() }
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
            list.Add(affiliate);
            await WriteAllAsync(list);
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

            list.Remove(existing);
            list.Add(affiliate);
            await WriteAllAsync(list);
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
        return await JsonSerializer.DeserializeAsync<List<Affiliate>>(fs, _opts) ?? new();
    }

    private async Task WriteAllAsync(List<Affiliate> list)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, list, _opts);
    }
}
