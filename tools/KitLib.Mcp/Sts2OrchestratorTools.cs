using System.ComponentModel;
using ModelContextProtocol.Server;

namespace KitLib.McpProxy;

[McpServerToolType]
internal sealed class Sts2OrchestratorTools {
    private readonly GameOrchestrator _orchestrator;

    public Sts2OrchestratorTools(GameOrchestrator orchestrator) => _orchestrator = orchestrator;

    [McpServerTool(Name = "dev_launch_game"), Description(
        "Launch STS2 via scripts/launch_sts2.py (~1s). Build/deploy mods separately (e.g. make sync). Follow with dev_wait_bridge.")]
    public string DevLaunchGame() => _orchestrator.Launch().ToJsonString();

    [McpServerTool(Name = "dev_wait_bridge", ReadOnly = true), Description(
        "Poll GET /health until the in-game MCP bridge is ready.")]
    public async Task<string> DevWaitBridge(
        [Description("Bridge port (default 9877).")]
        int port = 9877,
        [Description("Timeout in seconds (default 120).")]
        double timeout_sec = 120,
        CancellationToken cancellationToken = default) =>
        (await _orchestrator.WaitBridgeAsync(port, timeout_sec, cancellationToken)).ToJsonString();

    [McpServerTool(Name = "dev_stop_game"), Description(
        "Stop all running STS2 processes on this machine (SlayTheSpire2 / Slay the Spire 2).")]
    public string DevStopGame() => _orchestrator.StopGame().ToJsonString();
}
