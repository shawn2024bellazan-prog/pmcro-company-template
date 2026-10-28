using PMCR.O.Continuation;
using PMCR.O.Continuation.Plugin;
using PMCR.O.Core;

namespace PMCR.O.Continuation.Integration;

/// <summary>
/// Supplied by the authenticated host from its invocation ledger and registry.
/// The child result and command context must never construct this binding.
/// </summary>
public sealed record TrustedContinuationInvocation(
    CallerContext Caller,
    PluginActivationGrant Grant,
    string Route);

public interface ITrustedContinuationInvocationResolver
{
    Task<TrustedContinuationInvocation?> ResolveAsync(
        PmcroCommand<CycleResultEnvelope> command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Application command handler for a child cycle result. The outer router
/// checks command authorization and idempotency before this handler is reached.
/// The plugin then checks its trusted activation grant and per-seed policy.
/// </summary>
public sealed class ApplicationContinuationHandler : IPmcroCommandHandler<CycleResultEnvelope, ContinuationReceipt>
{
    public const string ReturnCommandType = "pmcro.continuation.return";

    private readonly ContinuationPlugin _plugin;
    private readonly ITrustedContinuationInvocationResolver _resolver;

    public string CommandType => ReturnCommandType;

    public ApplicationContinuationHandler(
        ContinuationPlugin plugin,
        ITrustedContinuationInvocationResolver resolver)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<CommandExecutionResult<ContinuationReceipt>> HandleAsync(
        PmcroCommand<CycleResultEnvelope> command,
        CancellationToken cancellationToken)
    {
        if (command.Payload is null)
            return Blocked("CONTINUATION_PAYLOAD_REQUIRED", "A cycle-result payload is required.");

        TrustedContinuationInvocation? trusted;
        try
        {
            trusted = await _resolver.ResolveAsync(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return Blocked("TRUSTED_CONTEXT_UNAVAILABLE", "The authenticated caller binding could not be resolved.");
        }

        if (trusted?.Caller is null || trusted.Grant is null || string.IsNullOrWhiteSpace(trusted.Route))
            return Blocked("TRUSTED_CONTEXT_MISSING", "No trusted invocation and activation binding was found.");

        var caller = trusted.Caller;
        if (!string.Equals(command.IntentId, caller.IntentId, StringComparison.Ordinal) ||
            !string.Equals(command.RunId, caller.RunId, StringComparison.Ordinal) ||
            !string.Equals(command.CorrelationId, caller.CorrelationId, StringComparison.Ordinal) ||
            !string.Equals(command.CausationId, caller.CommandId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(caller.ParentFrameId))
            return Blocked("CALLER_BINDING_MISMATCH", "Native command lineage does not match the trusted caller invocation.");

        // A returned seed is never submitted directly to an executable route.
        // Plugin activation, independent evidence, policy, budgets and claims
        // are enforced by the existing continuation handler.
        var receipt = await _plugin.InvokeAsync(
            trusted.Route, command.Payload, caller, trusted.Grant, cancellationToken);

        var status = receipt.Status switch
        {
            ContinuationStatus.Dispatched or ContinuationStatus.NoContinuation => "completed",
            ContinuationStatus.Deferred => "deferred",
            ContinuationStatus.ExecutionUncertain => "failed",
            _ => "rejected"
        };
        var code = receipt.Status == ContinuationStatus.ExecutionUncertain
            ? "CONTINUATION_OUTCOME_UNCERTAIN"
            : "CONTINUATION_" + receipt.Status.ToString().ToUpperInvariant();

        return new CommandExecutionResult<ContinuationReceipt>(
            status, code, receipt.ReasonCode, receipt,
            ["continuation:" + receipt.ReasonCode, "continuation-status:" + receipt.Status],
            RequiresHumanAction: false,
            NextSeed: null);
    }

    private static CommandExecutionResult<ContinuationReceipt> Blocked(string code, string reason) =>
        new("rejected", code, reason, null, ["continuation:" + code], false, null);
}
