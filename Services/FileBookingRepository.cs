using System.Text.Json;
using Bellwood.AdminApi.Models;
using System.Text.Json.Serialization;

namespace Bellwood.AdminApi.Services;

public sealed class FileBookingRepository : IBookingRepository
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
            new BookingStatusFlexConverter()
        }
    };

    public FileBookingRepository(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "bookings.json");
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

    public async Task<BookingRecord> AddAsync(BookingRecord rec, CancellationToken ct = default)
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

    public async Task<BookingRecord?> GetAsync(string id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        return (await ReadAllAsync()).FirstOrDefault(x => x.Id == id);
    }

    public async Task<IReadOnlyList<BookingRecord>> ListAsync(int take = 50, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        return (await ReadAllAsync()).Take(take).ToList();
    }

    public async Task UpdateStatusAsync(string id, BookingStatus status, CancellationToken ct = default)
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

    public async Task UpdateRideStatusAsync(string id, RideStatus rideStatus, BookingStatus bookingStatus, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var rec = list.FirstOrDefault(x => x.Id == id);
            if (rec is null) return;
            
            // Update BOTH statuses - this is the fix for Issue #1
            rec.CurrentRideStatus = rideStatus;
            rec.Status = bookingStatus;
            
            await WriteAllAsync(list);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateDriverAssignmentAsync(string bookingId, string? driverId, string? driverUid, string? driverName, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync(ct);
        try
        {
            var list = await ReadAllAsync();
            var rec = list.FirstOrDefault(x => x.Id == bookingId);
            if (rec is null) return;

            rec.AssignedDriverId = driverId;
            rec.AssignedDriverUid = driverUid;
            rec.AssignedDriverName = driverName;

            // Normalize status if still in requested/confirmed
            if (rec.Status == BookingStatus.Requested || rec.Status == BookingStatus.Confirmed)
            {
                rec.Status = BookingStatus.Scheduled;
            }

            // Initialize RideStatus if not set
            if (!rec.CurrentRideStatus.HasValue)
            {
                rec.CurrentRideStatus = RideStatus.Scheduled;
            }

            await WriteAllAsync(list);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<BookingRecord>> ReadAllAsync()
    {
        // Check if file exists before trying to open it
        if (!File.Exists(_filePath))
        {
            return new List<BookingRecord>();
        }
        
        using var fs = File.OpenRead(_filePath);
        var list = await JsonSerializer.DeserializeAsync<List<BookingRecord>>(fs, _opts) ?? new();
        return list;
    }

    private async Task WriteAllAsync(List<BookingRecord> list)
    {
        using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, list, _opts);
    }
}