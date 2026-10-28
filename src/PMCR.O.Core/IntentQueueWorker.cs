using System.Collections.Concurrent;

namespace PMCR.O.Core;

public sealed record IntentRefinement(string? Objective, bool NeedsClarification = false, string? ClarificationReason = null);

public interface IIntentRefiner
{
    Task<IntentRefinement> RefineAsync(string rawMessage, CancellationToken ct);
}

public interface ICycleRunnerFactory
{
    CycleRunner Create(SeedIntent seed);
}

public sealed record IntentProcessingResult(string IntentId, IntentStatus Status, int Cycles, string Summary);

/// <summary>
/// Sequential queue worker for local/single-process deployments. It never marks work complete
/// without an ACCEPT outcome and successful independent check. Durable broker leases and
/// distributed concurrency are intentionally left to a later adapter.
/// </summary>
public sealed class IntentQueueWorker
{
    private readonly IIntentQueue _queue;
    private readonly IIntentRefiner _refiner;
    private readonly ICycleRunnerFactory _runnerFactory;

    public IntentQueueWorker(IIntentQueue queue, IIntentRefiner refiner, ICycleRunnerFactory runnerFactory)
        => (_queue, _refiner, _runnerFactory) = (queue, refiner, runnerFactory);

    public async Task<IReadOnlyList<IntentProcessingResult>> DrainAsync(int maxItems = 10, CancellationToken ct = default)
    {
        if (maxItems < 1) throw new ArgumentOutOfRangeException(nameof(maxItems));
        var results = new List<IntentProcessingResult>();
        var items = await _queue.ListAsync(ct);
        foreach (var snapshot in items.Where(x => x.Status is IntentStatus.Pending or IntentStatus.Ready).Take(maxItems))
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await ProcessAsync(snapshot, ct));
        }
        return results;
    }

    private async Task<IntentProcessingResult> ProcessAsync(IntentQueueItem item, CancellationToken ct)
    {
        try
        {
            var current = item;
            if (current.Status == IntentStatus.Pending)
            {
                current = await _queue.TransitionAsync(current.IntentId, IntentStatus.Refining, ct: ct);
                var refined = await _refiner.RefineAsync(current.RawMessage, ct);
                if (refined.NeedsClarification || string.IsNullOrWhiteSpace(refined.Objective))
                {
                    var reason = string.IsNullOrWhiteSpace(refined.ClarificationReason)
                        ? "Intent needs clarification before execution; original message is preserved."
                        : refined.ClarificationReason;
                    var blocked = await _queue.TransitionAsync(current.IntentId, IntentStatus.NeedsClarification,
                        blockReason: reason, lastOutcome: "NEEDS_CLARIFICATION", ct: ct);
                    return new(blocked.IntentId, blocked.Status, blocked.CycleCount, reason);
                }
                current = await _queue.TransitionAsync(current.IntentId, IntentStatus.Ready,
                    refinedObjective: refined.Objective, lastOutcome: "REFINED", ct: ct);
            }

            current = await _queue.TransitionAsync(current.IntentId, IntentStatus.Running,
                lastOutcome: "RUNNING", ct: ct);
            var objective = current.RefinedObjective;
            if (string.IsNullOrWhiteSpace(objective))
                throw new InvalidOperationException("Ready intent has no refined objective.");

            var seed = new SeedIntent(current.IntentId, objective, current.ParentIntentId);
            var outcomes = await _runnerFactory.Create(seed).RunAsync(seed, ct);
            var final = outcomes.LastOrDefault();
            var cycles = outcomes.Count;
            if (final is null)
                throw new InvalidOperationException("Cycle runner returned no outcomes.");

            if (final.Verdict == CycleVerdict.Accept && final.ObjectiveComplete && final.CheckResult.Passed && final.CheckResult.PolicyCompliant)
            {
                var done = await _queue.TransitionAsync(current.IntentId, IntentStatus.Completed,
                    lastCycleId: final.CycleId, lastOutcome: "ACCEPT_VERIFIED", cycleCount: current.CycleCount + cycles, ct: ct);
                return new(done.IntentId, done.Status, done.CycleCount, "Objective independently checked and accepted.");
            }

            if (final.CheckResult.Findings.Any(x => x == $"approval-disposition:{ApprovalDisposition.RequireHumanApproval}"))
            {
                var waiting = await _queue.TransitionAsync(current.IntentId, IntentStatus.WaitingForApproval,
                    lastCycleId: final.CycleId, lastOutcome: "AWAITING_HUMAN_APPROVAL", blockReason: final.Reason,
                    cycleCount: current.CycleCount + cycles, ct: ct);
                return new(waiting.IntentId, waiting.Status, waiting.CycleCount, final.Reason);
            }

            if (final.Verdict == CycleVerdict.Escalate || !final.CheckResult.PolicyCompliant)
            {
                var escalated = await _queue.TransitionAsync(current.IntentId, IntentStatus.Escalated,
                    lastCycleId: final.CycleId, lastOutcome: "ESCALATE", blockReason: final.Reason,
                    cycleCount: current.CycleCount + cycles, ct: ct);
                return new(escalated.IntentId, escalated.Status, escalated.CycleCount, final.Reason);
            }

            var failed = await _queue.TransitionAsync(current.IntentId, IntentStatus.Failed,
                lastCycleId: final.CycleId, lastOutcome: "NOT_VERIFIED", blockReason: final.Reason,
                cycleCount: current.CycleCount + cycles, ct: ct);
            return new(failed.IntentId, failed.Status, failed.CycleCount, "Outcome did not satisfy the completion gate.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Preserve the queue record. A running item is not marked complete on cancellation.
            throw;
        }
        catch (Exception ex)
        {
            var latest = await _queue.GetAsync(item.IntentId, CancellationToken.None);
            if (latest is not null && latest.Status is not (IntentStatus.Completed or IntentStatus.Cancelled or IntentStatus.Escalated or IntentStatus.Failed or IntentStatus.NeedsClarification))
            {
                var failed = await _queue.TransitionAsync(item.IntentId, IntentStatus.Failed,
                    lastOutcome: "WORKER_ERROR", blockReason: ex.Message, ct: CancellationToken.None);
                return new(failed.IntentId, failed.Status, failed.CycleCount, ex.Message);
            }
            throw;
        }
    }
}
