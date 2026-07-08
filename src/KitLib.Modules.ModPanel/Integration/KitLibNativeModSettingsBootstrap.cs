using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.addons.mega_text;

namespace KitLib.Integration;

internal static class KitLibNativeModSettingsBootstrap {
    internal static void RegisterKitLibPages() {
        var modId = KitLibModuleIds.Core;
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "general",
            TitleKey = "modpanel.kitlib.page.general",
            Title = "General",
            SortOrder = 0,
            BuildBody = BuildGeneralPage,
        });
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "modules",
            TitleKey = "modpanel.kitlib.page.modules",
            Title = "Modules",
            SortOrder = 3,
            BuildBody = () => KitLibSatelliteModuleSettingsUi.BuildPage(),
        });
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "progressGuard",
            TitleKey = "modpanel.kitlib.page.progressGuard",
            Title = "Progress protection",
            SortOrder = 5,
            BuildBody = BuildProgressGuardPage,
        });
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "performance",
            TitleKey = "modpanel.kitlib.page.performance",
            Title = "Performance",
            SortOrder = 10,
            BuildBody = BuildPerformancePage,
        });
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = modId,
            PageId = "hotkeys",
            TitleKey = "modpanel.kitlib.page.hotkeys",
            Title = "Hotkeys",
            SortOrder = 20,
            BuildBody = BuildHotkeysPage,
        });
    }

    static Control BuildGeneralPage() {
        var stack = CreatePageStack();
        stack.AddChild(KitLibNativeModSettingsUi.CreateAccentColorRow());
        stack.AddChild(KitLibNativeModSettingsUi.CreateNormalRunModeRow());
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("modpanel.kitlib.diagnosticMode", "Mod panel diagnostic mode"),
            I18N.T("modpanel.kitlib.diagnosticMode.desc",
                "Log [ModPanelPerf] timings and verbose sidebar diagnostics to the main log."),
            () => SettingsStore.Current.ModPanelDiagnosticMode,
            SettingsStore.SetModPanelDiagnosticMode));
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("settings.launchKitlogOnStartup", "Open live log terminal on startup"),
            I18N.T("settings.launchKitlogOnStartup.desc",
                "When KitLib loads, open a system terminal that streams this session's game log in real time. Requires the optional KitLog.Cli tool (kitlog) from the tools zip."),
            () => SettingsStore.Current.LaunchKitlogOnStartup,
            SettingsStore.SetLaunchKitlogOnStartup));
        stack.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("modpanel.kitlib.multiplayerCheatOptIn", "Multiplayer cheat opt-in"),
            I18N.T("modpanel.kitlib.multiplayerCheatOptIn.desc",
                "Allow KitLib cheat tools during multiplayer runs. Required for pseudo co-op and dual-instance LAN testing."),
            () => SettingsStore.Current.MultiplayerCheatOptIn,
            SettingsStore.SetMultiplayerCheatOptIn));
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

    static Control BuildProgressGuardPage() {
        var built = KitLibPanelUiOps.BuildProgressGuardModSettingsPage?.Invoke();
        if (built is Control control)
            return control;
        return CreateUnavailablePage(
            I18N.T("modpanel.kitlib.progressGuard.unavailable",
                "Progress protection requires the KitLib.Panel module."));
    }

    static MegaRichTextLabel CreateUnavailablePage(string message) {
        var label = new MegaRichTextLabel {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            Text = message,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("normal_font_size", 14);
        ModPanelUI.ApplyMegaRichTextFontOverrides(label);
        return label;
    }

    internal static string ResolvePageTitle(KitLibModSettingsPageRegistration page) =>
        string.IsNullOrWhiteSpace(page.TitleKey)
            ? page.Title
            : I18N.T(page.TitleKey, page.Title);

    static VBoxContainer CreatePageStack() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);
        return stack;
    }
}
