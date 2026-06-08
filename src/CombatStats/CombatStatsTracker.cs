using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.CombatStats;

/// <summary>
/// Aggregates combat statistics from <see cref="CombatHistory"/> for the DevMode stats panel.
/// </summary>
internal static class CombatStatsTracker {
    private const int MaxEventsPerPlayer = 200;

    private static readonly CombatHistoryTailer _tailer = new();
    private static bool _initialized;

    private static CombatStatsSnapshot _current = new();
    private static CombatStatsSnapshot? _last;
    private static CombatStatsSnapshot _runTotal = new();
    private static int _runCombatCount;

    /// <summary>Maps (receiver creature, power id) → player stats key who applied it.</summary>
    private static readonly Dictionary<(string ReceiverKey, string PowerId), string> _powerAppliers = new();

    internal static PowerDamageContext PendingPowerDamage { get; set; }

    public static event Action? Changed;

    public static CombatStatsSnapshot Current => _current;
    public static CombatStatsSnapshot? Last => _last;
    public static CombatStatsSnapshot RunTotal => _runTotal;
    public static int RunCombatCount => _runCombatCount;
    public static bool IsTracking => _current.IsActive;

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        RunManager.Instance.RunStarted += OnRunStarted;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    private static void OnRunStarted(RunState state) {
        if (!KitLibState.IsActive) return;
        _runTotal = new CombatStatsSnapshot();
        _runCombatCount = 0;
    }

    private static void OnCombatSetUp(CombatState state) {
        if (!KitLibState.IsActive) return;

        _current = new CombatStatsSnapshot {
            EncounterKey = ResolveEncounterKey(state),
            IsActive = true,
        };
        PendingPowerDamage = PowerDamageContext.None;
        _powerAppliers.Clear();

        foreach (Player player in state.Players) {
            if (player?.Creature != null)
                GetOrCreate(player.Creature);
        }

        _tailer.Attach(CombatManager.Instance.History, state);
        NotifyChanged();
    }

    private static void OnCombatEnded(CombatRoom room) {
        _tailer.Detach();
        PendingPowerDamage = PowerDamageContext.None;
        _powerAppliers.Clear();

        if (!_current.IsActive) return;

        _current.IsActive = false;
        _last = _current.Clone();
        _runTotal.MergeInto(_last);
        _runCombatCount++;
        NotifyChanged();
    }

    internal static void RecordDamage(
        CombatState combatState,
        Creature? dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource,
        int roundNumber,
        CombatSide currentSide) {
        if (!_current.IsActive) return;

        _current.MaxTurn = Math.Max(_current.MaxTurn, roundNumber);

        if (TryRecordPlayerDamageDealt(dealer, receiver, result, cardSource, roundNumber))
            return;

        if (dealer == null && receiver.IsEnemy && PendingPowerDamage.IsActive) {
            RecordPowerDamageToEnemy(receiver, result, roundNumber);
            return;
        }

        if (receiver.IsPlayer) {
            var stats = GetOrCreate(receiver);
            int taken = result.UnblockedDamage;
            stats.DamageTaken += taken;
            stats.DamageBlockedOnTaken += result.BlockedDamage;

            string sourceKey = ResolveDamageSourceKey(dealer);
            AddToDict(stats.DamageTakenBySource, sourceKey, taken);

            if (taken > 0) {
                AddEvent(stats, roundNumber, CombatStatEventKind.DamageTaken, $"{sourceKey} → {taken}", taken, 0);
                RecordTakenSynergies(dealer, receiver, result, cardSource, roundNumber);
            }
        }
    }

    private static bool TryRecordPlayerDamageDealt(
        Creature? dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource,
        int roundNumber) {
        if (dealer == null || !(dealer.IsPlayer || dealer.IsPet) || !receiver.IsEnemy)
            return false;

        var owner = ResolveDamageOwner(dealer);
        var stats = GetOrCreate(owner);
        int total = result.UnblockedDamage + result.BlockedDamage + result.OverkillDamage;
        stats.DamageDealt += total;
        stats.OverkillDealt += result.OverkillDamage;
        stats.BlockedByTarget += result.BlockedDamage;
        stats.HitCount++;

        AddToDict(stats.DamagePerTurn, roundNumber.ToString(), total);

        string cardKey = ResolveDamageCardKey(dealer, cardSource);
        AddToDict(stats.DamageByCard, cardKey, total);

        if (total > 0)
            AddEvent(stats, roundNumber, CombatStatEventKind.DamageDealt, $"{cardKey} → {total}", total,
                CombatScoreCalculator.DamageScore(total));

        RecordDealSynergies(owner, dealer, receiver, result, cardSource, roundNumber);
        return true;
    }

    private static void RecordPowerDamageToEnemy(Creature receiver, DamageResult result, int roundNumber) {
        var owner = PendingPowerDamage.Owner;
        if (owner == null || !owner.IsPlayer) {
            owner = ResolvePrimaryPlayerCreature();
            if (owner == null) return;
        }

        var stats = GetOrCreate(owner);
        int total = result.UnblockedDamage + result.BlockedDamage + result.OverkillDamage;
        stats.DamageDealt += total;
        stats.OverkillDealt += result.OverkillDamage;
        stats.BlockedByTarget += result.BlockedDamage;
        stats.HitCount++;

        AddToDict(stats.DamagePerTurn, roundNumber.ToString(), total);
        AddToDict(stats.PowerDamageBySource, PendingPowerDamage.SourceKey, total);

        if (total > 0) {
            AddEvent(stats, roundNumber, CombatStatEventKind.DamageDealt,
                $"{PendingPowerDamage.SourceKey} → {total}", total,
                CombatScoreCalculator.DamageScore(total));
        }
    }

    internal static void RecordBlockGained(Creature receiver, int amount, CardPlay? cardPlay, int roundNumber) {
        if (!_current.IsActive || amount <= 0 || !receiver.IsPlayer) return;

        var stats = GetOrCreate(receiver);
        stats.BlockGained += amount;

        string cardKey = ResolveCardKey(cardPlay?.Card);
        if (cardKey != null)
            AddToDict(stats.BlockByCard, cardKey, amount);

        AddEvent(stats, roundNumber, CombatStatEventKind.BlockGained, $"+{amount} block", amount,
            CombatScoreCalculator.BlockScore(amount));
    }

    internal static void RecordCardPlay(CardPlay cardPlay, int roundNumber) {
        if (!_current.IsActive) return;

        var owner = cardPlay.Card.Owner?.Creature;
        if (owner == null || !owner.IsPlayer) return;

        var stats = GetOrCreate(owner);
        stats.CardsPlayed++;

        string? title = ResolveCardKey(cardPlay.Card);
        string cardKey = title ?? cardPlay.Card.Id.Entry;
        int energy = Math.Max(0, cardPlay.Resources.EnergySpent);
        if (energy > 0)
            AddToDict(stats.EnergySpentByCard, cardKey, energy);

        int utilityScore = cardPlay.Card.Type == CardType.Attack
            ? 0
            : CombatScoreCalculator.UtilityPlayScore(energy);
        AddEvent(stats, roundNumber, CombatStatEventKind.CardPlayed, cardKey, energy, utilityScore);
    }

    internal static void RecordEnergySpent(int amount, Creature playerCreature, int roundNumber) {
        if (!_current.IsActive || amount <= 0 || !playerCreature.IsPlayer) return;

        var stats = GetOrCreate(playerCreature);
        stats.EnergySpent += amount;
        AddEvent(stats, roundNumber, CombatStatEventKind.EnergySpent, $"-{amount} energy", amount, 0);
    }

    internal static void RecordPotionUsed(PotionModel potion, int roundNumber) {
        if (!_current.IsActive) return;

        var owner = potion.Owner?.Creature;
        if (owner == null || !owner.IsPlayer) return;

        var stats = GetOrCreate(owner);
        stats.PotionsUsed++;
        string key = potion.Id.Entry;
        AddToDict(stats.PotionUseCount, key, 1);
        AddEvent(stats, roundNumber, CombatStatEventKind.PotionUsed, key, 1, CombatScoreCalculator.PotionScore());
    }

    internal static void RecordDebuffApplied(
        PowerModel power,
        Creature receiver,
        Creature? applier,
        int roundNumber,
        int stacks) {
        if (!_current.IsActive) return;
        if (power.Type != PowerType.Debuff) return;

        // Only score debuffs applied by players (or their pets), not enemy debuffs on players.
        if (applier == null)
            return;
        var credit = ResolveDamageOwner(applier);
        if (!credit.IsPlayer)
            return;

        int amount = Math.Max(1, stacks);
        var stats = GetOrCreate(credit);
        stats.DebuffsApplied += amount;
        string key = power.Id.Entry;
        AddToDict(stats.DebuffsByPower, key, amount);
        RegisterPowerApplier(receiver, key, stats);
        AddEvent(stats, roundNumber, CombatStatEventKind.DebuffApplied, key, amount,
            CombatScoreCalculator.DebuffScore(amount));
    }

    internal static void RecordBuffApplied(
        PowerModel power,
        Creature receiver,
        Creature? applier,
        int roundNumber,
        int stacks) {
        if (!_current.IsActive) return;
        if (power.Type != PowerType.Buff) return;
        if (!IsTrackablePower(power, stacks)) return;

        Creature? credit = applier is { IsPlayer: true } ? applier : receiver.IsPlayer ? receiver : applier;
        if (credit == null || !credit.IsPlayer) return;

        int amount = stacks;
        var stats = GetOrCreate(credit);
        stats.BuffsApplied += amount;
        string key = power.Id.Entry;
        RegisterPowerApplier(receiver, key, stats);
        AddEvent(stats, roundNumber, CombatStatEventKind.BuffApplied, key, amount,
            CombatScoreCalculator.BuffScore(amount));
    }

    private static bool IsTrackablePower(PowerModel power, int stacks) {
        if (stacks <= 0)
            return false;

        // Invisible powers are usually internal meters, not combat contributions.
        if (!power.IsVisible)
            return false;

        // None-stack powers are passive trait flags, not stackable effects.
        if (power.StackType == PowerStackType.None)
            return false;

        return true;
    }

    private static void RecordDealSynergies(
        Creature owner,
        Creature dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource,
        int roundNumber) {
        foreach (var hit in CombatDamageSynergyScorer.AnalyzeDealDamage(dealer, receiver, result, cardSource))
            CreditSynergy(hit, receiver, owner, roundNumber);
    }

    private static void RecordTakenSynergies(
        Creature? dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource,
        int roundNumber) {
        if (dealer == null) return;
        foreach (var hit in CombatDamageSynergyScorer.AnalyzeDamageTaken(dealer, receiver, result, cardSource))
            CreditSynergy(hit, dealer, receiver, roundNumber);
    }

    private static void CreditSynergy(
        CombatDamageSynergyScorer.SynergyHit hit,
        Creature powerHost,
        Creature? fallbackPlayer,
        int roundNumber) {
        var stats = ResolveSynergyCreditStats(powerHost, hit.PowerId, fallbackPlayer);
        if (stats == null) return;

        int score = CombatScoreCalculator.SynergyScore(hit.Amount);
        AddEvent(stats, roundNumber, CombatStatEventKind.PowerSynergy, $"{hit.Label} → {hit.Amount}", hit.Amount, score);
    }

    private static PlayerCombatStats? ResolveSynergyCreditStats(
        Creature powerHost,
        string powerId,
        Creature? fallbackPlayer) {
        if (TryGetApplierStats(powerHost, powerId, out var applier))
            return applier;

        if (fallbackPlayer != null && fallbackPlayer.IsPlayer)
            return GetOrCreate(fallbackPlayer);

        return null;
    }

    private static bool TryGetApplierStats(Creature receiver, string powerId, out PlayerCombatStats stats) {
        stats = null!;
        string receiverKey = CreatureKey(receiver);
        if (!_powerAppliers.TryGetValue((receiverKey, powerId), out string? playerKey))
            return false;
        return _current.Players.TryGetValue(playerKey, out stats);
    }

    private static void RegisterPowerApplier(Creature receiver, string powerId, PlayerCombatStats applier) {
        _powerAppliers[(CreatureKey(receiver), powerId)] = applier.Key;
    }

    private static string CreatureKey(Creature creature) {
        if (creature.Player != null)
            return creature.Player.NetId.ToString();
        if (TryResolveCombatPlayerKey(creature, out string? netKey))
            return netKey;
        return $"c_{creature.GetHashCode()}";
    }

    internal static void RecordEnemyMove(MonsterModel monster, int roundNumber) {
        if (!_current.IsActive) return;

        var player = ResolvePrimaryPlayerCreature();
        if (player == null) return;

        var stats = GetOrCreate(player);
        string name = monster.Id.Entry;
        AddEvent(stats, roundNumber, CombatStatEventKind.EnemyMove, name, 0, 0);
    }

    private static PlayerCombatStats GetOrCreate(Creature creature) {
        string key = ResolvePlayerKey(creature);

        if (_current.Players.TryGetValue(key, out var existing)) {
            RefreshPlayerIdentity(existing, creature);
            return existing;
        }

        string orphanKey = CreatureKey(creature);
        if (orphanKey != key && _current.Players.Remove(orphanKey, out var orphaned)) {
            var merged = new PlayerCombatStats {
                Key = key,
                DisplayName = orphaned.DisplayName,
                CharacterId = orphaned.CharacterId,
                DamageDealt = orphaned.DamageDealt,
                DamageTaken = orphaned.DamageTaken,
                BlockGained = orphaned.BlockGained,
                CardsPlayed = orphaned.CardsPlayed,
                HitCount = orphaned.HitCount,
                OverkillDealt = orphaned.OverkillDealt,
                BlockedByTarget = orphaned.BlockedByTarget,
                DamageBlockedOnTaken = orphaned.DamageBlockedOnTaken,
                EnergySpent = orphaned.EnergySpent,
                PotionsUsed = orphaned.PotionsUsed,
                DebuffsApplied = orphaned.DebuffsApplied,
                BuffsApplied = orphaned.BuffsApplied,
            };
            foreach (var (k, v) in orphaned.DamageByCard)
                merged.DamageByCard[k] = v;
            foreach (var (k, v) in orphaned.DamageTakenBySource)
                merged.DamageTakenBySource[k] = v;
            foreach (var (k, v) in orphaned.DamagePerTurn)
                merged.DamagePerTurn[k] = v;
            foreach (var (k, v) in orphaned.BlockByCard)
                merged.BlockByCard[k] = v;
            foreach (var (k, v) in orphaned.EnergySpentByCard)
                merged.EnergySpentByCard[k] = v;
            foreach (var (k, v) in orphaned.PotionUseCount)
                merged.PotionUseCount[k] = v;
            foreach (var (k, v) in orphaned.DebuffsByPower)
                merged.DebuffsByPower[k] = v;
            foreach (var (k, v) in orphaned.PowerDamageBySource)
                merged.PowerDamageBySource[k] = v;
            merged.Events.AddRange(orphaned.Events);
            RefreshPlayerIdentity(merged, creature);
            _current.Players[key] = merged;
            return merged;
        }

        var stats = new PlayerCombatStats {
            Key = key,
            DisplayName = ResolveCreatureDisplayName(creature),
            CharacterId = creature.Player?.Character.Id.Entry ?? "",
        };
        _current.Players[key] = stats;
        return stats;
    }

    private static string ResolvePlayerKey(Creature creature) {
        if (creature.Player != null)
            return creature.Player.NetId.ToString();

        if (TryResolveCombatPlayerKey(creature, out string? netKey))
            return netKey;

        return CreatureKey(creature);
    }

    private static bool TryResolveCombatPlayerKey(Creature creature, out string netKey) {
        netKey = "";
        CombatState? state = Sts2CombatCompat.GetCreatureCombatState(creature)
            ?? CombatManager.Instance?.DebugOnlyGetState();
        if (state == null)
            return false;

        foreach (Player player in state.Players) {
            if (player.Creature != creature)
                continue;
            netKey = player.NetId.ToString();
            return true;
        }

        return false;
    }

    private static void RefreshPlayerIdentity(PlayerCombatStats stats, Creature creature) {
        string name = ResolveCreatureDisplayName(creature);
        if (!string.IsNullOrWhiteSpace(name))
            stats.DisplayName = name;

        string charId = creature.Player?.Character.Id.Entry ?? "";
        if (!string.IsNullOrWhiteSpace(charId))
            stats.CharacterId = charId;
    }

    private static string ResolveCreatureDisplayName(Creature creature) {
        try {
            string name = creature.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch {
            // Platform name lookup can fail in some contexts.
        }

        Player? player = creature.Player;
        if (player?.Character != null) {
            try {
                string title = player.Character.Title.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                    return title;
            }
            catch {
                // fall through
            }
        }

        if (player != null)
            return player.NetId.ToString();

        return I18N.T("combatStats.player.unknown", "Player");
    }

    private static Creature? ResolvePrimaryPlayerCreature() {
        RunState? run = RunManager.Instance?.DebugOnlyGetState();
        if (RunContext.TryGetRunAndPlayer(out var runState, out var player)) {
            run = runState;
            return player.Creature;
        }

        if (run == null || _current.Players.Count == 0) return null;
        string key = _current.Players.Keys.First();
        foreach (var p in run.Players) {
            if (p.NetId.ToString() == key)
                return p.Creature;
        }
        return null;
    }

    private static Creature ResolveDamageOwner(Creature dealer) {
        if (dealer.IsPet && dealer.PetOwner != null)
            return dealer.PetOwner.Creature;
        return dealer;
    }

    private static string ResolveDamageCardKey(Creature dealer, CardModel? cardSource) {
        if (dealer.IsPet)
            return dealer.Monster?.Id.Entry ?? "Pet";
        return ResolveCardKey(cardSource) ?? I18N.T("combatStats.source.other", "Other");
    }

    private static string? ResolveCardKey(CardModel? card) {
        if (card == null) return null;
        try {
            string title = card.Title;
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch {
            // fall through
        }
        return card.Id.Entry;
    }

    private static string ResolveDamageSourceKey(Creature? dealer) {
        if (dealer == null)
            return I18N.T("combatStats.source.unknown", "Unknown");

        if (dealer.IsMonster) {
            try {
                if (!string.IsNullOrWhiteSpace(dealer.Name))
                    return dealer.Name;
            }
            catch {
                // fall through
            }
            return dealer.Monster?.Id.Entry ?? I18N.T("combatStats.source.enemy", "Enemy");
        }

        return I18N.T("combatStats.source.other", "Other");
    }

    private static string ResolveEncounterKey(CombatState state) {
        try {
            return state.Encounter?.Id.Entry ?? "";
        }
        catch {
            return "";
        }
    }

    private static void AddEvent(
        PlayerCombatStats stats,
        int turn,
        CombatStatEventKind kind,
        string text,
        int amount,
        int scorePoints) {
        if (stats.Events.Count >= MaxEventsPerPlayer)
            stats.Events.RemoveAt(0);
        stats.Events.Add(new CombatStatEvent {
            Turn = turn,
            Kind = kind,
            Text = text,
            Amount = amount,
            ScorePoints = scorePoints,
        });
    }

    private static void AddToDict<TKey>(Dictionary<TKey, int> dict, TKey key, int amount)
        where TKey : notnull {
        dict.TryGetValue(key, out int prev);
        dict[key] = prev + amount;
    }

    private static void NotifyChanged() => Changed?.Invoke();

    /// <summary>Called when live combat stats change (history tailer processed new entries).</summary>
    internal static void NotifyStatsUpdated() => NotifyChanged();
}

internal readonly struct PowerDamageContext {
    public bool IsActive { get; init; }
    public Creature? Owner { get; init; }
    public string SourceKey { get; init; }

    public static PowerDamageContext None => default;

    public static PowerDamageContext Create(Creature? owner, string sourceKey) => new() {
        IsActive = true,
        Owner = owner,
        SourceKey = sourceKey,
    };
}
