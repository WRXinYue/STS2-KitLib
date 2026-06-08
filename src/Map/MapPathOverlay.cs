using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Map;

/// <summary>Highlights AI-planned map path segments by tinting vanilla path dots.</summary>
internal static class MapPathOverlay {
    static readonly Color AiPathColor = new(1f, 0.82f, 0.15f, 1f);

    static readonly Dictionary<TextureRect, Color> SavedModulates = new();

    public static void Apply(NMapScreen screen, IReadOnlyList<(MapCoord From, MapCoord To)> edges) {
        Clear(screen);

        var paths = MapScreenReflection.GetPaths(screen);
        if (paths == null) return;

        foreach (var (from, to) in edges) {
            if (!paths.TryGetValue((from, to), out var ticks) || ticks == null)
                continue;

            foreach (var tick in ticks) {
                if (!GodotObject.IsInstanceValid(tick)) continue;
                if (!SavedModulates.ContainsKey(tick))
                    SavedModulates[tick] = tick.Modulate;
                tick.Modulate = AiPathColor;
            }
        }
    }

    public static void Clear(NMapScreen screen) {
        foreach (var (tick, original) in SavedModulates) {
            if (GodotObject.IsInstanceValid(tick))
                tick.Modulate = original;
        }

        SavedModulates.Clear();
    }
}
