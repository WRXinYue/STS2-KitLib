using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace KitLib.McpProxy;

/// <summary>
/// Forwards MCP tool calls to the in-game DevMode HTTP bridge.
/// </summary>
internal sealed class GameBridgeClient : IDisposable {
    private readonly HttpClient _http;
    private readonly string _messagesUrl;
    private readonly int _port;

    public GameBridgeClient(int port) {
        _port = port;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _messagesUrl = $"http://localhost:{port}/messages";
    }

    public async Task<string> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default) {
        var request = new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject {
                ["name"] = toolName,
                ["arguments"] = arguments,
            },
        };

        try {
            using var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(_messagesUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractToolResultText(body);
        }
        catch (Exception ex) {
            throw new InvalidOperationException(
                $"KitLib MCP bridge unreachable on port {_port}. Is the game running with DevMode loaded? {ex.Message}",
                ex);
        }
    }

    private static string ExtractToolResultText(string jsonRpcBody) {
        var node = JsonNode.Parse(jsonRpcBody)?.AsObject();
        if (node?["error"] is JsonObject err)
            return err["message"]?.GetValue<string>() ?? jsonRpcBody;

        var content = node?["result"]?["content"]?.AsArray();
        if (content == null || content.Count == 0)
            return jsonRpcBody;

        return content[0]?["text"]?.GetValue<string>() ?? jsonRpcBody;
    }

    public void Dispose() => _http.Dispose();
}
