using System.Text.RegularExpressions;

namespace PMCR.O.Core;

public enum BoundedExecutionStatus { Completed, TimedOutOutcomeUnknown, CancelledOutcomeUnknown, FailedOutcomeUnknown }

/// <summary>Outcome of a bounded call. Unknown means the remote/local operation may have produced side effects.</summary>
public sealed record BoundedExecutionResult<T>(BoundedExecutionStatus Status, T? Value, string Code, IReadOnlyList<string> EvidenceRefs);

public static class ExecutionBoundary
{
    /// <summary>
    /// Runs a cooperative operation with a deadline. A timeout cancels the supplied operation token, but never asserts
    /// that the operation stopped or rolled back; callers must reconcile by operation ID before retrying side effects.
    /// </summary>
    public static async Task<BoundedExecutionResult<T>> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), "A finite positive timeout is required.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        try
        {
            var value = await operation(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            return new(BoundedExecutionStatus.Completed, value, "EXECUTION_COMPLETED", ["execution:completed"]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(BoundedExecutionStatus.CancelledOutcomeUnknown, default, "EXECUTION_CANCELLED_OUTCOME_UNKNOWN", ["execution:cancelled", "execution:outcome-unknown"]);
        }
        catch (OperationCanceledException)
        {
            return new(BoundedExecutionStatus.TimedOutOutcomeUnknown, default, "EXECUTION_TIMEOUT_OUTCOME_UNKNOWN", ["execution:timeout", "execution:outcome-unknown", "retry:reconcile-before-retry"]);
        }
        catch (Exception ex)
        {
            // Do not copy exception messages into general evidence; they may contain credentials or user data.
            return new(BoundedExecutionStatus.FailedOutcomeUnknown, default, "EXECUTION_FAILED_OUTCOME_UNKNOWN", ["execution:failed", "error-type:" + ex.GetType().Name, "execution:outcome-unknown"]);
        }
    }
}

public enum RetrySafety { SafeToRetry, ReconcileBeforeRetry, NeverRetry }

public static class RetrySafetyPolicy
{
    public static RetrySafety Evaluate(bool operationIsIdempotent, bool confirmedNotStarted, bool permanentFailure)
    {
        if (permanentFailure) return RetrySafety.NeverRetry;
        if (confirmedNotStarted) return RetrySafety.SafeToRetry;
        if (operationIsIdempotent) return RetrySafety.SafeToRetry;
        return RetrySafety.ReconcileBeforeRetry;
    }
}

/// <summary>Conservative text scrubber for trail summaries/evidence. Prefer structured allow-listed fields over raw logs.</summary>
public static class TrailEvidenceSanitizer
{
    private static readonly Regex SecretAssignment = new(
        @"(?i)\b(password|passwd|secret|client_secret|access_token|refresh_token|api[_-]?key|authorization)\s*[:=]\s*(?:bearer\s+)?[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex BearerToken = new(
        @"(?i)\bBearer\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static string Sanitize(string? value, int maxLength = 512)
    {
        if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var scrubbed = SecretAssignment.Replace(value, "$1=[REDACTED]");
        scrubbed = BearerToken.Replace(scrubbed, "Bearer [REDACTED]");
        return scrubbed.Length <= maxLength ? scrubbed : scrubbed[..maxLength] + "…[TRUNCATED]";
    }
}
