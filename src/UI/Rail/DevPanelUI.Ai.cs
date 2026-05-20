using DevMode;
using DevMode.AI.AutoPlay;
using DevMode.Panels;
using DevMode.Multiplayer.Cheat;
using DevMode.Multiplayer.SyncBot;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    internal static void ShowAiOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        var existing = ((Node)globalUi).GetNodeOrNull<Control>(AiRootName);
        if (existing != null) {
            ((Node)globalUi).RemoveChild(existing);
            existing.QueueFree();
        }

        var (root, _, vbox) = CreateOverlayRoot(globalUi, AiRootName, 520f);

        AddBrowserNavTab(vbox, I18N.T("panel.ai", "AI Host"));

        if (MpCheatSession.InMultiplayerRun)
            MpCheatUi.AddSessionBanner(vbox);

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 12);

        // ── AI Host ──
        inner.AddChild(CreateSectionHeader(I18N.T("autoplay.section", "AI Host")));
        inner.AddChild(CreateCheatToggle(
            I18N.T("autoplay.enabled", "AI Host"),
            I18N.T("autoplay.enabled.desc", "Rule-based bot drives your character (map, combat, rewards)."),
            () => SettingsStore.Current.AutoPlayEnabled,
            v => {
                SettingsStore.Current.AutoPlayEnabled = v;
                SettingsStore.Save();
                if (v && RunManager.Instance?.IsInProgress == true) {
                    if (MpCheatSession.InMultiplayerRun)
                        MainFile.Logger.Warn(I18N.T("autoplay.mpWarn", "AI Host in multiplayer only controls your character; remote choices may block progress."));
                    AiPlayModule.Instance.StartLoop();
                }
                else
                    AiPlayModule.Instance.StopLoop();
            }));
        if (MpCheatSession.InMultiplayerRun) {
            var mpWarn = new Label {
                Text = I18N.T("autoplay.mpWarn", "Multiplayer: only your character is hosted. Use SyncBot for remote ACKs."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            mpWarn.AddThemeFontSizeOverride("font_size", 12);
            mpWarn.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(mpWarn);
        }
        inner.AddChild(CreateCheatSlider(
            I18N.T("autoplay.delay", "Action Delay (ms)"),
            I18N.T("autoplay.delay.desc", "Pause between automated actions"),
            0, 3000, 50,
            () => SettingsStore.Current.AutoPlayDelayMs,
            v => {
                SettingsStore.Current.AutoPlayDelayMs = (int)v;
                SettingsStore.Save();
            }));

        // ── SyncBot ──
        if (MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost) {
            inner.AddChild(CreateSectionHeader(I18N.T("syncbot.section", "SyncBot (Dev)")));
            inner.AddChild(CreateCheatToggle(
                I18N.T("syncbot.enabled", "Simulate Remote Peers"),
                I18N.T("syncbot.enabled.desc", "Inject MpCheat ACKs and default remote choices on this machine (not real clients)."),
                () => SettingsStore.Current.SyncBotEnabled,
                v => {
                    SettingsStore.Current.SyncBotEnabled = v;
                    SettingsStore.Save();
                    MpCheatSyncBot.RefreshSimulatedPeers();
                }));
            inner.AddChild(CreateCheatToggle(
                I18N.T("syncbot.autoEndTurn", "Auto End Turn (remotes)"),
                I18N.T("syncbot.autoEndTurn.desc", "Ready-to-end-turn for non-local players in co-op combat"),
                () => SettingsStore.Current.SyncBotAutoEndTurn,
                v => {
                    SettingsStore.Current.SyncBotAutoEndTurn = v;
                    SettingsStore.Save();
                }));
            inner.AddChild(CreateCheatToggle(
                I18N.T("syncbot.phantom", "Spawn Phantom Player"),
                I18N.T("syncbot.phantom.desc", "Experimental: add NetId 1001 on next host launch (1-player run only)"),
                () => SettingsStore.Current.SyncBotSpawnPhantomPlayer,
                v => {
                    SettingsStore.Current.SyncBotSpawnPhantomPlayer = v;
                    SettingsStore.Save();
                }));
            var syncHint = new Label {
                Text = I18N.T("syncbot.hint", "Does not replace ENet dual-instance tests or StateDivergence checks."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            syncHint.AddThemeFontSizeOverride("font_size", 12);
            syncHint.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(syncHint);
        }
        else if (MpCheatSession.InMultiplayerRun) {
            inner.AddChild(CreateSectionHeader(I18N.T("syncbot.section", "SyncBot (Dev)")));
            var hostOnly = new Label {
                Text = I18N.T("syncbot.hostOnly", "SyncBot is host-only. Join as host or use a second client."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            hostOnly.AddThemeFontSizeOverride("font_size", 12);
            hostOnly.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(hostOnly);
        }
        else {
            var offlineHint = new Label {
                Text = I18N.T("syncbot.hint", "Does not replace ENet dual-instance tests or StateDivergence checks."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            offlineHint.AddThemeFontSizeOverride("font_size", 12);
            offlineHint.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(offlineHint);
        }

        scroll.AddChild(inner);
        vbox.AddChild(scroll);

        ((Node)globalUi).AddChild(root);
    }
}
