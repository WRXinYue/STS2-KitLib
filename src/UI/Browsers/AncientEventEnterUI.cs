using System;
using KitLib.Actions;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.UI;

internal static class AncientEventEnterUI
{
    internal static void PopulateChoices(
        EventModel eventModel,
        VBoxContainer host,
        Action<AncientEventEnterRequest> onChosen)
    {
        ClearHost(host);

        foreach (var choice in AncientEventActions.GetEnterChoices(eventModel))
        {
            var captured = choice;
            AddChoice(host,
                FormatChoiceListLabel(captured.Label, captured.Token),
                captured.Token,
                () => onChosen(captured.Request));
        }
    }

    private static string FormatChoiceListLabel(string label, string? secondary)
    {
        if (string.IsNullOrWhiteSpace(secondary)
            || string.Equals(label, secondary, StringComparison.OrdinalIgnoreCase))
            return label;

        return $"{label} — {secondary}";
    }

    private static void ClearHost(VBoxContainer host)
    {
        foreach (var child in host.GetChildren())
            ((Node)child).QueueFree();
    }

    private static void AddChoice(VBoxContainer host, string label, string? tooltip, Action onPressed)
    {
        var btn = DevPanelUI.CreateListItemButton(label);
        btn.Alignment = HorizontalAlignment.Left;
        if (!string.IsNullOrWhiteSpace(tooltip))
            btn.TooltipText = tooltip;
        btn.Pressed += () => onPressed();
        host.AddChild(btn);
    }
}
