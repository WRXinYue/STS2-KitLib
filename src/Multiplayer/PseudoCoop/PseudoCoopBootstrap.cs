using KitLib.AI.AutoPlay;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>One-click preset: host hand-plays, phantom + SyncBot + AI teammate.</summary>
internal static class PseudoCoopBootstrap {
    public static void ApplyPreset() {
        var s = SettingsStore.Current;
        s.AutoPlayEnabled = false;
        s.SyncBotEnabled = true;
        s.SyncBotSpawnPhantomPlayer = true;
        s.SyncBotAutoEndTurn = true;
        s.MpAiTeammateEnabled = true;
        s.MpAiTeammateDriveLiveEnet = false;
        SettingsStore.Save();
        SimulatedPeerRegistry.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
        AiPlayModule.Instance.StopLoop();
        MainFile.Logger.Info("[PseudoCoop] Preset applied (hand-play host + AI teammate + SyncBot).");
    }

    /// <summary>LAN dual-instance: host drives live ENet teammates via action queue; no phantom/SyncBot ACK.</summary>
    public static void ApplyLanHostPreset() {
        var s = SettingsStore.Current;
        s.AutoPlayEnabled = false;
        s.SyncBotEnabled = false;
        s.SyncBotSpawnPhantomPlayer = false;
        s.SyncBotAutoEndTurn = true;
        s.MpAiTeammateEnabled = true;
        s.MpAiTeammateDriveLiveEnet = true;
        SettingsStore.Save();
        SimulatedPeerRegistry.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
        AiPlayModule.Instance.StopLoop();
        MainFile.Logger.Info(
            "[PseudoCoop] LAN host preset applied (AI drives live ENet teammates — enable AFK on client).");
    }

    public static void TryAutoPresetOnLaunch() {
        if (!SettingsStore.Current.PseudoCoopAutoPresetOnLaunch) return;
        ApplyPreset();
    }

    /// <summary>LAN dual-instance client: AFK combat; host mirrors map votes when LAN preset is on.</summary>
    public static void ApplyLanClientPreset() {
        MpAiTeammateAfkClient.SetSessionEnabled(true);
        AiPlayModule.Instance.StopLoop();
        MainFile.Logger.Info("[PseudoCoop] LAN client AFK preset applied.");
    }
}
