using DevMode;
using DevMode.AI.AutoPlay;
using DevMode.Multiplayer.PseudoCoop;
using DevMode.Panels;
using DevMode.Icons;
using DevMode.Multiplayer.Cheat;
using DevMode.Multiplayer.PseudoCoop;
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
        var clientMp = MpCheatSession.InMultiplayerRun && !MpCheatSession.IsHost;
        var mpRun = MpCheatSession.InMultiplayerRun;
        if (clientMp || (mpRun && MpCheatSession.IsHost)) {
            AiPlayModule.Instance.StopLoop();
            if (SettingsStore.Current.AutoPlayEnabled) {
                SettingsStore.Current.AutoPlayEnabled = false;
                SettingsStore.Save();
            }
            var blocked = new Label {
                Text = clientMp
                    ? I18N.T("autoplay.mpClientBlocked", "联机客户端不可用 AI 托管；请由主机开启「托管队友」代打战斗。")
                    : I18N.T("autoplay.mpHostBlocked", "联机时不可用 AI 托管（会本地改状态导致 desync）。请手打本机角色；仅幻影/离线队友可用「托管模拟队友」。"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            blocked.AddThemeFontSizeOverride("font_size", 12);
            blocked.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(blocked);
        }
        else {
            inner.AddChild(CreateCheatToggle(
                I18N.T("autoplay.enabled", "AI Host"),
                I18N.T("autoplay.enabled.desc", "Rule-based bot drives your character (map, combat, rewards)."),
                () => SettingsStore.Current.AutoPlayEnabled,
                v => {
                    SettingsStore.Current.AutoPlayEnabled = v;
                    SettingsStore.Save();
                    if (v && RunManager.Instance?.IsInProgress == true)
                        AiPlayModule.Instance.StartLoop();
                    else
                        AiPlayModule.Instance.StopLoop();
                }));
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

        // ── SyncBot / Pseudo Co-op ──
        if (MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost) {
            inner.AddChild(CreateSectionHeader(I18N.T("pseudocoop.section", "Pseudo Co-op (Dev)")));
            var liveEnet = SimulatedPeerRegistry.HasLiveEnetTeammate();
            if (liveEnet) {
                var lanPresetBtn = CreatePlainButton(
                    I18N.T("pseudocoop.applyLanPreset", "Apply LAN Host-Drive Preset"),
                    MdiIcon.Robot);
                lanPresetBtn.Pressed += () => {
                    PseudoCoopBootstrap.ApplyLanHostPreset();
                    AiPlayModule.Instance.StopLoop();
                };
                inner.AddChild(lanPresetBtn);
                var lanHint = new Label {
                    Text = I18N.T(
                        "pseudocoop.lanDriveHint",
                        "LAN: host hand-plays; AI enqueues combat for connected clients. Client must enable AFK mode and stay connected."),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                lanHint.AddThemeFontSizeOverride("font_size", 12);
                lanHint.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
                inner.AddChild(lanHint);
            }
            else {
                var presetBtn = CreatePlainButton(
                    I18N.T("pseudocoop.applyPreset", "Apply Hand-Play + AI Teammate Preset"),
                    MdiIcon.Robot);
                presetBtn.Pressed += () => {
                    PseudoCoopBootstrap.ApplyPreset();
                    AiPlayModule.Instance.StopLoop();
                };
                inner.AddChild(presetBtn);
            }
            inner.AddChild(CreateCheatToggle(
                I18N.T("pseudocoop.teammate", "Host AI Teammate"),
                I18N.T("pseudocoop.teammate.desc", "SimpleStrategy plays combat for host-driven peers via action queue."),
                () => SettingsStore.Current.MpAiTeammateEnabled,
                v => {
                    SettingsStore.Current.MpAiTeammateEnabled = v;
                    SettingsStore.Save();
                    SimulatedPeerRegistry.Refresh();
                    MpCheatSyncBot.RefreshSimulatedPeers();
                }));
            if (liveEnet) {
                inner.AddChild(CreateCheatToggle(
                    I18N.T("pseudocoop.driveLiveEnet", "AI Drives Live ENet Teammates"),
                    I18N.T("pseudocoop.driveLiveEnet.desc", "Host enqueues combat for connected clients (requires client AFK mode)."),
                    () => SettingsStore.Current.MpAiTeammateDriveLiveEnet,
                    v => {
                        SettingsStore.Current.MpAiTeammateDriveLiveEnet = v;
                        if (v) {
                            SettingsStore.Current.SyncBotEnabled = false;
                            SettingsStore.Current.SyncBotSpawnPhantomPlayer = false;
                        }
                        SettingsStore.Save();
                        SimulatedPeerRegistry.Refresh();
                        MpCheatSyncBot.RefreshSimulatedPeers();
                    }));
            }
            if (!liveEnet) {
                inner.AddChild(CreateCheatToggle(
                    I18N.T("pseudocoop.autoPreset", "Auto Preset on Host Launch"),
                    I18N.T("pseudocoop.autoPreset.desc", "Apply preset when a host run starts."),
                    () => SettingsStore.Current.PseudoCoopAutoPresetOnLaunch,
                    v => {
                        SettingsStore.Current.PseudoCoopAutoPresetOnLaunch = v;
                        SettingsStore.Save();
                    }));
            }
            var pseudoHint = new Label {
                Text = liveEnet
                    ? I18N.T(
                        "pseudocoop.lanHint",
                        "Pseudo co-op phantom preset is for solo host only. Use LAN preset above when a real client is connected.")
                    : I18N.T(
                        "pseudocoop.hint",
                        "Main menu Developer Mode → Pseudo Co-op (Host): options, character, one-click start. Map/rewards: host only."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            pseudoHint.AddThemeFontSizeOverride("font_size", 12);
            pseudoHint.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(pseudoHint);

            if (!liveEnet) {
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
        }
        else if (MpCheatSession.InMultiplayerRun) {
            inner.AddChild(CreateSectionHeader(I18N.T("pseudocoop.lanClientSection", "LAN AFK (Client)")));
            var afkStatus = new Label {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            afkStatus.AddThemeFontSizeOverride("font_size", 13);
            void RefreshAfkStatus() {
                var on = MpAiTeammateAfkClient.IsSessionEnabled;
                afkStatus.Text = on
                    ? I18N.T("pseudocoop.afkClientOn", "● AFK on — host drives your combat")
                    : I18N.T("pseudocoop.afkClientOff", "○ AFK off — you play combat locally");
                afkStatus.AddThemeColorOverride(
                    "font_color",
                    on ? DevModeTheme.Accent : DevModeTheme.TextSecondary);
            }
            RefreshAfkStatus();
            inner.AddChild(afkStatus);
            var lanClientPresetBtn = CreatePlainButton(
                I18N.T("pseudocoop.applyLanClientPreset", "Apply LAN Client AFK Preset"),
                MdiIcon.Robot);
            lanClientPresetBtn.Pressed += () => {
                PseudoCoopBootstrap.ApplyLanClientPreset();
                RefreshAfkStatus();
            };
            inner.AddChild(lanClientPresetBtn);
            inner.AddChild(CreateCheatToggle(
                I18N.T("pseudocoop.afkClient", "AFK — Host Drives Combat"),
                I18N.T("pseudocoop.afkClient.desc", "Block local combat input; host AI enqueues your actions. Do not click cards or end turn."),
                () => MpAiTeammateAfkClient.IsSessionEnabled,
                v => {
                    MpAiTeammateAfkClient.SetSessionEnabled(v);
                    RefreshAfkStatus();
                }));
            var afkHint = new Label {
                Text = I18N.T(
                    "pseudocoop.afkClientHint",
                    "Host must use the LAN preset. Dual-instance: AFK is per-window. Host map votes mirror to you automatically."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            afkHint.AddThemeFontSizeOverride("font_size", 12);
            afkHint.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            inner.AddChild(afkHint);
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
