using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PMCR.O.Core;

namespace PMCR.O.SubagentReturn;

/// <summary>
/// Transactional, persistent hash-linked Cognitive Trail observations. Hashes
/// detect accidental or unauthorized modification when compared against a
/// separately trusted head. A database administrator able to rewrite the full
/// chain can recompute hashes: production must anchor each head externally.
/// </summary>
public sealed class SqliteHashChainedTrailLedger
{
    private readonly string _cs;
    public SqliteHashChainedTrailLedger(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _cs = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false, DefaultTimeout = 30 }.ToString();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS trail_ledger(
            run_id TEXT NOT NULL, seq INTEGER NOT NULL, frame_id TEXT NOT NULL,
            payload TEXT NOT NULL, prior_hash TEXT NOT NULL, head_hash TEXT NOT NULL,
            PRIMARY KEY(run_id,seq), UNIQUE(run_id,frame_id))";
        cmd.ExecuteNonQuery();
    }
    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=30000;";
        pragma.ExecuteNonQuery();
        return c;
    }

    public async Task<string> AppendAsync(EnterpriseTrailFrame frame, CancellationToken ct = default)
    {
        if (!EnterpriseTrailFrameValidator.Validate(frame).IsValid)
            throw new ArgumentException("Trail frame failed contract validation.", nameof(frame));
        using var conn = Open();
        using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
        long seq = 1;
        string previous = "GENESIS";
        using (var prior = conn.CreateCommand())
        {
            prior.Transaction = tx;
            prior.CommandText = @"SELECT seq,head_hash FROM trail_ledger
                WHERE run_id=$run ORDER BY seq DESC LIMIT 1";
            prior.Parameters.AddWithValue("$run", frame.RunId);
            using var reader = await prior.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                seq = reader.GetInt64(0) + 1;
                previous = reader.GetString(1);
            }
        }
        var json = JsonSerializer.Serialize(frame with { Sequence = seq });
        var hash = Hash(seq, previous, json);
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO trail_ledger(run_id,seq,frame_id,payload,prior_hash,head_hash)
            VALUES($run,$seq,$id,$json,$prior,$head)";
        cmd.Parameters.AddWithValue("$run", frame.RunId);
        cmd.Parameters.AddWithValue("$seq", seq);
        cmd.Parameters.AddWithValue("$id", frame.FrameId);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.Parameters.AddWithValue("$prior", previous);
        cmd.Parameters.AddWithValue("$head", hash);
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return hash;
    }

    public async Task<bool> VerifyAsync(string runId, string? trustedHead = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT seq,payload,prior_hash,head_hash FROM trail_ledger
            WHERE run_id=$run ORDER BY seq";
        cmd.Parameters.AddWithValue("$run", runId);
        using var rd = await cmd.ExecuteReaderAsync(ct);
        long expected = 1;
        string prior = "GENESIS";
        while (await rd.ReadAsync(ct))
        {
            var seq = rd.GetInt64(0);
            var payload = rd.GetString(1);
            if (seq != expected++ || rd.GetString(2) != prior ||
                rd.GetString(3) != Hash(seq, prior, payload)) return false;
            var frame = JsonSerializer.Deserialize<EnterpriseTrailFrame>(payload);
            if (frame is null || frame.RunId != runId || frame.Sequence != seq ||
                !EnterpriseTrailFrameValidator.Validate(frame).IsValid) return false;
            prior = rd.GetString(3);
        }
        return trustedHead is null || trustedHead == prior;
    }

    private static string Hash(long seq, string previous, string payload)
    {
        var input = seq.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
            previous.Length + ":" + previous + payload.Length + ":" + payload;
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
