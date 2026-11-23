using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Expected exactly one argument: path to the built server DLL.");
    return 2;
}

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "PMCR-O MCP Add proof",
    Command = "dotnet",
    Arguments = [Path.GetFullPath(args[0])]
});

// A real MCP initialize exchange occurs inside CreateAsync.
await using var client = await McpClient.CreateAsync(transport);
var tools = await client.ListToolsAsync(); // real tools/list
var names = tools.Select(t => t.Name).ToArray();
var addTool = tools.Single(t => string.Equals(t.Name, "add", StringComparison.OrdinalIgnoreCase));
var result = await client.CallToolAsync(addTool.Name,
    new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 }); // real tools/call
var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(t => t.Text)).Trim();
var verified = text.Trim('"') == "5";
Console.WriteLine(JsonSerializer.Serialize(new
{
    initialize = true,
    tools_list = names,
    tools_call = addTool.Name,
    arguments = new { a = 2, b = 3 },
    content_text = text,
    expected = "5",
    verified
}, new JsonSerializerOptions { WriteIndented = true }));
return verified ? 0 : 3;
