namespace PMCR.O.Core;

/// <summary>Declarative route binding from an application command to a registered executable skill.</summary>
public sealed record SkillRouteDefinition(
    string RouteId,
    string CommandType,
    string CapabilityId,
    string SkillId,
    string VersionConstraint,
    bool Enabled,
    string PolicyRef,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SkillRouteResolution(bool Matched, string Code, string Summary, SkillRouteDefinition? Route);

public interface ISkillRouteCatalog
{
    Task<IReadOnlyList<SkillRouteDefinition>> ListAsync(CancellationToken cancellationToken = default);
}

public interface ISkillCommandDispatcher<TPayload, TResult>
{
    Task<CommandExecutionResult<TResult>> DispatchSkillAsync(SkillRouteDefinition route, PmcroCommand<TPayload> command, CancellationToken cancellationToken);
}

/// <summary>Resolves declarative skill routes; authorization remains in the application router and runtime executor.</summary>
public sealed class SkillRouteResolver(ISkillRouteCatalog catalog)
{
    public async Task<SkillRouteResolution> ResolveAsync(string commandType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandType))
            return new(false, "ROUTE_COMMAND_REQUIRED", "Command type is required for skill route resolution.", null);

        var routes = await catalog.ListAsync(cancellationToken);
        var matches = routes.Where(x => string.Equals(x.CommandType, commandType, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
            return new(false, "SKILL_ROUTE_NOT_FOUND", "No skill route is registered for this command type.", null);
        if (matches.Length > 1)
            return new(false, "SKILL_ROUTE_AMBIGUOUS", "Multiple skill routes match; execution is blocked until the route is unambiguous.", null);
        var route = matches[0];
        if (!route.Enabled)
            return new(false, "SKILL_ROUTE_DISABLED", "The matching skill route is disabled.", route);
        if (string.IsNullOrWhiteSpace(route.RouteId) || string.IsNullOrWhiteSpace(route.CapabilityId) ||
            string.IsNullOrWhiteSpace(route.SkillId) || string.IsNullOrWhiteSpace(route.PolicyRef) ||
            route.RequiredPermissions is null || route.Metadata is null)
            return new(false, "SKILL_ROUTE_INVALID", "The matching skill route is missing required governance metadata.", route);
        return new(true, "SKILL_ROUTE_RESOLVED", "A unique enabled skill route was resolved; policy authorization is still required.", route);
    }
}

/// <summary>Declarative JSON catalog. The host must protect write access to the catalog file.</summary>
public sealed class JsonSkillRouteCatalog : ISkillRouteCatalog
{
    private readonly string _path;
    private static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true };
    public JsonSkillRouteCatalog(string path) => _path = Path.GetFullPath(path);

    public async Task<IReadOnlyList<SkillRouteDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return Array.Empty<SkillRouteDefinition>();
        await using var stream = File.OpenRead(_path);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<List<SkillRouteDefinition>>(stream, Options, cancellationToken)
            ?? Array.Empty<SkillRouteDefinition>();
    }

    public async Task ReplaceAsync(IReadOnlyList<SkillRouteDefinition> routes, CancellationToken cancellationToken = default)
    {
        var duplicates = routes.GroupBy(x => x.CommandType, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0) throw new InvalidOperationException("Command route must be unique: " + string.Join(", ", duplicates));
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                await System.Text.Json.JsonSerializer.SerializeAsync(stream, routes, Options, cancellationToken);
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
