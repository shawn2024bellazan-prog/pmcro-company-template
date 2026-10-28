using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectName.Application.Abstractions;
using ProjectName.AgentCli;
using ProjectName.Harness;
using ProjectName.Marketplace;

// Plan-only bootstrap must bypass runtime host construction entirely.
if (args.Length > 0 && (args[0].Equals("init", StringComparison.OrdinalIgnoreCase)
    || args[0].Equals("doctor", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = InitCommand.Run(args[0].ToLowerInvariant(), args.Skip(1).ToArray());
    return;
}

var builder = Host.CreateApplicationBuilder(args);
var projectRoot = RuntimePathResolver.ResolveProjectRoot(
    builder.Configuration["AgentSystem:ProjectRoot"]
    ?? Environment.GetEnvironmentVariable("AGENTSYSTEM_PROJECT_ROOT"));
var configuredAgentSystemRoot = builder.Configuration["AgentSystem:Root"]
    ?? Environment.GetEnvironmentVariable("AGENTSYSTEM_ROOT");
var agentSystemRoot = string.IsNullOrWhiteSpace(configuredAgentSystemRoot)
    ? null
    : RuntimePathResolver.ResolveAgentSystemRoot(configuredAgentSystemRoot, projectRoot);
var marketplaceRoot = builder.Configuration["AgentSystem:MarketplaceRoot"]
    ?? Environment.GetEnvironmentVariable("AGENTSYSTEM_MARKETPLACE_ROOT")
    ?? projectRoot;
var pmcroMarketplaceRoot = builder.Configuration["AgentSystem:PmcroMarketplaceRoot"]
    ?? Environment.GetEnvironmentVariable("PMCRO_MARKETPLACE_ROOT")
    ?? projectRoot;
var messageBusRoot = RuntimePathResolver.ResolveProjectMessageBusRoot(
    builder.Configuration["AgentSystem:MessageBusRoot"]
    ?? Environment.GetEnvironmentVariable("PMCRO_MESSAGE_BUS_ROOT"), projectRoot);

builder.Services.AddProjectNamePmcroRuntime(
    autoRun: PmcroRuntimePolicy.Bounded,
    marketplaceRoot: marketplaceRoot,
    messageBusRoot: messageBusRoot,
    agentSystemRoot: agentSystemRoot,
    pmcroMarketplaceRoot: pmcroMarketplaceRoot);
using var host = builder.Build();
var operations = host.Services.GetRequiredService<IPmcroOperations>();

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine("ProjectName Agent CLI");
    Console.WriteLine("  init [--json]        Plan PMCR-O Studio, MCP, VS Code, and model setup without changes.");
    Console.WriteLine("  doctor [--json]      Inspect bootstrap readiness without executing services.");
    Console.WriteLine("  commands [--json]    Show PMCR-O commands, roles, phases, and route syntax.");
    Console.WriteLine("  ui                   Render the rich terminal projection.");
    Console.WriteLine("  control-plane ...    Run the Trail-bound production control-plane command.");
    Console.WriteLine("  marketplace [--json] Show discovered marketplace counts and authority boundary.");
    Console.WriteLine("  messaging [--json]   Show PMCR-O message-contract/runtime bootstrap readiness.");
    return;
}

if (args[0].Equals("control-plane", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = await ControlPlaneCommand.RunAsync(args.Skip(1).ToArray());
    return;
}

if (args[0].Equals("ui", StringComparison.OrdinalIgnoreCase))
{
    new TerminalUi(operations).Render();
    return;
}

if (args[0].Equals("marketplace", StringComparison.OrdinalIgnoreCase))
{
    var catalog = host.Services.GetRequiredService<IMarketplaceCatalog>();
    var payload = new
    {
        configured = catalog.Snapshot.Configured,
        root = catalog.Snapshot.Root,
        pluginCount = catalog.Snapshot.PluginCount,
        pluginSkillCount = catalog.Snapshot.PluginSkillCount,
        authoringSkillCount = catalog.Snapshot.AuthoringSkillCount,
        authorityGrantedByDiscovery = false
    };
    if (args.Skip(1).Any(x => x.Equals("--json", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    else
        Console.WriteLine($"configured={payload.configured} plugins={payload.pluginCount} pluginSkills={payload.pluginSkillCount} authoringSkills={payload.authoringSkillCount} authorityGranted=false");
    return;
}

if (args[0].Equals("messaging", StringComparison.OrdinalIgnoreCase))
{
    var policy = host.Services.GetRequiredService<IMessagePolicyCatalog>();
    var s = policy.Snapshot;
    var payload = new
    {
        configured = s.Configured,
        marketplaceRoot = s.MarketplaceRoot,
        apiVersion = s.ApiVersion,
        contractRef = s.ContractRef,
        defaultOutputStyle = s.DefaultOutputStyle,
        skillsOwnPayloadOnly = s.SkillsOwnPayloadOnly,
        runtimeOwnsEnvelope = s.RuntimeOwnsEnvelope,
        persistBeforeRender = s.PersistBeforeRender,
        durableMessageBus = !string.IsNullOrWhiteSpace(messageBusRoot),
        messageBusRoot,
        authorityGrantedByBootstrap = false
    };
    if (args.Skip(1).Any(x => x.Equals("--json", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    else
        Console.WriteLine($"configured={payload.configured} contract={payload.contractRef} style={payload.defaultOutputStyle} durableBus={payload.durableMessageBus} authorityGranted=false");
    return;
}

if (args[0].Equals("commands", StringComparison.OrdinalIgnoreCase))
{
    var catalog = operations.GetCommands();
    if (args.Skip(1).Any(x => x.Equals("--json", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine(JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
        return;
    }

    Console.WriteLine(catalog.HumanRoute);
    Console.WriteLine($"Raw text: {catalog.RawTextPolicy}");
    Console.WriteLine($"Init: {catalog.Init}");
    Console.WriteLine("Commands: " + string.Join(", ", catalog.Commands));
    Console.WriteLine("Roles: " + string.Join(", ", catalog.Roles));
    Console.WriteLine("Phases: " + string.Join(", ", catalog.Phases));
    return;
}

Console.Error.WriteLine($"unknown-command:{args[0]}");
Environment.ExitCode = 2;
