using ProjectName.AgentCli;
using Xunit;

namespace ProjectName.Runtime.Tests;

public sealed class PmcroInitCommandTests
{
    [Fact]
    public void Default_init_does_not_cross_any_execution_boundary()
    {
        var plan = InitCommand.CreatePlan([]);

        Assert.Equal("pmcro.bootstrap-plan/v1", plan.Contract);
        Assert.Equal("CANDIDATE_PLAN_ONLY", plan.Outcome);
        Assert.Equal("copilotkit", plan.Ui);
        Assert.Equal("ollama", plan.Model);
        Assert.Equal("vscode", plan.Client);
        Assert.False(plan.InstallPerformed);
        Assert.False(plan.DownloadPerformed);
        Assert.False(plan.RegistrationPerformed);
        Assert.False(plan.GrantIssued);
        Assert.False(plan.ActivationPerformed);
        Assert.False(plan.WorkersStarted);
        Assert.Contains("0033", plan.Authority);
        Assert.Contains("0034", plan.Authority);
        Assert.Equal(6, plan.NextSteps.Count);
    }

    [Fact]
    public void Maui_and_hybrid_mode_are_candidates_not_installed_products()
    {
        var plan = InitCommand.CreatePlan([
            "--root", Directory.GetCurrentDirectory(), "--ui", "both",
            "--model", "hybrid", "--client", "none", "--json"
        ]);

        Assert.Equal("both", plan.Ui);
        Assert.Equal("hybrid", plan.Model);
        Assert.DoesNotContain(plan.Inventory, item => item.Name == "vscode-cli");
        Assert.Contains(plan.Inventory, item => item.Name == "maui-studio");
        Assert.Contains(plan.Inventory, item => item.Name == "copilotkit-studio");
        Assert.Contains(plan.Inventory, item => item.Name == "ollama-cli");
        Assert.False(plan.ActivationPerformed);
    }

    [Theory]
    [InlineData("--apply")]
    [InlineData("--install")]
    [InlineData("--activate")]
    [InlineData("--grant")]
    [InlineData("--start")]
    [InlineData("--yes")]
    public void Mutating_flags_are_rejected_not_silently_ignored(string flag)
    {
        var err = Assert.Throws<ArgumentException>(() => InitCommand.CreatePlan([flag]));
        Assert.Contains("mutation-not-supported", err.Message);
    }

    [Fact]
    public void Unknown_or_malformed_options_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => InitCommand.CreatePlan(["--ui", "desktop"]));
        Assert.Throws<ArgumentException>(() => InitCommand.CreatePlan(["--model", "unknown"]));
        Assert.Throws<ArgumentException>(() => InitCommand.CreatePlan(["--root"]));
        Assert.Throws<ArgumentException>(() => InitCommand.CreatePlan(["--made-up"]));
    }
}