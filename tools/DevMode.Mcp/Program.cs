using KitLib.McpProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var port = 9877;
for (int i = 0; i < args.Length; i++) {
    if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
        port = p;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions => {
    // MCP stdio transport owns stdout; keep logs on stderr.
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(new GameBridgeClient(port));
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
