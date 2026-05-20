using System.Linq;
using DevMode.Actions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

public static class MpCheatCommandExecutor {
    public static void Execute(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats) return;

        switch (message.Kind) {
            case MpCheatCommandKind.KillAllEnemies:
                TaskHelper.RunSafely(CombatEnemyActions.KillAllEnemies());
                break;
            case MpCheatCommandKind.AddCardPrepare:
                MpCheatCardAddCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.AddCardExecute:
                MpCheatCardAddCoordinator.OnExecuteReceived(message);
                break;
            case MpCheatCommandKind.RemoveCardPrepare:
                MpCheatCardRemoveCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.RemoveCardExecute:
                MpCheatCardRemoveCoordinator.OnExecuteReceived(message);
                break;
        }
    }

    public static bool TryPublishHostCommand(MpCheatCommandKind kind) {
        if (!MpCheatSession.CanEditMultiplayerCheats) return false;

        var netId = RunManager.Instance?.NetService?.NetId ?? 0;
        var msg = new MpCheatCommandMessage { Kind = kind, IssuedByNetId = netId };
        MpCheatSync.BroadcastCommand(msg);
        Execute(msg);
        return true;
    }
}
