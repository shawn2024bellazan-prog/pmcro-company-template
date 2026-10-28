namespace ProjectName.AgentCli;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Immutable self-bootstrap projection. Parsing a manifest never authorizes execution.
/// </summary>
public static class SelfBootstrapContract
{
    public const string RelativeManifestPath = "runtime/self-bootstrap.candidate.json";

    public sealed record StageSpec(string Id, string[] DependsOn, string Mode, bool Mutates, string Gate);
    public sealed record FrozenState(string Seed0033, string Seed0034, bool PreserveHistoricalTrails);
    public sealed record Manifest(
        string Contract, string Id, string Lifecycle, string Target, string IAm,
        string SkillType, string ActivationMode, int Depth, int MaxDepth,
        bool CheckerIndependent, bool AuthorityCreatesItself, bool AllowsWorkerStart,
        string SourceRef, string[] Roles, StageSpec[] Stages, FrozenState Frozen,
        string[] NonGoals);
    public sealed record StageState(
        string Id, string Mode, string Gate, string Status, bool Executed);
    public sealed record Projection(
        string Contract, string ManifestPath, string ManifestSha256,
        string ManifestStatus, string Decision, string Reason,
        bool IndependentlyChecked, bool GrantVerified, bool AnyStageExecuted,
        IReadOnlyList<StageState> Stages);

    private static readonly StageSpec[] ExpectedStages =
    [
        new("discover", [], "inspect", false, "none"),
        new("plan", ["discover"], "simulate", false, "none"),
        new("stage", ["plan"], "stage", true, "approved-pinned-provenance"),
        new("check", ["stage"], "check", false, "independent-checker"),
        new("authorize", ["check"], "authorize", true, "host-owner-grant"),
        new("activate", ["authorize"], "execute", true, "verified-activation-grant"),
        new("reflect", ["check"], "reflect", false, "append-only-trail")
    ];

    public static Projection Inspect(string root)
    {
        var path = Path.Combine(root, RelativeManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return Blocked(path, "", "MISSING", "bootstrap-manifest-not-found");

        try
        {
            var bytes = File.ReadAllBytes(path);
            var sha = Convert.ToHexString(SHA256.HashData(bytes));
            return Evaluate(System.Text.Encoding.UTF8.GetString(bytes), path, sha);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Blocked(path, "", "UNREADABLE", "bootstrap-manifest-unreadable");
        }
    }

    public static Projection Evaluate(string json, string path = "inline", string sha256 = "")
    {
        Manifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            });
        }
        catch (JsonException)
        {
            return Blocked(path, sha256, "INVALID", "bootstrap-manifest-malformed");
        }

        if (!Valid(manifest))
            return Blocked(path, sha256, "INVALID", "bootstrap-manifest-invariant-failed");

        var projection = ExpectedStages.Select(s => new StageState(
            s.Id, s.Mode, s.Gate,
            s.Id switch
            {
                "discover" => "READY_TO_INSPECT",
                "plan" => "READY_TO_PLAN",
                "stage" => "BLOCKED_APPROVAL",
                "check" => "BLOCKED_INDEPENDENT_CHECKER",
                "authorize" => "BLOCKED_HOST_GRANT",
                "activate" => "BLOCKED_VERIFIED_GRANT",
                _ => "PENDING_REFLECTION"
            },
            Executed: false)).ToArray();

        return new Projection(
            "pmcro.self-bootstrap/v1", path, sha256, "VALID_CANDIDATE",
            "HALT", "candidate-graph-only-no-independent-checker-or-grant",
            IndependentlyChecked: false, GrantVerified: false,
            AnyStageExecuted: false, projection);
    }

    private static bool Valid(Manifest? x)
    {
        if (x is null || x.Contract != "pmcro.self-bootstrap/v1"
            || x.Id != "pmcro-self-bootstrap" || x.Lifecycle != "generated"
            || x.Target != "self" || x.IAm != "I AM the Bootstrap Planner"
            || x.SkillType != "workflow" || x.ActivationMode != "inspect"
            || x.Depth != 0 || x.MaxDepth != 1 || !x.CheckerIndependent
            || x.AuthorityCreatesItself || x.AllowsWorkerStart
            || x.SourceRef != RelativeManifestPath
            || x.Frozen is null || x.Frozen.Seed0033 != "HALT"
            || x.Frozen.Seed0034 != "UNEXECUTED"
            || !x.Frozen.PreserveHistoricalTrails
            || x.Roles is null || x.Stages is null || x.NonGoals is null)
            return false;

        if (!x.Roles.SequenceEqual(["orchestrator", "planner", "maker", "checker", "reflector"]))
            return false;
        if (!x.NonGoals.Contains("self-issued grants", StringComparer.Ordinal)
            || !x.NonGoals.Contains("self-checking", StringComparer.Ordinal)
            || !x.NonGoals.Contains("unbounded recursion", StringComparer.Ordinal))
            return false;
        if (x.Stages.Length != ExpectedStages.Length)
            return false;

        for (var i = 0; i < ExpectedStages.Length; i++)
        {
            var actual = x.Stages[i];
            var expected = ExpectedStages[i];
            if (actual is null || actual.Id != expected.Id || actual.Mode != expected.Mode
                || actual.Mutates != expected.Mutates || actual.Gate != expected.Gate
                || actual.DependsOn is null || !actual.DependsOn.SequenceEqual(expected.DependsOn))
                return false;
        }
        return true;
    }

    private static Projection Blocked(string path, string sha, string status, string reason) =>
        new("pmcro.self-bootstrap/v1", path, sha, status, "HALT", reason,
            IndependentlyChecked: false, GrantVerified: false,
            AnyStageExecuted: false, []);
}