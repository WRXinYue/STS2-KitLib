using System;
using Godot;
using KitLib;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private const string CombatRingMetricOverlayId = "combatRings.metric";

    /// <summary>
    /// Settings behind the rings gear. Solo lists every metric at once, so its switches only hide
    /// and show rows; team play picks the single metric behind all the share rings.
    /// </summary>
    internal static void ShowCombatRingMetricPicker(NGlobalUi globalUi) {
        ToggleOverlay(globalUi, CombatRingMetricOverlayId, content => {
            content.AddChild(CreatePanelTitle(I18N.T("combatStats.ring.metric", "Ring metric")));

            if (!CombatRingsHud.IsSharedMode()) {
                AddMetricSwitches(content);
                return;
            }

            AddMetricPicker(content, globalUi);
        });
    }

    /// <summary>Solo: one switch per metric, each controlling whether its row shows at all.</summary>
    private static void AddMetricSwitches(Control content) {
        foreach (var metric in Enum.GetValues<CombatRingMetric>()) {
            var toggle = new CheckBox {
                Text = CombatRingsHud.MetricLabel(metric),
                ButtonPressed = SettingsStore.IsCombatRingMetricEnabled(metric),
            };
            toggle.Toggled += on => {
                SettingsStore.SetCombatRingMetricEnabled(metric, on);
                CombatRingsHud.Refresh();
            };
            content.AddChild(toggle);
        }
    }

    /// <summary>Team play: the chosen metric drives every share ring, so picking one closes the panel.</summary>
    private static void AddMetricPicker(Control content, NGlobalUi globalUi) {
        var current = SettingsStore.GetCombatRingMetric();
        foreach (var metric in Enum.GetValues<CombatRingMetric>()) {
            var btn = CreateListItemButton(CombatRingsHud.MetricLabel(metric));
            if (metric == current) {
                btn.AddThemeColorOverride("font_color", KitLibTheme.Accent);
                btn.AddThemeColorOverride("font_hover_color", KitLibTheme.Accent);
                btn.AddThemeColorOverride("font_pressed_color", KitLibTheme.Accent);
            }

            var picked = metric;
            btn.Pressed += () => {
                SettingsStore.SetCombatRingMetric(picked);
                CloseOverlay(globalUi);
                CombatRingsHud.Refresh();
            };
            content.AddChild(btn);
        }
    }
}
