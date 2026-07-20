using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.Host;
using KitLib.Logging;

namespace KitLib.CombatStats;

/// <summary>Local dev panel at <c>http://127.0.0.1:9878</c> — logs, combat stats, and future tools.</summary>
internal static class DevViewerServer {
    public const int Port = 9878;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static string? _viewerHtml;
    private static readonly ConcurrentDictionary<WebSocket, byte> _statsClients = new();
    private static readonly ConcurrentDictionary<WebSocket, byte> _logClients = new();
    private static bool _trackerSubscribed;
    private static bool _logHubSubscribed;
    private static Action<LogStreamEntry>? _logHubHandler;
    private static bool _browserLaunchedThisSession;
    private static int _startupOpenGeneration;
    private const int StartupOpenDelayMs = 2500;

    public static bool IsRunning => _listener?.IsListening ?? false;

    public static bool HasConnectedViewerClients =>
        !_logClients.IsEmpty || !_statsClients.IsEmpty;

    public static string BaseUrl => $"http://127.0.0.1:{Port}/";

    public static string LogsUrl => $"{BaseUrl}#/logs";

    public static void EnsureStarted() {
        if (_listener != null)
            return;

        if (!KitLibBootstrapGate.CanStartHttpListener)
            throw new InvalidOperationException("Dev viewer is not available yet.");

        try {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            _ = Task.Run(() => ListenLoop(_cts.Token));
            EnsureTrackerSubscription();
            EnsureLogHubSubscription();
        }
        catch (Exception ex) {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
            BootstrapDiagnostics.RecordFailure("DevViewerServer", ex);
            throw;
        }
    }

    /// <param name="force">When false, skips <see cref="OS.ShellOpen"/> if the viewer was already opened this session.</param>
    public static string OpenInBrowser(string? hash = null, bool force = false) {
        EnsureStarted();
        var url = string.IsNullOrEmpty(hash) ? BaseUrl : $"{BaseUrl}#{hash.TrimStart('#')}";
        if (!force && _browserLaunchedThisSession)
            return url;

        OS.ShellOpen(url);
        _browserLaunchedThisSession = true;
        return url;
    }

    public static string OpenLogsInBrowser(string? query = null, bool force = false) {
        var hash = string.IsNullOrEmpty(query) ? "logs" : $"logs?{query.TrimStart('?')}";
        return OpenInBrowser(hash, force);
    }

    /// <summary>
    /// Startup auto-open: wait briefly for an existing browser tab to reconnect, then open only if none connected.
    /// </summary>
    public static void ScheduleOpenLogsIfNoClient(string? query = null) {
        EnsureStarted();
        var generation = ++_startupOpenGeneration;
        _ = Task.Run(async () => {
            try {
                await Task.Delay(StartupOpenDelayMs).ConfigureAwait(false);
                if (generation != _startupOpenGeneration)
                    return;

                Callable.From(() => {
                    if (generation != _startupOpenGeneration)
                        return;
                    if (HasConnectedViewerClients) {
                        _browserLaunchedThisSession = true;
                        return;
                    }
                    OpenLogsInBrowser(query, force: false);
                }).CallDeferred();
            }
            catch (Exception ex) {
                KitLog.Debug("DevViewer", $"Startup open skipped: {ex.Message}");
            }
        });
    }

    public static void Shutdown() {
        _startupOpenGeneration++;
        if (_trackerSubscribed) {
            CombatStatsTracker.Changed -= OnTrackerChanged;
            _trackerSubscribed = false;
        }

        if (_logHubSubscribed && _logHubHandler != null) {
            LogStreamHub.Unsubscribe(_logHubHandler);
            _logHubHandler = null;
            _logHubSubscribed = false;
        }

        _cts?.Cancel();
        CloseAll(_statsClients);
        CloseAll(_logClients);
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _browserLaunchedThisSession = false;
    }

    static void CloseAll(ConcurrentDictionary<WebSocket, byte> clients) {
        foreach (var client in clients.Keys) {
            try { client.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None).Wait(200); } catch { }
            try { client.Dispose(); } catch { }
        }
        clients.Clear();
    }

    private static void EnsureTrackerSubscription() {
        if (_trackerSubscribed)
            return;

        CombatStatsTracker.Changed += OnTrackerChanged;
        _trackerSubscribed = true;
    }

    private static void EnsureLogHubSubscription() {
        if (_logHubSubscribed)
            return;

        _logHubHandler = OnLogEntry;
        LogStreamHub.Subscribe(_logHubHandler);
        _logHubSubscribed = true;
    }

    private static void OnTrackerChanged() {
        var live = CombatStatsLiveBuffer.Latest;
        if (live == null)
            return;

        long revision = CombatStatsLiveBuffer.Revision;
        foreach (var socket in _statsClients.Keys.ToArray())
            _ = SendStats(socket, live, revision);
    }

    private static void OnLogEntry(LogStreamEntry entry) {
        foreach (var socket in _logClients.Keys.ToArray())
            _ = SendLogEntry(socket, entry);
    }

    private static async Task ListenLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _listener != null) {
            try {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx, ct), ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) {
                KitLog.Warn("DevViewer", $"Listen error: {ex.Message}");
            }
        }
    }

    private static async Task HandleRequest(HttpListenerContext ctx, CancellationToken serverCt) {
        var req = ctx.Request;
        var res = ctx.Response;
        var path = req.Url?.AbsolutePath ?? "/";

        try {
            if (req.IsWebSocketRequest) {
                if (path == "/api/ws") {
                    var wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null);
                    await HandleStatsWebSocket(wsContext.WebSocket, serverCt);
                    return;
                }

                if (path == "/api/logs/ws") {
                    var wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null);
                    await HandleLogWebSocket(wsContext.WebSocket, serverCt);
                    return;
                }
            }

            switch (path) {
                case "/":
                case "/index.html":
                    await WriteBuffer(res, Encoding.UTF8.GetBytes(GetViewerHtml()), "text/html; charset=utf-8");
                    return;

                case "/api/live":
                    await WriteLiveSnapshot(res);
                    return;

                case "/api/export/json":
                    await WriteExportJson(res);
                    return;

                default:
                    res.StatusCode = 404;
                    await WriteBuffer(res, Encoding.UTF8.GetBytes("""{"error":"Not found"}"""), "application/json; charset=utf-8");
                    return;
            }
        }
        catch (Exception ex) {
            KitLog.Warn("DevViewer", $"Request error: {ex.Message}");
        }
        finally {
            try { res.Close(); } catch { }
        }
    }

    private static async Task WriteLiveSnapshot(HttpListenerResponse res) {
        if (!CombatStatsLiveBuffer.TryReadJson(out string json) || string.IsNullOrWhiteSpace(json)) {
            json = JsonSerializer.Serialize(new CombatStatsLiveDto {
                Active = null,
                IsActive = false,
            }, JsonOptions);
        }

        res.Headers.Add("X-Combat-Stats-Revision", CombatStatsLiveBuffer.Revision.ToString());
        await WriteBuffer(res, Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8");
    }

    private static async Task WriteExportJson(HttpListenerResponse res) {
        var bundle = CombatStatsExport.CaptureBundle();
        string json = CombatStatsExport.ToJson(bundle);
        string filename = $"combat-stats-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        res.Headers.Add("Content-Disposition", $"attachment; filename=\"{filename}\"");
        await WriteBuffer(res, Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8");
    }

    private static async Task WriteBuffer(HttpListenerResponse res, byte[] buffer, string contentType) {
        res.ContentType = contentType;
        res.Headers.Add("Cache-Control", "no-store");
        res.ContentLength64 = buffer.Length;
        await res.OutputStream.WriteAsync(buffer);
    }

    private static async Task HandleStatsWebSocket(WebSocket socket, CancellationToken serverCt) {
        _statsClients[socket] = 0;

        try {
            long revision = CombatStatsLiveBuffer.Revision;
            await SendJson(socket, new { type = "hello", revision });
            await SendStats(socket);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
            await ReceiveStatsLoop(socket, linked.Token);
        }
        catch (Exception ex) {
            KitLog.Warn("DevViewer", $"Stats WebSocket error: {ex.Message}");
        }
        finally {
            _statsClients.TryRemove(socket, out _);
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
            socket.Dispose();
        }
    }

    private static async Task HandleLogWebSocket(WebSocket socket, CancellationToken serverCt) {
        EnsureLogHubSubscription();
        _logClients[socket] = 0;

        try {
            await SendJson(socket, new { type = "hello", stream = "logs" });

            var filter = LogStreamHub.CurrentFilter;
            if (filter != null)
                await SendJson(socket, new { type = "filter", filter });

            foreach (var entry in LogStreamHub.GetHistorySnapshot())
                await SendLogEntry(socket, entry);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
            await ReceiveLogLoop(socket, linked.Token);
        }
        catch (Exception ex) {
            KitLog.Warn("DevViewer", $"Log WebSocket error: {ex.Message}");
        }
        finally {
            _logClients.TryRemove(socket, out _);
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
            socket.Dispose();
        }
    }

    private static async Task ReceiveStatsLoop(WebSocket socket, CancellationToken ct) {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await HandleStatsClientMessage(socket, text);
        }
    }

    private static async Task ReceiveLogLoop(WebSocket socket, CancellationToken ct) {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            try {
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
                string type = doc.RootElement.TryGetProperty("type", out var typeEl)
                    ? typeEl.GetString() ?? ""
                    : "";
                if (type == "ping")
                    await SendJson(socket, new { type = "pong" });
            }
            catch {
                // ignore malformed frames
            }
        }
    }

    private static async Task HandleStatsClientMessage(WebSocket socket, string text) {
        try {
            using var doc = JsonDocument.Parse(text);
            string type = doc.RootElement.TryGetProperty("type", out var typeEl)
                ? typeEl.GetString() ?? ""
                : "";

            switch (type) {
                case "ping":
                    await SendJson(socket, new { type = "pong" });
                    break;
                case "requestStats":
                    await SendStats(socket);
                    break;
                case "exportJson": {
                        var bundle = CombatStatsExport.CaptureBundle();
                        string filename = $"combat-stats-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                        await SendJson(socket, new { type = "exported", format = "json", filename, payload = bundle });
                        break;
                    }
            }
        }
        catch (Exception ex) {
            KitLog.Warn("DevViewer", $"Stats client message error: {ex.Message}");
        }
    }

    private static async Task SendStats(WebSocket socket) {
        if (socket.State != WebSocketState.Open)
            return;

        var live = CombatStatsLiveBuffer.Latest ?? CombatStatsLiveBuffer.Persist();
        await SendStats(socket, live, CombatStatsLiveBuffer.Revision);
    }

    private static async Task SendStats(WebSocket socket, CombatStatsLiveDto live, long revision) {
        if (socket.State != WebSocketState.Open)
            return;

        await SendJson(socket, new { type = "stats", payload = live, revision });
    }

    private static async Task SendLogEntry(WebSocket socket, LogStreamEntry entry) {
        if (socket.State != WebSocketState.Open)
            return;

        if (entry.IsFilterFrame) {
            await SendJson(socket, new { type = "filter", filter = entry.Filter });
            return;
        }

        await SendJson(socket, new {
            type = "log",
            entry = new {
                entry.Ts,
                entry.Lvl,
                entry.Text,
                entry.Mod,
                entry.Scope,
                entry.Boundary,
            },
        });
    }

    private static async Task SendJson(WebSocket socket, object message) {
        if (socket.State != WebSocketState.Open)
            return;

        string json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    private static string GetViewerHtml() {
        _viewerHtml ??= CombatStatsExport.LoadLiveViewerShell();
        return _viewerHtml;
    }
}
