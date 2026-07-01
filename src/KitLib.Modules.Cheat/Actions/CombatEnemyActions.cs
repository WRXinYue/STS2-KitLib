using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.Combat;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

/// <summary>
/// Mid-combat enemy manipulation: add monsters, kill enemies, remove enemies.
/// Uses the game's <see cref="CreatureCmd"/> API (same as boss summon mechanics).
/// </summary>
internal static class CombatEnemyActions {
    private const int MonsterVisualPreloadTimeoutMs = 30_000;

    /// <summary>Get the current combat state, or null if not in combat.</summary>
    public static CombatState? GetCombatState() {
        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return null;
        return player.Creature != null
            ? Sts2CombatCompat.GetCreatureCombatState(player.Creature)
            : null;
    }

    /// <summary>Get current enemies in combat.</summary>
    public static IReadOnlyList<Creature> GetCurrentEnemies() {
        var cs = GetCombatState();
        return cs?.Enemies ?? (IReadOnlyList<Creature>)[];
    }

    /// <summary>Add a monster to the current combat.</summary>
    public static Task<Creature?> AddMonster(MonsterModel canonicalMonster) =>
        AddMonsterInternal(canonicalMonster, mpSync: false);

    /// <summary>Add all monsters from an encounter to the current combat.</summary>
    public static Task AddEncounterMonsters(EncounterModel encounter) =>
        AddEncounterMonstersInternal(encounter, mpSync: false);

    internal static MonsterModel? FindMonsterById(string monsterId) {
        if (string.IsNullOrEmpty(monsterId)) return null;
        return EnemyActions.GetAllMonsters().FirstOrDefault(m => ((AbstractModel)m).Id.Entry == monsterId);
    }

    internal static EncounterModel? FindEncounterById(string encounterId) {
        if (string.IsNullOrEmpty(encounterId)) return null;
        return EnemyActions.GetAllEncounters().FirstOrDefault(e => ((AbstractModel)e).Id.Entry == encounterId);
    }

    internal static bool TryValidateAddMonster(MpCheatItemPayload payload, out string? error) {
        error = null;
        if (!IsCombatReadyForAdd(out error))
            return false;

        if (FindMonsterById(payload.ItemId) == null) {
            error = "monster not found";
            return false;
        }

        return true;
    }

    internal static bool TryValidateAddEncounter(MpCheatItemPayload payload, out string? error) {
        error = null;
        if (!IsCombatReadyForAdd(out error))
            return false;

        var encounter = FindEncounterById(payload.ItemId);
        if (encounter == null) {
            error = "encounter not found";
            return false;
        }

        var monsters = encounter.AllPossibleMonsters?.ToList();
        if (monsters == null || monsters.Count == 0) {
            error = "encounter has no monsters";
            return false;
        }

        return true;
    }

    internal static Task ExecuteAddMonsterFromMpSync(MpCheatItemPayload payload) {
        var monster = FindMonsterById(payload.ItemId);
        return monster == null ? Task.CompletedTask : AddMonsterInternal(monster, mpSync: true);
    }

    internal static Task ExecuteAddEncounterFromMpSync(MpCheatItemPayload payload) {
        var encounter = FindEncounterById(payload.ItemId);
        return encounter == null ? Task.CompletedTask : AddEncounterMonstersInternal(encounter, mpSync: true);
    }

    private static bool IsCombatReadyForAdd(out string? error) {
        error = null;
        if (GetCombatState() == null) {
            error = I18N.T("mpcheat.combatAdd.notInCombat", "Not in combat.");
            LogCombatSnapshot("validate-fail:not-in-combat");
            return false;
        }

        if (!CombatManager.Instance.IsInProgress) {
            error = I18N.T("mpcheat.combatAdd.notInProgress", "Combat is not in progress.");
            LogCombatSnapshot("validate-fail:not-in-progress");
            return false;
        }

        return true;
    }

    private static void LogCombatSnapshot(string phase, CombatState? cs = null) {
        cs ??= GetCombatState();
        var state = RunManager.Instance?.DebugOnlyGetState();
        bool inProgress = CombatManager.Instance?.IsInProgress ?? false;
        var enc = cs?.Encounter;
        string encId = enc != null ? ((AbstractModel)enc).Id.Entry : "null";
        int aliveEnemies = cs?.Enemies?.Count(e => !e.IsDead) ?? -1;
        string room = state?.CurrentRoom?.GetType().Name ?? "null";
        KitLog.Info("CombatAdd",
            $"{phase}: inProgress={inProgress} combatState={(cs != null)} " +
            $"aliveEnemies={aliveEnemies} encounter={encId} hasScene={enc?.HasScene} " +
            $"combatRoom={NCombatRoom.Instance != null} actFloor={state?.ActFloor} room={room}");
    }

    private static async Task<Creature?> AddMonsterInternal(MonsterModel canonicalMonster, bool mpSync) {
        string monsterId = ((AbstractModel)canonicalMonster).Id.Entry;
        var sw = Stopwatch.StartNew();

        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            KitLog.Warn("CombatAdd", $"blocked (mp, no sync): {monsterId}");
            return null;
        }

        LogCombatSnapshot(
            $"begin {monsterId} mpSync={mpSync} netMp={MpCheatSession.InMultiplayerRun}");

        var cs = GetCombatState();
        if (cs == null) {
            KitLog.Warn("CombatAdd", $"abort: not in combat ({monsterId}, {sw.ElapsedMilliseconds}ms)");
            return null;
        }

        if (!CombatManager.Instance.IsInProgress) {
            KitLog.Warn("CombatAdd", $"abort: combat not in progress ({monsterId}, {sw.ElapsedMilliseconds}ms)");
            return null;
        }

        var mutable = canonicalMonster.ToMutable();

        string? slot = null;
        try {
            slot = cs.Encounter?.GetNextSlot(cs);
            if (string.IsNullOrEmpty(slot)) slot = null;
        }
        catch (Exception ex) {
            KitLog.Warn("CombatAdd", $"GetNextSlot failed: {ex.Message}");
        }

        KitLog.Info("CombatAdd", $"slot={slot ?? "(auto)"} side={cs.CurrentSide} ({monsterId})");

        if (slot == null && cs.Encounter is { HasScene: false })
            LogSlotlessSummonWarning((AbstractModel)canonicalMonster);

        try {
            if (!await TryPreloadMonsterVisualsAsync(monsterId, sw))
                KitLog.Warn("CombatAdd", $"preload incomplete, Add may hitch ({monsterId})");

            KitLog.Info("CombatAdd", $"CreatureCmd.Add starting ({monsterId})");
            var creature = await CreatureCmd.Add(mutable, cs, CombatSide.Enemy, slot)
                .ConfigureAwait(false);
            KitLog.Info("CombatAdd", $"CreatureCmd.Add done ({monsterId}, {sw.ElapsedMilliseconds}ms)");

            if (slot == null) {
                KitLog.Info("CombatAdd", $"RepositionEnemies starting ({monsterId})");
                RepositionEnemies(cs);
                KitLog.Info("CombatAdd", $"RepositionEnemies done ({monsterId}, {sw.ElapsedMilliseconds}ms)");
            }

            KitLog.Info("CombatAdd", $"success: {monsterId} ({sw.ElapsedMilliseconds}ms)");
            return creature;
        }
        catch (Exception ex) {
            KitLog.Warn("CombatAdd", $"failed: {monsterId} ({sw.ElapsedMilliseconds}ms): {ex}");
            return null;
        }
    }

    private static async Task AddEncounterMonstersInternal(EncounterModel encounter, bool mpSync) {
        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn(
                $"CombatEnemyActions: Cannot add encounter {((AbstractModel)encounter).Id.Entry} locally in multiplayer — use host combat sync.");
            return;
        }

        var monsters = encounter.AllPossibleMonsters?.ToList();
        if (monsters == null || monsters.Count == 0) return;

        foreach (var monster in monsters)
            await AddMonsterInternal(monster, mpSync);
    }

    private static string GetMonsterVisualScenePath(string monsterId) =>
        SceneHelper.GetScenePath($"creature_visuals/{monsterId.ToLowerInvariant()}");

    private static async Task<bool> TryPreloadMonsterVisualsAsync(string monsterId, Stopwatch sw) {
        string path = GetMonsterVisualScenePath(monsterId);
        if (PreloadManager.Cache.ContainsKey(path)) {
            KitLog.Info("CombatAdd", $"preload cache hit ({monsterId})");
            return true;
        }

        if (!ResourceLoader.Exists(path)) {
            KitLog.Warn("CombatAdd", $"preload missing scene ({monsterId}): {path}");
            return false;
        }

        KitLog.Info("CombatAdd", $"preload threaded load ({monsterId}): {path}");
        var err = ResourceLoader.LoadThreadedRequest(path, "PackedScene", true);
        if (err != Error.Ok) {
            KitLog.Warn("CombatAdd", $"preload request failed ({monsterId}): {err}");
            return false;
        }

        while (sw.ElapsedMilliseconds < MonsterVisualPreloadTimeoutMs) {
            var status = ResourceLoader.LoadThreadedGetStatus(path);
            switch (status) {
                case ResourceLoader.ThreadLoadStatus.Loaded: {
                        var resource = ResourceLoader.LoadThreadedGet(path);
                        if (resource is PackedScene scene)
                            PreloadManager.Cache.SetAsset(path, scene);
                        KitLog.Info("CombatAdd", $"preload done ({monsterId}, {sw.ElapsedMilliseconds}ms)");
                        return true;
                    }
                case ResourceLoader.ThreadLoadStatus.Failed:
                case ResourceLoader.ThreadLoadStatus.InvalidResource:
                    KitLog.Warn("CombatAdd", $"preload failed ({monsterId}): status={status}");
                    return false;
                default:
                    await Task.Delay(16).ConfigureAwait(false);
                    break;
            }
        }

        KitLog.Warn("CombatAdd", $"preload timeout ({monsterId}, {MonsterVisualPreloadTimeoutMs}ms)");
        return false;
    }

    internal static void LogSlotlessSummonWarning(AbstractModel monster) =>
        MainFile.Logger.Warn(
            $"CombatEnemyActions: Added {monster.Id.Entry} to an encounter without slot markers " +
            $"(Encounter.HasScene=false). Mid-combat summons (e.g. Ovicopter lay eggs) rely on auto-layout; " +
            "prefer OVICOPTER_NORMAL or another slotted encounter for slot-based monsters.");

    /// <summary>
    /// Reposition all enemy NCreature nodes using the same algorithm as
    /// NCombatRoom.PositionEnemies (auto-layout for encounters without scene slots).
    /// </summary>
    internal static void RepositionEnemies(CombatState cs) {
        var combatRoom = NCombatRoom.Instance;
        if (combatRoom == null) return;

        float scaling = cs.Encounter?.GetCameraScaling() ?? 1f;

        // Collect enemy creature nodes (non-player, non-pet, alive, visuals ready for layout)
        var enemies = combatRoom.CreatureNodes
            .Where(n => GodotObject.IsInstanceValid(n)
                     && n.Visuals != null
                     && GodotObject.IsInstanceValid(n.Visuals)
                     && !n.Entity.IsPlayer
                     && n.Entity.PetOwner == null
                     && !n.Entity.IsDead)
            .ToList();

        if (enemies.Count == 0) return;

        // --- Replicate NCombatRoom.PositionEnemies ---
        float halfScreen = 960f / scaling;
        float padding = 70f;
        float totalCreatureWidth = enemies.Sum(n => {
            try {
                return n.Visuals!.Bounds.Size.X;
            }
            catch {
                return 120f;
            }
        });
        float totalWidth = totalCreatureWidth + (enemies.Count - 1) * padding;
        float startX = (halfScreen - totalWidth) * 0.5f;
        startX = Math.Max(startX, 150f);

        float altY = 0f;
        if (startX + totalWidth > halfScreen) {
            padding = Math.Max((halfScreen - 150f - totalCreatureWidth) / (enemies.Count - 1), 5f);
            totalWidth = totalCreatureWidth + (enemies.Count - 1) * padding;
            startX = (halfScreen - totalWidth) * 0.5f;
            if (padding < 30f)
                altY = float.Lerp(60f, 40f, (padding - 5f) / 25f);
        }

        float x = startX;
        for (int i = 0; i < enemies.Count; i++) {
            var n = enemies[i];
            float width;
            try {
                width = n.Visuals!.Bounds.Size.X;
            }
            catch {
                width = 120f;
            }

            n.Position = new Vector2(
                x + width * 0.5f,
                200f - ((i % 2 != 0) ? altY : 0f));
            x += width + padding;
        }
    }

    /// <summary>Kill a specific enemy creature.</summary>
    public static Task KillEnemy(Creature creature) => KillEnemyInternal(creature, mpSync: false);

    /// <summary>Kill all current enemies.</summary>
    public static Task KillAllEnemies() => KillAllEnemiesInternal(mpSync: false);

    internal static Creature? FindEnemyByKey(string enemyKey) {
        if (string.IsNullOrEmpty(enemyKey)) return null;
        return GetCurrentEnemies().FirstOrDefault(e => !e.IsDead && EnemyKey.Build(e) == enemyKey);
    }

    internal static bool TryValidateKillEnemy(MpCheatItemPayload payload, out string? error) {
        error = null;
        if (!IsCombatReadyForAdd(out error))
            return false;
        if (FindEnemyByKey(payload.ItemId) == null) {
            error = "enemy not found";
            return false;
        }
        return true;
    }

    internal static bool TryValidateKillAll(MpCheatItemPayload payload, out string? error) {
        error = null;
        if (!IsCombatReadyForAdd(out error))
            return false;
        if (!GetCurrentEnemies().Any(e => !e.IsDead)) {
            error = "no enemies to kill";
            return false;
        }
        return true;
    }

    internal static Task ExecuteKillEnemyFromMpSync(MpCheatItemPayload payload) {
        var enemy = FindEnemyByKey(payload.ItemId);
        return enemy == null ? Task.CompletedTask : KillEnemyInternal(enemy, mpSync: true);
    }

    internal static Task ExecuteKillAllFromMpSync(MpCheatItemPayload payload) =>
        KillAllEnemiesInternal(mpSync: true);

    private static async Task KillEnemyInternal(Creature creature, bool mpSync) {
        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn("CombatEnemyActions: Cannot kill enemy locally in multiplayer — use host combat sync.");
            return;
        }
        if (creature.IsDead) return;
        await CreatureCmd.Kill(creature, force: true);
        MainFile.Logger.Info($"CombatEnemyActions: Killed {creature.Monster?.Title?.GetFormattedText() ?? "enemy"}");
    }

    private static async Task KillAllEnemiesInternal(bool mpSync) {
        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn("CombatEnemyActions: Cannot kill all locally in multiplayer — use host combat sync.");
            return;
        }
        var enemies = GetCurrentEnemies().Where(e => !e.IsDead).ToList();
        if (enemies.Count == 0) return;
        await CreatureCmd.Kill((IReadOnlyCollection<Creature>)enemies, force: true);
        MainFile.Logger.Info($"CombatEnemyActions: Killed all {enemies.Count} enemies");
    }

    // ── Monster editing enhancements ──

    /// <summary>Set a monster's current HP.</summary>
    public static async Task SetMonsterHp(Creature creature, int hp) {
        await CreatureCmd.SetCurrentHp(creature, hp);
    }

    /// <summary>Set a monster's max HP.</summary>
    public static async Task SetMonsterMaxHp(Creature creature, int maxHp) {
        await CreatureCmd.SetMaxHp(creature, maxHp);
    }

    /// <summary>Clear all powers from a monster.</summary>
    public static void ClearMonsterPowers(Creature creature) {
        foreach (var power in creature.Powers.ToArray()) {
            if (power != null)
                PowerCmd.Remove(power);
        }
    }

    /// <summary>Duplicate a monster in combat (add another copy).</summary>
    public static async Task<Creature?> DuplicateMonster(Creature creature) {
        var monsterModel = creature.Monster;
        if (monsterModel == null) return null;
        return await AddMonster(monsterModel);
    }

    /// <summary>Get display info for a creature.</summary>
    public static string GetCreatureInfo(Creature creature) {
        var name = creature.Monster?.Title?.GetFormattedText() ?? "?";
        return $"{name} (HP: {creature.CurrentHp}/{creature.MaxHp}, Block: {creature.Block}, Powers: {creature.Powers.Count})";
    }
}
