using System;
using System.Collections.Generic;
using DevMode.Presets;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace DevMode;

public enum CardTarget {
    DrawPile,
    Hand,
    DiscardPile,
    Deck,
    ExhaustPile
}

public enum EffectDuration {
    Temporary,
    Permanent
}

public enum ActivePanel {
    None,
    Cards,
    Relics,
    Enemies,
    Powers,
    Potions,
    Events,
    Rooms,
    Console,
    Presets,
    CardEdit,
    Hooks,
    Scripts,
    Logs,
    HarmonyAnalysis,
    Frameworks
}

public enum PowerTarget {
    Self,
    AllEnemies,
    SpecificTarget,
    Allies
}

public enum MapRewriteMode {
    None,
    AllChest,
    AllElite,
    AllBoss
}

public enum CardMode {
    View,
    Add,
    Upgrade,
    Edit,
    Delete
}

public enum RelicMode {
    View,
    Add,
    Delete
}

public enum EnemyMode {
    Global,
    PerType,
    Off
}

public enum DebugMode {
    Off,
    Panel,
    Full,
}

public static class DevModeState {
    public static bool InDevRun { get; set; }

    public static DebugMode DebugMode { get; set; } = DebugMode.Off;

    public static bool CheatsInRun => InDevRun || DebugMode == DebugMode.Full;

    public static bool InMenuPreview { get; set; }

    public static Action? OnMenuPreviewClosed { get; set; }

    public static int MaxEnergy { get; set; } = 0;

    public static CardTarget CardTarget { get; set; } = CardTarget.Hand;
    public static EffectDuration EffectDuration { get; set; } = EffectDuration.Permanent;
    public static ActivePanel ActivePanel { get; set; } = ActivePanel.None;
    public static CardMode CardMode { get; set; } = CardMode.View;
    public static RelicMode RelicMode { get; set; } = RelicMode.View;

    // ── Restart-with-Seed ──

    public static LoadoutPreset? PendingRestartPreset { get; set; }

    public static PresetContents PendingRestartScope { get; set; }

    public static int? PendingRestartGold { get; set; }

    /// <summary>Injected via StartNewSingleplayerRun; char select overwrites/clears DebugSeedOverride.</summary>
    public static string? PendingRestartSeed { get; set; }

    public static RuntimeStatModifiers? StatModifiers { get; set; }

    // ── Enemy overrides ──

    public static EnemyMode EnemyMode { get; set; } = EnemyMode.Off;

    public static EncounterModel? GlobalEncounterOverride { get; set; }

    public static Dictionary<RoomType, EncounterModel?> RoomTypeOverrides { get; } = new() {
        [RoomType.Monster] = null,
        [RoomType.Elite] = null,
        [RoomType.Boss] = null,
    };

    public static Dictionary<int, EncounterModel?> FloorOverrides { get; } = new();

    public static class PlayerCheats {
        public static bool InfiniteHp { get; set; }
        public static bool InfiniteBlock { get; set; }
        public static bool InfiniteEnergy { get; set; }
        public static bool InfiniteStars { get; set; }
        public static bool AlwaysRewardPotion { get; set; }
        public static bool AlwaysUpgradeCardReward { get; set; }
        public static bool MaxCardRewardRarity { get; set; }
        public static float DefenseMultiplier { get; set; } = 1.0f;

        public static void Reset() {
            InfiniteHp = false;
            InfiniteBlock = false;
            InfiniteEnergy = false;
            InfiniteStars = false;
            AlwaysRewardPotion = false;
            AlwaysUpgradeCardReward = false;
            MaxCardRewardRarity = false;
            DefenseMultiplier = 1.0f;
        }
    }

    public static class EnemyCheats {
        public static bool FreezeEnemies { get; set; }
        public static bool OneHitKill { get; set; }
        public static float DamageMultiplier { get; set; } = 1.0f;

        public static void Reset() {
            FreezeEnemies = false;
            OneHitKill = false;
            DamageMultiplier = 1.0f;
        }
    }

    public static class GameplayModifiers {
        public static float GameSpeed { get; set; } = 1.0f;
        public static float GoldMultiplier { get; set; } = 1.0f;
        public static float ScoreMultiplier { get; set; } = 1.0f;
        public static bool FreeShop { get; set; }
        public static bool MaxScore { get; set; }

        public static void Reset() {
            GameSpeed = 1.0f;
            GoldMultiplier = 1.0f;
            ScoreMultiplier = 1.0f;
            FreeShop = false;
            MaxScore = false;
        }
    }

    public static class MapCheats {
        public static bool UnknownMapAlwaysTreasure { get; set; }
        public static bool MapRewriteEnabled { get; set; }
        public static bool FreeTravelFromDevRoomMap { get; set; }
        public static MapRewriteMode MapRewriteMode { get; set; } = MapRewriteMode.None;
        public static bool MapKeepFinalBoss { get; set; } = true;

        public static void Reset() {
            UnknownMapAlwaysTreasure = false;
            MapRewriteEnabled = false;
            FreeTravelFromDevRoomMap = false;
            MapRewriteMode = MapRewriteMode.None;
            MapKeepFinalBoss = true;
        }
    }

    public static EncounterModel? ResolveOverride(RoomType roomType, int floor) {
        if (FloorOverrides.TryGetValue(floor, out var floorEnc) && floorEnc != null)
            return floorEnc;

        return EnemyMode switch {
            EnemyMode.Global => GlobalEncounterOverride,
            EnemyMode.PerType => RoomTypeOverrides.TryGetValue(roomType, out var enc) ? enc : null,
            _ => null
        };
    }

    public static void ClearEnemyOverrides() {
        EnemyMode = EnemyMode.Off;
        GlobalEncounterOverride = null;
        RoomTypeOverrides[RoomType.Monster] = null;
        RoomTypeOverrides[RoomType.Elite] = null;
        RoomTypeOverrides[RoomType.Boss] = null;
        FloorOverrides.Clear();
    }

    public static bool AutoProceedToCharSelect { get; set; }

    public static void ClearPendingRestart() {
        PendingRestartPreset = null;
        PendingRestartScope = PresetContents.None;
        PendingRestartGold = null;
        PendingRestartSeed = null;
        AutoProceedToCharSelect = false;
    }

    public static void OnRunEnded() {
        InDevRun = false;
        ClearEnemyOverrides();
        ResetAllCheats();
        // PendingRestart survives across run boundaries — cleared on consumption.
    }

    public static void ResetAllCheats() {
        PlayerCheats.Reset();
        EnemyCheats.Reset();
        GameplayModifiers.Reset();
        MapCheats.Reset();
        StatModifiers = null;
    }
}
