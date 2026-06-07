using System;
using DevMode.Actions;
using Godot;

namespace DevMode.UI;

internal static class AncientEventEnterUI
{
    internal static void PopulateDarvChoices(VBoxContainer host, Action<AncientEventEnterRequest> onChosen)
    {
        foreach (var child in host.GetChildren())
            ((Node)child).QueueFree();

        AddChoice(host, I18N.T("ancient.darv.random", "Random (vanilla 50%)"), () =>
            onChosen(new AncientEventEnterRequest()));
        AddChoice(host, I18N.T("ancient.darv.twoPlusTome", "2 boss relics + Dusty Tome"), () =>
            onChosen(new AncientEventEnterRequest(DarvIncludeDustyTome: true)));
        AddChoice(host, I18N.T("ancient.darv.threeBoss", "3 boss relics (no tome)"), () =>
            onChosen(new AncientEventEnterRequest(DarvIncludeDustyTome: false)));
    }

    private static void AddChoice(VBoxContainer host, string label, Action onPressed)
    {
        var btn = DevPanelUI.CreateListItemButton(label);
        btn.Alignment = HorizontalAlignment.Left;
        btn.Pressed += () => onPressed();
        host.AddChild(btn);
    }
}
