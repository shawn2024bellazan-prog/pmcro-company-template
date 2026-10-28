namespace PMCR.O.Core;

public interface ISkillRouteTrailRecorder
{
    Task<string> RecordResolutionAsync<TPayload>(PmcroCommand<TPayload> command, SkillRouteResolution resolution, CommandExecutionContext context, CancellationToken cancellationToken = default);
}

/// <summary>Writes a distinct Trail Frame for skill-route resolution, including fail-closed decisions.</summary>
public sealed class EnterpriseSkillRouteTrailRecorder(IEnterpriseTrailStore store) : ISkillRouteTrailRecorder
{
    public async Task<string> RecordResolutionAsync<TPayload>(PmcroCommand<TPayload> command, SkillRouteResolution resolution, CommandExecutionContext context, CancellationToken cancellationToken = default)
    {
        var id = "frame-" + Guid.NewGuid().ToString("N");
        var allowed = resolution.Matched && resolution.Route is not null;
        var route = resolution.Route;
        var evidence = new List<string>
        {
            "skill-route:" + resolution.Code,
            "route-id:" + (route?.RouteId ?? "none"),
            "capability-id:" + (route?.CapabilityId ?? "none"),
            "skill-id:" + (route?.SkillId ?? "none"),
            "policy-ref:" + (route?.PolicyRef ?? "none")
        };
        if (route is not null)
            evidence.AddRange(route.RequiredPermissions.Select(p => "required-permission:" + p));
        var frame = new EnterpriseTrailFrame("1.0", context.OrganizationId, context.ProjectId, command.IntentId,
            command.RunId, context.AgentId, command.CorrelationId, id, 1, null, [], DateTimeOffset.UtcNow,
            command.Context.TryGetValue("original_seed", out var original) ? original : command.CommandType,
            command.Context.TryGetValue("refined_objective", out var refined) ? refined : command.CommandType,
            [], allowed ? null : "skill.route.repair", "route-resolution", command.CommandType,
            route?.SkillId ?? "skill-route-resolver", allowed ? "resolved" : "blocked", evidence,
            context.PolicyId, allowed ? "not_applicable" : "deny", null,
            allowed ? "PASS" : "NOT_PASS", resolution.Summary, [], false, null, "observation", null,
            "skill-route-resolver", false, null);
        await store.AppendAsync(frame, cancellationToken);
        return id;
    }
}

/// <summary>Adapts declarative skill routing to the application command-handler contract. The downstream dispatcher must enforce runtime policy and permissions before invoking the skill.</summary>
public sealed class SkillRoutedCommandHandler<TPayload, TResult>(
    string commandType,
    SkillRouteResolver resolver,
    ISkillCommandDispatcher<TPayload, TResult> dispatcher,
    ISkillRouteTrailRecorder trailRecorder,
    IEnterpriseTrailStore trailStore,
    CommandExecutionContext context) : IPmcroCommandHandler<TPayload, TResult>
{
    public string CommandType { get; } = string.IsNullOrWhiteSpace(commandType)
        ? throw new ArgumentException("Command type is required.", nameof(commandType))
        : commandType;

    public async Task<CommandExecutionResult<TResult>> HandleAsync(PmcroCommand<TPayload> command, CancellationToken cancellationToken)
    {
        SkillRouteResolution resolution;
        try
        {
            resolution = await resolver.ResolveAsync(command.CommandType, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            resolution = new(false, "SKILL_ROUTE_CATALOG_UNAVAILABLE", "Skill route catalog could not be read; execution was blocked.", null);
            await trailRecorder.RecordResolutionAsync(command, resolution with { Summary = resolution.Summary + " Error type: " + ex.GetType().Name }, context, CancellationToken.None);
            return new("failed", resolution.Code, resolution.Summary, default, ["skill-route:catalog-unavailable", "error-type:" + ex.GetType().Name], false, "skill.route.catalog.repair");
        }

        await trailRecorder.RecordResolutionAsync(command, resolution, context, cancellationToken);
        if (!resolution.Matched || resolution.Route is null)
            return new("rejected", resolution.Code, resolution.Summary, default, ["skill-route:" + resolution.Code], false, "skill.route.repair");

        try
        {
            var result = await dispatcher.DispatchSkillAsync(resolution.Route, command, cancellationToken);
            var evidence = result.EvidenceRefs.Concat(["skill-route:" + resolution.Code, "route-id:" + resolution.Route.RouteId,
                "capability-id:" + resolution.Route.CapabilityId, "skill-id:" + resolution.Route.SkillId]).ToArray();
            return result with { EvidenceRefs = evidence };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Do not leak exception messages or imply the skill had no side effects.
            await trailStore.AppendAsync(new EnterpriseTrailFrame("1.0", context.OrganizationId, context.ProjectId,
                command.IntentId, command.RunId, context.AgentId, command.CorrelationId, "frame-" + Guid.NewGuid().ToString("N"),
                1, null, [], DateTimeOffset.UtcNow,
                command.Context.TryGetValue("original_seed", out var original) ? original : command.CommandType,
                command.Context.TryGetValue("refined_objective", out var refined) ? refined : command.CommandType,
                [], "skill.execution.failure.review", "skill-execution", command.CommandType, resolution.Route.SkillId,
                "failed", ["skill-route:" + resolution.Code, "route-id:" + resolution.Route.RouteId, "error-type:" + ex.GetType().Name],
                context.PolicyId, "allow", null, "NOT_PASS", "Skill dispatcher failed; inspect Trail evidence before retrying.", [], false,
                null, "observation", null, "skill-routed-command-handler", false, null), CancellationToken.None);
            return new("failed", "SKILL_DISPATCH_FAILED", "Skill dispatch failed; inspect Trail evidence before retrying because side effects may have occurred.", default,
                ["skill-route:" + resolution.Code, "route-id:" + resolution.Route.RouteId, "error-type:" + ex.GetType().Name], false, "skill.execution.failure.review");
        }
    }
}
