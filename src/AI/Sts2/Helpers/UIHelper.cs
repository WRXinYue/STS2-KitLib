using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace KitLib.AI.Sts2.Helpers;

internal static class UIHelper
{
    public static Task Click(NClickableControl button) {
        button.ForceClick();
        return Task.CompletedTask;
    }

    public static Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout) =>
        Sts2WaitHelper.Until(condition, timeout);

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
