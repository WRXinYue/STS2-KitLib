using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace DevMode.Presets;

public sealed class CardEditTemplate {
    [JsonPropertyName("baseCost")]
    public int? BaseCost { get; set; }

    [JsonPropertyName("replayCount")]
    public int? ReplayCount { get; set; }

    [JsonPropertyName("damage")]
    public int? Damage { get; set; }

    [JsonPropertyName("block")]
    public int? Block { get; set; }

    [JsonPropertyName("dynamicVars")]
    public Dictionary<string, int>? DynamicVars { get; set; }

    [JsonPropertyName("exhaust")]
    public bool? Exhaust { get; set; }

    [JsonPropertyName("ethereal")]
    public bool? Ethereal { get; set; }

    [JsonPropertyName("unplayable")]
    public bool? Unplayable { get; set; }

    [JsonPropertyName("exhaustOnNextPlay")]
    public bool? ExhaustOnNextPlay { get; set; }

    [JsonPropertyName("singleTurnRetain")]
    public bool? SingleTurnRetain { get; set; }

    [JsonPropertyName("singleTurnSly")]
    public bool? SingleTurnSly { get; set; }

    [JsonPropertyName("nameOverride")]
    public string? NameOverride { get; set; }

    [JsonPropertyName("descriptionOverride")]
    public string? DescriptionOverride { get; set; }
}

public sealed class CardEditNamedPreset {
    [JsonPropertyName("cardId")]
    public string CardId { get; set; } = "";

    [JsonPropertyName("template")]
    public CardEditTemplate Template { get; set; } = new();
}

internal static class CardEditPresetManager {
    private static string PresetsDir => DataPaths.PresetsDir;

    private static readonly System.Lazy<PresetStore<CardEditNamedPreset>> _store = new(() => {
        var store = new PresetStore<CardEditNamedPreset>(Path.Combine(PresetsDir, "card-edit-presets.json"));
        store.Load();
        return store;
    });

    public static PresetStore<CardEditNamedPreset> Store => _store.Value;
}
