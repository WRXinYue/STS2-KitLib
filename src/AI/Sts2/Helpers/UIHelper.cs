using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DevMode.AI.Sts2.Helpers;

internal static class UIHelper
{
    public static async Task Click(NClickableControl button, int delayMs = 100)
    {
        button.ForceClick();
        await Task.Delay(delayMs);
    }

    /// <summary>
    /// Polls <paramref name="condition"/> until true or <paramref name="timeout"/> elapses.
    /// </summary>
    public static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout, int pollMs = 200)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(pollMs);
        }
        return condition();
    }

    public static List<T> FindAll<T>(Node start) where T : Node
    {
        var list = new List<T>();
        if (GodotObject.IsInstanceValid(start))
            FindAllRecursive(start, list);
        return list;
    }

    public static T? FindFirst<T>(Node start) where T : Node
    {
        if (!GodotObject.IsInstanceValid(start))
            return null;
        if (start is T match)
            return match;
        foreach (var child in start.GetChildren())
        {
            var found = FindFirst<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static void FindAllRecursive<T>(Node node, List<T> found) where T : Node
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        if (node is T item) found.Add(item);
        foreach (var child in node.GetChildren())
            FindAllRecursive(child, found);
    }
}
