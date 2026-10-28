using System.Text.Json.Nodes;
using ProjectName.AgentCli;
using Xunit;

namespace ProjectName.Runtime.Tests;

public sealed class PmcroSelfBootstrapContractTests
{
    private static string FindRoot()
    {
        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "runtime", "self-bootstrap.candidate.json")))
                return cursor.FullName;
            cursor = cursor.Parent;
        }
        throw new DirectoryNotFoundException("self-bootstrap-candidate-not-found");
    }

    private static string ManifestJson() =>
        File.ReadAllText(Path.Combine(FindRoot(), "runtime", "self-bootstrap.candidate.json"));

    [Fact]
    public void Self_bootstrap_manifest_projects_a_finite_graph_without_execution()
    {
        var actual = SelfBootstrapContract.Inspect(FindRoot());
        Assert.Equal("VALID_CANDIDATE", actual.ManifestStatus);
        Assert.Equal("HALT", actual.Decision);
        Assert.Equal(64, actual.ManifestSha256.Length);
        Assert.False(actual.IndependentlyChecked);
        Assert.False(actual.GrantVerified);
        Assert.False(actual.AnyStageExecuted);
        Assert.Equal(new[] { "discover", "plan", "stage", "check", "authorize", "activate", "reflect" },
            actual.Stages.Select(s => s.Id));
        Assert.All(actual.Stages, s => Assert.False(s.Executed));
        Assert.Equal("BLOCKED_INDEPENDENT_CHECKER",
            actual.Stages.Single(s => s.Id == "check").Status);
    }

    [Theory]
    [InlineData("maxDepth", 2)]
    [InlineData("depth", 2)]
    public void Recursive_expansion_above_one_is_rejected(string field, int replacement)
    {
        var node = JsonNode.Parse(ManifestJson())!;
        node[field] = replacement;
        Assert.Equal("INVALID", SelfBootstrapContract.Evaluate(node.ToJsonString()).ManifestStatus);
    }

    [Theory]
    [InlineData("checkerIndependent")]
    [InlineData("authorityCreatesItself")]
    [InlineData("allowsWorkerStart")]
    public void Forgeable_authority_claims_are_rejected(string field)
    {
        var node = JsonNode.Parse(ManifestJson())!;
        node[field] = field != "checkerIndependent";
        Assert.Equal("INVALID", SelfBootstrapContract.Evaluate(node.ToJsonString()).ManifestStatus);
    }

    [Fact]
    public void Checker_removal_or_bypass_cannot_become_a_valid_graph()
    {
        var node = JsonNode.Parse(ManifestJson())!;
        node["stages"]!.AsArray()[3]!["id"] = "auto-checker";
        Assert.Equal("INVALID", SelfBootstrapContract.Evaluate(node.ToJsonString()).ManifestStatus);
    }

    [Fact]
    public void Unexpected_grant_fields_are_rejected()
    {
        var node = JsonNode.Parse(ManifestJson())!;
        node["effectiveGrant"] = "forged";
        Assert.Equal("INVALID", SelfBootstrapContract.Evaluate(node.ToJsonString()).ManifestStatus);
    }

    [Fact]
    public void Missing_manifest_stops_without_side_effects()
    {
        var root = Path.GetTempPath();
        var actual = SelfBootstrapContract.Inspect(root);
        Assert.Equal("HALT", actual.Decision);
        Assert.False(actual.AnyStageExecuted);
        Assert.False(actual.GrantVerified);
    }
}