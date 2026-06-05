using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DevMode.Map;

/// <summary>Reflection helpers for private <see cref="NMapScreen"/> members used by map cheats and AI path overlay.</summary>
internal static class MapScreenReflection {
    private static readonly MethodInfo? RecalculateTravelabilityMethod =
        AccessTools.Method(typeof(NMapScreen), "RecalculateTravelability");

    private static readonly AccessTools.FieldRef<NMapScreen, Dictionary<MapCoord, NMapPoint>>? MapPointDictionaryRef =
        AccessTools.FieldRefAccess<NMapScreen, Dictionary<MapCoord, NMapPoint>>("_mapPointDictionary");

    private static readonly FieldInfo? PathsField =
        AccessTools.Field(typeof(NMapScreen), "_paths");

    public static void RecalculateTravelability(NMapScreen screen) {
        if (RecalculateTravelabilityMethod == null) {
            MainFile.Logger.Warn("[DevMode.MapJump] RecalculateTravelability method not found");
            return;
        }

        try {
            RecalculateTravelabilityMethod.Invoke(screen, null);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode.MapJump] RecalculateTravelability failed: {ex.Message}");
        }
    }

    public static Dictionary<MapCoord, NMapPoint>? GetMapPoints(NMapScreen screen) {
        if (MapPointDictionaryRef == null) {
            MainFile.Logger.Warn("[DevMode.MapJump] _mapPointDictionary field not found");
            return null;
        }

        try {
            return MapPointDictionaryRef(screen);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode.MapJump] GetMapPoints failed: {ex.Message}");
            return null;
        }
    }

    public static Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>? GetPaths(NMapScreen screen) {
        if (PathsField == null) {
            MainFile.Logger.Warn("[DevMode.MapAiPath] _paths field not found");
            return null;
        }

        try {
            return PathsField.GetValue(screen) as Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode.MapAiPath] GetPaths failed: {ex.Message}");
            return null;
        }
    }
}
