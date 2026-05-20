using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace DevMode.Actions;

internal static class EnemyActions {
    /// <summary>All encounters grouped by room type, sorted by name.</summary>
    public static IReadOnlyList<EncounterModel> GetAllEncounters(RoomType? filter = null) {
        var all = ModelDb.AllEncounters;
        if (filter != null)
            all = all.Where(e => e.RoomType == filter.Value);
        return all.OrderBy(e => ((AbstractModel)e).Id.Entry).ToList();
    }

    /// <summary>Set a global encounter override (all combat rooms).</summary>
    public static void SetGlobalOverride(EncounterModel encounter) {
        DevModeState.EnemyMode = EnemyMode.Global;
        DevModeState.GlobalEncounterOverride = encounter;
        ClearRoomTypeOverrides();
        MainFile.Logger.Info($"EnemyActions: Global override set to {((AbstractModel)encounter).Id.Entry}");
    }

    /// <summary>Set a per-room-type encounter override.</summary>
    public static void SetRoomTypeOverride(RoomType roomType, EncounterModel encounter) {
        DevModeState.EnemyMode = EnemyMode.PerType;
        DevModeState.GlobalEncounterOverride = null;
        DevModeState.RoomTypeOverrides[roomType] = encounter;
        MainFile.Logger.Info($"EnemyActions: {roomType} override set to {((AbstractModel)encounter).Id.Entry}");
    }

    /// <summary>Set a per-floor encounter override.</summary>
    public static void SetFloorOverride(int floor, EncounterModel encounter) {
        DevModeState.FloorOverrides[floor] = encounter;
        MainFile.Logger.Info($"EnemyActions: Floor {floor} override set to {((AbstractModel)encounter).Id.Entry}");
    }

    /// <summary>Remove a per-floor override.</summary>
    public static void ClearFloorOverride(int floor) {
        DevModeState.FloorOverrides.Remove(floor);
        MainFile.Logger.Info($"EnemyActions: Floor {floor} override cleared");
    }

    public static void ClearGlobalOverride() {
        DevModeState.GlobalEncounterOverride = null;
        if (DevModeState.EnemyMode == EnemyMode.Global)
            DevModeState.EnemyMode = EnemyMode.Off;
        MainFile.Logger.Info("EnemyActions: Global override cleared");
    }

    public static void ClearRoomTypeOverride(RoomType roomType) {
        DevModeState.RoomTypeOverrides[roomType] = null;
        if (DevModeState.EnemyMode == EnemyMode.PerType
            && DevModeState.RoomTypeOverrides.Values.All(enc => enc == null)) {
            DevModeState.EnemyMode = EnemyMode.Off;
        }
        MainFile.Logger.Info($"EnemyActions: {roomType} override cleared");
    }

    /// <summary>Clear all overrides.</summary>
    public static void ClearAll() {
        DevModeState.ClearEnemyOverrides();
        MainFile.Logger.Info("EnemyActions: All enemy overrides cleared");
    }

    /// <summary>Get a display-friendly name for an encounter.</summary>
    public static string GetDisplayName(EncounterModel encounter) {
        var id = ((AbstractModel)encounter).Id.Entry;
        var monsters = encounter.AllPossibleMonsters;
        if (monsters != null && monsters.Any()) {
            var names = monsters.Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry).Distinct();
            return $"{id} ({string.Join(", ", names)})";
        }
        return id;
    }

    /// <summary>Short name for compact UI.</summary>
    public static string GetShortName(EncounterModel encounter) {
        return ((AbstractModel)encounter).Id.Entry;
    }

    private static void ClearRoomTypeOverrides() {
        DevModeState.RoomTypeOverrides[RoomType.Monster] = null;
        DevModeState.RoomTypeOverrides[RoomType.Elite] = null;
        DevModeState.RoomTypeOverrides[RoomType.Boss] = null;
    }
}
