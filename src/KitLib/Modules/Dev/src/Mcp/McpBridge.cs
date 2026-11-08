using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using KitLib;
using KitLib.Host;
using KitLib.Mcp.Tools;

namespace KitLib.Mcp;

/// <summary>
/// In-game MCP server. Serves the MCP Streamable HTTP transport at <c>POST /mcp</c> (full tool set)
/// and <c>POST /mcp-core</c> (Core profile for token-sensitive clients), so MCP clients (DSH, Cursor,
/// Claude Code, ...) can connect to the running game directly and update together with the mod.
/// Keeps the legacy <c>POST /messages</c> JSON-RPC endpoint for the optional stdio proxy
/// (<c>tools/KitLib.Mcp</c>) used by stdio-only clients.
/// </summary>
internal static class McpBridge {
    private const string HealthPath = "/health";
    private const string LegacyPath = "/messages";
    private const string McpPath = "/mcp";
    private const string McpCorePath = "/mcp-core";

    private static readonly DateTime StartedAt = DateTime.UtcNow;
    private static readonly McpSessionStore Sessions = new();

    private static readonly string ServerVersion =
        typeof(McpBridge).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>Shipped to every MCP client in the <c>initialize</c> result.</summary>
    private const string ServerInstructions = """
        KitLib dev bridge for Slay the Spire 2 (in-game MCP server). The game must be running; the
        server lives inside the KitLib mod, so it updates together with the mod.

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

        Launch helpers (dev_launch_game, dev_wait_bridge, dev_stop_game) exist only in the optional
        stdio proxy (tools/KitLib.Mcp). Over Streamable HTTP, start the game yourself (make dev-session).
        """;

    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static readonly McpToolRegistry Tools = new();

    public static bool IsRunning => _listener?.IsListening ?? false;

    public static void Start() {
        if (_listener != null)
            return;

        ThreadPool.QueueUserWorkItem(_ => StartCore());
    }

    internal static void StartCore() {
        if (_listener != null || !KitLibBootstrapGate.CanStartHttpListener)
            return;

        Tools.Register(new GameStateTool());
        Tools.Register(new CombatActionTool());
        Tools.Register(new GetSelectionStateTool());
        Tools.Register(new SelectionActionTool());
        Tools.Register(new GetCombatStatsTool());
        Tools.Register(new MapActionTool());
        Tools.Register(new GetMapStateTool());
        Tools.Register(new DevGetSessionTool());
        Tools.Register(new DevListSaveSlotsTool());
        Tools.Register(new DevTagSaveSlotTool());
        Tools.Register(new DevLoadSaveSlotTool());
        Tools.Register(new DevStartTestRunTool());
        Tools.Register(new DevListCardsTool());
        Tools.Register(new DevAddCardTool());
        Tools.Register(new DevRemoveCardTool());
        Tools.Register(new DevAddRelicTool());
        Tools.Register(new DevHoverTool());
        Tools.Register(new DevListMonstersTool());
        Tools.Register(new DevListEnemiesTool());
        Tools.Register(new DevAddMonsterTool());
        Tools.Register(new DevSetCheatTool());
        Tools.Register(new DevSetStatTool());
        Tools.Register(new DevWaitPhaseTool());
        Tools.Register(new DevReadTextTool());
        Tools.Register(new DevCombatTurnTool());
        Tools.Register(new DevBatchTool(Tools));

        try {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{McpConfig.Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{McpConfig.Port}/");
            _listener.Start();
            KitLog.Info("McpBridge",
                $"MCP server listening on http://127.0.0.1:{McpConfig.Port}/mcp " +
                $"(core={Tools.CoreCount}/{Tools.Count} tools, auth={(McpConfig.Token is null ? "none" : "bearer")})");
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }
        catch (Exception ex) {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            BootstrapDiagnostics.RecordFailure("McpBridge", ex);
        }
    }

    public static void Shutdown() {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
        Sessions.Clear();
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
                KitLog.Warn("McpBridge", $"Listen error: {ex.Message}");
            }
        }
    }

    private static async Task HandleRequest(HttpListenerContext ctx) {
        var req = ctx.Request;
        var res = ctx.Response;

        try {
            var path = req.Url?.AbsolutePath ?? "";

            switch (path) {
                case HealthPath:
                    await WriteJsonAsync(res, 200, BuildHealthJson());
                    return;

                case LegacyPath when req.HttpMethod == "POST": {
                        if (!await EnsureAuthorizedAsync(req, res))
                            return;
                        var body = await HandleLegacyJsonRpcAsync(req, coreOnly: false);
                        await WriteJsonAsync(res, 200, body);
                        return;
                    }

                case McpPath when req.HttpMethod == "POST":
                    await HandleMcpPostAsync(req, res, coreOnly: false);
                    return;

                case McpCorePath when req.HttpMethod == "POST":
                    await HandleMcpPostAsync(req, res, coreOnly: true);
                    return;

                case McpPath or McpCorePath when req.HttpMethod == "GET":
                    // No server-initiated SSE stream; MCP allows 405 here.
                    res.AddHeader("Allow", "POST, DELETE");
                    await WriteJsonAsync(res, 405, """{"error":"SSE stream not supported; use POST /mcp"}""");
                    return;

                case McpPath or McpCorePath when req.HttpMethod == "DELETE":
                    // The bridge is stateless; sessions need no explicit termination.
                    res.AddHeader("Allow", "POST");
                    await WriteJsonAsync(res, 405, """{"error":"Session termination not supported"}""");
                    return;

                default:
                    await WriteJsonAsync(res, 404, """{"error":"Not found"}""");
                    return;
            }
        }
        catch (Exception ex) {
            KitLog.Warn("McpBridge", $"Request error: {ex.Message}");
        }
        finally {
            try { res.Close(); } catch { }
        }
    }

    /// <summary>MCP Streamable HTTP: one JSON-RPC message per POST, JSON response or 202 for notifications.</summary>
    private static async Task HandleMcpPostAsync(HttpListenerRequest req, HttpListenerResponse res, bool coreOnly) {
        if (!await EnsureAuthorizedAsync(req, res))
            return;

        var contentType = req.ContentType ?? "";
        if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)) {
            await WriteJsonAsync(res, 415, McpJsonRpc.Error(null, -32600, "Content-Type must be application/json"));
            return;
        }

        // Per spec, clients send the negotiated revision on every follow-up request.
        var protocolHeader = req.Headers["MCP-Protocol-Version"];
        if (!string.IsNullOrEmpty(protocolHeader) && !McpProtocol.IsSupported(protocolHeader)) {
            await WriteJsonAsync(res, 400,
                McpJsonRpc.Error(null, -32600, $"Unsupported MCP-Protocol-Version: {protocolHeader}"));
            return;
        }

        // A client that presents a session id must reference a live session (404 => re-initialize).
        var sessionId = req.Headers["Mcp-Session-Id"];
        if (!string.IsNullOrEmpty(sessionId)) {
            if (!Sessions.Contains(sessionId)) {
                await WriteJsonAsync(res, 404,
                    McpJsonRpc.Error(null, -32001, "Unknown MCP session; re-initialize."));
                return;
            }
            Sessions.Touch(sessionId);
        }

        string rawJson;
        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
            rawJson = await reader.ReadToEndAsync();

        JsonObject? msg;
        try {
            msg = JsonNode.Parse(rawJson) as JsonObject;
        }
        catch {
            await WriteJsonAsync(res, 400, McpJsonRpc.Error(null, -32700, "Parse error"));
            return;
        }

        if (msg == null) {
            await WriteJsonAsync(res, 400, McpJsonRpc.Error(null, -32600, "Expected a single JSON-RPC object"));
            return;
        }

        var method = msg["method"]?.GetValue<string>() ?? "";
        var isInitialize = string.Equals(method, "initialize", StringComparison.Ordinal);
        var newSessionId = isInitialize ? Guid.NewGuid().ToString("N") : null;

        string? response;
        try {
            response = await DispatchAsync(msg, coreOnly);
        }
        catch (Exception ex) {
            response = McpJsonRpc.Error(msg["id"], -32603, ex.Message);
        }

        if (response == null) {
            // Notification (no id): acknowledge with 202 and an empty body.
            res.StatusCode = 202;
            res.ContentLength64 = 0;
            return;
        }

        if (newSessionId != null && response.Contains("\"result\"", StringComparison.Ordinal)) {
            Sessions.Add(newSessionId);
            res.AddHeader("Mcp-Session-Id", newSessionId);
        }

        await WriteJsonAsync(res, 200, response);
    }

    /// <summary>Legacy endpoint used by <c>tools/KitLib.Mcp</c>; always answers 200 with a JSON body.</summary>
    private static async Task<string> HandleLegacyJsonRpcAsync(HttpListenerRequest req, bool coreOnly) {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var rawJson = await reader.ReadToEndAsync();

        try {
            var msg = JsonNode.Parse(rawJson)?.AsObject();
            if (msg == null)
                return McpJsonRpc.Error(null, -32700, "Parse error");

            return await DispatchAsync(msg, coreOnly) ?? "";
        }
        catch (Exception ex) {
            return McpJsonRpc.Error(null, -32603, ex.Message);
        }
    }

    /// <summary>Routes one JSON-RPC message. Returns <c>null</c> for notifications (no response).</summary>
    private static async Task<string?> DispatchAsync(JsonObject msg, bool coreOnly) {
        var method = msg["method"]?.GetValue<string>() ?? "";
        var paramsObj = msg["params"] as JsonObject;

        // Notifications carry no "id" and get no JSON-RPC response.
        if (!msg.ContainsKey("id"))
            return null;

        var id = msg["id"];

        switch (method) {
            case "initialize": {
                    var init = BuildInitializeResult(paramsObj);
                    KitLog.Debug("McpBridge",
                        $"initialize client={ClientLabel(paramsObj)} protocol={init["protocolVersion"]} core={coreOnly}");
                    return McpJsonRpc.Result(id, init);
                }

            case "tools/list": {
                    var schemas = Tools.ListToolSchemas(coreOnly);
                    KitLog.Debug("McpBridge", $"tools/list core={coreOnly} count={schemas.Count}");
                    return McpJsonRpc.Result(id, new JsonObject { ["tools"] = schemas });
                }

            case "tools/call":
                return await HandleToolCallAsync(id, paramsObj, coreOnly);

            case "ping":
                return McpJsonRpc.Result(id, new JsonObject());

            default:
                return McpJsonRpc.Error(id, -32601, $"Method not found: {method}");
        }
    }

    private static JsonObject BuildInitializeResult(JsonObject? paramsObj) {
        var requested = paramsObj?["protocolVersion"]?.GetValue<string>();

        return new JsonObject {
            ["protocolVersion"] = McpProtocol.Negotiate(requested),
            ["capabilities"] = new JsonObject {
                ["tools"] = new JsonObject(),
            },
            ["serverInfo"] = new JsonObject {
                ["name"] = "kitlib-devmode",
                ["version"] = ServerVersion,
            },
            ["instructions"] = ServerInstructions,
        };
    }

    private static string ClientLabel(JsonObject? paramsObj) {
        var info = paramsObj?["clientInfo"] as JsonObject;
        var name = info?["name"]?.GetValue<string>();
        var version = info?["version"]?.GetValue<string>();
        return string.IsNullOrEmpty(name) ? "unknown" : $"{name}/{version ?? "?"}";
    }

    private static async Task<string> HandleToolCallAsync(JsonNode? id, JsonObject? paramsObj, bool coreOnly) {
        var toolName = paramsObj?["name"]?.GetValue<string>() ?? "";
        var args = paramsObj?["arguments"]?.AsObject();

        if (coreOnly && !McpToolCatalog.IsCore(toolName)) {
            return McpJsonRpc.Result(id, McpToolRegistry.ErrorResult(
                $"Tool '{toolName}' is not exposed by the Core endpoint; connect to /mcp for the full set."));
        }

        try {
            var result = await McpMainThread.InvokeAsync(() => Tools.CallAsync(toolName, args));
            return McpJsonRpc.Result(id, result);
        }
        catch (Exception ex) {
            // Tool-level failure (e.g. game scene not available) stays a visible result, not a
            // JSON-RPC error, so clients hand the reason to the model.
            return McpJsonRpc.Result(id, McpToolRegistry.ErrorResult(ex.Message));
        }
    }

    private static async Task<bool> EnsureAuthorizedAsync(HttpListenerRequest req, HttpListenerResponse res) {
        var token = McpConfig.Token;
        if (token is null || McpAuth.IsAuthorized(token, req.Headers["Authorization"]))
            return true;

        res.AddHeader("WWW-Authenticate", "Bearer");
        await WriteJsonAsync(res, 401, McpJsonRpc.Error(null, -32001, "Unauthorized"));
        return false;
    }

    private static string BuildHealthJson() {
        var protocols = new JsonArray();
        foreach (var version in McpProtocol.SupportedProtocolVersions)
            protocols.Add(version);

        return new JsonObject {
            ["status"] = "ok",
            ["mod"] = "KitLib",
            ["version"] = ServerVersion,
            ["port"] = McpConfig.Port,
            ["toolCount"] = Tools.Count,
            ["coreToolCount"] = Tools.CoreCount,
            ["sessions"] = Sessions.Count,
            ["auth"] = McpConfig.Token is null ? "none" : "bearer",
            ["protocols"] = protocols,
            ["uptimeSec"] = (int)(DateTime.UtcNow - StartedAt).TotalSeconds,
        }.ToJsonString();
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, int statusCode, string body) {
        res.StatusCode = statusCode;
        res.ContentType = "application/json";
        var buffer = Encoding.UTF8.GetBytes(body);
        res.ContentLength64 = buffer.Length;
        await res.OutputStream.WriteAsync(buffer);
    }
}
