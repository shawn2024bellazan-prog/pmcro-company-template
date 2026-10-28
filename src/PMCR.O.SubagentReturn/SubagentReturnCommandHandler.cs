using PMCR.O.Continuation.Plugin;
using PMCR.O.Core;

namespace PMCR.O.SubagentReturn;

/// <summary>
/// Host-supplied binding for a delegated subagent run, resolved from the authenticated
/// invocation ledger and plugin registry. The child's message must never construct it.
/// </summary>
public sealed record TrustedSubagentBinding(
    TrustedSubagentInvocation Invocation,
    PluginActivationGrant Grant);

public interface ITrustedSubagentInvocationResolver
{
    Task<TrustedSubagentBinding?> ResolveAsync(
        PmcroCommand<SubagentReturnEnvelope> command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Application command handler for a subagent's terminal return. The outer router has
/// already applied command authorization and idempotency before this handler runs.
/// A null payload is accepted only because the host may observe a timeout or interruption
/// when the child never returned; the completion handler decides what that means.
/// </summary>
public sealed class SubagentReturnCommandHandler :
    IPmcroCommandHandler<SubagentReturnEnvelope, SubagentReturnReceipt>
{
    private readonly SubagentCompletionHandler _completion;
    private readonly ITrustedSubagentInvocationResolver _resolver;

    public string CommandType => SubagentReturnSchemas.ReturnCommandType;

    public SubagentReturnCommandHandler(
        SubagentCompletionHandler completion,
        ITrustedSubagentInvocationResolver resolver)
    {
        _completion = completion ?? throw new ArgumentNullException(nameof(completion));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<CommandExecutionResult<SubagentReturnReceipt>> HandleAsync(
        PmcroCommand<SubagentReturnEnvelope> command,
        CancellationToken cancellationToken)
    {
        TrustedSubagentBinding? trusted;
        try
        {
            trusted = await _resolver.ResolveAsync(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Blocked("TRUSTED_CONTEXT_UNAVAILABLE", "The authenticated subagent binding could not be resolved.");
        }

        if (trusted?.Invocation?.Caller is null || trusted.Grant is null)
            return Blocked("TRUSTED_CONTEXT_MISSING", "No trusted subagent invocation and activation binding was found.");

        var caller = trusted.Invocation.Caller;
        if (!string.Equals(command.IntentId, caller.IntentId, StringComparison.Ordinal) ||
            !string.Equals(command.RunId, caller.RunId, StringComparison.Ordinal) ||
            !string.Equals(command.CorrelationId, caller.CorrelationId, StringComparison.Ordinal) ||
            !string.Equals(command.CausationId, caller.CommandId, StringComparison.Ordinal))
            return Blocked("CALLER_BINDING_MISMATCH", "Native command lineage does not match the trusted subagent invocation.");

        var receipt = await _completion.HandleAsync(
            command.Payload, trusted.Invocation, trusted.Grant, cancellationToken);

        var status = receipt.Status switch
        {
            SubagentReturnStatus.Completed => "completed",
            SubagentReturnStatus.Deferred => "deferred",
            SubagentReturnStatus.Rejected or SubagentReturnStatus.Duplicate => "rejected",
            _ => "failed"
        };
        var code = "SUBAGENT_RETURN_" + receipt.Status.ToString().ToUpperInvariant();

        return new CommandExecutionResult<SubagentReturnReceipt>(
            status, code, receipt.ReasonCode, receipt,
            ["subagent-return:" + receipt.ReasonCode, "subagent-return-status:" + receipt.Status],
            RequiresHumanAction: false,
            NextSeed: null);
    }

    private static CommandExecutionResult<SubagentReturnReceipt> Blocked(string code, string reason) =>
        new("rejected", code, reason, null, ["subagent-return:" + code], false, null);
}

public static class SubagentReturnComposition
{
    /// <summary>
    /// Connects the plugin to the native command router. The trusted resolver, authorization gate
    /// and plugin grant MUST come from trusted runtime services. Calling this factory does not
    /// register or enable the plugin.
    /// </summary>
    public static PmcroApplicationCommandRouter<SubagentReturnEnvelope, SubagentReturnReceipt> CreateRouter(
        SubagentCompletionHandler completion,
        ITrustedSubagentInvocationResolver resolver,
        ICommandAuthorizationGate<SubagentReturnEnvelope> authorization,
        IEnterpriseTrailStore trails,
        CommandExecutionContext context,
        ICommandIdempotencyStore? idempotency = null) =>
        new([new SubagentReturnCommandHandler(completion, resolver)], authorization, trails, context, idempotency);
}
