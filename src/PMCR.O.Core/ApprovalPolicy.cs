namespace PMCR.O.Core;

/// <summary>Conservative default. A production host should compose an authenticated policy service.</summary>
public sealed class ConservativeApprovalPolicy : IApprovalPolicy
{
    private readonly HashSet<string> _allowlist;
    public ConservativeApprovalPolicy(IEnumerable<string>? allowlist = null) =>
        _allowlist = new HashSet<string>(allowlist ?? ["repo.read", "docs.search", "tests.local"], StringComparer.OrdinalIgnoreCase);

    public Task<ApprovalDecision> EvaluateAsync(ActionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Risk is ActionRisk.SecretAccess or ActionRisk.PrivilegeChange or ActionRisk.Destructive or ActionRisk.ExternalCodeExecution or ActionRisk.Unknown)
            return Task.FromResult(new ApprovalDecision(ApprovalDisposition.RequireHumanApproval, "conservative-default", $"Risk {request.Risk} is not auto-approved."));
        if ((request.Risk is ActionRisk.ReadOnly or ActionRisk.LocalValidation) && _allowlist.Contains(request.Capability))
            return Task.FromResult(new ApprovalDecision(ApprovalDisposition.Allow, "conservative-default", "Explicitly allowlisted low-risk capability."));
        return Task.FromResult(new ApprovalDecision(ApprovalDisposition.RequireHumanApproval, "conservative-default", "Action is outside the low-risk allowlist."));
    }
}
