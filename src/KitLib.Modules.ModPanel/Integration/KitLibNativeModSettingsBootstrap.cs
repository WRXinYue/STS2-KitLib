using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Integration;

internal static class KitLibNativeModSettingsBootstrap {
    internal static void RegisterKitLibPages() {
        var modId = KitLibModuleIds.Core;
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "general",
            Title = I18N.T("modpanel.kitlib.page.general", "General"),
            SortOrder = 0,
            BuildBody = BuildGeneralPage,
        });
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "performance",
            Title = I18N.T("modpanel.kitlib.page.performance", "Performance"),
            SortOrder = 10,
            BuildBody = BuildPerformancePage,
        });
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "hotkeys",
            Title = I18N.T("modpanel.kitlib.page.hotkeys", "Hotkeys"),
            SortOrder = 20,
            BuildBody = BuildHotkeysPage,
        });
    }

    static Control BuildGeneralPage() {
        var stack = CreatePageStack();
        stack.AddChild(KitLibNativeModSettingsUi.CreateNormalRunModeRow());
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("settings.gameContextPane", "In-game right sidebar"),
            I18N.T("settings.gameContextPane.desc",
                "Show the right combat sidebar during fights (stats, enemy intent, combat tools)."),
            () => SettingsStore.Current.GameContextPaneEnabled,
            enabled => {
                SettingsStore.SetGameContextPaneEnabled(enabled);
                KitLibHost.NotifyGameContextPaneChanged?.Invoke();
            }));
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("modpanel.kitlib.diagnosticMode", "Mod panel diagnostic mode"),
            I18N.T("modpanel.kitlib.diagnosticMode.desc",
                "Log [ModPanelPerf] timings and verbose sidebar diagnostics to the main log."),
            () => SettingsStore.Current.ModPanelDiagnosticMode,
            SettingsStore.SetModPanelDiagnosticMode));
        return stack;
    }

    static Control BuildPerformancePage() {
        var stack = CreatePageStack();
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("perfHud.enabled", "Performance overlay"),
            I18N.T("perfHud.enabled.desc",
                "Fixed top-right debug text for transitions, warmup, and frame spikes. Rebind the toggle hotkey on the Hotkeys page."),
            () => SettingsStore.Current.PerfHudEnabled,
            enabled => {
                SettingsStore.SetPerfHudEnabled(enabled);
                KitLibHost.SyncPerfHudOverlay?.Invoke();
            }));
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("perfHud.traceToFile", "Write perf trace to file"),
            I18N.T("perfHud.traceToFile.desc",
                "Append CSV lines to instances/{pid}/perf-trace.log when transitions or frame spikes are logged."),
            () => SettingsStore.Current.PerfHudTraceToFile,
            SettingsStore.SetPerfHudTraceToFile));
        return stack;
    }

    static Control BuildHotkeysPage() {
        var stack = CreatePageStack();
        stack.AddChild(KitLibHotkeySettingsUi.BuildSection());
        return stack;
    }

    static VBoxContainer CreatePageStack() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);
        return stack;
    }
}
