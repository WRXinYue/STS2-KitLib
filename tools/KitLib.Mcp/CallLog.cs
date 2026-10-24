using System.Text;
using System.Text.Json.Nodes;

namespace KitLib.McpProxy;

/// <summary>
/// Appends one JSON line per bridge call so real interaction latency can be reviewed later.
/// 每次桥接调用追加一行 JSON，便于回看真实交互耗时。
/// </summary>
internal static class CallLog {
    private static readonly object Gate = new();
    private static readonly StreamWriter? Writer = CreateWriter();

    private static StreamWriter? CreateWriter() {
        try {
            string? custom = Environment.GetEnvironmentVariable("KITLIB_MCP_CALL_LOG");
            string path = custom ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KitLib.Mcp", "calls.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new StreamWriter(path, append: true, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch {
            return null;
        }
    }

    public static void Write(string tool, double elapsedMs, int bytes, bool isError) {
        if (Writer is null)
            return;

        var line = new JsonObject {
            ["ts"] = DateTimeOffset.Now.ToString("O"),
            ["tool"] = tool,
            ["ms"] = Math.Round(elapsedMs, 1),
            ["bytes"] = bytes,
            ["error"] = isError,
        }.ToJsonString();

        lock (Gate)
            Writer.WriteLine(line);
    }
}
