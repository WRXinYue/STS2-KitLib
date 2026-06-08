using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

/// <summary>
/// Frame-level runtime stat modifiers. Called from _Process each frame.
/// Provides: God Mode, Kill All, Infinite Energy, Always Player Turn,
/// Draw to Hand Limit, Extra Draw Each Turn, Auto-Act Friendly Monsters,
/// Negate Debuffs, and 7 Stat Locks.
/// </summary>
public sealed class RuntimeStatModifiers {
    private const BindingFlags ReflFlags = BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private double _killCooldown;
    private double _godModeCooldown;
    private double _debuffCooldown;
    private double _drawToLimitCooldown;
    private double _forcePlayerTurnCooldown;
    private double _allyAutoTurnCooldown;
    private double _statLockCooldown;

    private bool _allyTurnInProgress;
    private bool _extraDrawInProgress;
    private bool _statLockInProgress;

    private readonly HashSet<string> _friendlyAutoTurnSkipLogKeys = new(StringComparer.OrdinalIgnoreCase);
    private int _lastAllyAutoRound = -1;
    private int _lastAllyAutoCount = -1;
    private CombatSide _lastAllyAutoSide = (CombatSide)(-1);
    private int _lastExtraDrawRound = -1;

    // ── Public toggles ──

    public bool KillAllEnemies { get; set; }
    public bool GodMode { get; set; }
    public bool InfiniteEnergy { get; set; }
    public bool AlwaysPlayerTurn { get; set; }
    public bool DrawToHandLimit { get; set; }
    public bool ExtraDrawEachTurn { get; set; }
    public int ExtraDrawEachTurnAmount { get; set; } = 1;
    public bool AutoActFriendlyMonsters { get; set; }
    public bool NegateDebuffs { get; set; }

    // ── Stat locks ──

    public bool LockGold { get; set; }
    public bool LockCurrentHp { get; set; }
    public bool LockMaxHp { get; set; }
    public bool LockCurrentEnergy { get; set; }
    public bool LockMaxEnergy { get; set; }
    public bool LockStars { get; set; }
    public bool LockOrbSlots { get; set; }

    public int LockedGoldValue { get; set; }
    public int LockedCurrentHpValue { get; set; }
    public int LockedMaxHpValue { get; set; }
    public int LockedCurrentEnergyValue { get; set; }
    public int LockedMaxEnergyValue { get; set; }
    public int LockedStarsValue { get; set; }
    public int LockedOrbSlotsValue { get; set; }

    private bool HasStatLocks =>
        LockGold || LockCurrentHp || LockMaxHp || LockCurrentEnergy || LockMaxEnergy || LockStars || LockOrbSlots;

    public bool HasActiveEffects =>
        KillAllEnemies || GodMode || InfiniteEnergy || AlwaysPlayerTurn || DrawToHandLimit
        || (ExtraDrawEachTurn && ExtraDrawEachTurnAmount > 0) || NegateDebuffs || AutoActFriendlyMonsters
        || HasStatLocks;

    // ── Main update loop (called each frame) ──

    public void Update(double delta) {
        TickCooldowns(delta);

        if (!RunManager.Instance.IsInProgress) {
            ResetTurnMarkers();
            return;
        }

        if (!TryGetPlayer(out var player) || player == null) return;

        if (GodMode) {
            int maxHp = Math.Max(1, player.Creature.MaxHp);
            if (player.Creature.CurrentHp < maxHp && _godModeCooldown <= 0) {
                _godModeCooldown = 0.2;
                Sts2ApiCompat.SetCurrentHpAsync(player.Creature, maxHp);
            }
        }

        if (InfiniteEnergy && player.PlayerCombatState != null)
            player.PlayerCombatState.Energy = Math.Max(player.PlayerCombatState.Energy, 99);

        if (ExtraDrawEachTurn && ExtraDrawEachTurnAmount > 0)
            TryDrawExtraCardsEachTurn(player);

        if (DrawToHandLimit)
            TryDrawToHandLimit(player);

        if (AutoActFriendlyMonsters)
            TryRunFriendlyMonsterTurns();

        if (AlwaysPlayerTurn)
            TryForcePlayerTurnOnce(out _);

        if (HasStatLocks)
            TryApplyStatLocks(player);

        if (NegateDebuffs)
            TryRemoveDebuffs(player);

        if (KillAllEnemies)
            TryKillAllEnemies();
    }

    public void NotifyFriendlyMonsterAdded() {
        _lastAllyAutoRound = -1;
        _lastAllyAutoCount = -1;
        _allyAutoTurnCooldown = 0;
        _friendlyAutoTurnSkipLogKeys.Clear();
    }

    public bool TryForcePlayerTurnOnce(out string error) {
        error = string.Empty;
        if (_forcePlayerTurnCooldown > 0) return true;

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null) { error = "Not in combat."; return false; }
        if (CombatManager.Instance.IsOverOrEnding) { error = "Combat is ending."; return false; }

        _forcePlayerTurnCooldown = 0.1;
        combatState.CurrentSide = (CombatSide)1;
        TrySetCombatManagerBool("IsEnemyTurnStarted", false);
        Sts2CombatCompat.ForcePlayPhase();
        TrySetCombatManagerBool("EndingPlayerTurnPhaseOne", false);
        TrySetCombatManagerBool("EndingPlayerTurnPhaseTwo", false);
        TrySetCombatManagerBool("PlayerActionsDisabled", false);
        return true;
    }

    // ── Private helpers ──

    private void TickCooldowns(double delta) {
        _killCooldown = Math.Max(0, _killCooldown - delta);
        _godModeCooldown = Math.Max(0, _godModeCooldown - delta);
        _debuffCooldown = Math.Max(0, _debuffCooldown - delta);
        _drawToLimitCooldown = Math.Max(0, _drawToLimitCooldown - delta);
        _forcePlayerTurnCooldown = Math.Max(0, _forcePlayerTurnCooldown - delta);
        _allyAutoTurnCooldown = Math.Max(0, _allyAutoTurnCooldown - delta);
        _statLockCooldown = Math.Max(0, _statLockCooldown - delta);
    }

    private static bool TryGetPlayer(out Player? player) {
        player = null;
        return RunContext.TryGetRunAndPlayer(out _, out player!);
    }

    private void ResetTurnMarkers() {
        _lastAllyAutoRound = -1;
        _lastAllyAutoCount = -1;
        _lastAllyAutoSide = (CombatSide)(-1);
        _lastExtraDrawRound = -1;
        _allyTurnInProgress = false;
        _extraDrawInProgress = false;
        _statLockInProgress = false;
        _friendlyAutoTurnSkipLogKeys.Clear();
    }

    private void TryKillAllEnemies() {
        if (_killCooldown > 0) return;
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null) return;

        var alive = combatState.Enemies.Where(e => e.IsAlive).ToArray();
        if (alive.Length == 0) return;

        _killCooldown = 0.25;
        foreach (var enemy in alive)
            CreatureCmd.Kill(enemy, true);
    }

    private void TryRemoveDebuffs(Player player) {
        if (_debuffCooldown > 0) return;
        var debuffs = player.Creature.Powers
            .Where(p => p != null && ((int)p.Type == 2 || (int)p.TypeForCurrentAmount == 2))
            .ToArray();
        if (debuffs.Length == 0) return;

        _debuffCooldown = 0.2;
        foreach (var debuff in debuffs)
            PowerCmd.Remove(debuff);
    }

    private void TryDrawToHandLimit(Player player) {
        if (_drawToLimitCooldown > 0 || player.PlayerCombatState == null) return;
        if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding) return;

        int handCount = player.PlayerCombatState.Hand.Cards.Count;
        int toDraw = Math.Max(0, 10 - handCount);
        if (toDraw > 0) {
            _drawToLimitCooldown = 0.2;
            CardPileCmd.Draw((PlayerChoiceContext)new BlockingPlayerChoiceContext(), (decimal)toDraw, player, true);
        }
    }

    private void TryDrawExtraCardsEachTurn(Player player) {
        if (_extraDrawInProgress || player.PlayerCombatState == null) return;
        if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding) {
            _lastExtraDrawRound = -1;
            return;
        }

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || (int)combatState.CurrentSide != 1) return;

        int round = combatState.RoundNumber;
        if (_lastExtraDrawRound == round) return;

        int drawAmount = Math.Clamp(ExtraDrawEachTurnAmount, 1, 20);
        _lastExtraDrawRound = round;
        _extraDrawInProgress = true;
        DrawExtraCardsAsync(player, drawAmount);
    }

    private async void DrawExtraCardsAsync(Player player, int amount) {
        try { await CardPileCmd.Draw((PlayerChoiceContext)new BlockingPlayerChoiceContext(), (decimal)amount, player, true); }
        catch (Exception ex) { MainFile.Logger.Warn($"Extra draw failed: {ex.Message}"); }
        finally { _extraDrawInProgress = false; }
    }

    private void TryRunFriendlyMonsterTurns() {
        if (_allyTurnInProgress || _allyAutoTurnCooldown > 0) return;

        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding) {
            _lastAllyAutoRound = -1;
            _lastAllyAutoCount = -1;
            return;
        }

        if ((int)combatState.CurrentSide != 2 && (!AlwaysPlayerTurn || (int)combatState.CurrentSide != 1))
            return;

        var allies = combatState.Allies.Where(c => c.IsMonster && c.IsAlive && !c.IsDead).ToArray();
        if (allies.Length == 0) return;

        int round = combatState.RoundNumber;
        if (_lastAllyAutoRound == round && _lastAllyAutoSide == combatState.CurrentSide && allies.Length <= _lastAllyAutoCount)
            return;

        _lastAllyAutoRound = round;
        _lastAllyAutoSide = combatState.CurrentSide;
        _lastAllyAutoCount = allies.Length;
        _allyTurnInProgress = true;
        ExecuteFriendlyMonsterTurnsAsync(combatState, allies);
    }

    private async void ExecuteFriendlyMonsterTurnsAsync(CombatState combatState, Creature[] allies) {
        try {
            foreach (var ally in allies) {
                if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding) break;
                if (!combatState.ContainsCreature(ally) || ally.IsDead || !ally.IsAlive) continue;

                if ((int)ally.Side != 2) {
                    LogSkipOnce(ally, "player-side", "Player-side monster cannot auto-act.");
                    continue;
                }

                try {
                    var enemies = combatState.Enemies.Where(e => e.IsAlive && !e.IsDead).ToArray();
                    if (enemies.Length == 0) break;

                    ally.PrepareForNextTurn(enemies, true);
                    if (HasUnsetMoveState(ally)) {
                        LogSkipOnce(ally, "unset-move", "Monster has UNSET_MOVE, skipping.");
                        continue;
                    }

                    await ally.TakeTurn();
                    await CombatManager.Instance.WaitForUnpause();
                    if (await CombatManager.Instance.CheckWinCondition()) break;
                }
                catch (Exception ex) when (ex is InvalidOperationException && ex.Message.Contains("Only enemy monsters", StringComparison.OrdinalIgnoreCase)) {
                    LogSkipOnce(ally, "enemy-only", "Monster is enemy-only for auto turns.");
                }
                catch (Exception ex) when (ex is InvalidOperationException && ex.Message.Contains("No move has been set", StringComparison.OrdinalIgnoreCase)) {
                    LogSkipOnce(ally, "no-move", "Monster has no move set, skipping.");
                }
                catch (Exception ex) {
                    MainFile.Logger.Warn($"Friendly monster auto turn failed: {ex.Message}");
                }
            }
        }
        finally {
            _allyTurnInProgress = false;
            _allyAutoTurnCooldown = 0.2;
        }
    }

    private void LogSkipOnce(Creature creature, string category, string reason) {
        string id = GetCreatureModelId(creature);
        if (_friendlyAutoTurnSkipLogKeys.Add($"{category}:{id}"))
            MainFile.Logger.Warn($"Friendly auto turn skipped [{id}]: {reason}");
    }

    private static string GetCreatureModelId(Creature creature) {
        try { var e = creature.ModelId.Entry; return string.IsNullOrWhiteSpace(e) ? "Unknown" : e; }
        catch { return "Unknown"; }
    }

    private static bool HasUnsetMoveState(Creature creature) {
        try {
            var monster = creature.Monster;
            if (monster == null) return false;
            if (TryReadMemberText(monster, out var text) && text.Contains("UNSET_MOVE", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }
        return false;
    }

    private static bool TryReadMemberText(object instance, out string text) {
        text = string.Empty;
        var type = instance.GetType();
        string[] names = ["MoveState", "CurrentMove", "NextMove", "Move", "CurrentMoveState", "MoveName"];
        foreach (var name in names) {
            var val = type.GetProperty(name, ReflFlags)?.GetValue(instance);
            if (val != null) { text = val.ToString() ?? ""; if (!string.IsNullOrWhiteSpace(text)) return true; }
            val = type.GetField(name, ReflFlags)?.GetValue(instance);
            if (val != null) { text = val.ToString() ?? ""; if (!string.IsNullOrWhiteSpace(text)) return true; }
        }
        return false;
    }

    private void TryApplyStatLocks(Player player) {
        if (_statLockInProgress || _statLockCooldown > 0) return;
        _statLockInProgress = true;
        ApplyStatLocksAsync(player);
    }

    private async void ApplyStatLocksAsync(Player player) {
        try {
            int safeMaxHp = Math.Max(1, LockedMaxHpValue);
            int maxHpCap = LockMaxHp ? safeMaxHp : Math.Max(1, player.Creature.MaxHp);
            int targetCurrentHp = Math.Clamp(LockedCurrentHpValue, 1, maxHpCap);

            if (LockMaxHp && player.Creature.MaxHp != safeMaxHp)
                await Sts2ApiCompat.SetMaxHpAsync(player.Creature, safeMaxHp);
            if (LockCurrentHp && player.Creature.CurrentHp != targetCurrentHp)
                await Sts2ApiCompat.SetCurrentHpAsync(player.Creature, targetCurrentHp);

            if (LockGold && player.Gold != Math.Max(0, LockedGoldValue))
                await PlayerCmd.SetGold((decimal)Math.Max(0, LockedGoldValue), player);

            if (LockMaxEnergy) {
                int safeMaxE = Math.Max(1, LockedMaxEnergyValue);
                if (player.MaxEnergy != safeMaxE) player.MaxEnergy = safeMaxE;
            }

            int maxECap = LockMaxEnergy ? Math.Max(1, LockedMaxEnergyValue) : Math.Max(1, player.MaxEnergy);
            int targetE = Math.Clamp(LockedCurrentEnergyValue, 0, maxECap);
            if (LockCurrentEnergy && player.PlayerCombatState != null && player.PlayerCombatState.Energy != targetE) {
                try { await PlayerCmd.SetEnergy((decimal)targetE, player); }
                catch { player.PlayerCombatState.Energy = targetE; }
            }

            if (LockStars && player.PlayerCombatState != null) {
                int s = Math.Max(0, LockedStarsValue);
                if (player.PlayerCombatState.Stars != s) player.PlayerCombatState.Stars = s;
            }

            if (LockOrbSlots)
                await ApplyOrbSlotsAsync(player, LockedOrbSlotsValue);
        }
        catch (Exception ex) { MainFile.Logger.Warn($"Stat lock failed: {ex.Message}"); }
        finally { _statLockInProgress = false; _statLockCooldown = 0.12; }
    }

    private static async Task ApplyOrbSlotsAsync(Player player, int targetSlots) {
        int safe = Math.Clamp(targetSlots, 0, 10);
        player.BaseOrbSlotCount = safe;
        if (player.PlayerCombatState != null && CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding) {
            // Try reflection for OrbCmd since it may not exist in all versions
            try {
                var orbCmdType = typeof(CreatureCmd).Assembly.GetType("MegaCrit.Sts2.Core.Commands.OrbCmd");
                if (orbCmdType != null) {
                    int current = Math.Max(0, player.PlayerCombatState.OrbQueue.Capacity);
                    int diff = safe - current;
                    if (diff > 0) {
                        var addMethod = orbCmdType.GetMethod("AddSlots", BindingFlags.Static | BindingFlags.Public);
                        if (addMethod != null) {
                            var result = addMethod.Invoke(null, new object[] { player, diff });
                            if (result is Task task) await task;
                        }
                    }
                    else if (diff < 0) {
                        var removeMethod = orbCmdType.GetMethod("RemoveSlots", BindingFlags.Static | BindingFlags.Public);
                        removeMethod?.Invoke(null, new object[] { player, -diff });
                    }
                }
            }
            catch { /* OrbCmd not available in this version */ }
        }
    }

    private static void TrySetCombatManagerBool(string name, bool value) {
        try {
            var instance = CombatManager.Instance;
            var type = instance.GetType();
            var prop = type.GetProperty(name, ReflFlags);
            if (prop is { CanWrite: true } && prop.PropertyType == typeof(bool)) {
                prop.SetValue(instance, value);
                return;
            }
            var field = type.GetField(name, ReflFlags)
                ?? type.GetField($"<{name}>k__BackingField", ReflFlags)
                ?? type.GetField($"_{char.ToLowerInvariant(name[0])}{name[1..]}", ReflFlags);
            if (field is { FieldType.Name: "Boolean", IsInitOnly: false })
                field.SetValue(instance, value);
        }
        catch { }
    }
}
