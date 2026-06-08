using KitLib.Settings;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Debug.Multiplayer;

namespace KitLib.Multiplayer.LanTest;
internal static class LanMultiplayerTestGuards {
    internal static bool IsTestSceneAlive(NMultiplayerTest test)
        => GodotObject.IsInstanceValid(test) && test.IsInsideTree();
}

/// <summary>
/// LAN debug multiplayer (<c>debug/multiplayer_test</c>): defer heavy DevMode init during embark
/// and guard lobby disconnect callbacks after the test scene is torn down.
/// </summary>
[HarmonyPatch(typeof(NMultiplayerTest), nameof(NMultiplayerTest.BeginRun))]
internal static class LanMultiplayerTestBeginRunPatch {
    static void Prefix() {
        DualInstanceTestBootstrap.EnsureMultiplayerDevActive("lan_begin_run");
        KitLibState.PseudoCoopDeferHeavyUi = true;
        KitLibState.PseudoCoopDeferMpCheatPublish = true;
        KitLibState.PseudoCoopAwaitingMapFinish = true;
        MainFile.Logger.Info("[LanTest] Deferring DevPanel/warmup until map opens.");
    }
}

[HarmonyPatch(typeof(NMultiplayerTest), nameof(NMultiplayerTest.LocalPlayerDisconnected))]
internal static class LanMultiplayerTestLocalDisconnectPatch {
    static readonly AccessTools.FieldRef<NMultiplayerTest, StartRunLobby?> LobbyRef =
        AccessTools.FieldRefAccess<NMultiplayerTest, StartRunLobby?>("_lobby");

    static bool Prefix(NMultiplayerTest __instance) {
        if (LanMultiplayerTestGuards.IsTestSceneAlive(__instance))
            return true;

        LobbyRef(__instance) = null;
        return false;
    }
}

[HarmonyPatch(typeof(NMultiplayerTest), nameof(NMultiplayerTest.RemotePlayerDisconnected))]
internal static class LanMultiplayerTestRemoteDisconnectPatch {
    static bool Prefix(NMultiplayerTest __instance)
        => LanMultiplayerTestGuards.IsTestSceneAlive(__instance);
}
