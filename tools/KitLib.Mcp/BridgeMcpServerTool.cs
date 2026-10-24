using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KitLib.McpProxy;

/// <summary>
/// Exposes one in-game bridge tool to the MCP client. The schema comes from the game's
/// <c>tools/list</c>, so the proxy declares no tool definitions of its own.
/// </summary>
internal sealed class BridgeMcpServerTool : McpServerTool {
    private readonly GameBridgeClient _bridge;
    private readonly Tool _protocolTool;

    public BridgeMcpServerTool(GameBridgeClient bridge, Tool protocolTool) {
        _bridge = bridge;
        _protocolTool = protocolTool;
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => Array.Empty<object>();

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default) {
        var arguments = new JsonObject();
        if (request.Params?.Arguments is { } raw) {
            foreach (var (key, value) in raw)
                arguments[key] = JsonNode.Parse(value.GetRawText());
        }

        long started = Stopwatch.GetTimestamp();
        try {
            string text = await _bridge.CallToolAsync(_protocolTool.Name, arguments, cancellationToken);
            CallLog.Write(_protocolTool.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Encoding.UTF8.GetByteCount(text), isError: false);
            return new CallToolResult {
                Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            };
        }
        catch (Exception ex) {
            CallLog.Write(_protocolTool.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Encoding.UTF8.GetByteCount(ex.Message), isError: true);
            return new CallToolResult {
                Content = new List<ContentBlock> { new TextContentBlock { Text = ex.Message } },
                IsError = true,
            };
        }
    }
}
