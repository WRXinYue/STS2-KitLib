using System;
using System.Collections.Generic;
using System.Linq;
using KitLib;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace KitLib.CombatStats;

/// <summary>
/// Aggregates combat statistics from <see cref="CombatHistory"/> for the browser viewer.
/// </summary>
internal static class CombatStatsTracker {
    private const int MaxEventsPerPlayer = 200;
    private const int MaxCombatEvents = 500;

    private static CombatHistoryTailer? _tailer;
    private static bool _initialized;
    private static bool _wired;
    private static CombatState? _combatState;

    private static CombatStatsSnapshot? _current;
    private static CombatStatsSnapshot? _last;
    private static CombatStatsSnapshot? _runTotal;
    private static int _runCombatCount;
    private static int _nextEventSequence;
    private static readonly HashSet<int> _turnEndStatesEmitted = new();
    private static int _displayTurn = 1;
    private static bool _playerTurnStarted;
    private static bool _cardPlayResolutionActive;
    private static CardPlay? _activeCardPlay;

    private static CombatHistoryTailer Tailer => _tailer ??= new CombatHistoryTailer();

    /// <summary>Maps (receiver creature, power id) → player stats key who applied it.</summary>
    private static readonly Dictionary<(string ReceiverKey, string PowerId), string> _powerAppliers = new();

    internal static PowerDamageContext PendingPowerDamage { get; set; }

    private static CombatStatSource? _pendingEffectSource;

    public static event Action? Changed;

    public static CombatStatsSnapshot Current => _current ??= new CombatStatsSnapshot();
    public static CombatStatsSnapshot? Last => _last;
    public static CombatStatsSnapshot RunTotal => _runTotal ??= new CombatStatsSnapshot();
    public static int RunCombatCount => _runCombatCount;
    public static bool IsTracking => _current?.IsActive ?? false;

    /// <summary>Player-facing turn counter (matches PlayerCombatState.TurnNumber).</summary>
    internal static int DisplayTurn => _displayTurn;

    internal static int ResolveEventTurn(CombatHistoryEntry entry) {
        int turn = Sts2CombatCompat.ResolveHistoryDisplayTurn(entry);
        if (Sts2CombatCompat.GetHistoryCurrentSide(entry) == CombatSide.Player)
            turn = Math.Max(turn, _displayTurn);
        return Math.Max(1, turn);
    }

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;
    }

    internal static void EnsureWired() {
        if (_wired) return;
        try {
            var runManager = RunManager.Instance;
            var combatManager = CombatManager.Instance;
            if (runManager == null || combatManager == null)
                return;

            runManager.RunStarted += OnRunStarted;
            combatManager.CombatSetUp += OnCombatSetUp;
            combatManager.CombatEnded += OnCombatEnded;
            combatManager.TurnStarted += OnTurnStarted;
            combatManager.TurnEnded += OnTurnEnded;
            _wired = true;
        }
        catch (Exception) {
        }
    }

    private static void OnRunStarted(RunState state) {
        if (!KitLibState.IsActive) return;
        _runTotal = new CombatStatsSnapshot();
        _runCombatCount = 0;
    }

    private static void OnCombatSetUp(CombatState state) {
        if (!KitLibState.IsActive) return;

        _combatState = state;
        _current = new CombatStatsSnapshot {
            EncounterKey = ResolveEncounterKey(state),
            IsActive = true,
        };
        _nextEventSequence = 0;
        _turnEndStatesEmitted.Clear();
        _displayTurn = Sts2CombatCompat.GetPrimaryPlayerTurnNumber(state);
        _playerTurnStarted = false;
        _cardPlayResolutionActive = false;
        PendingPowerDamage = PowerDamageContext.None;
        _pendingEffectSource = null;
        _powerAppliers.Clear();

        CombatStatsLiveBuffer.ResetForNewCombat();

        foreach (Player player in state.Players) {
            if (player?.Creature != null)
                GetOrCreate(player.Creature);
        }

        int openingTurn = Sts2CombatCompat.GetPrimaryPlayerTurnNumber(state);
        RecordTurnSnapshot(state, openingTurn, "start");
        EmitOpeningPlayerStates(openingTurn);
        RefreshLiveCreatures(state);

        Tailer.Attach(CombatManager.Instance.History, state);

        try {
            DevViewerServer.EnsureStarted();
        }
        catch (Exception ex) {
            KitLog.Warn("CombatStats", $"Viewer server unavailable: {ex.Message}");
        }

        NotifyChanged();
    }

    private static void OnTurnStarted(CombatState state) {
        if (_current is not { IsActive: true })
            return;

        _combatState = state;

        if (state.CurrentSide == CombatSide.Player) {
            int newTurn = Sts2CombatCompat.GetPrimaryPlayerTurnNumber(state);
            if (_playerTurnStarted && newTurn > _displayTurn)
                EmitTurnEndCreatureStates(_displayTurn);
            _playerTurnStarted = true;
            _displayTurn = Math.Max(1, newTurn);
        }

        _current.MaxTurn = Math.Max(_current.MaxTurn, _displayTurn);
        RecordTurnSnapshot(state, _displayTurn, "start");
        RefreshLiveCreatures(state);
        NotifyChanged();
    }

    private static void OnTurnEnded(CombatState state) {
        if (_current is not { IsActive: true })
            return;

        _combatState = state;
        Tailer.FlushPending();
        _current.MaxTurn = Math.Max(_current.MaxTurn, _displayTurn);
        RecordTurnSnapshot(state, _displayTurn, "end");
        RefreshLiveCreatures(state);
        NotifyChanged();
    }

    private static void OnCombatEnded(CombatRoom room) {
        Tailer.Detach();
        PendingPowerDamage = PowerDamageContext.None;
        _powerAppliers.Clear();

        if (_current is { IsActive: true }) {
            var state = _combatState ?? CombatManager.Instance?.DebugOnlyGetState();
            if (state != null) {
                _current.MaxTurn = Math.Max(_current.MaxTurn, _displayTurn);
                Tailer.FlushPending();
                RecordTurnSnapshot(state, _displayTurn, "end");
                EmitTurnEndCreatureStates(_displayTurn);
                RefreshLiveCreatures(state, includeDefeated: true);
            }

            _current.IsActive = false;
            _last = _current.Clone();
            _runTotal.MergeInto(_last);
            _runCombatCount++;
        }

        _combatState = null;
        NotifyChanged(forcePersist: true);
    }

    internal static void SetPendingEffectSource(CombatStatSource source) {
        if (source.IsKnown)
            _pendingEffectSource = source;
    }

    internal static CombatStatSource? TakePendingEffectSource() {
        var source = _pendingEffectSource;
        _pendingEffectSource = null;
        return source is { IsKnown: true } ? source : null;
    }

    internal static void SetCardPlayResolutionActive(bool active) {
        _cardPlayResolutionActive = active;
        if (!active)
            _activeCardPlay = null;
    }

    internal static void SetActiveCardPlay(CardPlay? cardPlay) => _activeCardPlay = cardPlay;

    internal static void RecordDamage(
        CombatState combatState,
        Creature? dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource,
        int roundNumber,
        CombatSide currentSide) {
        if (_current is not { IsActive: true }) return;

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
                var source = ResolveDamageTakenSource(dealer, cardSource);
                AddPlayerEvent(receiver, roundNumber, CombatStatEventKind.DamageTaken, $"{source.Name} → {taken}", taken,
                    source: source);
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

        if (total > 0) {
            var source = ResolveDamageDealtSource(dealer, cardSource);
            AddPlayerEvent(owner, roundNumber, CombatStatEventKind.DamageDealt, $"{source.Name} → {total}", total,
                source: source);
            RecordCreatureStateAfterHit(receiver, roundNumber);
        }

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
            var source = CombatStatSource.FromPower(
                PendingPowerDamage.SourceKey, PendingPowerDamage.SourceDisplayName);
            AddPlayerEvent(owner, roundNumber, CombatStatEventKind.DamageDealt,
                $"{source.Name} → {total}", total, source: source);
            RecordCreatureStateAfterHit(receiver, roundNumber);
        }
    }

    internal static void RecordBlockGained(
        Creature receiver,
        int amount,
        ValueProp props,
        CardPlay? cardPlay,
        int roundNumber) {
        if (_current is not { IsActive: true } || amount <= 0 || !receiver.IsPlayer) return;

        var stats = GetOrCreate(receiver);
        stats.BlockGained += amount;

        string? cardKey = ResolveCardKey(cardPlay?.Card);
        if (cardKey != null)
            AddToDict(stats.BlockByCard, cardKey, amount);

        var source = TakePendingEffectSource()
            ?? CombatStatsSourceResolver.ResolveBlock(cardPlay, props, receiver);
        AddPlayerEvent(receiver, roundNumber, CombatStatEventKind.BlockGained, $"+{amount} block", amount,
            linkedToCardPlay: cardPlay != null && _cardPlayResolutionActive,
            source: source);
    }

    internal static void RecordCardPlay(CardPlay cardPlay, int roundNumber) {
        if (_current is not { IsActive: true }) return;

        var owner = cardPlay.Card.Owner?.Creature;
        if (owner == null || !owner.IsPlayer) return;

        var stats = GetOrCreate(owner);
        stats.CardsPlayed++;

        string? title = ResolveCardKey(cardPlay.Card);
        string cardKey = title ?? cardPlay.Card.Id.Entry;
        int energy = Math.Max(0, cardPlay.Resources.EnergySpent);
        if (energy > 0)
            AddToDict(stats.EnergySpentByCard, cardKey, energy);

        AddPlayerEvent(owner, roundNumber, CombatStatEventKind.CardPlayed, cardKey, energy,
            source: CombatStatSource.FromCard(cardPlay.Card));
        _cardPlayResolutionActive = false;
        _activeCardPlay = null;
    }

    internal static void RecordEnergySpent(int amount, Creature playerCreature, int roundNumber) {
        if (_current is not { IsActive: true } || amount <= 0 || !playerCreature.IsPlayer) return;

        var stats = GetOrCreate(playerCreature);
        stats.EnergySpent += amount;
        var source = ResolveEnergySource();
        AddPlayerEvent(playerCreature, roundNumber, CombatStatEventKind.EnergySpent, $"-{amount} energy", amount,
            source: source);
    }

    internal static void RecordPotionUsed(PotionModel potion, int roundNumber) {
        if (_current is not { IsActive: true }) return;

        var owner = potion.Owner?.Creature;
        if (owner == null || !owner.IsPlayer) return;

        var stats = GetOrCreate(owner);
        stats.PotionsUsed++;
        string key = potion.Id.Entry;
        AddToDict(stats.PotionUseCount, key, 1);
        AddPlayerEvent(owner, roundNumber, CombatStatEventKind.PotionUsed,
            CombatStatsDisplayNames.ResolvePotionName(potion), 1,
            source: CombatStatSource.FromPotion(potion));
    }

    internal static void RecordDebuffApplied(
        PowerModel power,
        Creature receiver,
        Creature? applier,
        int roundNumber,
        int stacks) {
        if (_current is not { IsActive: true }) return;
        if (power.Type != PowerType.Debuff) return;

        // Only track debuffs applied by players (or their pets), not enemy debuffs on players.
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
        var source = ResolvePowerApplicationSource(applier, power);
        AddPlayerEvent(credit, roundNumber, CombatStatEventKind.DebuffApplied,
            CombatStatsDisplayNames.ResolvePowerName(power), amount, source: source);
    }

    internal static void RecordBuffApplied(
        PowerModel power,
        Creature receiver,
        Creature? applier,
        int roundNumber,
        int stacks) {
        if (_current is not { IsActive: true }) return;
        if (power.Type != PowerType.Buff) return;
        if (!IsTrackablePower(power, stacks)) return;

        Creature? credit = applier is { IsPlayer: true } ? applier : receiver.IsPlayer ? receiver : applier;
        if (credit == null || !credit.IsPlayer) return;

        int amount = stacks;
        var stats = GetOrCreate(credit);
        stats.BuffsApplied += amount;
        string key = power.Id.Entry;
        RegisterPowerApplier(receiver, key, stats);
        var source = ResolvePowerApplicationSource(applier, power);
        AddPlayerEvent(credit, roundNumber, CombatStatEventKind.BuffApplied,
            CombatStatsDisplayNames.ResolvePowerName(power), amount, source: source);
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

        AddEvent(stats, roundNumber, CombatStatEventKind.PowerSynergy, $"{hit.Label} → {hit.Amount}", hit.Amount,
            stats.Key, "player", stats.DisplayName,
            source: CombatStatSource.Synergy(hit.PowerId, hit.Label));
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
        if (_current is not { IsActive: true }) return;

        string actorKey = monster.Id.Entry;
        string actorName = actorKey;
        try {
            string title = monster.Title.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(title))
                actorName = title;
        }
        catch { }

        AddCombatEvent(roundNumber, CombatStatEventKind.EnemyMove, actorName, 0, actorKey, "enemy", actorName,
            source: CombatStatSource.FromMonster(monster));
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
        if (card == null)
            return null;
        string name = CombatStatsDisplayNames.ResolveCardName(card);
        return string.IsNullOrWhiteSpace(name) ? card.Id.Entry : name;
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

    private static CombatStatSource ResolveBlockSource(CardPlay? cardPlay, ValueProp props, Creature? receiver) =>
        CombatStatsSourceResolver.ResolveBlock(cardPlay, props, receiver);

    private static CombatStatSource ResolveDamageDealtSource(Creature? dealer, CardModel? cardSource) {
        if (cardSource != null)
            return CombatStatSource.FromCard(cardSource);
        if (CombatStatsSourceContext.TryPeek(out var ctx))
            return ctx;
        return CombatStatSource.FromCreature(dealer);
    }

    private static CombatStatSource ResolveDamageTakenSource(Creature? dealer, CardModel? cardSource) {
        if (cardSource != null)
            return CombatStatSource.FromCard(cardSource);
        if (dealer != null) {
            var fromDealer = CombatStatSource.FromCreature(dealer);
            if (fromDealer.IsKnown)
                return fromDealer;
        }
        if (CombatStatsSourceContext.TryPeek(out var ctx))
            return ctx;
        return CombatStatSource.Unknown;
    }

    private static CombatStatSource ResolvePowerApplicationSource(Creature? applier, PowerModel? power = null) {
        var pending = TakePendingEffectSource();
        if (pending is { IsKnown: true } known)
            return known;

        if (power != null) {
            var resolved = CombatStatsSourceResolver.ResolvePowerApply(power, applier);
            if (resolved.IsKnown)
                return resolved;
        }

        if (applier != null) {
            var fromApplier = CombatStatSource.FromCreature(applier);
            if (fromApplier.IsKnown && fromApplier.Kind != CombatStatSourceKind.Player)
                return fromApplier;
        }

        return CombatStatSource.Unknown;
    }

    private static CombatStatSource ResolveEnergySource() {
        if (_activeCardPlay?.Card is CardModel card)
            return CombatStatSource.FromCard(card);
        if (CombatStatsSourceContext.TryPeek(out var ctx))
            return ctx;
        return CombatStatSource.Unknown;
    }

    private static void AddPlayerEvent(
        Creature creature,
        int turn,
        CombatStatEventKind kind,
        string text,
        int amount,
        bool? linkedToCardPlay = null,
        CombatStatSource? source = null) {
        var stats = GetOrCreate(creature);
        AddEvent(stats, turn, kind, text, amount, stats.Key, "player", stats.DisplayName, linkedToCardPlay, source);
    }

    private static void AddCombatEvent(
        int turn,
        CombatStatEventKind kind,
        string text,
        int amount,
        string actorKey,
        string actorSide,
        string actorName,
        CreatureState? creature = null,
        string statePhase = "",
        bool? linkedToCardPlay = null,
        CombatStatSource? source = null) {
        if (_current == null)
            return;

        if (_current.CombatEvents.Count >= MaxCombatEvents)
            _current.CombatEvents.RemoveAt(0);

        CombatStatSource resolved = source ?? CombatStatSource.Unknown;
        _current.CombatEvents.Add(new CombatStatEvent {
            Sequence = ++_nextEventSequence,
            Turn = turn,
            Kind = kind,
            Text = text,
            Amount = amount,
            ActorKey = actorKey,
            ActorSide = actorSide,
            ActorName = actorName,
            Creature = creature,
            StatePhase = statePhase,
            LinkedToCardPlay = linkedToCardPlay ?? ResolveLinkedToCardPlay(kind, statePhase),
            SourceKind = resolved.Kind,
            SourceKey = resolved.Key,
            SourceName = resolved.Name,
        });
    }

    private static bool ResolveLinkedToCardPlay(CombatStatEventKind kind, string statePhase = "") {
        if (!_cardPlayResolutionActive)
            return false;
        if (kind == CombatStatEventKind.BlockGained)
            return false;
        if (kind == CombatStatEventKind.CreatureState)
            return statePhase == "hit";
        return kind is not (CombatStatEventKind.DebuffApplied or CombatStatEventKind.BuffApplied);
    }

    private static void AddEvent(
        PlayerCombatStats stats,
        int turn,
        CombatStatEventKind kind,
        string text,
        int amount,
        string actorKey,
        string actorSide,
        string actorName,
        bool? linkedToCardPlay = null,
        CombatStatSource? source = null) {
        if (stats.Events.Count >= MaxEventsPerPlayer)
            stats.Events.RemoveAt(0);

        CombatStatSource resolved = source ?? CombatStatSource.Unknown;
        var ev = new CombatStatEvent {
            Turn = turn,
            Kind = kind,
            Text = text,
            Amount = amount,
            ActorKey = actorKey,
            ActorSide = actorSide,
            ActorName = actorName,
            SourceKind = resolved.Kind,
            SourceKey = resolved.Key,
            SourceName = resolved.Name,
        };
        stats.Events.Add(ev);
        AddCombatEvent(turn, kind, text, amount, actorKey, actorSide, actorName,
            linkedToCardPlay: linkedToCardPlay, source: resolved);
    }

    private static void RecordTurnSnapshot(CombatState state, int turn, string phase) {
        if (_current == null)
            return;

        var snapshot = CombatStatsSnapshotCapture.CaptureTurn(state, turn, phase);
        int existing = _current.TurnSnapshots.FindIndex(t => t.Turn == turn && t.Phase == phase);
        if (existing >= 0)
            _current.TurnSnapshots[existing] = snapshot;
        else
            _current.TurnSnapshots.Add(snapshot);
    }

    private static void RecordCreatureStateAfterHit(Creature creature, int turn) {
        if (!creature.IsEnemy)
            return;

        var state = CombatStatsSnapshotCapture.CaptureEnemyCreature(creature);
        RecordCreatureStateEvent(state, turn, "hit");
    }

    private static void EmitOpeningPlayerStates(int turn) {
        if (_current == null)
            return;

        var snapshot = _current.TurnSnapshots.Find(t => t.Turn == turn && t.Phase == "start");
        if (snapshot == null)
            return;

        foreach (CreatureState player in snapshot.Creatures.Where(c => c.Side == "player"))
            RecordCreatureStateEvent(player, turn, "start");
    }

    private static void EmitTurnEndCreatureStates(int turn) {
        if (_current == null || !_turnEndStatesEmitted.Add(turn))
            return;

        var snapshot = _current.TurnSnapshots.Find(t => t.Turn == turn && t.Phase == "end");
        if (snapshot == null)
            return;

        foreach (CreatureState player in snapshot.Creatures.Where(c => c.Side == "player"))
            RecordCreatureStateEvent(player, turn, "end");

        foreach (CreatureState enemy in snapshot.Creatures.Where(c => c.Side == "enemy"))
            RecordCreatureStateEvent(enemy, turn, "end");
    }

    private static void RecordCreatureStateEvent(CreatureState state, int turn, string statePhase) {
        AddCombatEvent(
            turn,
            CombatStatEventKind.CreatureState,
            state.DisplayName,
            state.CurrentHp,
            state.Key,
            state.Side,
            state.DisplayName,
            state,
            statePhase);
    }

    private static void RefreshLiveCreatures(CombatState state, bool includeDefeated = false) {
        if (_current == null)
            return;

        _current.LiveCreatures.Clear();
        _current.LiveCreatures.AddRange(CombatStatsSnapshotCapture.CaptureLive(state, includeDefeated));
    }

    private static void AddToDict<TKey>(Dictionary<TKey, int> dict, TKey key, int amount)
        where TKey : notnull {
        dict.TryGetValue(key, out int prev);
        dict[key] = prev + amount;
    }

    private static void NotifyChanged(bool forcePersist = false) {
        CombatStatsLiveBuffer.Persist(forcePersist);
        Changed?.Invoke();
    }

    /// <summary>Called when live combat stats change (history tailer processed new entries).</summary>
    internal static void NotifyStatsUpdated() {
        if (_current is { IsActive: true } && _combatState != null)
            RefreshLiveCreatures(_combatState);
        NotifyChanged();
    }
}

internal readonly struct PowerDamageContext {
    public bool IsActive { get; init; }
    public Creature? Owner { get; init; }
    public string SourceKey { get; init; }
    public string SourceDisplayName { get; init; }

    public static PowerDamageContext None => default;

    public static PowerDamageContext Create(Creature? owner, string sourceKey, string sourceDisplayName) => new() {
        IsActive = true,
        Owner = owner,
        SourceKey = sourceKey,
        SourceDisplayName = sourceDisplayName,
    };
}
