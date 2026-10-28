using PMCR.O.Core;
using System.Text.Json;

namespace PMCR.O.SubagentReturn;

/// <summary>External trust boundary: production must configure a separately governed
/// anchored store and prevent the Trail writer from modifying earlier heads.</summary>
public interface ITrustedTrailHeadAnchor
{
    Task<string?> ReadHeadAsync(string runId, CancellationToken cancellationToken = default);
    Task<bool> CompareAndSetAsync(string runId, string? expectedHead, string newHead,
        CancellationToken cancellationToken = default);
}

/// <summary>Reference test adapter only; not a durable or independent trust anchor.</summary>
public sealed class InMemoryTrustedTrailHeadAnchor : ITrustedTrailHeadAnchor
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _heads = new();
    public Task<string?> ReadHeadAsync(string runId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_heads.TryGetValue(runId, out var value) ? value : null);
    }
    public Task<bool> CompareAndSetAsync(string runId, string? expectedHead, string newHead,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newHead);
        if (expectedHead is null) return Task.FromResult(_heads.TryAdd(runId, newHead));
        return Task.FromResult(_heads.TryUpdate(runId, newHead, expectedHead));
    }
}

/// <summary>Fails closed when no independent head is available.</summary>
public sealed class TrustedTrailHeadVerifier
{
    private readonly SqliteHashChainedTrailLedger _ledger;
    private readonly ITrustedTrailHeadAnchor _anchor;
    public TrustedTrailHeadVerifier(SqliteHashChainedTrailLedger ledger, ITrustedTrailHeadAnchor anchor)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
    }
    public async Task<bool> VerifyAsync(string runId, CancellationToken ct = default)
    {
        var head = await _anchor.ReadHeadAsync(runId, ct);
        return !string.IsNullOrWhiteSpace(head) && await _ledger.VerifyAsync(runId, head, ct);
    }

    /// <summary>Requires the source Checker Frame's *entire payload* to exist in
    /// the verified anchored chain, normalizing only the ledger-assigned sequence.</summary>
    public async Task<bool> VerifyEvidenceAsync(EnterpriseTrailFrame frame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!await VerifyAsync(frame.RunId, ct)) return false;
        var stored = await _ledger.ReadFrameAsync(frame.RunId, frame.FrameId, ct);
        return stored is not null &&
            string.Equals(JsonSerializer.Serialize(frame with { Sequence = stored.Sequence }),
                JsonSerializer.Serialize(stored), StringComparison.Ordinal);
    }
}
