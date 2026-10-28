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

/// <summary>Host-attested, immutable authorization facts bound to one result and caller.
 /// The issuer and verifier must be independent of child-supplied messages.</summary>
public sealed record DurableAuthorizationSnapshot(
    string IdempotencyKey, string ResultDigest, string SeedId,
    string PolicyScope, string PolicyVersion,
    string CheckerAgentId, string CheckerFrameId, string CheckerEvidenceDigest,
    string CallerAgentId, string RunId, string CorrelationId,
    string AuthorizationEvidenceRef);

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
            digest TEXT NOT NULL, seed TEXT NOT NULL, external_id TEXT NULL);
        CREATE TABLE IF NOT EXISTS continuation_auth(
            key TEXT PRIMARY KEY, digest TEXT NOT NULL, seed TEXT NOT NULL,
            policy_scope TEXT NOT NULL, policy_version TEXT NOT NULL,
            checker_agent TEXT NOT NULL, checker_frame TEXT NOT NULL, checker_digest TEXT NOT NULL,
            caller_agent TEXT NOT NULL, run_id TEXT NOT NULL, correlation_id TEXT NOT NULL,
            authorization_ref TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS continuation_policy_epoch(
            scope TEXT PRIMARY KEY, version TEXT NOT NULL)";
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

    /// <summary>Only a trusted host policy controller may update this mirror of its
    /// current version. Reservation compares the version in the same SQLite update.</summary>
    public async Task SetTrustedPolicyVersionAsync(string policyScope, string version, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO continuation_policy_epoch(scope,version) VALUES($scope,$version)
            ON CONFLICT(scope) DO UPDATE SET version=excluded.version";
        cmd.Parameters.AddWithValue("$scope", policyScope);
        cmd.Parameters.AddWithValue("$version", version);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Atomically inserts the outbox row and immutable authorization evidence.
    /// An existing idempotency key is accepted only when ALL authorization facts match.
    /// No dispatch is performed by this method.</summary>
    public async Task<DurableOutboxRecord?> AdmitAuthorizedAsync(DurableAuthorizationSnapshot a, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (new[] { a.IdempotencyKey, a.ResultDigest, a.SeedId, a.PolicyScope, a.PolicyVersion,
                a.CheckerAgentId, a.CheckerFrameId, a.CheckerEvidenceDigest, a.CallerAgentId,
                a.RunId, a.CorrelationId, a.AuthorizationEvidenceRef }.Any(string.IsNullOrWhiteSpace))
            return null;
        using var c = Open();
        using var tx = (SqliteTransaction)await c.BeginTransactionAsync(ct);
        using (var check = c.CreateCommand())
        {
            check.Transaction = tx;
            check.CommandText = "SELECT version FROM continuation_policy_epoch WHERE scope=$scope";
            check.Parameters.AddWithValue("$scope", a.PolicyScope);
            if (!string.Equals(await check.ExecuteScalarAsync(ct) as string, a.PolicyVersion, StringComparison.Ordinal))
                return null;
        }
        using (var insert = c.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = @"INSERT OR IGNORE INTO continuation_outbox(key,version,status,digest,seed)
                VALUES($key,1,$status,$digest,$seed)";
            insert.Parameters.AddWithValue("$key", a.IdempotencyKey);
            insert.Parameters.AddWithValue("$status", (int)DurableOutboxStatus.Deferred);
            insert.Parameters.AddWithValue("$digest", a.ResultDigest);
            insert.Parameters.AddWithValue("$seed", a.SeedId);
            await insert.ExecuteNonQueryAsync(ct);
        }
        using (var insert = c.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = @"INSERT OR IGNORE INTO continuation_auth(
                key,digest,seed,policy_scope,policy_version,checker_agent,checker_frame,checker_digest,
                caller_agent,run_id,correlation_id,authorization_ref)
                VALUES($k,$d,$s,$scope,$pv,$ca,$cf,$cd,$caller,$run,$corr,$ar)";
            string[] values = [a.IdempotencyKey,a.ResultDigest,a.SeedId,a.PolicyScope,a.PolicyVersion,
                a.CheckerAgentId,a.CheckerFrameId,a.CheckerEvidenceDigest,a.CallerAgentId,
                a.RunId,a.CorrelationId,a.AuthorizationEvidenceRef];
            string[] columns = ["$k","$d","$s","$scope","$pv","$ca","$cf","$cd","$caller","$run","$corr","$ar"];
            for(var i=0;i<columns.Length;i++) insert.Parameters.AddWithValue(columns[i],values[i]);
            await insert.ExecuteNonQueryAsync(ct);
        }
        using (var check = c.CreateCommand())
        {
            check.Transaction = tx;
            check.CommandText = @"SELECT o.version,o.status,o.digest,o.seed,o.external_id,
                a.digest,a.seed,a.policy_scope,a.policy_version,a.checker_agent,a.checker_frame,
                a.checker_digest,a.caller_agent,a.run_id,a.correlation_id,a.authorization_ref
                FROM continuation_outbox o JOIN continuation_auth a ON a.key=o.key
                WHERE o.key=$key";
            check.Parameters.AddWithValue("$key", a.IdempotencyKey);
            using var r = await check.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct) || r.GetInt32(1) != (int)DurableOutboxStatus.Deferred ||
                !r.IsDBNull(4)) return null;
            string[] bound=[a.ResultDigest,a.SeedId,a.ResultDigest,a.SeedId,a.PolicyScope,a.PolicyVersion,
                a.CheckerAgentId,a.CheckerFrameId,a.CheckerEvidenceDigest,a.CallerAgentId,
                a.RunId,a.CorrelationId,a.AuthorizationEvidenceRef];
            int[] fields=[2,3,5,6,7,8,9,10,11,12,13,14,15];
            for(var i=0;i<fields.Length;i++)
                if (!string.Equals(r.GetString(fields[i]),bound[i],StringComparison.Ordinal)) return null;
        }
        await tx.CommitAsync(ct);
        return await ReadAsync(a.IdempotencyKey,ct);
    }

    /// <summary>Single atomic CAS that checks the immutable authorization snapshot,
    /// currently mirrored policy epoch, expected version, and external operation ID.
    /// Neither this method nor its caller may automatically replay a reserved dispatch.</summary>
    public async Task<DurableOutboxRecord?> ReserveAuthorizedAsync(string key, long version,
        string policyVersion, string checkerFrameId, string externalOperationId,
        CancellationToken ct = default)
    {
        if (new[] {key,policyVersion,checkerFrameId,externalOperationId}.Any(string.IsNullOrWhiteSpace))
            return null;
        using var c=Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText=@"UPDATE continuation_outbox SET version=version+1,
            status=$dispatching,external_id=$external
            WHERE key=$key AND version=$version AND status=$deferred AND external_id IS NULL
            AND EXISTS(SELECT 1 FROM continuation_auth a JOIN continuation_policy_epoch p
                ON p.scope=a.policy_scope AND p.version=a.policy_version
                WHERE a.key=$key AND a.policy_version=$policyVersion
                AND a.checker_frame=$checkerFrame AND a.digest=continuation_outbox.digest
                AND a.seed=continuation_outbox.seed)";
        cmd.Parameters.AddWithValue("$dispatching",(int)DurableOutboxStatus.Dispatching);
        cmd.Parameters.AddWithValue("$deferred",(int)DurableOutboxStatus.Deferred);
        cmd.Parameters.AddWithValue("$external",externalOperationId);
        cmd.Parameters.AddWithValue("$key",key);
        cmd.Parameters.AddWithValue("$version",version);
        cmd.Parameters.AddWithValue("$policyVersion",policyVersion);
        cmd.Parameters.AddWithValue("$checkerFrame",checkerFrameId);
        return await cmd.ExecuteNonQueryAsync(ct)==1?await ReadAsync(key,ct):null;
    }

    // Legacy test-only reservation. Production uses ReserveAuthorizedAsync.
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
            external_id=COALESCE($ext,external_id) WHERE key=$k AND version=$v AND status=$prior
            AND ($ext IS NULL OR external_id IS NULL OR external_id=$ext)
            AND ($prior != $deferred OR NOT EXISTS(
                SELECT 1 FROM continuation_auth WHERE key=$k))";
        cmd.Parameters.AddWithValue("$next", (int)next);cmd.Parameters.AddWithValue("$ext", (object?)externalId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$k", key);cmd.Parameters.AddWithValue("$v", version);
        cmd.Parameters.AddWithValue("$prior", (int)required);
        cmd.Parameters.AddWithValue("$deferred", (int)DurableOutboxStatus.Deferred);
        return await cmd.ExecuteNonQueryAsync(ct) == 1 ? await ReadAsync(key, ct) : null;
    }
}
