namespace PMCR.O.Core;

/// <summary>Stable, protocol-neutral conformance checks for adapters that translate native messages to PMCR-O envelopes.</summary>
public static class AdapterConformance
{
    public static IReadOnlyList<string> ValidateInbound<TPayload>(PmcroCommand<TPayload> command)
    {
        try { return PmcroCommandValidator.Validate(command); }
        catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
        { return ["Command envelope could not be validated: " + ex.GetType().Name]; }
    }

    /// <summary>Checks that a response preserves the command's lineage and exposes a machine-readable outcome.</summary>
    public static IReadOnlyList<string> ValidateResponse<TPayload, TResult>(PmcroCommand<TPayload> command, PmcroMessage<TResult> response)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(response);
        var errors = new List<string>();
        if (!string.Equals(response.CommandId, command.CommandId, StringComparison.Ordinal)) errors.Add("Response CommandId does not match request.");
        if (!string.Equals(response.IntentId, command.IntentId, StringComparison.Ordinal)) errors.Add("Response IntentId does not match request.");
        if (!string.Equals(response.RunId, command.RunId, StringComparison.Ordinal)) errors.Add("Response RunId does not match request.");
        if (!string.Equals(response.CorrelationId, command.CorrelationId, StringComparison.Ordinal)) errors.Add("Response CorrelationId does not match request.");
        if (string.IsNullOrWhiteSpace(response.MessageId)) errors.Add("Response MessageId is required.");
        if (string.IsNullOrWhiteSpace(response.Status)) errors.Add("Response Status is required.");
        if (string.IsNullOrWhiteSpace(response.Code)) errors.Add("Response Code is required.");
        if (string.IsNullOrWhiteSpace(response.Summary)) errors.Add("Response Summary is required.");
        if (response.Timestamp == default) errors.Add("Response Timestamp is required.");
        if (response.EvidenceRefs is null) errors.Add("Response EvidenceRefs must be present, even when empty.");
        if (response.RequiresHumanAction && response.Status is not ("waiting_for_approval" or "failed" or "rejected"))
            errors.Add("Human action must be represented by an approval, failure, or rejection outcome.");
        if (response.Status == "waiting_for_approval" && !response.RequiresHumanAction)
            errors.Add("Approval-waiting responses must set RequiresHumanAction.");
        return errors;
    }
}

/// <summary>Adapter authors implement this at the transport boundary; native protocol semantics stay in the adapter.</summary>
public interface IPmcroCommandAdapter<TNativeRequest, TNativeResponse, TPayload, TResult>
{
    string AdapterId { get; }
    Task<PmcroCommand<TPayload>> NormalizeAsync(TNativeRequest request, CancellationToken cancellationToken = default);
    Task<TNativeResponse> MapResponseAsync(PmcroMessage<TResult> response, CancellationToken cancellationToken = default);
}
