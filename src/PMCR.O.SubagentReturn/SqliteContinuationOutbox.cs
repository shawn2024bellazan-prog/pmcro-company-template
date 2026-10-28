using Microsoft.Data.Sqlite;

namespace PMCR.O.SubagentReturn;

/// <summary>
/// Transactional outbox reservation; caller policy must authorize BEFORE ReserveAsync.
/// ReserveAsync is not a dispatcher, and crashes after reservation require external
/// reconciliation rather than automatic replay. This reference does not supply
/// policy authorization or invoke external operations.
/// </summary>
public enum DurableOutboxStatus { Deferred, Dispatching, ExecutionUncertain, Dispatched, Closed }

public sealed record DurableOutboxRecord(
    string IdempotencyKey, long Version, DurableOutboxStatus Status,
    string ResultDigest, string SeedId, string? ExternalOperationId);

public sealed class SqliteContinuationOutbox
{
    private readonly string _cs;
    public SqliteContinuationOutbox(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _cs = new SqliteConnectionStringBuilder {
            DataSource = Path.GetFullPath(databasePath), Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, DefaultTimeout = 30
        }.ToString();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS continuation_outbox(
            key TEXT PRIMARY KEY, version INTEGER NOT NULL, status INTEGER NOT NULL,
            digest TEXT NOT NULL, seed TEXT NOT NULL, external_id TEXT NULL)";
        cmd.ExecuteNonQuery();
    }
    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA busy_timeout=30000;";
        cmd.ExecuteNonQuery(); return c;
    }
    public async Task<DurableOutboxRecord?> ReadAsync(string key, CancellationToken ct = default)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT version,status,digest,seed,external_id FROM continuation_outbox WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct)
            ? new(key, rd.GetInt64(0), (DurableOutboxStatus)rd.GetInt32(1), rd.GetString(2),
                rd.GetString(3), rd.IsDBNull(4) ? null : rd.GetString(4)) : null;
    }
    public async Task<DurableOutboxRecord?> AdmitAsync(string key, string digest, string seed, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO continuation_outbox(key,version,status,digest,seed)
            VALUES($k,1,$s,$d,$seed)";
        cmd.Parameters.AddWithValue("$k", key);cmd.Parameters.AddWithValue("$s", (int)DurableOutboxStatus.Deferred);
        cmd.Parameters.AddWithValue("$d", digest);cmd.Parameters.AddWithValue("$seed", seed);
        return await cmd.ExecuteNonQueryAsync(ct) == 1
            ? new(key, 1, DurableOutboxStatus.Deferred, digest, seed, null) : null;
    }
    public Task<DurableOutboxRecord?> ReserveAsync(string key, long version, CancellationToken ct = default) =>
        TransitionAsync(key, version, DurableOutboxStatus.Deferred, DurableOutboxStatus.Dispatching, null, ct);
    public Task<DurableOutboxRecord?> MarkUncertainAsync(string key, long version, CancellationToken ct = default) =>
        TransitionAsync(key, version, DurableOutboxStatus.Dispatching, DurableOutboxStatus.ExecutionUncertain, null, ct);
    public Task<DurableOutboxRecord?> ReconcileAsync(
        string key, long version, string externalOperationId, bool dispatched, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalOperationId);
        return TransitionAsync(key, version, DurableOutboxStatus.ExecutionUncertain,
            dispatched ? DurableOutboxStatus.Dispatched : DurableOutboxStatus.Closed, externalOperationId, ct);
    }
    private async Task<DurableOutboxRecord?> TransitionAsync(string key, long version, DurableOutboxStatus required,
        DurableOutboxStatus next, string? externalId, CancellationToken ct)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE continuation_outbox SET version=version+1,status=$next,
            external_id=COALESCE($ext,external_id) WHERE key=$k AND version=$v AND status=$prior";
        cmd.Parameters.AddWithValue("$next", (int)next);cmd.Parameters.AddWithValue("$ext", (object?)externalId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$k", key);cmd.Parameters.AddWithValue("$v", version);
        cmd.Parameters.AddWithValue("$prior", (int)required);
        return await cmd.ExecuteNonQueryAsync(ct) == 1 ? await ReadAsync(key, ct) : null;
    }
}
