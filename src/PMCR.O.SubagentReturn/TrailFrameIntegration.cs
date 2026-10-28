using PMCR.O.Continuation;
using PMCR.O.Core;

namespace PMCR.O.SubagentReturn;

/// <summary>
/// Bridges the subagent-return plugin to the existing Enterprise Trail store
/// (the Cognitive Trail). The store assigns sequence, parent and hash linkage;
/// this class only validates, appends and reads correlated evidence.
/// </summary>
public sealed class TrailFrameIntegration
{
    private readonly IEnterpriseTrailStore _store;

    public TrailFrameIntegration(IEnterpriseTrailStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Appends a frame and returns its ID. Cancellation cannot silently erase evidence,
    /// so the write is not cancellable; a failed write propagates and stops the caller's decision.
    /// </summary>
    public async Task<string> AppendAsync(EnterpriseTrailFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var validation = EnterpriseTrailFrameValidator.Validate(frame);
        if (!validation.IsValid)
            throw new ArgumentException("Invalid Trail Frame: " + string.Join("; ", validation.Errors), nameof(frame));
        await _store.AppendAsync(frame, CancellationToken.None);
        return frame.FrameId;
    }

    /// <summary>True only if the caller's parent frame really exists in the same intent, run and correlation.</summary>
    public async Task<bool> ParentFrameExistsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var frames = await _store.ReadRunAsync(caller.RunId, cancellationToken);
        return frames.Any(frame =>
            string.Equals(frame.FrameId, caller.ParentFrameId, StringComparison.Ordinal) &&
            string.Equals(frame.IntentId, caller.IntentId, StringComparison.Ordinal) &&
            string.Equals(frame.RunId, caller.RunId, StringComparison.Ordinal) &&
            string.Equals(frame.CorrelationId, caller.CorrelationId, StringComparison.Ordinal));
    }

    /// <summary>Reads the run's frames filtered to this caller's intent and correlation, for independent checker proofs.</summary>
    public async Task<IReadOnlyList<EnterpriseTrailFrame>> ReadCorrelatedAsync(
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var frames = await _store.ReadRunAsync(caller.RunId, cancellationToken);
        return frames.Where(frame =>
            string.Equals(frame.IntentId, caller.IntentId, StringComparison.Ordinal) &&
            string.Equals(frame.CorrelationId, caller.CorrelationId, StringComparison.Ordinal)).ToArray();
    }
}
