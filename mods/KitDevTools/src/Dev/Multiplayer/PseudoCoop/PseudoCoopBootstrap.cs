using KitLib.Host;
using KitLib.Multiplayer.Play;
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
        HostDrivenPeers.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
        KitLibHost.StopAiPlayLoop?.Invoke();
        KitLog.Info("PseudoCoop", $"Preset applied (hand-play host + AI teammate + SyncBot).");
    }

    public static void ApplyLanHostPreset() {
        AiSessionSettings.AutoPlayEnabled = false;
        AiSessionSettings.SyncBotEnabled = false;
        AiSessionSettings.SyncBotSpawnPhantomPlayer = false;
        SettingsStore.Current.SyncBotAutoEndTurn = true;
        AiSessionSettings.MpAiTeammateEnabled = true;
        AiSessionSettings.MpAiTeammateDriveLiveEnet = true;
        HostDrivenPeers.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
        KitLibHost.StopAiPlayLoop?.Invoke();
        KitLog.Info("PseudoCoop", $"LAN host preset applied (AI drives live ENet teammates — enable AFK on client).");
    }

    public static void TryAutoPresetOnLaunch() {
        if (!AiSessionSettings.PseudoCoopAutoPresetOnLaunch) return;
        ApplyPreset();
    }

    public static void ApplyLanClientPreset() {
        AiSessionSettings.MpAiTeammateAfkClient = true;
        KitLibHost.StopAiPlayLoop?.Invoke();
        KitLog.Info("PseudoCoop", $"LAN client AFK preset applied.");
    }
}
