using System.Text.Json;
using Bellwood.AdminApi.Models;
using System.Text.Json.Serialization;

namespace Bellwood.AdminApi.Services;

public sealed class FileQuoteRepository : IQuoteRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new QuoteStatusFlexConverter() 
        }
    };

    public FileQuoteRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "quotes.json");
        if (!File.Exists(_filePath)) File.WriteAllText(_filePath, "[]");
    }

    public async Task<QuoteRecord> AddAsync(QuoteRecord rec, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            list.Insert(0, rec); // newest first
            await WriteAllAsync(list);
            return rec;
        }
        finally { _gate.Release(); }
    }

    public async Task<QuoteRecord?> GetAsync(string id, CancellationToken ct = default)
        => (await ReadAllAsync()).FirstOrDefault(x => x.Id == id);

    public async Task<IReadOnlyList<QuoteRecord>> ListAsync(int take = 50, CancellationToken ct = default)
        => (await ReadAllAsync()).Take(take).ToList();

    public async Task UpdateStatusAsync(string id, QuoteStatus status, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var rec = list.FirstOrDefault(x => x.Id == id);
            if (rec is null) return;
            rec.Status = status;
            await WriteAllAsync(list);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<QuoteRecord>> ReadAllAsync()
    {
        using var fs = File.OpenRead(_filePath);
        var list = await JsonSerializer.DeserializeAsync<List<QuoteRecord>>(fs, _opts) ?? new();
        return list;
    }

    private async Task WriteAllAsync(List<QuoteRecord> list)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, list, _opts);
    }
}
