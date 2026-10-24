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

var bridge = new GameBridgeClient(port);
// Tool schemas live in the game; the proxy only adds the local launch/wait/stop helpers.
var bridgeTools = await BridgeToolCatalog.LoadAsync(bridge);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(consoleLogOptions => {
    // MCP stdio transport owns stdout; keep logs on stderr.
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(bridge);
builder.Services.AddSingleton<GameOrchestrator>();
builder.Services
    .AddMcpServer(options => {
        // Shipped to every client in initialize, so the usage policy travels with the server.
        options.ServerInstructions = """
        KitLib dev bridge for Slay the Spire 2. The game must be running; call dev_launch_game
        and dev_wait_bridge first, and dev_stop_game when done.

        Efficiency rules:
        - Read state with get_game_state. Add "sections" only when you need deck/relics/potions.
        - Merge independent reads into one dev_batch call.
        - Combat: plan the turn, then play it with one dev_combat_turn call (cards + end_turn).
          Do not call combat_action once per card unless you need to branch between plays.
        - Card indexes come from the latest get_game_state hand. dev_combat_turn re-resolves
          them by card identity, so the given order is safe.
        - After an action, wait with dev_wait_phase (play_phase, not_combat, or a phase name)
          instead of polling get_game_state.
        - Call get_map_state once per act for route planning, then map_action select_map_node
          with the returned available.index.
        - Use dev_read_text only for on-screen text; state reads are much cheaper.
        - Never poll get_game_state in a loop; every call costs one model round trip.
        """;
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithTools(bridgeTools);

await builder.Build().RunAsync();
