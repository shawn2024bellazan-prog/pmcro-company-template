namespace ProjectName.AgentCli;

using System.Text.Json;

/// <summary>
/// Read-only product bootstrap plan. This intentionally does not use the runtime host,
/// control-plane stores, installers, model processes, or network transports.
/// </summary>
public static class InitCommand
{
    public sealed record Component(string Name, bool Present, string Evidence, string Meaning);
    public sealed record Step(string Id, string Action, string Gate);
    public sealed record BootstrapPlan(
        string Contract,
        string Outcome,
        string Root,
        string Ui,
        string Model,
        string Client,
        IReadOnlyList<Component> Inventory,
        IReadOnlyList<Step> NextSteps,
        bool InstallPerformed,
        bool DownloadPerformed,
        bool RegistrationPerformed,
        bool GrantIssued,
        bool ActivationPerformed,
        bool WorkersStarted,
        string Authority);

    public static int Run(string command, string[] args)
    {
        if (args.Any(a => a is "--help" or "-h"))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var plan = CreatePlan(args);
            var json = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.WriteLine($"PMCR-O {command}: {plan.Outcome}");
            Console.WriteLine($"Root: {plan.Root}");
            Console.WriteLine($"Target: UI={plan.Ui}, model={plan.Model}, client={plan.Client}");
            foreach (var item in plan.Inventory)
                Console.WriteLine($"  {(item.Present ? "FOUND" : "MISSING")} {item.Name}: {item.Meaning}");
            if (command.Equals("init", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Unexecuted setup sequence:");
                foreach (var step in plan.NextSteps)
                    Console.WriteLine($"  {step.Id}: {step.Action} [gate: {step.Gate}]");
            }
            Console.WriteLine($"Authority: {plan.Authority}");
            Console.WriteLine("No downloads, installs, registrations, grants, activations, or worker starts occurred.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"pmcro-{command}-error:{ex.Message}");
            return 2;
        }
    }

    public static BootstrapPlan CreatePlan(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var ui = "copilotkit";
        var model = "ollama";
        var client = "vscode";

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (key == "--json") continue;
            if (key is "--apply" or "--install" or "--activate" or "--grant" or "--start" or "--yes")
                throw new ArgumentException("mutation-not-supported-by-candidate-init");
            if (key is not ("--root" or "--ui" or "--model" or "--client") || i + 1 == args.Length)
                throw new ArgumentException("unknown-or-incomplete-option:" + key);
            var value = args[++i];
            if (value.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("option-value-required:" + key);
            switch (key)
            {
                case "--root": root = value; break;
                case "--ui": ui = value.ToLowerInvariant(); break;
                case "--model": model = value.ToLowerInvariant(); break;
                case "--client": client = value.ToLowerInvariant(); break;
            }
        }

        if (ui is not ("copilotkit" or "maui" or "both" or "none"))
            throw new ArgumentException("ui-must-be-copilotkit-maui-both-or-none");
        if (model is not ("ollama" or "cloud" or "hybrid"))
            throw new ArgumentException("model-must-be-ollama-cloud-or-hybrid");
        if (client is not ("vscode" or "none"))
            throw new ArgumentException("client-must-be-vscode-or-none");
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new ArgumentException("root-directory-not-found:" + root);

        Component FileCheck(string name, string relative, string meaning)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return new Component(name, File.Exists(path), path, meaning);
        }

        var inventory = new List<Component>
        {
            FileCheck("governance", "AGENTS.md", "workspace policy available; not a grant"),
            FileCheck("marketplace-catalog", ".agents/plugins/marketplace.json", "discovery metadata only"),
            FileCheck("vscode-mcp", ".vscode/mcp.json", "client configuration; not server activation"),
            FileCheck("aspire-apphost", "plugins/pmcro-runtime/src/AgentSystem.AppHost/AppHost.cs", "source present; not running"),
            FileCheck("marketplace-mcp", "plugins/pmcro-runtime/src/ProjectName.Marketplace.Mcp/Program.cs", "source present; no authenticated execution implied"),
            FileCheck("cli", "plugins/pmcro-runtime/src/ProjectName.AgentCli/Program.cs", "source present; plan-only init is independent of runtime host")
        };

        if (ui is "maui" or "both")
            inventory.Add(FileCheck("maui-studio", "plugins/pmcro-runtime/src/ProjectName.Studio.Maui/ProjectName.Studio.Maui.csproj",
                "optional MAUI product shell; candidate not provisioned by this command"));
        if (ui is "copilotkit" or "both")
            inventory.Add(FileCheck("copilotkit-studio", "apps/pmcro-studio/package.json",
                "web product shell; another repository may contain a version"));
        if (client == "vscode")
            inventory.Add(new Component("vscode-cli", ExecutableOnPath("code"), "code on PATH",
                "discovery only; marketplace and MCP configuration require separate review"));
        if (model is "ollama" or "hybrid")
            inventory.Add(new Component("ollama-cli", ExecutableOnPath("ollama"), "ollama on PATH",
                "discovery only; models are not pulled or started"));
        inventory.Add(new Component("dotnet-cli", ExecutableOnPath("dotnet"), "dotnet on PATH",
            "discovery only; SDK compatibility/build is not tested"));

        var steps = new List<Step>
        {
            new("1", "Review inventory, product target, source hashes, and frozen candidate boundary", "no authority required"),
            new("2", "Review pinned marketplace downloads, provenance, SHA-256, and local staging destination", "explicit user approval before download"),
            new("3", "Generate VS Code MCP and PMCR-O Studio projection proposals", "candidate-only; no registration"),
            new("4", "Build and test .NET, web, or MAUI targets in an isolated development branch", "test evidence and code review"),
            new("5", "Obtain host-authenticated independent Checker verdict for exact artifacts", "independent Checker with verified registration, grant, and identity"),
            new("6", "Request separately scoped runtime grants and activation, if desired", "host owner authorization and PASS; never implicit")
        };

        return new BootstrapPlan(
            "pmcro.bootstrap-plan/v1",
            "CANDIDATE_PLAN_ONLY",
            root,
            ui,
            model,
            client,
            inventory,
            steps,
            InstallPerformed: false,
            DownloadPerformed: false,
            RegistrationPerformed: false,
            GrantIssued: false,
            ActivationPerformed: false,
            WorkersStarted: false,
            Authority: "Discovery is not authorization. G: Seed 0033 remains HALT; Seed 0034 remains unexecuted.");
    }

    private static bool ExecutableOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                try
                {
                    if (File.Exists(Path.Combine(directory.Trim('"'), name + extension)))
                        return true;
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.Security.SecurityException)
                {
                    // Malformed/untrusted PATH entries are not execution authority.
                }
            }
        }
        return false;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("pmcro init [--root <existing-directory>] [--ui copilotkit|maui|both|none] [--model ollama|cloud|hybrid] [--client vscode|none] [--json]");
        Console.WriteLine("pmcro doctor [same options]");
        Console.WriteLine("init creates an in-memory candidate setup plan; doctor inspects local artifacts only.");
        Console.WriteLine("No network, installers, writes, grants, registration, activation, workers, or AppHost startup.");
        Console.WriteLine("Chat route /pmcro:init requires a separate authorized adapter; the CLI does not install one.");
    }
}