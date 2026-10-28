using System.Text.Json;

namespace PMCR.O.Core;

public enum IntentStatus
{
    Pending,
    Refining,
    Ready,
    Running,
    WaitingForApproval,
    NeedsClarification,
    Completed,
    Cancelled,
    Escalated,
    Failed
}

/// <summary>A durable record of the user's original intent and its execution lifecycle.</summary>
public sealed record IntentQueueItem(
    string IntentId,
    string RawMessage,
    string? RefinedObjective,
    IntentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CycleCount,
    string? LastCycleId,
    string? LastOutcome,
    string? BlockReason,
    string? ParentIntentId = null);

public interface IIntentQueue
{
    Task<IntentQueueItem> EnqueueAsync(string rawMessage, string? parentIntentId = null, CancellationToken ct = default);
    Task<IReadOnlyList<IntentQueueItem>> ListAsync(CancellationToken ct = default);
    Task<IntentQueueItem?> GetAsync(string intentId, CancellationToken ct = default);
    Task<IntentQueueItem> TransitionAsync(string intentId, IntentStatus status, string? refinedObjective = null,
        string? lastCycleId = null, string? lastOutcome = null, string? blockReason = null,
        int? cycleCount = null, CancellationToken ct = default);
}

/// <summary>
/// Local durable queue for a single process / local development. Writes replace a temp file atomically.
/// For multi-replica production use a transactional database or durable broker with leases and deduplication.
/// </summary>
public sealed class JsonIntentQueue : IIntentQueue
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonIntentQueue(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task<IntentQueueItem> EnqueueAsync(string rawMessage, string? parentIntentId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawMessage)) throw new ArgumentException("Raw user message must not be empty.", nameof(rawMessage));
        await _gate.WaitAsync(ct);
        try
        {
            var items = await ReadUnsafeAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var item = new IntentQueueItem(Guid.NewGuid().ToString("N"), rawMessage, null, IntentStatus.Pending,
                now, now, 0, null, null, null, parentIntentId);
            items.Add(item);
            await WriteUnsafeAsync(items, ct);
            return item;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<IntentQueueItem>> ListAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadUnsafeAsync(ct)).OrderBy(x => x.CreatedAt).ToArray(); }
        finally { _gate.Release(); }
    }

    public async Task<IntentQueueItem?> GetAsync(string intentId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadUnsafeAsync(ct)).SingleOrDefault(x => x.IntentId == intentId); }
        finally { _gate.Release(); }
    }

    public async Task<IntentQueueItem> TransitionAsync(string intentId, IntentStatus status, string? refinedObjective = null,
        string? lastCycleId = null, string? lastOutcome = null, string? blockReason = null,
        int? cycleCount = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var items = await ReadUnsafeAsync(ct);
            var index = items.FindIndex(x => x.IntentId == intentId);
            if (index < 0) throw new KeyNotFoundException($"Intent '{intentId}' was not found.");
            var current = items[index];
            EnsureTransition(current.Status, status);
            var updated = current with
            {
                Status = status,
                RefinedObjective = refinedObjective ?? current.RefinedObjective,
                UpdatedAt = DateTimeOffset.UtcNow,
                LastCycleId = lastCycleId ?? current.LastCycleId,
                LastOutcome = lastOutcome ?? current.LastOutcome,
                BlockReason = status is IntentStatus.WaitingForApproval or IntentStatus.NeedsClarification or IntentStatus.Escalated or IntentStatus.Failed
                    ? blockReason ?? current.BlockReason : null,
                CycleCount = cycleCount ?? current.CycleCount
            };
            items[index] = updated;
            await WriteUnsafeAsync(items, ct);
            return updated;
        }
        finally { _gate.Release(); }
    }

    private static void EnsureTransition(IntentStatus from, IntentStatus to)
    {
        if (from == to) return;
        var allowed = from switch
        {
            IntentStatus.Pending => new[] { IntentStatus.Refining, IntentStatus.Cancelled, IntentStatus.Escalated, IntentStatus.Failed },
            IntentStatus.Refining => new[] { IntentStatus.Ready, IntentStatus.WaitingForApproval, IntentStatus.NeedsClarification, IntentStatus.Escalated, IntentStatus.Cancelled, IntentStatus.Failed },
            IntentStatus.Ready => new[] { IntentStatus.Running, IntentStatus.WaitingForApproval, IntentStatus.Cancelled, IntentStatus.Escalated, IntentStatus.Failed },
            IntentStatus.Running => new[] { IntentStatus.Running, IntentStatus.WaitingForApproval, IntentStatus.Completed, IntentStatus.Escalated, IntentStatus.Failed },
            IntentStatus.WaitingForApproval => new[] { IntentStatus.Ready, IntentStatus.Running, IntentStatus.Cancelled, IntentStatus.Escalated },
            IntentStatus.NeedsClarification => new[] { IntentStatus.Refining, IntentStatus.Cancelled, IntentStatus.Escalated },
            IntentStatus.Escalated => new[] { IntentStatus.Ready, IntentStatus.Cancelled },
            IntentStatus.Failed => new[] { IntentStatus.Ready, IntentStatus.Cancelled },
            IntentStatus.Completed or IntentStatus.Cancelled => Array.Empty<IntentStatus>(),
            _ => Array.Empty<IntentStatus>()
        };
        if (!allowed.Contains(to)) throw new InvalidOperationException($"Invalid intent transition: {from} → {to}.");
    }

    private async Task<List<IntentQueueItem>> ReadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await JsonSerializer.DeserializeAsync<List<IntentQueueItem>>(stream, Options, ct) ?? [];
    }

    private async Task WriteUnsafeAsync(List<IntentQueueItem> items, CancellationToken ct)
    {
        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await JsonSerializer.SerializeAsync(stream, items, Options, ct);
        File.Move(temp, _path, overwrite: true);
    }
}
