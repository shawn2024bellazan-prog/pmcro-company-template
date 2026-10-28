using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
// MCP stdio is reserved for JSON-RPC. Every log must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class PmcroCandidateMarketplaceTools
{
    private static readonly HttpClient Ollama = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static string Root => Path.GetFullPath(
        Environment.GetEnvironmentVariable("PMCRO_CANDIDATE_ROOT") ?? Directory.GetCurrentDirectory());

    private static object ReadCandidateManifest(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z][a-z0-9-]{0,60}$"))
            return new { status = "rejected", reason = "invalid-marketplace-name" };
        string path = Path.Combine(Root, "marketplaces", name, "manifest.json");
        if (!File.Exists(path)) return new { status = "not_found", marketplace = name };
        var info = new FileInfo(path);
        if (info.Length is <= 0 or > 131072)
            return new { status = "rejected", reason = "manifest-size-limit" };
        // Candidate metadata never authenticates a registration or grant.
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var raw = json.RootElement;
        return new {
            status = "candidate_metadata_only",
            marketplace = name,
            nameField = raw.TryGetProperty("name", out var n) ? n.GetString() : name,
            version = raw.TryGetProperty("version", out var v) ? v.GetString() : null,
            declaredStatus = raw.TryGetProperty("status", out var s) ? s.GetString() : null,
            registered = false,
            granted = false,
            activated = false,
            executable = false
        };
    }

    [McpServerTool, Description("Read-only: list PMCR-O candidate marketplace names; discovery never grants execution.")]
    public static string ListCandidateMarketplaces()
    {
        string path = Path.Combine(Root, "marketplaces");
        string[] names = Directory.Exists(path)
            ? Directory.GetDirectories(path).Select(Path.GetFileName).Where(x => x is not null)
                .Select(x => x!).Where(x => File.Exists(Path.Combine(path, x, "manifest.json")))
                .OrderBy(x => x, StringComparer.Ordinal).Take(64).ToArray()
            : [];
        return JsonSerializer.Serialize(new {
            status = "candidate_discovery_only", root = Root, marketplaces = names,
            grantsAuthority = false, activated = false
        });
    }

    [McpServerTool, Description("Read-only: inspect a named candidate marketplace's manifest metadata. Cannot register or activate.")]
    public static string InspectCandidateMarketplace(
        [Description("Marketplace directory name, such as csuite-marketplace")] string marketplaceName)
        => JsonSerializer.Serialize(ReadCandidateManifest(marketplaceName));

    [McpServerTool, Description("Read-only: list candidate PMCR-O marketplace product manifests. Discovery cannot download, install, register or execute tools.")]
    public static string ListCandidateMarketplaceProducts()
    {
        var folder = Path.Combine(Root, "marketplaces", "pmcro-marketplace", "products");
        var products = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.candidate.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Where(x => x is not null)
                .Select(x => x!).OrderBy(x => x, StringComparer.Ordinal).Take(64).ToArray()
            : [];
        return JsonSerializer.Serialize(new {
            status = "candidate_discovery_only", products,
            registered = false, granted = false, activated = false,
            executable = false, mayDownload = false
        });
    }

    [McpServerTool, Description("Read-only: inspect a candidate marketplace product by safe filename; never executes a downloaded action.")]
    public static string InspectCandidateMarketplaceProduct(
        [Description("Safe candidate filename, for example download-action.candidate.json")] string productFile)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(productFile,
                "^[a-z][a-z0-9-]{0,60}\\.candidate\\.json$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return "{\"status\":\"rejected\",\"reason\":\"invalid-product-name\"}";
        var folder = Path.Combine(Root, "marketplaces", "pmcro-marketplace", "products");
        var file = Path.Combine(folder, productFile);
        if (!File.Exists(file)) return "{\"status\":\"not_found\"}";
        var info = new FileInfo(file);
        if (info.Length is <= 0 or > 65536)
            return "{\"status\":\"rejected\",\"reason\":\"product-manifest-size-limit\"}";
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var obj = json.RootElement;
        return JsonSerializer.Serialize(new {
            status = "candidate_metadata_only",
            product = productFile,
            id = obj.TryGetProperty("id", out var id) ? id.GetString() : null,
            declaredStatus = obj.TryGetProperty("status", out var state) ? state.GetString() : null,
            registered = false, granted = false, activated = false, executable = false
        });
    }

    [McpServerTool, Description("Read-only: report PMCR-O MCP host and authorization boundary; never claims Checker PASS.")]
    public static string GetPmcroCandidateStatus()
        => JsonSerializer.Serialize(new {
            status = "development_candidate",
            runtime = "stdio_dotnet_10_mcp",
            marketplaceRoot = Root,
            checker = "not_authenticated",
            roleAdmission = "deny_by_default",
            registration = "not_supported",
            grants = "not_supported",
            activation = "not_supported",
            externalToolExecution = "not_supported",
            ollamaInferenceEnabled = string.Equals(
                Environment.GetEnvironmentVariable("PMCRO_LOCAL_INFERENCE_ENABLED"),
                "true", StringComparison.Ordinal),
            grantsAuthority = false
        });

    [McpServerTool, Description("Read-only: check if the local Ollama model endpoint is reachable at 127.0.0.1:11434.")]
    public static async Task<string> OllamaLocalStatus(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Ollama.GetAsync("http://127.0.0.1:11434/api/tags", cancellationToken);
            return JsonSerializer.Serialize(new {
                status = response.IsSuccessStatusCode ? "ollama_reachable" : "ollama_error",
                httpStatus = (int)response.StatusCode,
                grantsAuthority = false
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return JsonSerializer.Serialize(new { status = "ollama_unavailable", grantsAuthority = false });
        }
    }

    [McpServerTool, Description("Optional: request a bounded proposal from a configured local Ollama model. No tool calls or side effects are executed.")]
    public static async Task<string> DraftWithLocalOllama(
        [Description("Messy intent to refine as an untrusted candidate proposal, maximum 2000 characters")] string messyIntent,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PMCRO_LOCAL_INFERENCE_ENABLED"), "true", StringComparison.Ordinal))
            return "{\"status\":\"blocked\",\"reason\":\"local-inference-opt-in-required\"}";
        if (string.IsNullOrWhiteSpace(messyIntent) || messyIntent.Length > 2000)
            return "{\"status\":\"blocked\",\"reason\":\"intent-size-invalid\"}";
        string? model = Environment.GetEnvironmentVariable("PMCRO_OLLAMA_MODEL");
        if (string.IsNullOrWhiteSpace(model) || model.Length > 128)
            return "{\"status\":\"blocked\",\"reason\":\"trusted-model-not-configured\"}";
        try
        {
            using var reply = await Ollama.PostAsJsonAsync("http://127.0.0.1:11434/api/generate",
                new {
                    model,
                    prompt = "Return one short, structured proposal for this input. Do not claim execution, permissions, grants, Checker PASS or activation.\n" + messyIntent,
                    stream = false,
                    options = new { num_predict = 320, temperature = 0.1 }
                }, cancellationToken);
            if (!reply.IsSuccessStatusCode)
                return "{\"status\":\"ollama_error\"}";
            using var payload = JsonDocument.Parse(await reply.Content.ReadAsStringAsync(cancellationToken));
            var result = payload.RootElement.TryGetProperty("response", out var prop) ? prop.GetString() ?? "" : "";
            return JsonSerializer.Serialize(new {
                status = "untrusted_model_proposal", result = result[..Math.Min(result.Length, 3000)],
                persisted = false, checkedByIndependentAgent = false, grantsAuthority = false,
                executedExternalTools = false
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return "{\"status\":\"ollama_unavailable_or_invalid_response\"}";
        }
    }
}