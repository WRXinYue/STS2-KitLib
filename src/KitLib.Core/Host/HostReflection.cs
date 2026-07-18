using System.Reflection;
using System.Text.Json.Nodes;

namespace KitLib.Host;

internal static class HostReflection {
    internal static string? GetStringProperty(object target, string propertyName) =>
        target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target) as string;

    internal static int GetIntProperty(object target, string propertyName) {
        var value = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);
        return value switch {
            int i => i,
            Enum e => Convert.ToInt32(e),
            _ => 0,
        };
    }

    internal static void InvokeParameterless(object target, string methodName) {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, binder: null, types: Type.EmptyTypes, modifiers: null);
        method?.Invoke(target, null);
    }

    internal static void InvokeEnrich(object contributor, JsonObject snapshot, object player, object gamePhase) {
        var method = contributor.GetType().GetMethod("Enrich", BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
            return;
        method.Invoke(contributor, [snapshot, player, gamePhase]);
    }

    internal static int InvokeModifyScore(object modifier, JsonObject snapshot, object move, int baseScore) {
        var method = modifier.GetType().GetMethod("ModifyScore", BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
            return baseScore;

        var result = method.Invoke(modifier, [snapshot, move, baseScore]);
        return result is int score ? score : baseScore;
    }

    internal static bool InvokeAppliesTo(object contributor, string? key) {
        var method = contributor.GetType().GetMethod("AppliesTo", BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
            return true;

        var parameters = method.GetParameters();
        if (parameters.Length != 1)
            return true;

        var arg = parameters[0].ParameterType == typeof(string) ? key : key;
        var result = method.Invoke(contributor, [arg!]);
        return result is bool applies && applies;
    }

    internal static IEnumerable<object> InvokeGetExtraTags(object provider, string cardId) {
        var method = provider.GetType().GetMethod("GetExtraTags", BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
            return [];

        if (method.Invoke(provider, [cardId]) is not System.Collections.IEnumerable enumerable)
            return [];

        var list = new List<object>();
        foreach (var item in enumerable)
            list.Add(item);
        return list;
    }
}
