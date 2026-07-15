using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitLib.Logging;

/// <summary>Structured log frame for the KitLib log pipe (version <see cref="LogStreamContract.Version"/>).</summary>
public sealed class LogStreamEntry {
    static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("v")]
    public int V { get; init; } = LogStreamContract.Version;

    [JsonPropertyName("ts")]
    public long Ts { get; init; }

    [JsonPropertyName("lvl")]
    public string Lvl { get; init; } = "info";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("mod")]
    public string? Mod { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("boundary")]
    public bool Boundary { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = KindLog;

    [JsonPropertyName("filter")]
    public LogViewerFilterSnapshot? Filter { get; init; }

    public const string KindLog = "log";
    public const string KindFilter = "filter";

    public bool IsFilterFrame => string.Equals(Kind, KindFilter, StringComparison.Ordinal);

    public string Fingerprint => $"{Lvl}|{Text}";

    public static LogStreamEntry FromKitLog(
        KitLogLevel level,
        string modId,
        string? scope,
        string message,
        string hostModId = "KitLib") {
        var text = KitLibLogFormat.FormatGameCallbackText(modId, scope, message, hostModId);
        return new LogStreamEntry {
            Ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Lvl = LevelToken(level),
            Text = text,
            Mod = modId,
            Scope = string.IsNullOrWhiteSpace(scope) ? null : scope.Trim(),
        };
    }

    public static LogStreamEntry FromGameCallback(string lvl, string text, long tsUnixMs, bool boundary)
        => new() {
            Ts = tsUnixMs,
            Lvl = lvl,
            Text = text,
            Boundary = boundary,
        };

    public static LogStreamEntry FromFilterSnapshot(LogViewerFilterSnapshot snapshot)
        => new() {
            Kind = KindFilter,
            Filter = snapshot,
            Ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

    public static string LevelToken(KitLogLevel level) => level switch {
        KitLogLevel.Error => "error",
        KitLogLevel.Warn => "warn",
        KitLogLevel.Debug => "debug",
        _ => "info",
    };

    public static string GameLevelToken(int gameLevelOrdinal) => gameLevelOrdinal switch {
        4 => "error",
        3 => "warn",
        1 => "debug",
        0 => "vdb",
        5 => "load",
        _ => "info",
    };

    public byte[] ToJsonBytes() {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public static LogStreamEntry? FromJsonBytes(ReadOnlySpan<byte> jsonBytes) {
        if (jsonBytes.Length == 0)
            return null;

        try {
            return JsonSerializer.Deserialize<LogStreamEntry>(jsonBytes, JsonOptions);
        }
        catch (JsonException) {
            return null;
        }
    }
}
