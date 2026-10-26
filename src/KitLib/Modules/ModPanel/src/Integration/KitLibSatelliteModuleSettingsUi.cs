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

/// <summary>KitLib mod-settings page listing the bundled satellite modules with per-module toggles.</summary>
internal static class KitLibSatelliteModuleSettingsUi {
    sealed class ModuleRowBinding {
        internal required SatelliteModuleLoadPolicy.ModuleInfo Module { get; init; }
        internal CheckBox? Toggle { get; init; }
        internal required Label StatusLabel { get; init; }
    }

    internal static Control BuildPage() {
        var stack = KitLibModSettingsUiBuilders.CreatePageStack();
        var bindings = new List<ModuleRowBinding>();

        if (KitLibWelcomeCard.TryCreate() is { } welcome)
            stack.AddChild(welcome);

        stack.AddChild(CreateNoticeLabel(
            "modpanel.kitlib.modules.restartNotice",
            "Changes take effect after restarting the game."));
        if (!ModPanelHostContext.IsModLoadEditingAllowed) {
            stack.AddChild(CreateNoticeLabel(
                "modpanel.inRun.readOnlyHint",
                "Mod enable/disable and module toggles are read-only during a run. "
                + "Return to the main menu to change them."));
        }

        foreach (var module in SatelliteModuleLoadPolicy.Modules)
            AddModuleRow(stack, module, bindings);

        RefreshModuleRows(bindings);
        return stack;
    }

    static void AddModuleRow(
        Control parent,
        SatelliteModuleLoadPolicy.ModuleInfo module,
        List<ModuleRowBinding> bindings) {
        var left = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        left.AddThemeConstantOverride("separation", 4);
        left.AddChild(DevModeFormChrome.CreateTitleLabel(module.Id));

        var description = BuildDescription(module);
        if (!string.IsNullOrWhiteSpace(description))
            left.AddChild(DevModeFormChrome.CreateDescriptionLabel(description));

        var status = CreateStatusLabel();
        left.AddChild(status);

        Control value;
        CheckBox? toggle = null;
        if (module.AlwaysOn) {
            value = CreateAlwaysOnLabel();
        }
        else {
            toggle = new CheckBox {
                ButtonPressed = IsModuleEnabledInSettings(module.Id),
                FocusMode = Control.FocusModeEnum.All,
            };
            DevModeFormChrome.ApplyToggle(toggle);
            toggle.Toggled += on => {
                SettingsStore.SetSatelliteModuleEnabled(module.Id, on);
                RefreshModuleRows(bindings);
            };
            value = toggle;
        }

        var row = new HBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        row.AddThemeConstantOverride("separation", 16);
        value.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        value.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        if (value is CheckBox)
            value.CustomMinimumSize = new Vector2(28, 28);
        row.AddChild(left);
        row.AddChild(value);

        bindings.Add(new ModuleRowBinding {
            Module = module,
            Toggle = toggle,
            StatusLabel = status,
        });
        parent.AddChild(row);
    }

    static Control CreateNoticeLabel(string key, string fallback) {
        var label = new MegaRichTextLabel {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            Text = I18N.T(key, fallback),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("normal_font_size", 13);
        label.AddThemeColorOverride("default_color", KitLibTheme.Subtle);
        ModPanelUI.ApplyMegaRichTextFontOverrides(label);
        return label;
    }

    static Label CreateAlwaysOnLabel() {
        var label = new Label {
            Text = I18N.T("modpanel.kitlib.modules.alwaysOn", "Always on"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return label;
    }

    static Label CreateStatusLabel() {
        var label = new Label {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return label;
    }

    static string BuildDescription(SatelliteModuleLoadPolicy.ModuleInfo module) {
        var parts = new List<string>();

        var (descKey, descFallback) = GetDescriptionCopy(module.Id);
        var description = I18N.T(descKey, descFallback);
        if (!string.IsNullOrWhiteSpace(description))
            parts.Add(description);

        if (module.Requires.Length > 0) {
            parts.Add(I18N.T(
                "modpanel.kitlib.modules.requires",
                "Requires: {0}",
                string.Join(", ", module.Requires)));
        }

        if (string.Equals(module.Id, KitLibModuleIds.ModPanel, StringComparison.OrdinalIgnoreCase)) {
            parts.Add(I18N.T(
                "modpanel.kitlib.modules.modPanel.disableWarning",
                "Disabling this removes KitLib's in-game settings screen. "
                + "Re-enable it by editing mod_data/KitLib/settings.json."));
        }

        return string.Join("\n", parts);
    }

    static (string DescKey, string DescFallback) GetDescriptionCopy(string moduleId) {
        if (string.Equals(moduleId, KitLibModuleIds.Panel, StringComparison.OrdinalIgnoreCase)) {
            return ("modpanel.kitlib.modules.panel.desc",
                "In-run dev rail, title-screen Dev Mode, progress protection, and most browsers.");
        }

        if (string.Equals(moduleId, KitLibModuleIds.Dev, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.dev.desc", "Hooks, Harmony tools, and MCP bridges.");

        if (string.Equals(moduleId, KitLibModuleIds.ModPanel, StringComparison.OrdinalIgnoreCase))
            return ("modpanel.kitlib.modules.modPanel.desc", "This settings UI in the main-menu Mods screen.");

        return ("", "");
    }

    static void RefreshModuleRows(IReadOnlyList<ModuleRowBinding> bindings) {
        var editingAllowed = ModPanelHostContext.IsModLoadEditingAllowed;
        foreach (var binding in bindings) {
            var enabledInSettings = IsModuleEnabledInSettings(binding.Module.Id);
            if (binding.Toggle != null) {
                binding.Toggle.SetPressedNoSignal(enabledInSettings);
                binding.Toggle.Disabled = !editingAllowed || HasDisabledPrerequisite(binding.Module);
            }

            binding.StatusLabel.Text = FormatRuntimeStatus(binding.Module.Id, enabledInSettings);
            binding.StatusLabel.AddThemeColorOverride(
                "font_color",
                KitLibHost.IsSatelliteDllPresent(binding.Module.Id)
                    ? KitLibTheme.Subtle
                    : ModPanelUiPalette.CompatWarning);
        }
    }

    static bool HasDisabledPrerequisite(SatelliteModuleLoadPolicy.ModuleInfo module) =>
        module.Requires.Any(required => !IsModuleEnabledInSettings(required));

    static string FormatRuntimeStatus(string moduleId, bool enabledInSettings) {
        if (!KitLibHost.IsSatelliteDllPresent(moduleId))
            return I18N.T("modpanel.kitlib.modules.runtime.missing", "Not installed");

        if (KitLibHost.IsModuleLoaded(moduleId))
            return I18N.T("modpanel.kitlib.modules.runtime.loaded", "Running");

        if (!enabledInSettings)
            return I18N.T("modpanel.kitlib.modules.runtime.disabled", "Disabled");

        return I18N.T("modpanel.kitlib.modules.runtime.pending", "Enabled");
    }

    static bool IsModuleEnabledInSettings(string moduleId) {
        if (!SatelliteModuleLoadPolicy.IsToggleable(moduleId))
            return true;
        return SettingsStore.Current.SatelliteModulesEnabled.TryGetValue(moduleId, out var enabled) && enabled;
    }
}
