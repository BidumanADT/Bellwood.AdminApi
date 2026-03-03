using System.Text.Json;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public sealed class FileSavedPassengerRepository : ISavedPassengerRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public FileSavedPassengerRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "passengers.json");
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

    public async Task<IReadOnlyList<SavedPassenger>> ListAsync(string userId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            return all.Where(x => x.UserId == userId)
                      .OrderByDescending(x => x.ModifiedUtc)
                      .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SavedPassenger?> GetAsync(string userId, Guid id, CancellationToken ct = default)
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

    public async Task<SavedPassenger> AddAsync(SavedPassenger passenger, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            passenger.Id          = Guid.NewGuid();
            passenger.CreatedUtc  = DateTime.UtcNow;
            passenger.ModifiedUtc = DateTime.UtcNow;
            all.Add(passenger);
            await WriteAllAsync(all, ct);
            return passenger;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SavedPassenger?> UpdateAsync(SavedPassenger passenger, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all      = await ReadAllAsync(ct);
            var existing = all.FirstOrDefault(x => x.Id == passenger.Id && x.UserId == passenger.UserId);
            if (existing is null) return null;

            existing.FirstName    = passenger.FirstName;
            existing.LastName     = passenger.LastName;
            existing.PhoneNumber  = passenger.PhoneNumber;
            existing.EmailAddress = passenger.EmailAddress;
            existing.ModifiedUtc  = DateTime.UtcNow;

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

    private async Task<List<SavedPassenger>> ReadAllAsync(CancellationToken ct)
    {
        try
        {
            using var fs = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<SavedPassenger>>(fs, cancellationToken: ct) ?? new();
        }
        catch (FileNotFoundException)
        {
            return new();
        }
    }

    private async Task WriteAllAsync(List<SavedPassenger> rows, CancellationToken ct)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, rows, cancellationToken: ct);
    }
}
