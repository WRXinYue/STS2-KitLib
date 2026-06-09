using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KitLib.Host;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Hosts ENet then uses the official character-select MP embark path (same as main-menu multiplayer).</summary>
internal static class PseudoCoopLobbyHost {
    const ushort HostPort = 33771;
    const int MaxPlayers = 4;

    public sealed class LaunchOptions {
        public CharacterModel Character { get; init; } = null!;
        public string? Seed { get; init; }
        public bool EnableMpCheatOptIn { get; init; } = true;
        public bool SyncBotEnabled { get; init; } = true;
        public bool SpawnPhantomPlayer { get; init; } = true;
        public bool SyncBotAutoEndTurn { get; init; } = true;
        public bool MpAiTeammateEnabled { get; init; } = true;
        public bool AutoPresetOnLaunch { get; init; }
    }

    public static Task<(bool ok, string error)> TryStartAsync(LaunchOptions options) {
        options ??= new LaunchOptions();
        if (options.Character == null)
            return Task.FromResult((false, "No character selected."));

        try {
            ApplySettings(options);

            if (RunManager.Instance.IsInProgress)
                RunManager.Instance.CleanUp(graceful: true);

            var game = NGame.Instance;
            if (game == null)
                return Task.FromResult((false, "NGame is not loaded."));

            var mainMenu = game.MainMenu;
            if (mainMenu == null)
                return Task.FromResult((false, "Main menu is not active. Return to the title screen and try again."));

            var netService = new NetHostGameService();
            var hostError = netService.StartENetHost(HostPort, MaxPlayers);
            if (hostError.HasValue)
                return Task.FromResult((false, $"ENet host failed: {hostError.Value}"));

            var charSelect = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
            // AfterInitialized (inside InitializeMultiplayerAsHost) wires RemoteCursor etc.
            // Do not call RemoteCursor.Initialize again — it Disposes the lobby InputSynchronizer.
            charSelect.InitializeMultiplayerAsHost(netService, MaxPlayers);

            var lobby = charSelect.Lobby;
            lobby.SetLocalCharacter(options.Character);
            if (!string.IsNullOrWhiteSpace(options.Seed))
                lobby.SetSeed(options.Seed.Trim());

            // Do not Push char select onto the main-menu stack: SetCurrentScene frees the menu
            // while StartNewMultiplayerRun is still running, which can tear down the embark flow.
            KitLibPseudoCoopOps.EnsureMultiplayerDevActive?.Invoke("pseudo_coop_host");
            KitLibState.PseudoCoopLaunchPending = true;
            KitLibState.PseudoCoopDeferHeavyUi = true;
            KitLibState.PseudoCoopDeferMpCheatPublish = true;
            KitLibState.PseudoCoopAwaitingMapFinish = true;
            lobby.SetReady(ready: true);

            MainFile.Logger.Info(
                "[PseudoCoop] Host lobby ready; embarked without pushing character select UI.");
            return Task.FromResult((true, string.Empty));
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[PseudoCoop] Host start failed: {ex}");
            return Task.FromResult((false, ex.Message));
        }
    }

    static void ApplySettings(LaunchOptions options) {
        if (options.EnableMpCheatOptIn && !SettingsStore.Current.MultiplayerCheatOptIn)
            SettingsStore.SetMultiplayerCheatOptIn(true);

        var s = SettingsStore.Current;
        s.AutoPlayEnabled = false;
        s.SyncBotEnabled = options.SyncBotEnabled;
        s.SyncBotSpawnPhantomPlayer = options.SpawnPhantomPlayer;
        s.SyncBotAutoEndTurn = options.SyncBotAutoEndTurn;
        s.MpAiTeammateEnabled = options.MpAiTeammateEnabled;
        s.PseudoCoopAutoPresetOnLaunch = options.AutoPresetOnLaunch;
        SettingsStore.Save();
        PseudoCoopBootstrap.TryAutoPresetOnLaunch();
        SimulatedPeerRegistry.Refresh();
        MpCheatSyncBot.RefreshSimulatedPeers();
    }
}
