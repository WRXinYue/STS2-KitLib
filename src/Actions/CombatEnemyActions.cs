using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevMode.Multiplayer.Cheat;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DevMode.Actions;

/// <summary>
/// Mid-combat enemy manipulation: add monsters, kill enemies, remove enemies.
/// Uses the game's <see cref="CreatureCmd"/> API (same as boss summon mechanics).
/// </summary>
internal static class CombatEnemyActions {
    /// <summary>Get the current combat state, or null if not in combat.</summary>
    public static CombatState? GetCombatState() {
        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return null;
#if STS2_BETA
        return player.Creature?.CombatState as CombatState;
#else
        return player.Creature?.CombatState;
#endif
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
            return false;
        }

        if (!CombatManager.Instance.IsInProgress) {
            error = I18N.T("mpcheat.combatAdd.notInProgress", "Combat is not in progress.");
            return false;
        }

        return true;
    }

    private static async Task<Creature?> AddMonsterInternal(MonsterModel canonicalMonster, bool mpSync) {
        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn(
                $"CombatEnemyActions: Cannot add {((AbstractModel)canonicalMonster).Id.Entry} locally in multiplayer — use host combat sync.");
            return null;
        }

        var cs = GetCombatState();
        if (cs == null) {
            MainFile.Logger.Info("CombatEnemyActions: Not in combat.");
            return null;
        }

        if (!CombatManager.Instance.IsInProgress) {
            MainFile.Logger.Info("CombatEnemyActions: Combat not in progress.");
            return null;
        }

        var mutable = canonicalMonster.ToMutable();

        string? slot = null;
        try {
            slot = cs.Encounter?.GetNextSlot(cs);
            if (string.IsNullOrEmpty(slot)) slot = null;
        }
        catch { /* no slots available */ }

        var creature = await CreatureCmd.Add(mutable, cs, CombatSide.Enemy, slot);
        MainFile.Logger.Info($"CombatEnemyActions: Added {((AbstractModel)canonicalMonster).Id.Entry} to combat");

        if (slot == null)
            RepositionEnemies(cs);

        return creature;
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
            await AddMonsterInternal(monster, mpSync: true);
    }

    /// <summary>
    /// Reposition all enemy NCreature nodes using the same algorithm as
    /// NCombatRoom.PositionEnemies (auto-layout for encounters without scene slots).
    /// </summary>
    private static void RepositionEnemies(CombatState cs) {
        var combatRoom = NCombatRoom.Instance;
        if (combatRoom == null) return;

        float scaling = cs.Encounter?.GetCameraScaling() ?? 1f;

        // Collect enemy creature nodes (non-player, non-pet, alive)
        var enemies = combatRoom.CreatureNodes
            .Where(n => GodotObject.IsInstanceValid(n)
                     && !n.Entity.IsPlayer
                     && n.Entity.PetOwner == null
                     && !n.Entity.IsDead)
            .ToList();

        if (enemies.Count == 0) return;

        // --- Replicate NCombatRoom.PositionEnemies ---
        float halfScreen = 960f / scaling;
        float padding = 70f;
        float totalCreatureWidth = enemies.Sum(n => n.Visuals.Bounds.Size.X);
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
            n.Position = new Vector2(
                x + n.Visuals.Bounds.Size.X * 0.5f,
                200f - ((i % 2 != 0) ? altY : 0f));
            x += n.Visuals.Bounds.Size.X + padding;
        }
    }

    /// <summary>Kill a specific enemy creature.</summary>
    public static async Task KillEnemy(Creature creature) {
        if (creature.IsDead) return;
        await CreatureCmd.Kill(creature, force: true);
        MainFile.Logger.Info($"CombatEnemyActions: Killed {creature.Monster?.Title?.GetFormattedText() ?? "enemy"}");
    }

    /// <summary>Kill all current enemies.</summary>
    public static async Task KillAllEnemies() {
        var enemies = GetCurrentEnemies().Where(e => !e.IsDead).ToList();
        if (enemies.Count == 0) return;
        await CreatureCmd.Kill((IReadOnlyCollection<Creature>)enemies, force: true);
        MainFile.Logger.Info($"CombatEnemyActions: Killed all {enemies.Count} enemies");
    }

    // ── Monster editing enhancements ──

    /// <summary>Set a monster's current HP.</summary>
    public static async Task SetMonsterHp(Creature creature, int hp) {
        await Sts2ApiCompat.SetCurrentHpAsync(creature, hp);
    }

    /// <summary>Set a monster's max HP.</summary>
    public static async Task SetMonsterMaxHp(Creature creature, int maxHp) {
        await Sts2ApiCompat.SetMaxHpAsync(creature, maxHp);
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
