using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(settings =>
{
    // MCP stdio must never have non-protocol text on stdout.
    settings.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class ArithmeticTools
{
    [McpServerTool, Description("Read-only proof: add two integers.")]
    public static int Add(int a, int b) => checked(a + b);
}
