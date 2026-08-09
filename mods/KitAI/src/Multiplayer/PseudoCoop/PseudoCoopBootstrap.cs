using KitLib.AI.AutoPlay;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>One-click preset: host hand-plays, phantom + SyncBot + AI teammate.</summary>
internal static class PseudoCoopBootstrap {
    public static void ApplyPreset() {
        AiSessionSettings.AutoPlayEnabled = false;
        AiSessionSettings.SyncBotEnabled = true;
        AiSessionSettings.SyncBotSpawnPhantomPlayer = true;
        SettingsStore.Current.SyncBotAutoEndTurn = true;
        AiSessionSettings.MpAiTeammateEnabled = true;
        AiSessionSettings.MpAiTeammateDriveLiveEnet = false;
        SimulatedPeerRegistry.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
        AiPlayModule.Instance.StopLoop();
        KitLog.Info("PseudoCoop", $"Preset applied (hand-play host + AI teammate + SyncBot).");
    }

    /// <summary>LAN dual-instance: host drives live ENet teammates via action queue; no phantom/SyncBot ACK.</summary>
    public static void ApplyLanHostPreset() {
        AiSessionSettings.AutoPlayEnabled = false;
        AiSessionSettings.SyncBotEnabled = false;
        AiSessionSettings.SyncBotSpawnPhantomPlayer = false;
        SettingsStore.Current.SyncBotAutoEndTurn = true;
        AiSessionSettings.MpAiTeammateEnabled = true;
        AiSessionSettings.MpAiTeammateDriveLiveEnet = true;
        SimulatedPeerRegistry.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
        AiPlayModule.Instance.StopLoop();
        KitLog.Info("PseudoCoop", $"LAN host preset applied (AI drives live ENet teammates — enable AFK on client).");
    }

    public static void TryAutoPresetOnLaunch() {
        if (!AiSessionSettings.PseudoCoopAutoPresetOnLaunch) return;
        ApplyPreset();
    }

    /// <summary>LAN dual-instance client: AFK combat; host mirrors map votes when LAN preset is on.</summary>
    public static void ApplyLanClientPreset() {
        MpAiTeammateAfkClient.SetSessionEnabled(true);
        AiPlayModule.Instance.StopLoop();
        KitLog.Info("PseudoCoop", $"LAN client AFK preset applied.");
    }
}
