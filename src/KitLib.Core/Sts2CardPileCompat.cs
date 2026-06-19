using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KitLib;

internal static class Sts2CardPileCompat {
    public static async Task AddGeneratedCardToCombatAsync(
        CardModel combatCard,
        PileType pileType,
        Player player) {
        if (await TryInvokeAddGeneratedCardToCombat(combatCard, pileType, player, preferPlayerArg: true))
            return;
        if (await TryInvokeAddGeneratedCardToCombat(combatCard, pileType, player, preferPlayerArg: false))
            return;

        throw new MissingMethodException("Unable to resolve AddGeneratedCardToCombat on CardPileCmd.");
    }

    static async Task<bool> TryInvokeAddGeneratedCardToCombat(
        CardModel combatCard,
        PileType pileType,
        Player player,
        bool preferPlayerArg) {
        var methods = typeof(CardPileCmd).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m => m.Name == nameof(CardPileCmd.AddGeneratedCardToCombat))
            .ToArray();

        foreach (var method in methods) {
            if (!MatchesAddSignature(method, preferPlayerArg))
                continue;
            if (!TryBuildAddArguments(method, combatCard, pileType, player, out var args))
                continue;
            if (method.Invoke(null, args) is Task task) {
                await task;
                return true;
            }
        }
        return false;
    }

    static bool MatchesAddSignature(MethodInfo method, bool preferPlayerArg) {
        var parameters = method.GetParameters();
        if (parameters.Length < 2)
            return false;
        if (parameters[0].ParameterType != typeof(CardModel)
            || parameters[1].ParameterType != typeof(PileType))
            return false;
        if (parameters.Length == 2)
            return !preferPlayerArg;
        var third = Nullable.GetUnderlyingType(parameters[2].ParameterType) ?? parameters[2].ParameterType;
        if (preferPlayerArg)
            return third == typeof(Player);
        return third == typeof(bool);
    }

    static bool TryBuildAddArguments(
        MethodInfo method,
        CardModel combatCard,
        PileType pileType,
        Player player,
        out object?[]? args) {
        var parameters = method.GetParameters();
        args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++) {
            var p = parameters[i];
            var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            if (type == typeof(CardModel)) { args[i] = combatCard; continue; }
            if (type == typeof(PileType)) { args[i] = pileType; continue; }
            if (type == typeof(Player)) { args[i] = player; continue; }
            if (type == typeof(bool)) { args[i] = true; continue; }
            if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }

            args = null;
            return false;
        }
        return true;
    }
}
