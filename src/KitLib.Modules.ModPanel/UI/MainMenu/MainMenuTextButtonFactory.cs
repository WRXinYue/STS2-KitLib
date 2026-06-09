using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class MainMenuTextButtonFactory {
    private static readonly FieldInfo? LocStringField =
        AccessTools.Field(typeof(NMainMenuTextButton), "_locString");

    private const int DuplicateFlags = 14; // Duplicate(14): no copied signals (template Released must not carry over).

    public static NMainMenuTextButton CreateFrom(
        NMainMenuTextButton template,
        Node parent,
        string name,
        string text,
        Action<NButton> onReleased
    ) {
        var btn = (NMainMenuTextButton)template.Duplicate(DuplicateFlags);
        btn.Name = name;
        btn.Visible = true;

        parent.AddChild(btn); // _Ready assigns label; set text only after AddChild.

        LocStringField?.SetValue(btn, null); // avoid locale refresh overwriting custom title
        if (btn.label != null)
            btn.label.Text = text;

        btn.Connect(NClickableControl.SignalName.Released, Callable.From(onReleased));
        return btn;
    }
}
