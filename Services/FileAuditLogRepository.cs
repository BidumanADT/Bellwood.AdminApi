using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// File-based implementation of audit log repository.
/// Phase 3: JSON storage for audit logs with filtering and pagination.
/// Future: Migrate to SQL Server or dedicated logging platform (e.g., Elasticsearch).
/// </summary>
public sealed class FileAuditLogRepository : IAuditLogRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized = false;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public FileAuditLogRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "audit-logs.json");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        await _gate.WaitAsync();
        try
        {
            if (_initialized) return;
            
            if (!File.Exists(_filePath))
            {
                await File.WriteAllTextAsync(_filePath, "[]");
            }
            
            _initialized = true;
        }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var logs = await ReadAllAsync();
            logs.Insert(0, entry); // Newest first
            await WriteAllAsync(logs);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<AuditLog>> GetLogsAsync(
        string? userId = null,
        string? entityType = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int take = 100,
        int skip = 0,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        
        // Validate pagination parameters
        if (take <= 0 || take > 1000) take = 100;
        if (skip < 0) skip = 0;

        var logs = await ReadAllAsync();

        // Apply filters
        var filtered = logs.AsEnumerable();

        if (!string.IsNullOrEmpty(userId))
        {
            filtered = filtered.Where(l => l.UserId == userId);
        }

        if (!string.IsNullOrEmpty(entityType))
        {
            filtered = filtered.Where(l => 
                l.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(action))
        {
            filtered = filtered.Where(l => 
                l.Action.Contains(action, StringComparison.OrdinalIgnoreCase));
        }

        if (startDate.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp <= endDate.Value);
        }

        // Apply pagination
        return filtered
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public async Task<int> GetCountAsync(
        string? userId = null,
        string? entityType = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        var logs = await ReadAllAsync();
        var filtered = logs.AsEnumerable();

        if (!string.IsNullOrEmpty(userId))
        {
            filtered = filtered.Where(l => l.UserId == userId);
        }

        if (!string.IsNullOrEmpty(entityType))
        {
            filtered = filtered.Where(l => 
                l.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(action))
        {
            filtered = filtered.Where(l => 
                l.Action.Contains(action, StringComparison.OrdinalIgnoreCase));
        }

        if (startDate.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp <= endDate.Value);
        }

        return filtered.Count();
    }

    public async Task<AuditLog?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        
        var logs = await ReadAllAsync();
        return logs.FirstOrDefault(l => l.Id == id);
    }

    public async Task<int> DeleteOldLogsAsync(int retentionDays = 90, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var logs = await ReadAllAsync();
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            var toKeep = logs.Where(l => l.Timestamp >= cutoffDate).ToList();
            var deletedCount = logs.Count - toKeep.Count;
            
            if (deletedCount > 0)
            {
                await WriteAllAsync(toKeep);
            }
            
            return deletedCount;
        }
        finally { _gate.Release(); }
    }

    public async Task<AuditLogStats> GetStatsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        
        var logs = await ReadAllAsync();
        
        return new AuditLogStats
        {
            Count = logs.Count,
            OldestUtc = logs.Any() ? logs.Min(l => l.Timestamp) : null,
            NewestUtc = logs.Any() ? logs.Max(l => l.Timestamp) : null
        };
    }

    public async Task<int> ClearAllAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var logs = await ReadAllAsync();
            var count = logs.Count;
            
            // Clear by writing empty array
            await WriteAllAsync(new List<AuditLog>());
            
            return count;
        }
        finally { _gate.Release(); }
    }

    private async Task<List<AuditLog>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<AuditLog>();
        }
        
        using var fs = File.OpenRead(_filePath);
        var logs = await JsonSerializer.DeserializeAsync<List<AuditLog>>(fs, _opts) ?? new();
        return logs;
    }

    private async Task WriteAllAsync(List<AuditLog> logs)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, logs, _opts);
    }
}
