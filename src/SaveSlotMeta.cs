using System;
using System.Collections.Generic;

namespace KitLib;

/// <summary>
/// Lightweight sidecar metadata stored alongside each save slot.
/// Used to populate the slot selection UI without deserializing the full run save.
/// </summary>
internal sealed class SaveSlotMeta {
    public string Name { get; set; } = "";
    public long SaveTime { get; set; }
    public int TotalFloor { get; set; }
    public int Gold { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public string CharacterId { get; set; } = "";
    public List<string> CardTitles { get; set; } = new();
    public List<string> RelicTitles { get; set; } = new();

    /// <summary>Run seed string captured at save time.</summary>
    public string Seed { get; set; } = "";

    /// <summary>Loaded mods at save time, each entry is "Name vX.Y.Z".</summary>
    public List<string> ModList { get; set; } = new();

    /// <summary>Optional AI/human debug label (e.g. "combat:ironclad-act1-boss"). Not shown in slot UI title.</summary>
    public string DebugNotes { get; set; } = "";

    public string FormattedTime => SaveTime > 0
        ? DateTimeOffset.FromUnixTimeSeconds(SaveTime).LocalDateTime.ToString("MM/dd HH:mm")
        : "";

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Save" : Name;
}
