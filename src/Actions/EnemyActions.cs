using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.Actions;

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
        KitLibState.EnemyMode = EnemyMode.Global;
        KitLibState.GlobalEncounterOverride = encounter;
        ClearRoomTypeOverrides();
        MainFile.Logger.Info($"EnemyActions: Global override set to {((AbstractModel)encounter).Id.Entry}");
    }

    /// <summary>Set a per-room-type encounter override.</summary>
    public static void SetRoomTypeOverride(RoomType roomType, EncounterModel encounter) {
        KitLibState.EnemyMode = EnemyMode.PerType;
        KitLibState.GlobalEncounterOverride = null;
        KitLibState.RoomTypeOverrides[roomType] = encounter;
        MainFile.Logger.Info($"EnemyActions: {roomType} override set to {((AbstractModel)encounter).Id.Entry}");
    }

    /// <summary>Set a per-floor encounter override.</summary>
    public static void SetFloorOverride(int floor, EncounterModel encounter) {
        KitLibState.FloorOverrides[floor] = encounter;
        MainFile.Logger.Info($"EnemyActions: Floor {floor} override set to {((AbstractModel)encounter).Id.Entry}");
    }

    /// <summary>Remove a per-floor override.</summary>
    public static void ClearFloorOverride(int floor) {
        KitLibState.FloorOverrides.Remove(floor);
        MainFile.Logger.Info($"EnemyActions: Floor {floor} override cleared");
    }

    public static void ClearGlobalOverride() {
        KitLibState.GlobalEncounterOverride = null;
        if (KitLibState.EnemyMode == EnemyMode.Global)
            KitLibState.EnemyMode = EnemyMode.Off;
        MainFile.Logger.Info("EnemyActions: Global override cleared");
    }

    public static void ClearRoomTypeOverride(RoomType roomType) {
        KitLibState.RoomTypeOverrides[roomType] = null;
        if (KitLibState.EnemyMode == EnemyMode.PerType
            && KitLibState.RoomTypeOverrides.Values.All(enc => enc == null)) {
            KitLibState.EnemyMode = EnemyMode.Off;
        }
        MainFile.Logger.Info($"EnemyActions: {roomType} override cleared");
    }

    /// <summary>Clear all overrides.</summary>
    public static void ClearAll() {
        KitLibState.ClearEnemyOverrides();
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

    /// <summary>Distinct monsters from all encounters, sorted by id.</summary>
    public static IReadOnlyList<MonsterModel> GetAllMonsters() {
        return ModelDb.AllEncounters
            .SelectMany(e => e.AllPossibleMonsters ?? Enumerable.Empty<MonsterModel>())
            .GroupBy(m => ((AbstractModel)m).Id.Entry)
            .Select(g => g.First())
            .OrderBy(m => ((AbstractModel)m).Id.Entry)
            .ToList();
    }

    private static void ClearRoomTypeOverrides() {
        KitLibState.RoomTypeOverrides[RoomType.Monster] = null;
        KitLibState.RoomTypeOverrides[RoomType.Elite] = null;
        KitLibState.RoomTypeOverrides[RoomType.Boss] = null;
    }
}
