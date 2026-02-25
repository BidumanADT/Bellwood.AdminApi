using Bellwood.AdminApi.Models;
using Microsoft.Data.Sqlite;

namespace Bellwood.AdminApi.Services;

public sealed class SqliteAuditEventRepository : IAuditEventRepository
{
    private readonly string _connectionString;

    public SqliteAuditEventRepository(IHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "audit-events.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureCreated();
    }

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO AuditEvent
(Id, TimestampUtc, ActorUserId, Action, TargetType, TargetId, Result, CorrelationId, IpAddress, UserAgent, MetadataJson)
VALUES
($Id, $TimestampUtc, $ActorUserId, $Action, $TargetType, $TargetId, $Result, $CorrelationId, $IpAddress, $UserAgent, $MetadataJson);";

        cmd.Parameters.AddWithValue("$Id", auditEvent.Id);
        cmd.Parameters.AddWithValue("$TimestampUtc", auditEvent.TimestampUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$ActorUserId", (object?)auditEvent.ActorUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$Action", auditEvent.Action);
        cmd.Parameters.AddWithValue("$TargetType", auditEvent.TargetType);
        cmd.Parameters.AddWithValue("$TargetId", (object?)auditEvent.TargetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$Result", auditEvent.Result);
        cmd.Parameters.AddWithValue("$CorrelationId", (object?)auditEvent.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$IpAddress", (object?)auditEvent.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$UserAgent", (object?)auditEvent.UserAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$MetadataJson", (object?)auditEvent.MetadataJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1;";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) == 1;
    }

    private void EnsureCreated()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS AuditEvent (
  Id TEXT PRIMARY KEY,
  TimestampUtc TEXT NOT NULL,
  ActorUserId TEXT NULL,
  Action TEXT NOT NULL,
  TargetType TEXT NOT NULL,
  TargetId TEXT NULL,
  Result TEXT NOT NULL,
  CorrelationId TEXT NULL,
  IpAddress TEXT NULL,
  UserAgent TEXT NULL,
  MetadataJson TEXT NULL
);";
        cmd.ExecuteNonQuery();
    }
}
