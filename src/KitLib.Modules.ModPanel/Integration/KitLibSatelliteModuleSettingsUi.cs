using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.addons.mega_text;

namespace KitLib.Integration;

internal static class KitLibSatelliteModuleSettingsUi {
    sealed class ModuleToggleBinding {
        internal required Control Row { get; init; }
        internal required CheckBox CheckBox { get; init; }
        internal required string ModuleId { get; init; }
    }

    internal static Control BuildPage() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);

        var toggleBindings = new List<ModuleToggleBinding>();

        stack.AddChild(CreateRestartNotice());
        var presetRow = CreatePresetRow(toggleBindings);
        stack.AddChild(presetRow.Row);
        var presetButton = presetRow.PresetButton;

        stack.AddChild(CreateAlwaysOnRow(
            I18N.T("modpanel.kitlib.modules.user", "Logs and session tools"),
            I18N.T("modpanel.kitlib.modules.user.desc", "Session logging, crash recovery, and progress helpers. Always loaded.")));
        stack.AddChild(CreateAlwaysOnRow(
            I18N.T("modpanel.kitlib.modules.modPanel", "Mod settings panel"),
            I18N.T("modpanel.kitlib.modules.modPanel.desc", "This settings UI in the main-menu Mods screen. Always loaded.")));

        foreach (var module in SatelliteModuleLoadPolicy.Modules.Where(m => !m.AlwaysOn)) {
            var binding = CreateModuleToggle(module.Id, toggleBindings, presetButton);
            toggleBindings.Add(binding);
            stack.AddChild(binding.Row);
        }

        RefreshToggleStates(toggleBindings);
        SyncPresetSelection(presetButton);
        return stack;
    }

    static Control CreateRestartNotice() {
        var label = new MegaRichTextLabel {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            Text = I18N.T("modpanel.kitlib.modules.restartNotice",
                "Changes take effect after restarting the game."),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("normal_font_size", 13);
        label.AddThemeColorOverride("default_color", KitLibTheme.Subtle);
        ModPanelUI.ApplyMegaRichTextFontOverrides(label);
        return label;
    }

    sealed class PresetRowBinding {
        internal required Control Row { get; init; }
        internal required OptionButton PresetButton { get; init; }
    }

    static PresetRowBinding CreatePresetRow(List<ModuleToggleBinding> toggleBindings) {
        var presetButton = new OptionButton {
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        DevModeFormChrome.ApplyOptionButton(presetButton);

        presetButton.AddItem(I18N.T("modpanel.kitlib.modules.profile.minimal", "Minimal"), 0);
        presetButton.AddItem(I18N.T("modpanel.kitlib.modules.profile.standard", "Standard"), 1);
        presetButton.AddItem(I18N.T("modpanel.kitlib.modules.profile.full", "Full"), 2);
        presetButton.AddItem(I18N.T("modpanel.kitlib.modules.profile.custom", "Custom"), 3);

        presetButton.ItemSelected += idx => {
            var profile = idx switch {
                0 => SatelliteModuleLoadProfileNames.Minimal,
                1 => SatelliteModuleLoadProfileNames.Standard,
                2 => SatelliteModuleLoadProfileNames.Full,
                _ => SatelliteModuleLoadProfileNames.Custom,
            };
            if (profile == SatelliteModuleLoadProfileNames.Custom)
                return;
            SettingsStore.ApplySatelliteLoadProfile(profile);
            RefreshToggleStates(toggleBindings);
            SyncPresetSelection(presetButton);
        };

        return new PresetRowBinding {
            Row = DevModeFormChrome.CreateLabeledValueRow(
                I18N.T("modpanel.kitlib.modules.profile.title", "Load profile"),
                I18N.T("modpanel.kitlib.modules.profile.desc",
                    "Quick presets for which KitLib feature modules load on startup. Custom appears when you change individual modules."),
                presetButton),
            PresetButton = presetButton,
        };
    }

    static ModuleToggleBinding CreateModuleToggle(
        string moduleId,
        List<ModuleToggleBinding> toggleBindings,
        OptionButton presetButton) {
        var (titleKey, titleFallback, descKey, descFallback) = GetModuleCopy(moduleId);
        var cb = new CheckBox {
            ButtonPressed = IsModuleEnabledInSettings(moduleId),
            FocusMode = Control.FocusModeEnum.All,
        };
        DevModeFormChrome.ApplyToggle(cb);
        cb.Toggled += on => {
            SettingsStore.SetSatelliteModuleEnabled(moduleId, on);
            RefreshToggleStates(toggleBindings);
            SyncPresetSelection(presetButton);
        };

        var row = DevModeFormChrome.CreateLabeledValueRow(
            I18N.T(titleKey, titleFallback),
            I18N.T(descKey, descFallback),
            cb);
        var binding = new ModuleToggleBinding {
            Row = row,
            CheckBox = cb,
            ModuleId = moduleId,
        };
        return binding;
    }

    static Control CreateAlwaysOnRow(string title, string description) {
        var label = new Label {
            Text = I18N.T("modpanel.kitlib.modules.alwaysOn", "Always on"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return DevModeFormChrome.CreateLabeledValueRow(title, description, label);
    }

    static void RefreshToggleStates(IReadOnlyList<ModuleToggleBinding> bindings) {
        var panelOn = IsModuleEnabledInSettings(KitLibModuleIds.Panel);
        foreach (var binding in bindings) {
            var enabled = IsModuleEnabledInSettings(binding.ModuleId);
            binding.CheckBox.SetPressedNoSignal(enabled);
            var requiresPanel = string.Equals(binding.ModuleId, KitLibModuleIds.Cheat, StringComparison.OrdinalIgnoreCase)
                || string.Equals(binding.ModuleId, KitLibModuleIds.Dev, StringComparison.OrdinalIgnoreCase);
            binding.CheckBox.Disabled = requiresPanel && !panelOn;
        }
    }

    static void SyncPresetSelection(OptionButton presetButton) {
        var profile = SettingsStore.Current.SatelliteLoadProfile;
        var idx = profile switch {
            SatelliteModuleLoadProfileNames.Minimal => 0,
            SatelliteModuleLoadProfileNames.Standard => 1,
            SatelliteModuleLoadProfileNames.Full => 2,
            _ => 3,
        };
        if (presetButton.Selected != idx)
            presetButton.Select(idx);
    }

    static bool IsModuleEnabledInSettings(string moduleId) {
        if (!SatelliteModuleLoadPolicy.IsToggleable(moduleId))
            return true;
        return SettingsStore.Current.SatelliteModulesEnabled.TryGetValue(moduleId, out var enabled) && enabled;
    }

    static (string titleKey, string titleFallback, string descKey, string descFallback) GetModuleCopy(string moduleId) {
        if (string.Equals(moduleId, KitLibModuleIds.Panel, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.panel", "Dev toolkit",
                "modpanel.kitlib.modules.panel.desc",
                "In-run dev rail, title-screen Dev Mode, progress protection, and most browsers.");
        if (string.Equals(moduleId, KitLibModuleIds.Ai, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.ai", "AI host and autoplay",
                "modpanel.kitlib.modules.ai.desc",
                "Rule-based solo autoplay, AI HUD, and multiplayer companion helpers.");
        if (string.Equals(moduleId, KitLibModuleIds.Cheat, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.cheat", "Cheat tools",
                "modpanel.kitlib.modules.cheat.desc",
                "Cheat tab and runtime cheat hooks. Requires Dev toolkit.");
        if (string.Equals(moduleId, KitLibModuleIds.Dev, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.dev", "Scripts and developer tools",
                "modpanel.kitlib.modules.dev.desc",
                "Hooks, SpireScratch scripts, Harmony tools, and MCP bridges. Requires Dev toolkit.");
        return ("modpanel.kitlib.modules.unknown", moduleId, "modpanel.kitlib.modules.unknown.desc", "");
    }
}
