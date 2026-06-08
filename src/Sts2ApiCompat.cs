using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

/// <summary>
/// Reflection-based API compatibility layer.
/// Dynamically resolves game API methods across sts2.dll versions.
/// </summary>
internal static class Sts2ApiCompat {
    public static CardModel CreateCardForCurrentContext(IRunState runState, CardModel canonicalCard, Player owner, bool inCombat) {
        if (inCombat) {
            var creature = owner.Creature;
            if (creature?.CombatState != null)
                return owner.Creature.CombatState.CreateCard(canonicalCard, owner);
        }
        return ((ICardScope)runState).CreateCard(canonicalCard, owner);
    }

    public static async Task SetMaxHpAsync(Creature creature, decimal targetMaxHp) {
        decimal safeTarget = Math.Max(1m, targetMaxHp);

        if (await TryInvokeCreatureCommandAsync("SetMaxHp", creature, safeTarget))
            return;

        decimal delta = safeTarget - (decimal)creature.MaxHp;
        if (delta > 0m && await TryInvokeCreatureCommandAsync("GainMaxHp", creature, delta))
            return;
        if (delta < 0m && await TryInvokeCreatureCommandAsync("LoseMaxHp", creature, Math.Abs(delta))) {
            if (creature.CurrentHp > creature.MaxHp)
                await SetCurrentHpAsync(creature, creature.MaxHp);
            return;
        }

        if (TryInvokeCreatureInternalSetter(creature, "SetMaxHpInternal", safeTarget)) {
            if (creature.CurrentHp > creature.MaxHp)
                await SetCurrentHpAsync(creature, creature.MaxHp);
            return;
        }

        throw new MissingMethodException("Unable to resolve a compatible max HP setter for current sts2.dll.");
    }

    public static async Task SetCurrentHpAsync(Creature creature, decimal targetCurrentHp) {
        decimal safeTarget = Math.Clamp(targetCurrentHp, 1m, Math.Max(1m, creature.MaxHp));

        if (await TryInvokeCreatureCommandAsync("SetCurrentHp", creature, safeTarget))
            return;
        if (TryInvokeCreatureInternalSetter(creature, "SetCurrentHpInternal", safeTarget))
            return;

        throw new MissingMethodException("Unable to resolve a compatible current HP setter for current sts2.dll.");
    }

    public static async Task RemoveFromCombatAsync(CardModel card, bool isBeingPlayed, bool skipVisuals) {
        var methods = typeof(CardPileCmd).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m => m.Name == "RemoveFromCombat")
            .ToArray();

        foreach (var method in methods) {
            if (TryBuildRemoveFromCombatArguments(method, card, isBeingPlayed, skipVisuals, out var args)
                && method.Invoke(null, args) is Task task) {
                await task;
                return;
            }
        }

        throw new MissingMethodException("Unable to resolve a compatible RemoveFromCombat overload for current sts2.dll.");
    }

    private static async Task<bool> TryInvokeCreatureCommandAsync(string methodName, Creature creature, decimal amount) {
        var methods = typeof(CreatureCmd).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m => m.Name == methodName)
            .ToArray();

        foreach (var method in methods) {
            if (TryBuildCreatureCommandArguments(method, creature, amount, out var args)
                && method.Invoke(null, args) is Task task) {
                await task;
                return true;
            }
        }
        return false;
    }

    private static bool TryBuildCreatureCommandArguments(MethodInfo method, Creature creature, decimal amount, out object?[]? args) {
        var parameters = method.GetParameters();
        args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++) {
            var p = parameters[i];
            var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            if (type == typeof(Creature)) { args[i] = creature; continue; }
            if (type == typeof(decimal)) { args[i] = amount; continue; }
            if (type == typeof(int)) { args[i] = (int)Math.Round(amount); continue; }
            if (type == typeof(float)) { args[i] = (float)amount; continue; }
            if (type == typeof(double)) { args[i] = (double)amount; continue; }
            if (type == typeof(bool)) { args[i] = false; continue; }
            if (!type.IsValueType) { args[i] = null; continue; }
            if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }

            args = null;
            return false;
        }
        return true;
    }

    private static bool TryBuildRemoveFromCombatArguments(MethodInfo method, CardModel card, bool isBeingPlayed, bool skipVisuals, out object?[]? args) {
        var parameters = method.GetParameters();
        args = new object[parameters.Length];
        int boolIdx = 0;
        for (int i = 0; i < parameters.Length; i++) {
            var p = parameters[i];
            var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            if (type == typeof(CardModel)) {
                args[i] = card;
                continue;
            }
            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string)) {
                args[i] = new[] { card };
                continue;
            }
            if (type == typeof(bool)) {
                args[i] = boolIdx == 0 ? isBeingPlayed : skipVisuals;
                boolIdx++;
                continue;
            }
            if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }

            args = null;
            return false;
        }
        return true;
    }

    private static bool TryInvokeCreatureInternalSetter(Creature creature, string methodName, decimal amount) {
        var method = creature.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null) return false;

        var parameters = method.GetParameters();
        if (parameters.Length != 1) return false;

        var converted = ConvertAmount(amount, parameters[0].ParameterType);
        method.Invoke(creature, new[] { converted });
        return true;
    }

    private static object ConvertAmount(decimal amount, Type parameterType) {
        var type = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        if (type == typeof(decimal)) return amount;
        if (type == typeof(int)) return (int)Math.Round(amount);
        if (type == typeof(float)) return (float)amount;
        if (type == typeof(double)) return (double)amount;
        return Convert.ChangeType(amount, type);
    }
}
