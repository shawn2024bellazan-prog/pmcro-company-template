using Microsoft.Data.Sqlite;

namespace PMCR.O.SubagentReturn;

public enum DurableReturnState
{
    Received, Verified, Deferred, Dispatching, Dispatched, Closed, ExecutionUncertain
}

public sealed record DurableReturnRecord(string InvocationKey, long Version, DurableReturnState State, string ResultDigest);

/// <summary>
/// SQLite-backed transactional state transitions for a single shared database file.
/// Transition admission uses versioned compare-and-swap and preserves an immutable result digest.
/// Callers must reconcile Dispatching/ExecutionUncertain with the external dispatcher; this
/// store never automatically replays an operation. Production needs storage health, backup,
/// permissions and operational reconciliation beyond this reference implementation.
/// </summary>
public sealed class SqliteSubagentReturnStateStore
{
    private readonly string _connectionString;

    public SqliteSubagentReturnStateStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder {
            DataSource = Path.GetFullPath(databasePath), Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, DefaultTimeout = 30
        }.ToString();
        using var connection = Connect();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS return_state(
            invocation_key TEXT PRIMARY KEY NOT NULL,
            version INTEGER NOT NULL,
            state INTEGER NOT NULL,
            result_digest TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=30000;";
        cmd.ExecuteNonQuery();
        return connection;
    }

    public async Task<DurableReturnRecord?> ReadAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT version,state,result_digest FROM return_state WHERE invocation_key=$key";
        cmd.Parameters.AddWithValue("$key", key);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(key, reader.GetInt64(0), (DurableReturnState)reader.GetInt32(1), reader.GetString(2))
            : null;
    }

    public async Task<DurableReturnRecord?> TryTransitionAsync(
        string key, long? expectedVersion, DurableReturnState next, string resultDigest,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultDigest);
        if (!Enum.IsDefined(next)) throw new ArgumentOutOfRangeException(nameof(next));
        using var c = Connect();
        using var tx = (SqliteTransaction)await c.BeginTransactionAsync(ct);
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        if (expectedVersion is null)
        {
            if (next != DurableReturnState.Received) return null;
            cmd.CommandText = @"INSERT OR IGNORE INTO return_state(invocation_key,version,state,result_digest)
                VALUES($key,1,$state,$digest)";
        }
        else
        {
            using var read = c.CreateCommand();
            read.Transaction = tx;
            read.CommandText = "SELECT state,result_digest FROM return_state WHERE invocation_key=$key AND version=$version";
            read.Parameters.AddWithValue("$key", key);
            read.Parameters.AddWithValue("$version", expectedVersion.Value);
            using var reader = await read.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            var prior = (DurableReturnState)reader.GetInt32(0);
            if (reader.GetString(1) != resultDigest || !IsAllowed(prior, next)) return null;
            await reader.DisposeAsync();
            cmd.CommandText = @"UPDATE return_state SET state=$state,version=version+1
                WHERE invocation_key=$key AND version=$version";
            cmd.Parameters.AddWithValue("$version", expectedVersion.Value);
        }
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$state", (int)next);
        cmd.Parameters.AddWithValue("$digest", resultDigest);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) return null;
        await tx.CommitAsync(ct);
        return new(key, (expectedVersion ?? 0) + 1, next, resultDigest);
    }

    private static bool IsAllowed(DurableReturnState previous, DurableReturnState next) =>
        (previous, next) switch
        {
            (DurableReturnState.Received, DurableReturnState.Verified) => true,
            (DurableReturnState.Received, DurableReturnState.Deferred) => true,
            (DurableReturnState.Deferred, DurableReturnState.Verified) => true,
            (DurableReturnState.Verified, DurableReturnState.Deferred) => true,
            (DurableReturnState.Verified, DurableReturnState.Dispatching) => true,
            (DurableReturnState.Verified, DurableReturnState.Closed) => true,
            (DurableReturnState.Dispatching, DurableReturnState.Dispatched) => true,
            (DurableReturnState.Dispatching, DurableReturnState.ExecutionUncertain) => true,
            (DurableReturnState.ExecutionUncertain, DurableReturnState.Dispatched) => true,
            (DurableReturnState.ExecutionUncertain, DurableReturnState.Closed) => true,
            (DurableReturnState.Dispatched, DurableReturnState.Closed) => true,
            _ => false
        };
}
