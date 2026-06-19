using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KitLib;

internal static class Sts2PowerCompat {
    public static async Task ApplyAsync(
        PowerModel mutable,
        Creature target,
        decimal amount,
        Creature source) {
        if (await TryInvokeApply(mutable, target, amount, source, preferChoiceContext: true))
            return;
        if (await TryInvokeApply(mutable, target, amount, source, preferChoiceContext: false))
            return;

        throw new MissingMethodException("Unable to resolve PowerCmd.Apply.");
    }

    static async Task<bool> TryInvokeApply(
        PowerModel mutable,
        Creature target,
        decimal amount,
        Creature source,
        bool preferChoiceContext) {
        var methods = typeof(PowerCmd).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(m => m.Name == nameof(PowerCmd.Apply))
            .ToArray();

        foreach (var method in methods) {
            if (!MatchesApplySignature(method, preferChoiceContext))
                continue;
            if (!TryBuildApplyArguments(method, mutable, target, amount, source, out var args))
                continue;
            if (method.Invoke(null, args) is Task task) {
                await task;
                return true;
            }
        }
        return false;
    }

    static bool MatchesApplySignature(MethodInfo method, bool preferChoiceContext) {
        var parameters = method.GetParameters();
        if (parameters.Length < 5)
            return false;
        if (preferChoiceContext) {
            var first = parameters[0].ParameterType;
            return first.IsClass && first != typeof(PowerModel) && first != typeof(Creature);
        }
        return parameters[0].ParameterType == typeof(PowerModel);
    }

    static bool TryBuildApplyArguments(
        MethodInfo method,
        PowerModel mutable,
        Creature target,
        decimal amount,
        Creature source,
        out object?[]? args) {
        var parameters = method.GetParameters();
        args = new object[parameters.Length];
        int creatureIndex = 0;
        for (int i = 0; i < parameters.Length; i++) {
            var p = parameters[i];
            var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            if (type == typeof(PowerModel)) { args[i] = mutable; continue; }
            if (type == typeof(Creature)) {
                args[i] = creatureIndex == 0 ? target : source;
                creatureIndex++;
                continue;
            }
            if (type == typeof(decimal)) { args[i] = amount; continue; }
            if (type == typeof(int)) { args[i] = (int)Math.Round(amount); continue; }
            if (type == typeof(bool)) { args[i] = false; continue; }
            if (!type.IsValueType) {
                args[i] = Activator.CreateInstance(type);
                continue;
            }
            if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }

            args = null;
            return false;
        }
        return true;
    }
}
