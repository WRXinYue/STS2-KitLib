using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Integration;

internal static class KitLibNativeModSettingsUi {
    static readonly List<(Func<bool> Get, CheckBox Box)> LiveBoolToggles = [];

    internal static void RefreshBoolToggles() {
        for (var i = LiveBoolToggles.Count - 1; i >= 0; i--) {
            var (get, box) = LiveBoolToggles[i];
            if (!GodotObject.IsInstanceValid(box)) {
                LiveBoolToggles.RemoveAt(i);
                continue;
            }

            box.SetPressedNoSignal(get());
        }
    }

    internal static Control CreateBoolToggle(string title, string? description, Func<bool> get, Action<bool> set) {
        var cb = new CheckBox {
            ButtonPressed = get(),
            FocusMode = Control.FocusModeEnum.All,
        };
        DevModeFormChrome.ApplyToggle(cb);
        cb.Toggled += on => set(on);
        LiveBoolToggles.Add((get, cb));
        cb.TreeExiting += () => LiveBoolToggles.RemoveAll(entry => entry.Box == cb);
        return DevModeFormChrome.CreateLabeledValueRow(title, description, cb);
    }

    internal static Control CreateNormalRunModeRow() {
        var ob = new OptionButton {
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        DevModeFormChrome.ApplyOptionButton(ob);
        ob.AddItem(I18N.T("modpanel.kitlib.normalRun.disabled", "Normal run: disabled"), (int)NormalRunMode.Disabled);
        ob.AddItem(I18N.T("modpanel.kitlib.normalRun.devPanel", "Normal run: Dev panel"), (int)NormalRunMode.DevPanel);
        ob.AddItem(I18N.T("modpanel.kitlib.normalRun.cheat", "Normal run: cheat tools"), (int)NormalRunMode.Cheat);
        ob.Selected = (int)KitLibState.NormalRunMode;
        ob.ItemSelected += idx => SettingsStore.SetNormalRunMode((NormalRunMode)(int)idx);
        return DevModeFormChrome.CreateLabeledValueRow(
            I18N.T("modpanel.kitlib.normalRun.title", "In-run DevMode level"),
            I18N.T("modpanel.kitlib.normalRun.desc",
                "Controls whether the DevPanel rail and cheat tools are available during normal (non test) runs."),
            ob);
    }

    internal static Control CreateRailOpenModeRow() {
        var ob = new OptionButton {
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        DevModeFormChrome.ApplyOptionButton(ob);
        ob.AddItem(I18N.T("modpanel.kitlib.railOpenMode.hoverButton", "Hover peek tab to expand"),
            (int)RailOpenMode.HoverButton);
        ob.AddItem(I18N.T("modpanel.kitlib.railOpenMode.hoverSide", "Hover left edge to expand"),
            (int)RailOpenMode.HoverSide);
        ob.Selected = (int)SettingsStore.GetRailOpenMode();
        ob.ItemSelected += idx => SettingsStore.SetRailOpenMode((RailOpenMode)(int)idx);
        return DevModeFormChrome.CreateLabeledValueRow(
            I18N.T("modpanel.kitlib.railOpenMode.title", "Rail expand trigger"),
            I18N.T("modpanel.kitlib.railOpenMode.desc",
                "HoverButton expands the rail when you hover the peek tab; HoverSide expands on the left edge and hides the peek tab."),
            ob);
    }

    internal static Control CreateAccentColorRow() {
        var cp = new ColorPickerButton {
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ColorSwatchSize,
                DevModeFormChrome.Metrics.ColorSwatchSize),
            EditAlpha = false,
            Color = ThemeManager.AccentColor,
            FocusMode = Control.FocusModeEnum.All,
        };
        cp.ColorChanged += ThemeManager.SetAccentColor;
        return DevModeFormChrome.CreateLabeledValueRow(
            I18N.T("modpanel.kitlib.accentColor.title", "Accent color"),
            I18N.T("modpanel.kitlib.accentColor.desc",
                "Highlight color for DevPanel and Mod settings (tabs, toggles, sidebar selection)."),
            cp);
    }
}
