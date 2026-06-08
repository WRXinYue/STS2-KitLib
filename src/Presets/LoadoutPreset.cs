using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KitLib.Presets;

/// <summary>Flags controlling which parts of a loadout are captured or applied.</summary>
[Flags]
public enum PresetContents {
    None = 0,
    Cards = 1,
    Relics = 2,
    Stats = 4,
    All = Cards | Relics | Stats,
}

/// <summary>A single card entry in a loadout preset.</summary>
public sealed class LoadoutCardEntry {
    [JsonPropertyName("id")]
    public string CardId { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    [JsonPropertyName("upgrade")]
    public int UpgradeLevel { get; set; }
}

/// <summary>
/// Complete loadout preset: deck, relics, gold, HP, energy, stars, orb slots.
/// </summary>
public sealed class LoadoutPreset {
    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("currentHp")]
    public int CurrentHp { get; set; }

    [JsonPropertyName("maxHp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("energy")]
    public int Energy { get; set; }

    [JsonPropertyName("maxEnergy")]
    public int MaxEnergy { get; set; }

    [JsonPropertyName("stars")]
    public int Stars { get; set; }

    [JsonPropertyName("orbSlots")]
    public int OrbSlots { get; set; }

    [JsonPropertyName("cards")]
    public List<LoadoutCardEntry> Cards { get; set; } = new();

    [JsonPropertyName("relics")]
    public List<string> Relics { get; set; } = new();

    /// <summary>Which parts were captured. Defaults to All for backward compatibility.</summary>
    [JsonPropertyName("contents")]
    public PresetContents Contents { get; set; } = PresetContents.All;

    // ── Combat snapshot (optional, null for non-combat saves) ──

    [JsonPropertyName("handCards")]
    public List<LoadoutCardEntry>? HandCards { get; set; }

    [JsonPropertyName("drawCards")]
    public List<LoadoutCardEntry>? DrawCards { get; set; }

    [JsonPropertyName("discardCards")]
    public List<LoadoutCardEntry>? DiscardCards { get; set; }

    [JsonIgnore]
    public bool HasCombatSnapshot => HandCards != null;
}

/// <summary>Named preset wrapper for serialization.</summary>
public sealed class NamedPreset<T> where T : class, new() {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("data")]
    public T Data { get; set; } = new();
}
