using System;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KitLib.Scripts;

/// <summary>
/// Local WebSocket bridge for the Blockly editor.
/// Listens on <c>ws://localhost:7878</c> and handles save/load/list/delete/state commands.
/// No extra process required — runs on a background thread inside the game process.
/// </summary>
internal static class ScriptBridge {
    public const int Port = 7878;

    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;

    public static bool IsRunning => _listener?.IsListening ?? false;

    // ──────── Lifecycle ────────

    public static void Start() {
        if (IsRunning) return;
        try {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            Task.Run(() => AcceptLoop(_cts.Token));
            MainFile.Logger.Info($"[ScriptBridge] Listening on ws://localhost:{Port}");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ScriptBridge] Failed to start: {ex.Message}");
            _listener = null;
        }
    }

    public static void Stop() {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener?.Close();
        _listener = null;
    }

    // ──────── Accept loop ────────

    private static async Task AcceptLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                var ctx = await _listener!.GetContextAsync();

                // CORS pre-flight / health-check for plain HTTP GET
                if (!ctx.Request.IsWebSocketRequest) {
                    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    ctx.Response.StatusCode = 200;
                    var hello = Encoding.UTF8.GetBytes("{\"spirescratch\":true}");
                    ctx.Response.ContentLength64 = hello.Length;
                    ctx.Response.OutputStream.Write(hello);
                    ctx.Response.Close();
                    continue;
                }

                _ = Task.Run(() => HandleClient(ctx, ct));
            }
            catch when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) {
                if (!ct.IsCancellationRequested)
                    MainFile.Logger.Warn($"[ScriptBridge] Accept error: {ex.Message}");
            }
        }
    }

    // ──────── Per-client handler ────────

    private static async Task HandleClient(HttpListenerContext ctx, CancellationToken ct) {
        WebSocket? ws = null;
        try {
            var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null);
            ws = wsCtx.WebSocket;
            MainFile.Logger.Info("[ScriptBridge] Editor connected.");

            // Push initial state so editor knows the game is alive
            await SendAsync(ws, MakeState(), ct);

            var buf = new byte[256 * 1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested) {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (result.MessageType == WebSocketMessageType.Close) {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                    break;
                }
                if (result.MessageType != WebSocketMessageType.Text) continue;

                var msg = Encoding.UTF8.GetString(buf, 0, result.Count);
                var response = Dispatch(msg);
                if (response != null)
                    await SendAsync(ws, response, ct);
            }
        }
        catch when (ct.IsCancellationRequested) { }
        catch (Exception ex) { MainFile.Logger.Warn($"[ScriptBridge] Client error: {ex.Message}"); }
        finally {
            ws?.Dispose();
            MainFile.Logger.Info("[ScriptBridge] Editor disconnected.");
        }
    }

    // ──────── Message dispatch ────────

    private static string? Dispatch(string json) {
        int? callId = null;
        try {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Echo id back so the JS Promise can resolve
            if (root.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var id))
                callId = id;

            var cmd = root.TryGetProperty("cmd", out var c) ? c.GetString() ?? "" : "";
            var response = cmd switch {
                "save" => CmdSave(root),
                "load" => CmdLoad(root),
                "list" => CmdList(),
                "delete" => CmdDelete(root),
                "state" => MakeState(),
                _ => Error($"Unknown command: {cmd}"),
            };

            return InjectId(response, callId);
        }
        catch (Exception ex) { return InjectId(Error(ex.Message), callId); }
    }

    private static string InjectId(string responseJson, int? id) {
        if (id == null) return responseJson;
        // Insert "id":N into the JSON object
        var trimmed = responseJson.TrimStart();
        if (!trimmed.StartsWith('{')) return responseJson;
        return "{\"id\":" + id.Value + "," + trimmed.Substring(1);
    }

    private static string CmdSave(JsonElement el) {
        var fileName = el.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "untitled.json" : "untitled.json";
        var content = el.TryGetProperty("content", out var ct) ? ct.GetString() ?? "{}" : "{}";
        ScriptManager.SaveRaw(fileName, content);
        ScriptManager.Reload();
        return Respond("saved", new { fileName });
    }

    private static string CmdLoad(JsonElement el) {
        var fileName = el.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
        var content = ScriptManager.ReadRaw(fileName);
        if (content == null) return Error($"File not found: {fileName}");
        return Respond("loaded", new { fileName, content });
    }

    private static string CmdList() {
        var files = ScriptManager.Scripts.Select(s => new {
            fileName = s.FileName,
            name = s.ParseError == null ? s.Entry.Name : null,
            enabled = s.ParseError == null ? s.Entry.Enabled : (bool?)null,
            error = s.ParseError,
        }).ToArray();
        return Respond("list", new { files });
    }

    private static string CmdDelete(JsonElement el) {
        var fileName = el.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
        ScriptManager.DeleteScript(fileName);
        ScriptManager.Reload();
        return Respond("deleted", new { fileName });
    }

    private static string MakeState() {
        try {
            RunContext.TryGetRunAndPlayer(out _, out var player);
            var vars = ScriptVariableStore.All
                .ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            return Respond("state", new {
                connected = true,
                hp = player?.Creature?.CurrentHp ?? 0,
                maxHp = player?.Creature?.MaxHp ?? 0,
                floor = GetFloor(),
                inCombat = MegaCrit.Sts2.Core.Combat.CombatManager.Instance?.IsInProgress ?? false,
                variables = vars,
                scripts = ScriptManager.Scripts.Select(s => new {
                    fileName = s.FileName,
                    name = s.ParseError == null ? s.Entry.Name : s.FileName,
                    enabled = s.ParseError == null && s.Entry.Enabled,
                    error = s.ParseError,
                }).ToArray(),
            });
        }
        catch (Exception ex) {
            return Respond("state", new { connected = true, error = ex.Message });
        }
    }

    // ──────── Helpers ────────

    private static int GetFloor() {
        try {
            var rm = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
            if (rm == null || !rm.IsInProgress) return 0;
            return rm.DebugOnlyGetState()?.ActFloor ?? 0;
        }
        catch { return 0; }
    }

    private static readonly SemaphoreSlim _sendLock = new(1, 1);

    private static async Task SendAsync(WebSocket ws, string msg, CancellationToken ct) {
        if (ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(msg);
        await _sendLock.WaitAsync(ct);
        try {
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: ct);
        }
        finally { _sendLock.Release(); }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string Respond(string type, object? data = null) {
        var payload = new { type, data };
        return JsonSerializer.Serialize(payload, _jsonOpts);
    }

    private static string Error(string message)
        => JsonSerializer.Serialize(new { type = "error", data = new { message } }, _jsonOpts);
}
