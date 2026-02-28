using System.Text.Json;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public sealed class FileBookerRepository : IBookerRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public FileBookerRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "bookers.json");
    }

    private async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            if (!File.Exists(_filePath))
            {
                await File.WriteAllTextAsync(_filePath, "[]", ct);
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BookerProfile?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var all = await ListAsync(int.MaxValue, ct);
        return all.FirstOrDefault(x => x.UserId == userId);
    }

    public async Task<IReadOnlyList<BookerProfile>> ListAsync(int take = 100, CancellationToken ct = default)
    {
        take = (take <= 0 || take > 500) ? 100 : take;

        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var all = await ReadAllAsync(ct);
            return all.OrderByDescending(x => x.ModifiedUtc).Take(take).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BookerProfile> UpsertAsync(BookerProfile profile, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await _gate.WaitAsync(ct);

        try
        {
            var all = await ReadAllAsync(ct);
            var existing = all.FirstOrDefault(x => x.UserId == profile.UserId);

            if (existing is null)
            {
                profile.CreatedUtc = DateTime.UtcNow;
                profile.ModifiedUtc = DateTime.UtcNow;
                all.Add(profile);
            }
            else
            {
                existing.FirstName = profile.FirstName;
                existing.LastName = profile.LastName;
                existing.PhoneNumber = profile.PhoneNumber;
                existing.EmailAddress = profile.EmailAddress;
                existing.ModifiedUtc = DateTime.UtcNow;
                profile = existing;
            }

            await WriteAllAsync(all, ct);
            return profile;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<BookerProfile>> ReadAllAsync(CancellationToken ct)
    {
        using var fs = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<BookerProfile>>(fs, cancellationToken: ct) ?? new();
    }

    private async Task WriteAllAsync(List<BookerProfile> rows, CancellationToken ct)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, rows, cancellationToken: ct);
    }
}
