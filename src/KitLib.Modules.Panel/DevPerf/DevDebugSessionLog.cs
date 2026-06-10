using System.Text.Json;

namespace KitLib.DevPerf;

internal static class DevDebugSessionLog {
    const string SessionId = "d18cf5";
    static readonly string WorkspaceLogPath =
        @"c:\Users\WRXinYue\Documents\Project\STS2\STS2-KitLib\debug-d18cf5.log";

    internal static void Write(string hypothesisId, string location, string message, object? data = null) {
        var payload = new Dictionary<string, object?> {
            ["sessionId"] = SessionId,
            ["hypothesisId"] = hypothesisId,
            ["location"] = location,
            ["message"] = message,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["data"] = data,
        };

        string line;
        try {
            line = JsonSerializer.Serialize(payload);
        }
        catch {
            line = $"{{\"sessionId\":\"{SessionId}\",\"message\":\"{message}\"}}";
        }

        MainFile.Logger.Info($"[DBG-d18cf5] {line}");

        try {
            File.AppendAllText(WorkspaceLogPath, line + Environment.NewLine);
        }
        catch {
            try {
                var fallback = Path.Combine(DataPaths.BaseDir, "debug-d18cf5.log");
                File.AppendAllText(fallback, line + Environment.NewLine);
            }
            catch {
                // Best-effort only.
            }
        }
    }
}
