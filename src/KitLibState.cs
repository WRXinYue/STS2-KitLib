using System;
using System.Collections.Generic;
using KitLib.Host;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib;

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
    Frameworks,
    Feedback,
    Manual,
    CombatStats,
    EnemyIntent
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

/// <summary>
/// Dev overlay level applied to normal (non-test) runs.
/// This is independent of <see cref="KitLibState.InDevRun"/>; it persists across run boundaries
/// and is cycled by the user from the Developer Mode menu.
/// </summary>
public enum NormalRunMode {
    /// <summary>No DevMode features on normal runs.</summary>
    Disabled,
    /// <summary>Dev sidebar and hooks are active; cheat patches are not.</summary>
    DevPanel,
    /// <summary>Dev sidebar, hooks, and all cheat patches are active.</summary>
    Cheat,
}

public static class KitLibState {
    /// <summary>True when this is a dev test run started from the Developer Mode menu (no save).</summary>
    public static bool InDevRun { get; set; }

    /// <summary>Dev overlay level for normal runs. Cycled by the user; persisted in settings.json.</summary>
    public static NormalRunMode NormalRunMode { get; set; } = NormalRunMode.DevPanel;

    /// <summary>
    /// DevMode is active in any form — the dev sidebar, hooks, and scripts are mounted.
    /// True for dev test runs and for any non-<see cref="NormalRunMode.Disabled"/> normal run.
    /// </summary>
    public static bool IsActive => InDevRun || NormalRunMode != NormalRunMode.Disabled;

    /// <summary>
    /// Cheat patches are active — either a dev test run or a <see cref="NormalRunMode.Cheat"/> normal run.
    /// </summary>
    public static bool CheatsInRun => InDevRun || NormalRunMode == NormalRunMode.Cheat;

    /// <summary>
    /// Pseudo-coop embark in progress: defer DevPanel/warmup and MpCheat until <c>EnterAct(0)</c> completes.
    /// </summary>
    public static bool PseudoCoopLaunchPending { get; set; }

    /// <summary>
    /// Pseudo-coop run: skip DevPanel + asset warmup on embark (attached later via <see cref="PseudoCoop.PseudoCoopMapFinishNode"/>).
    /// </summary>
    public static bool PseudoCoopDeferHeavyUi { get; set; }

    /// <summary>Pseudo-coop: block MpCheat config publish until map opens (avoids stack overflow during Neow).</summary>
    public static bool PseudoCoopDeferMpCheatPublish { get; set; }

    /// <summary>Pseudo-coop: DevPanel + config publish run when <see cref="NMapScreen"/> opens.</summary>
    public static bool PseudoCoopAwaitingMapFinish { get; set; }

    /// <summary>Dual-instance LAN: rail shows only AI Host; skips context pane and asset warmup.</summary>
    public static bool DualInstanceMinimalRail { get; set; }

    public static int MaxEnergy { get; set; } = 0;

    public static CardTarget CardTarget { get; set; } = CardTarget.Hand;
    public static EffectDuration EffectDuration { get; set; } = EffectDuration.Permanent;
    public static ActivePanel ActivePanel { get; set; } = ActivePanel.None;
    public static CardMode CardMode { get; set; } = CardMode.View;
    public static RelicMode RelicMode { get; set; } = RelicMode.View;

    // ── Restart-with-Seed ──

    public static int? PendingRestartGold { get; set; }

    /// <summary>Injected via StartNewSingleplayerRun; char select overwrites/clears DebugSeedOverride.</summary>
    public static string? PendingRestartSeed { get; set; }

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
        /// <summary>When true, vanilla map allows debug-jump to any node while the map screen is open.</summary>
        public static bool MapDebugJumpEnabled { get; set; }
        public static MapRewriteMode MapRewriteMode { get; set; } = MapRewriteMode.None;
        public static bool MapKeepFinalBoss { get; set; } = true;

        public static void Reset() {
            UnknownMapAlwaysTreasure = false;
            MapRewriteEnabled = false;
            MapDebugJumpEnabled = false;
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
        PendingRestartGold = null;
        PendingRestartSeed = null;
        AutoProceedToCharSelect = false;
    }

    public static void OnRunEnded() {
        PseudoCoopLaunchPending = false;
        PseudoCoopDeferHeavyUi = false;
        PseudoCoopDeferMpCheatPublish = false;
        PseudoCoopAwaitingMapFinish = false;
        DualInstanceMinimalRail = false;
        InDevRun = false;
        ClearEnemyOverrides();
        ResetAllCheats();
        // NormalRunMode and PendingRestart survive across run boundaries — NormalRunMode is
        // a persistent user choice; PendingRestart is cleared on consumption.
    }

    public static void ResetAllCheats() {
        PlayerCheats.Reset();
        EnemyCheats.Reset();
        GameplayModifiers.Reset();
        MapCheats.Reset();
        KitLibCheatOps.ClearRunState?.Invoke();
    }
}
