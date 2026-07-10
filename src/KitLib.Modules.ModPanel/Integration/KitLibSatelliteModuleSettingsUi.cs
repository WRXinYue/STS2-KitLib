using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.addons.mega_text;

namespace KitLib.Integration;

internal static class KitLibSatelliteModuleSettingsUi {
    sealed class ModuleRowBinding {
        internal required Control Row { get; init; }
        internal required string ModuleId { get; init; }
        internal CheckBox? Toggle { get; init; }
        internal Label? StatusLabel { get; init; }
    }

    internal static Control BuildPage() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 12);

        var toggleBindings = new List<ModuleRowBinding>();

        stack.AddChild(CreateRestartNotice());

        stack.AddChild(CreateSectionHeader(
            I18N.T("modpanel.kitlib.modules.section.core", "Core modules")));
        foreach (var module in SatelliteModuleLoadPolicy.Modules.Where(m => m.AlwaysOn)) {
            var binding = CreateModuleRow(module.Id, toggleBindings, alwaysOn: true);
            toggleBindings.Add(binding);
            stack.AddChild(binding.Row);
        }

        stack.AddChild(CreateSectionHeader(
            I18N.T("modpanel.kitlib.modules.section.optional", "Optional modules")));
        foreach (var module in SatelliteModuleLoadPolicy.Modules.Where(m => !m.AlwaysOn)) {
            var binding = CreateModuleRow(module.Id, toggleBindings, alwaysOn: false);
            toggleBindings.Add(binding);
            stack.AddChild(binding.Row);
        }

        RefreshModuleRows(toggleBindings);
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

    static Label CreateSectionHeader(string text) {
        var label = new Label {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return label;
    }

    static ModuleRowBinding CreateModuleRow(
        string moduleId,
        List<ModuleRowBinding> toggleBindings,
        bool alwaysOn) {
        var description = BuildDescription(moduleId, GetModuleDescription(moduleId));

        var status = CreateMetaLabel("");

        var left = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        left.AddThemeConstantOverride("separation", 4);
        left.AddChild(DevModeFormChrome.CreateTitleLabel(moduleId));
        if (!string.IsNullOrWhiteSpace(description))
            left.AddChild(DevModeFormChrome.CreateDescriptionLabel(description));
        left.AddChild(status);

        Control value;
        CheckBox? toggle = null;
        if (alwaysOn) {
            var label = new Label {
                Text = I18N.T("modpanel.kitlib.modules.alwaysOn", "Always on"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            value = label;
        }
        else {
            toggle = new CheckBox {
                ButtonPressed = IsModuleEnabledInSettings(moduleId),
                FocusMode = Control.FocusModeEnum.All,
            };
            DevModeFormChrome.ApplyToggle(toggle);
            toggle.Toggled += on => {
                SettingsStore.SetSatelliteModuleEnabled(moduleId, on);
                RefreshModuleRows(toggleBindings);
            };
            value = toggle;
        }

        var row = new HBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        row.AddThemeConstantOverride("separation", 16);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        value.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        value.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        if (value is CheckBox)
            value.CustomMinimumSize = new Vector2(28, 28);
        row.AddChild(left);
        row.AddChild(value);

        return new ModuleRowBinding {
            Row = row,
            ModuleId = moduleId,
            Toggle = toggle,
            StatusLabel = status,
        };
    }

    static Label CreateMetaLabel(string text) {
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = !string.IsNullOrWhiteSpace(text),
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return label;
    }

    static string BuildDescription(string moduleId, string baseDescription) {
        if (!SatelliteModuleLoadPolicy.TryGetModule(moduleId, out var module))
            return baseDescription;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseDescription))
            parts.Add(baseDescription);

        if (module.Requires.Length > 0) {
            parts.Add(I18N.T(
                "modpanel.kitlib.modules.requires",
                "Requires: {0}",
                string.Join(", ", module.Requires)));
        }

        return string.Join("\n", parts);
    }

    static string GetModuleDescription(string moduleId) {
        var (descKey, descFallback) = GetModuleDescriptionCopy(moduleId);
        return I18N.T(descKey, descFallback);
    }

    static (string descKey, string descFallback) GetModuleDescriptionCopy(string moduleId) {
        if (string.Equals(moduleId, KitLibModuleIds.Panel, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.panel.desc",
                "In-run dev rail, title-screen Dev Mode, progress protection, and most browsers.");
        if (string.Equals(moduleId, KitLibModuleIds.Ai, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.ai.desc",
                "Rule-based solo autoplay, AI HUD, and multiplayer companion helpers.");
        if (string.Equals(moduleId, KitLibModuleIds.Cheat, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.cheat.desc",
                "Cheat tab and runtime cheat hooks.");
        if (string.Equals(moduleId, KitLibModuleIds.Dev, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.dev.desc",
                "Hooks, Harmony tools, and MCP bridges.");
        if (string.Equals(moduleId, KitLibModuleIds.User, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.user.desc",
                "Session logging, crash recovery, and progress helpers.");
        if (string.Equals(moduleId, KitLibModuleIds.ModPanel, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.modPanel.desc",
                "This settings UI in the main-menu Mods screen.");
        return ("modpanel.kitlib.modules.unknown.desc", "");
    }

    static void RefreshModuleRows(IReadOnlyList<ModuleRowBinding> bindings) {
        var panelOn = IsModuleEnabledInSettings(KitLibModuleIds.Panel);
        foreach (var binding in bindings) {
            var enabledInSettings = IsModuleEnabledInSettings(binding.ModuleId);
            if (binding.Toggle != null) {
                binding.Toggle.SetPressedNoSignal(enabledInSettings);
                var requiresPanel = string.Equals(binding.ModuleId, KitLibModuleIds.Cheat, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(binding.ModuleId, KitLibModuleIds.Dev, StringComparison.OrdinalIgnoreCase);
                binding.Toggle.Disabled = requiresPanel && !panelOn;
            }

            if (binding.StatusLabel == null)
                continue;

            var status = FormatRuntimeStatus(binding.ModuleId, enabledInSettings);
            binding.StatusLabel.Text = status;
            binding.StatusLabel.Visible = !string.IsNullOrWhiteSpace(status);
            binding.StatusLabel.AddThemeColorOverride(
                "font_color",
                KitLibHost.IsSatelliteDllPresent(binding.ModuleId) ? KitLibTheme.Subtle : ModPanelUiPalette.CompatWarning);
        }
    }

    static string FormatRuntimeStatus(string moduleId, bool enabledInSettings) {
        if (!KitLibHost.IsSatelliteDllPresent(moduleId)) {
            return I18N.T("modpanel.kitlib.modules.runtime.missing", "Not installed");
        }

        if (KitLibHost.IsModuleLoaded(moduleId)) {
            return I18N.T("modpanel.kitlib.modules.runtime.loaded", "Running");
        }

        if (!enabledInSettings) {
            return I18N.T("modpanel.kitlib.modules.runtime.disabled", "Disabled");
        }

        return I18N.T("modpanel.kitlib.modules.runtime.pending", "Enabled");
    }

    static bool IsModuleEnabledInSettings(string moduleId) {
        if (!SatelliteModuleLoadPolicy.IsToggleable(moduleId))
            return true;
        return SettingsStore.Current.SatelliteModulesEnabled.TryGetValue(moduleId, out var enabled) && enabled;
    }
}
