using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using KitLib.Mcp.Tools;

namespace KitLib.Mcp;

/// <summary>
/// In-game HTTP JSON-RPC bridge for MCP tools. External <c>tools/KitLib.Mcp</c> proxies stdio MCP here.
/// </summary>
internal static class McpBridge {
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static readonly McpToolRegistry Tools = new();

    public static bool IsRunning => _listener?.IsListening ?? false;

    public static void Start() {
        if (_listener != null)
            return;

        Tools.Register(new GameStateTool());
        Tools.Register(new CombatActionTool());
        Tools.Register(new MapActionTool());
        Tools.Register(new DevGetSessionTool());
        Tools.Register(new DevListSaveSlotsTool());
        Tools.Register(new DevTagSaveSlotTool());
        Tools.Register(new DevLoadSaveSlotTool());
        Tools.Register(new DevStartTestRunTool());
        Tools.Register(new DevListCardsTool());
        Tools.Register(new DevAddCardTool());
        Tools.Register(new DevRemoveCardTool());
        Tools.Register(new DevListMonstersTool());
        Tools.Register(new DevDumpMonsterMechanicsTool());
        Tools.Register(new DevListEnemiesTool());
        Tools.Register(new DevAddMonsterTool());
        Tools.Register(new DevSetCheatTool());
        Tools.Register(new DevSetStatTool());

        try {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{McpConfig.Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{McpConfig.Port}/");
            _listener.Start();
            _ = Task.Run(() => ListenLoop(_cts.Token));
            MainFile.Logger.Info(
                $"[McpBridge] Listening on http://localhost:{McpConfig.Port}/ (tools: {string.Join(", ", Tools.ListToolNames())})");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[McpBridge] Failed to start: {ex.Message}");
            _listener = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public static void Shutdown() {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    private static async Task ListenLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _listener != null) {
            try {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx), ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[McpBridge] Listen error: {ex.Message}");
            }
        }
    }

    private static async Task HandleRequest(HttpListenerContext ctx) {
        var req = ctx.Request;
        var res = ctx.Response;

        try {
            var path = req.Url?.AbsolutePath ?? "";
            string body;

            switch (path) {
                case "/health":
                    body = """{"status":"ok","mod":"KitLib"}""";
                    break;

                case "/messages" when req.HttpMethod == "POST":
                    body = await HandleJsonRpc(req);
                    break;

                default:
                    res.StatusCode = 404;
                    body = """{"error":"Not found"}""";
                    break;
            }

            var buffer = Encoding.UTF8.GetBytes(body);
            res.ContentType = "application/json";
            res.ContentLength64 = buffer.Length;
            await res.OutputStream.WriteAsync(buffer);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[McpBridge] Request error: {ex.Message}");
        }
        finally {
            try { res.Close(); } catch { }
        }
    }

    private static async Task<string> HandleJsonRpc(HttpListenerRequest req) {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var rawJson = await reader.ReadToEndAsync();

        try {
            var msg = JsonNode.Parse(rawJson)?.AsObject();
            if (msg == null)
                return JsonRpcError(null, -32700, "Parse error");

            var method = msg["method"]?.GetValue<string>() ?? "";
            var id = msg["id"];
            var paramsObj = msg["params"]?.AsObject();

            return method switch {
                "initialize" => JsonRpcResult(id, new JsonObject {
                    ["protocolVersion"] = "2024-11-05",
                    ["serverInfo"] = new JsonObject {
                        ["name"] = "devmode-mcp-bridge",
                        ["version"] = "1.0.0",
                    },
                    ["capabilities"] = new JsonObject {
                        ["tools"] = new JsonObject(),
                    },
                }),

                "tools/list" => JsonRpcResult(id, new JsonObject {
                    ["tools"] = Tools.ListToolSchemas(),
                }),

                "tools/call" => await HandleToolCall(id, paramsObj),

                "ping" => JsonRpcResult(id, new JsonObject()),

                _ => JsonRpcError(id, -32601, $"Method not found: {method}"),
            };
        }
        catch (Exception ex) {
            return JsonRpcError(null, -32603, ex.Message);
        }
    }

    private static async Task<string> HandleToolCall(JsonNode? id, JsonObject? paramsObj) {
        var toolName = paramsObj?["name"]?.GetValue<string>() ?? "";
        var args = paramsObj?["arguments"]?.AsObject();

        try {
            var result = await McpMainThread.InvokeAsync(() => Tools.CallAsync(toolName, args));
            return JsonRpcResult(id, result);
        }
        catch (Exception ex) {
            return JsonRpcError(id, -32000, ex.Message);
        }
    }

    private static string JsonRpcResult(JsonNode? id, JsonNode result) {
        var response = new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result,
        };
        return response.ToJsonString();
    }

    private static string JsonRpcError(JsonNode? id, int code, string message) {
        var response = new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject {
                ["code"] = code,
                ["message"] = message,
            },
        };
        return response.ToJsonString();
    }
}
