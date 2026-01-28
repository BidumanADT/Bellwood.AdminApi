using System.Text.Json;
using Bellwood.AdminApi.Models;
using System.Text.Json.Serialization;

namespace Bellwood.AdminApi.Services;

public sealed class FileQuoteRepository : IQuoteRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized = false;

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

    public async Task<QuoteRecord> AddAsync(QuoteRecord rec, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
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
    {
        await EnsureInitializedAsync();
        return (await ReadAllAsync()).FirstOrDefault(x => x.Id == id);
    }

    public async Task<IReadOnlyList<QuoteRecord>> ListAsync(int take = 50, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        return (await ReadAllAsync()).Take(take).ToList();
    }

    // Phase Alpha: Update complete quote record (for lifecycle transitions)
    public async Task UpdateAsync(QuoteRecord rec, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var idx = list.FindIndex(x => x.Id == rec.Id);
            if (idx >= 0)
            {
                list[idx] = rec;
                await WriteAllAsync(list);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateStatusAsync(string id, QuoteStatus status, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
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
        // Check if file exists before trying to open it
        if (!File.Exists(_filePath))
        {
            return new List<QuoteRecord>();
        }
        
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
