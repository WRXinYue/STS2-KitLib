using System.Collections.Generic;
using System.Linq;
using System.Text;
using KitLib.AI.AutoPlay;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.Companion;
using KitLib.Host;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI;

/// <summary>Live AI status and mode hints for the AI Host rail panel.</summary>
internal static class AiHostPanelModel {
    public static string BuildStatusText() {
        var lines = new List<string>();
        var runActive = RunManager.Instance?.IsInProgress == true;
        lines.Add(I18N.T("ai.status.run", "Run: {0}", runActive ? I18N.T("ai.status.runActive", "active") : I18N.T("ai.status.runIdle", "none")));

        if (!KitLibState.IsActive)
            lines.Add(I18N.T("ai.status.devInactive", "KitLib rail: inactive (enable Normal run on title screen)"));
        else if (KitLibState.PseudoCoopDeferHeavyUi)
            lines.Add(I18N.T("ai.status.deferredUi", "Sidebar: deferred until map opens (LAN / pseudo-coop)"));

        if (runActive) {
            var phase = AiPlayServices.StateProvider.CurrentPhase;
            lines.Add(I18N.T("ai.status.phase", "Phase: {0}", phase));
        }

        if (AiHostContext.ActiveNetId is ulong activeId)
            lines.Add(I18N.T("ai.status.deciding", "Deciding for netId {0}", activeId));

        if (MultiplayerRunProbe.InMultiplayerRun) {
            lines.Add(I18N.T(
                "ai.status.mpRole",
                "Multiplayer: {0}",
                MultiplayerRunProbe.IsHost ? I18N.T("ai.status.host", "host") : I18N.T("ai.status.client", "client")));
        }

        lines.Add(I18N.T(
            "ai.status.autoplay",
            "Solo AI Host: {0}",
            AiPlayModule.Instance.IsRunning
                ? I18N.T("ai.status.on", "running")
                : I18N.T("ai.status.off", "off")));

        if (MultiplayerRunProbe.InMultiplayerRun && MultiplayerRunProbe.IsHost) {
            lines.Add(I18N.T(
                "ai.status.teammate",
                "Host AI Teammate: {0}",
                SettingsStore.Current.MpAiTeammateEnabled
                    ? I18N.T("ai.status.on", "on")
                    : I18N.T("ai.status.off", "off")));
            var targets = SimulatedPeerRegistry.GetMpAiTeammateTargets().Count();
            if (targets > 0)
                lines.Add(I18N.T("ai.status.aiPeers", "AI-driven peers: {0}", targets));
        }

        if (MultiplayerRunProbe.InMultiplayerRun && !MultiplayerRunProbe.IsHost) {
            lines.Add(I18N.T(
                "ai.status.afk",
                "Client AFK: {0}",
                MpAiTeammateAfkClient.IsSessionEnabled
                    ? I18N.T("ai.status.on", "on")
                    : I18N.T("ai.status.off", "off")));
        }

        if (MultiplayerRunProbe.InMultiplayerRun && LanLocalDecisionHost.IsEnabled) {
            lines.Add(I18N.T(
                "ai.status.lanLocal",
                "LAN local AI (Neow/rewards): {0}",
                I18N.T("ai.status.on", "on")));
        }

        var companions = CompanionBridge.ListCompanions();
        if (companions.Count > 0) {
            var summary = string.Join(", ", companions.Select(c =>
                $"{c.NetId}:{c.CharacterId.Entry}{(c.IsAiDriven ? "*" : "")}"));
            lines.Add(I18N.T("ai.status.companions", "Companions: {0}", summary));
        }

        if (runActive && AiPlayServices.StateProvider.TryGetRunAndPlayer(out _, out var local)) {
            var charId = local.Character?.Id.Entry;
            if (CharacterAiRegistry.TryGet(charId, out _))
                lines.Add(I18N.T("ai.status.charStrategy", "Character strategy: {0}", charId));
        }

        return string.Join("\n", lines);
    }

    public static IReadOnlyList<string> BuildRecommendations() {
        var tips = new List<string>();

        if (!KitLibState.IsActive)
            tips.Add(I18N.T("ai.rec.normalRun", "Title screen → DEVMODE → set Normal run to Dev Mode or Cheat Mode."));

        if (KitLibState.DualInstanceMinimalRail)
            tips.Add(I18N.T("ai.rec.minimalRail", "Dual-instance rail shows AI Host + Logs; use a full Dev run for all panels."));

        if (MultiplayerRunProbe.InMultiplayerRun && LanLocalDecisionHost.IsEnabled)
            tips.Add(I18N.T(
                "ai.rec.lanLocal",
                "LAN: Neow and reward screens are automated locally; host still hand-plays combat."));

        if (MultiplayerRunProbe.InMultiplayerRun && MultiplayerRunProbe.IsHost && !LanLocalDecisionHost.IsEnabled)
            tips.Add(I18N.T(
                "ai.rec.lanLocalOff",
                "Apply LAN host preset (AI Host panel) to auto-pick Neow and rewards on both windows."));

        if (KitLibState.PseudoCoopDeferHeavyUi)
            tips.Add(I18N.T("ai.rec.deferUi", "Finish Neow and open the map once — the sidebar attaches after deferred init."));

        if (MultiplayerRunProbe.InMultiplayerRun && MultiplayerRunProbe.IsHost) {
            if (SettingsStore.Current.AutoPlayEnabled)
                tips.Add(I18N.T("ai.rec.noAutoplayMp", "Turn off Solo AI Host in multiplayer; hand-play locally and use Host AI Teammate for remotes."));
            if (!SettingsStore.Current.MpAiTeammateEnabled)
                tips.Add(I18N.T("ai.rec.enableTeammate", "Enable Host AI Teammate for phantom or connected peers."));
            if (SimulatedPeerRegistry.HasLiveEnetTeammate() && !SettingsStore.Current.MpAiTeammateDriveLiveEnet)
                tips.Add(I18N.T("ai.rec.driveEnet", "Real client connected: enable AI Drives Live ENet Teammates + LAN preset."));
            if (!SimulatedPeerRegistry.HasLiveEnetTeammate() && !SettingsStore.Current.SyncBotEnabled
                && CompanionBridge.ListCompanions().Count == 0)
                tips.Add(I18N.T("ai.rec.syncbot", "Solo host testing: enable SyncBot or spawn a phantom player."));
        }

        if (MultiplayerRunProbe.InMultiplayerRun && !MultiplayerRunProbe.IsHost && !MpAiTeammateAfkClient.IsSessionEnabled)
            tips.Add(I18N.T("ai.rec.clientAfk", "Client: enable AFK so the host can enqueue your combat actions."));

        if (!MultiplayerRunProbe.InMultiplayerRun && RunManager.Instance?.IsInProgress == true
            && !SettingsStore.Current.AutoPlayEnabled)
            tips.Add(I18N.T("ai.rec.soloAutoplay", "Solo run: enable AI Host below to automate map, combat, and rewards."));

        foreach (var companion in CompanionBridge.ListCompanions()) {
            if (!CompanionNonCombatRegistry.IsEnabled(companion.NetId)
                && CharacterAiRegistry.SupportsNonCombat(companion.CharacterId.Entry))
                tips.Add(I18N.T(
                    "ai.rec.nonCombat",
                    "Companion {0}: spawn with EnableNonCombatAi for events, shop, and rewards.",
                    companion.NetId));
        }

        if (tips.Count == 0)
            tips.Add(I18N.T("ai.rec.ok", "Current AI mode looks appropriate for this session."));

        return tips;
    }

    public static string BuildTerminalText() {
        var lines = AiDecisionLog.Snapshot();
        if (lines.Count == 0)
            return I18N.T("ai.terminal.empty", "No AI decisions yet this run.");
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.AppendLine(line);
        return sb.ToString().TrimEnd();
    }
}
