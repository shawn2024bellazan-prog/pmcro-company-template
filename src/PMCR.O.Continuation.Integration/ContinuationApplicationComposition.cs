using PMCR.O.Continuation;
using PMCR.O.Continuation.Plugin;
using PMCR.O.Core;

namespace PMCR.O.Continuation.Integration;

public static class ContinuationApplicationComposition
{
    /// <summary>
    /// Connects continuation to the native command router. Caller context,
    /// plugin grants and authorization MUST come from trusted runtime services.
    /// Calling this factory does not register or enable the plugin.
    /// </summary>
    public static PmcroApplicationCommandRouter<CycleResultEnvelope, ContinuationReceipt> CreateRouter(
        ContinuationPlugin plugin,
        ITrustedContinuationInvocationResolver resolver,
        ICommandAuthorizationGate<CycleResultEnvelope> authorization,
        IEnterpriseTrailStore trails,
        CommandExecutionContext context,
        ICommandIdempotencyStore? idempotency = null)
    {
        return new PmcroApplicationCommandRouter<CycleResultEnvelope, ContinuationReceipt>(
            [new ApplicationContinuationHandler(plugin, resolver)],
            authorization, trails, context, idempotency);
    }
}
