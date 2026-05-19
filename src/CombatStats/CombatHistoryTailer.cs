using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DevMode.CombatStats;

/// <summary>
/// Incrementally consumes <see cref="CombatHistory"/> entries and forwards them to
/// <see cref="CombatStatsTracker"/>.
/// </summary>
internal sealed class CombatHistoryTailer {
    private CombatHistory? _history;
    private CombatState? _combatState;
    private int _lastSeenIndex;
    private bool _lastCardWasSoul;

    public void Attach(CombatHistory history, CombatState combatState) {
        Detach();
        _history = history;
        _combatState = combatState;
        _lastSeenIndex = 0;
        _lastCardWasSoul = false;
        _history.Changed += OnChanged;
        Drain();
    }

    public void Detach() {
        if (_history != null)
            _history.Changed -= OnChanged;
        _history = null;
        _combatState = null;
        _lastSeenIndex = 0;
        _lastCardWasSoul = false;
        CombatStatsTracker.PendingPowerDamage = PowerDamageContext.None;
    }

    private void OnChanged() => Drain();

    private void Drain() {
        if (_history == null || _combatState == null) return;

        var entries = _history.Entries.ToList();
        if (entries.Count < _lastSeenIndex)
            _lastSeenIndex = 0;

        int before = _lastSeenIndex;
        for (int i = _lastSeenIndex; i < entries.Count; i++)
            DispatchEntry(entries[i]);

        _lastSeenIndex = entries.Count;
        if (_lastSeenIndex > before)
            CombatStatsTracker.NotifyStatsUpdated();
    }

    private void DispatchEntry(CombatHistoryEntry entry) {
        if (_combatState == null) return;

        try {
            switch (entry) {
                case DamageReceivedEntry dmg:
                    InferPowerDamageContext(dmg, entry);
                    CombatStatsTracker.RecordDamage(_combatState, dmg.Dealer, dmg.Receiver,
                        dmg.Result, dmg.CardSource, entry.RoundNumber, entry.CurrentSide);
                    CombatStatsTracker.PendingPowerDamage = PowerDamageContext.None;
                    _lastCardWasSoul = false;
                    break;
                case BlockGainedEntry block:
                    CombatStatsTracker.RecordBlockGained(block.Receiver, block.Amount, block.CardPlay, entry.RoundNumber);
                    break;
                case CardPlayFinishedEntry play:
                    _lastCardWasSoul = play.CardPlay.Card is Soul;
                    CombatStatsTracker.RecordCardPlay(play.CardPlay, entry.RoundNumber);
                    break;
                case EnergySpentEntry energy:
                    CombatStatsTracker.RecordEnergySpent(energy.Amount, energy.Actor, entry.RoundNumber);
                    break;
                case PotionUsedEntry potion:
                    CombatStatsTracker.RecordPotionUsed(potion.Potion, entry.RoundNumber);
                    break;
                case PowerReceivedEntry power: {
                    int stacks = (int)Math.Round(power.Amount);
                    if (stacks <= 0)
                        break;

                    if (power.Power.Type == PowerType.Debuff) {
                        CombatStatsTracker.RecordDebuffApplied(
                            power.Power, power.Actor, power.Applier, entry.RoundNumber, stacks);
                    }
                    else if (power.Power.Type == PowerType.Buff) {
                        CombatStatsTracker.RecordBuffApplied(
                            power.Power, power.Actor, power.Applier, entry.RoundNumber, stacks);
                    }
                    break;
                }
                case MonsterPerformedMoveEntry move:
                    CombatStatsTracker.RecordEnemyMove(move.Monster, entry.RoundNumber);
                    break;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatHistoryTailer: dispatch failed ({entry.GetType().Name}): {ex.Message}");
        }
    }

    private void InferPowerDamageContext(DamageReceivedEntry dmg, CombatHistoryEntry entry) {
        if (dmg.Dealer != null || !dmg.Receiver.IsEnemy || _combatState == null) {
            CombatStatsTracker.PendingPowerDamage = PowerDamageContext.None;
            return;
        }

        if (entry.CurrentSide == CombatSide.Enemy) {
            var poison = dmg.Receiver.GetPower<PoisonPower>();
            if (poison != null) {
                SetPendingPowerDamage(null, poison);
                return;
            }
        }

        if (entry.CurrentSide == CombatSide.Player) {
            var strangle = dmg.Receiver.GetPower<StranglePower>();
            if (strangle != null) {
                SetPendingPowerDamage(null, strangle);
                return;
            }
        }

        if (_lastCardWasSoul) {
            foreach (Player player in _combatState.Players) {
                var haunt = player.Creature.GetPower<HauntPower>();
                if (haunt != null) {
                    SetPendingPowerDamage(player.Creature, haunt);
                    return;
                }
            }
        }

        CombatStatsTracker.PendingPowerDamage = PowerDamageContext.None;
    }

    private static void SetPendingPowerDamage(Creature? owner, PowerModel power) {
        CombatStatsTracker.PendingPowerDamage = PowerDamageContext.Create(owner, power.Id.Entry);
    }
}
