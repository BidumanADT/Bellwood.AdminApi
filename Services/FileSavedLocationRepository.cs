using System.Text.Json;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public sealed class FileSavedLocationRepository : ISavedLocationRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public FileSavedLocationRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "locations.json");
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            if (!File.Exists(_filePath))
                await File.WriteAllTextAsync(_filePath, "[]", ct);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SavedLocation>> ListAsync(string userId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            return all.Where(x => x.UserId == userId)
                      .OrderByDescending(x => x.IsFavorite)
                      .ThenByDescending(x => x.UseCount)
                      .ThenByDescending(x => x.ModifiedUtc)
                      .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SavedLocation?> GetAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            return all.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SavedLocation> AddAsync(SavedLocation location, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            location.Id          = Guid.NewGuid();
            location.UseCount    = 0;
            location.CreatedUtc  = DateTime.UtcNow;
            location.ModifiedUtc = DateTime.UtcNow;
            all.Add(location);
            await WriteAllAsync(all, ct);
            return location;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SavedLocation?> UpdateAsync(SavedLocation location, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all      = await ReadAllAsync(ct);
            var existing = all.FirstOrDefault(x => x.Id == location.Id && x.UserId == location.UserId);
            if (existing is null) return null;

            existing.Label       = location.Label;
            existing.Address     = location.Address;
            existing.Latitude    = location.Latitude;
            existing.Longitude   = location.Longitude;
            existing.IsFavorite  = location.IsFavorite;
            existing.UseCount    = location.UseCount;
            existing.ModifiedUtc = DateTime.UtcNow;

            await WriteAllAsync(all, ct);
            return existing;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all     = await ReadAllAsync(ct);
            var removed = all.RemoveAll(x => x.Id == id && x.UserId == userId);
            if (removed == 0) return false;
            await WriteAllAsync(all, ct);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<SavedLocation>> ReadAllAsync(CancellationToken ct)
    {
        try
        {
            using var fs = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<SavedLocation>>(fs, cancellationToken: ct) ?? new();
        }
        catch (FileNotFoundException)
        {
            return new();
        }
    }

    private async Task WriteAllAsync(List<SavedLocation> rows, CancellationToken ct)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, rows, cancellationToken: ct);
    }
}
