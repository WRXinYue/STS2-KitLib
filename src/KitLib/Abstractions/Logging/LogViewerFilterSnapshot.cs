using System.Text.Json.Serialization;

using KitLib.Abstractions.Logging;

namespace KitLib.Logging;

/// <summary>Filter state streamed over the KitLib log pipe for <c>kitlog attach --sync-viewer</c>.</summary>
public sealed class LogViewerFilterSnapshot {
    [JsonPropertyName("version")]
    public int Version { get; init; } = LogViewerFilterContract.Version;

    [JsonPropertyName("minLevel")]
    public string? MinLevel { get; init; }

    [JsonPropertyName("textFilter")]
    public string TextFilter { get; init; } = "";

    [JsonPropertyName("suppressRules")]
    public SuppressRule[] SuppressRules { get; init; } = [];

    [JsonPropertyName("hiddenSources")]
    public string[] HiddenSources { get; init; } = [];

    [JsonPropertyName("loadedModIds")]
    public string[] LoadedModIds { get; init; } = [];

    [JsonPropertyName("modIdAliases")]
    public Dictionary<string, string> ModIdAliases { get; init; } = new(StringComparer.Ordinal);

    public sealed class SuppressRule {
        [JsonPropertyName("pattern")]
        public string Pattern { get; init; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; } = true;
    }
}
