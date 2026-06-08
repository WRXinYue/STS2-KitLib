using System.Collections.Generic;
using KitLib;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Serializable multiplayer cheat snapshot (Tier 0/1 sync).</summary>
public sealed class MpCheatConfig {
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public bool SessionEnabled { get; set; }

    /// <summary>Legacy / unused in MP runs — player cheats use <see cref="PerPlayer"/> only.</summary>
    public MpCheatPlayerFlags GlobalPlayer { get; set; } = new();

    public MpCheatEnemyFlags GlobalEnemy { get; set; } = new();

    public MpCheatGameplayFlags GlobalGameplay { get; set; } = new();

    public MpCheatMapFlags GlobalMap { get; set; } = new();

    /// <summary>Per-player cheat flags keyed by net id (MP player toggles).</summary>
    public Dictionary<ulong, MpCheatPlayerFlags> PerPlayer { get; set; } = new();

    public static MpCheatConfig FromKitLibState() {
        return new MpCheatConfig {
            SessionEnabled = true,
            GlobalPlayer = MpCheatPlayerFlags.FromDevMode(),
            GlobalEnemy = MpCheatEnemyFlags.FromDevMode(),
            GlobalGameplay = MpCheatGameplayFlags.FromDevMode(),
            GlobalMap = MpCheatMapFlags.FromDevMode(),
        };
    }

    public MpCheatConfig Clone() {
        var copy = new MpCheatConfig {
            Version = Version,
            SessionEnabled = SessionEnabled,
            GlobalPlayer = GlobalPlayer.Clone(),
            GlobalEnemy = GlobalEnemy.Clone(),
            GlobalGameplay = GlobalGameplay.Clone(),
            GlobalMap = GlobalMap.Clone(),
        };
        foreach (var (netId, flags) in PerPlayer)
            copy.PerPlayer[netId] = flags.Clone();
        return copy;
    }

    /// <summary>Merge local UI edits into a publish snapshot (player flags → PerPlayer only).</summary>
    public static MpCheatConfig MergeLocalEdits(MpCheatConfig baseline, ulong localNetId, bool includeSharedGlobals) {
        var merged = baseline.Clone();
        merged.SessionEnabled = true;
        merged.GlobalPlayer = new MpCheatPlayerFlags();
        merged.PerPlayer[localNetId] = MpCheatPlayerFlags.FromDevMode();
        if (includeSharedGlobals) {
            merged.GlobalEnemy = MpCheatEnemyFlags.FromDevMode();
            merged.GlobalGameplay = MpCheatGameplayFlags.FromDevMode();
            merged.GlobalMap = MpCheatMapFlags.FromDevMode();
        }
        return merged;
    }

    /// <summary>Client → host: only the requester's player flags (no shared globals).</summary>
    public static MpCheatConfig BuildClientPlayerPatch(ulong requesterNetId) =>
        new() {
            SessionEnabled = true,
            PerPlayer = { [requesterNetId] = MpCheatPlayerFlags.FromDevMode() },
        };

    /// <summary>Host merges a client patch without overwriting shared globals.</summary>
    public MpCheatConfig MergeClientPlayerPatch(MpCheatConfig incoming, ulong requesterNetId) {
        var merged = Clone();
        merged.SessionEnabled = true;
        merged.GlobalPlayer = new MpCheatPlayerFlags();

        if (incoming.PerPlayer.TryGetValue(requesterNetId, out var per))
            merged.PerPlayer[requesterNetId] = per.Clone();
        else if (HasAnyPlayerFlag(incoming.GlobalPlayer))
            merged.PerPlayer[requesterNetId] = incoming.GlobalPlayer.Clone();

        return merged;
    }

    public void ApplyToKitLibState() {
        GlobalPlayer.ApplyToDevMode();
        GlobalEnemy.ApplyToDevMode();
        GlobalGameplay.ApplyToDevMode();
        GlobalMap.ApplyToDevMode();
    }

    /// <summary>Apply synced snapshot to local DevMode UI (MP: only this machine's player flags).</summary>
    public void ApplyToLocalKitLibState(ulong localNetId) {
        if (TryGetPlayerFlags(localNetId, out var per))
            per.ApplyToDevMode();
        else
            new MpCheatPlayerFlags().ApplyToDevMode();

        GlobalEnemy.ApplyToDevMode();
        GlobalGameplay.ApplyToDevMode();
        GlobalMap.ApplyToDevMode();
    }

    /// <summary>Move legacy PerPlayer[0] to the real local net id after run start.</summary>
    public void NormalizePerPlayerKeys(ulong localNetId) {
        if (localNetId == 0) return;
        if (PerPlayer.TryGetValue(0, out var legacy) && !PerPlayer.ContainsKey(localNetId))
            PerPlayer[localNetId] = legacy.Clone();
        if (localNetId != 0)
            PerPlayer.Remove(0);
    }

    public bool TryGetPlayerFlags(ulong netId, out MpCheatPlayerFlags flags) {
        if (netId != 0 && PerPlayer.TryGetValue(netId, out var per)) {
            flags = per;
            return true;
        }
        if (netId != 0 && PerPlayer.TryGetValue(0, out var legacy)) {
            flags = legacy;
            return true;
        }
        flags = new MpCheatPlayerFlags();
        return false;
    }

    private static bool HasAnyPlayerFlag(MpCheatPlayerFlags flags) =>
        flags.InfiniteHp || flags.InfiniteBlock || flags.InfiniteEnergy || flags.InfiniteStars
        || flags.AlwaysRewardPotion || flags.AlwaysUpgradeCardReward || flags.MaxCardRewardRarity
        || flags.DefenseMultiplier != 1f;
}

public sealed class MpCheatPlayerFlags {
    public bool InfiniteHp { get; set; }
    public bool InfiniteBlock { get; set; }
    public bool InfiniteEnergy { get; set; }
    public bool InfiniteStars { get; set; }
    public bool AlwaysRewardPotion { get; set; }
    public bool AlwaysUpgradeCardReward { get; set; }
    public bool MaxCardRewardRarity { get; set; }
    public float DefenseMultiplier { get; set; } = 1f;

    public MpCheatPlayerFlags Clone() => new() {
        InfiniteHp = InfiniteHp,
        InfiniteBlock = InfiniteBlock,
        InfiniteEnergy = InfiniteEnergy,
        InfiniteStars = InfiniteStars,
        AlwaysRewardPotion = AlwaysRewardPotion,
        AlwaysUpgradeCardReward = AlwaysUpgradeCardReward,
        MaxCardRewardRarity = MaxCardRewardRarity,
        DefenseMultiplier = DefenseMultiplier,
    };

    public static MpCheatPlayerFlags FromDevMode() => new() {
        InfiniteHp = KitLibState.PlayerCheats.InfiniteHp,
        InfiniteBlock = KitLibState.PlayerCheats.InfiniteBlock,
        InfiniteEnergy = KitLibState.PlayerCheats.InfiniteEnergy,
        InfiniteStars = KitLibState.PlayerCheats.InfiniteStars,
        AlwaysRewardPotion = KitLibState.PlayerCheats.AlwaysRewardPotion,
        AlwaysUpgradeCardReward = KitLibState.PlayerCheats.AlwaysUpgradeCardReward,
        MaxCardRewardRarity = KitLibState.PlayerCheats.MaxCardRewardRarity,
        DefenseMultiplier = KitLibState.PlayerCheats.DefenseMultiplier,
    };

    public void ApplyToDevMode() {
        KitLibState.PlayerCheats.InfiniteHp = InfiniteHp;
        KitLibState.PlayerCheats.InfiniteBlock = InfiniteBlock;
        KitLibState.PlayerCheats.InfiniteEnergy = InfiniteEnergy;
        KitLibState.PlayerCheats.InfiniteStars = InfiniteStars;
        KitLibState.PlayerCheats.AlwaysRewardPotion = AlwaysRewardPotion;
        KitLibState.PlayerCheats.AlwaysUpgradeCardReward = AlwaysUpgradeCardReward;
        KitLibState.PlayerCheats.MaxCardRewardRarity = MaxCardRewardRarity;
        KitLibState.PlayerCheats.DefenseMultiplier = DefenseMultiplier;
    }
}

public sealed class MpCheatEnemyFlags {
    public bool FreezeEnemies { get; set; }
    public bool OneHitKill { get; set; }
    public float DamageMultiplier { get; set; } = 1f;

    public MpCheatEnemyFlags Clone() => new() {
        FreezeEnemies = FreezeEnemies,
        OneHitKill = OneHitKill,
        DamageMultiplier = DamageMultiplier,
    };

    public static MpCheatEnemyFlags FromDevMode() => new() {
        FreezeEnemies = KitLibState.EnemyCheats.FreezeEnemies,
        OneHitKill = KitLibState.EnemyCheats.OneHitKill,
        DamageMultiplier = KitLibState.EnemyCheats.DamageMultiplier,
    };

    public void ApplyToDevMode() {
        KitLibState.EnemyCheats.FreezeEnemies = FreezeEnemies;
        KitLibState.EnemyCheats.OneHitKill = OneHitKill;
        KitLibState.EnemyCheats.DamageMultiplier = DamageMultiplier;
    }
}

public sealed class MpCheatGameplayFlags {
    public float GoldMultiplier { get; set; } = 1f;
    public bool FreeShop { get; set; }

    public MpCheatGameplayFlags Clone() => new() {
        GoldMultiplier = GoldMultiplier,
        FreeShop = FreeShop,
    };

    public static MpCheatGameplayFlags FromDevMode() => new() {
        GoldMultiplier = KitLibState.GameplayModifiers.GoldMultiplier,
        FreeShop = KitLibState.GameplayModifiers.FreeShop,
    };

    public void ApplyToDevMode() {
        KitLibState.GameplayModifiers.GoldMultiplier = GoldMultiplier;
        KitLibState.GameplayModifiers.FreeShop = FreeShop;
    }
}

public sealed class MpCheatMapFlags {
    public bool UnknownMapAlwaysTreasure { get; set; }
    public bool FreeTravelFromDevRoomMap { get; set; }

    public MpCheatMapFlags Clone() => new() {
        UnknownMapAlwaysTreasure = UnknownMapAlwaysTreasure,
        FreeTravelFromDevRoomMap = FreeTravelFromDevRoomMap,
    };

    public static MpCheatMapFlags FromDevMode() => new() {
        UnknownMapAlwaysTreasure = KitLibState.MapCheats.UnknownMapAlwaysTreasure,
        FreeTravelFromDevRoomMap = KitLibState.MapCheats.MapDebugJumpEnabled,
    };

    public void ApplyToDevMode() {
        KitLibState.MapCheats.UnknownMapAlwaysTreasure = UnknownMapAlwaysTreasure;
        KitLibState.MapCheats.MapDebugJumpEnabled = FreeTravelFromDevRoomMap;
    }
}
