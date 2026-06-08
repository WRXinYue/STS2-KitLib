using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

public static class MpCheatCommandExecutor {
    public static void Execute(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats) return;

        switch (message.Kind) {
            case MpCheatCommandKind.KillAllEnemies:
                TaskHelper.RunSafely(CombatEnemyActions.ExecuteKillAllFromMpSync(new MpCheatItemPayload()));
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
            case MpCheatCommandKind.EditCardPrepare:
                MpCheatCardEditCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.EditCardExecute:
                MpCheatCardEditCoordinator.OnExecuteReceived(message);
                break;
            case MpCheatCommandKind.AddRelicPrepare:
            case MpCheatCommandKind.RemoveRelicPrepare:
                MpCheatRelicCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.AddRelicExecute:
            case MpCheatCommandKind.RemoveRelicExecute:
                MpCheatRelicCoordinator.OnExecuteReceived(message);
                break;
            case MpCheatCommandKind.AddPotionPrepare:
            case MpCheatCommandKind.RemovePotionPrepare:
                MpCheatPotionCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.AddPotionExecute:
            case MpCheatCommandKind.RemovePotionExecute:
                MpCheatPotionCoordinator.OnExecuteReceived(message);
                break;
            case MpCheatCommandKind.AddMonsterPrepare:
            case MpCheatCommandKind.AddEncounterPrepare:
                MpCheatCombatEnemyCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.AddMonsterExecute:
            case MpCheatCommandKind.AddEncounterExecute:
                MpCheatCombatEnemyCoordinator.OnExecuteReceived(message);
                break;
            case MpCheatCommandKind.KillEnemyPrepare:
                MpCheatCombatEnemyCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.KillEnemyExecute:
                MpCheatCombatEnemyCoordinator.OnExecuteReceived(message);
                break;
            case MpCheatCommandKind.AddPowerPrepare:
            case MpCheatCommandKind.RemovePowerPrepare:
            case MpCheatCommandKind.ClearPowersPrepare:
                MpCheatPowerCoordinator.OnPrepareReceived(message);
                break;
            case MpCheatCommandKind.AddPowerExecute:
            case MpCheatCommandKind.RemovePowerExecute:
            case MpCheatCommandKind.ClearPowersExecute:
                MpCheatPowerCoordinator.OnExecuteReceived(message);
                break;
        }
    }

    public static bool TryPublishHostCommand(MpCheatCommandKind kind) {
        if (!MpCheatSession.CanUseMultiplayerCheats) return false;
        if (!MpCheatSession.IsHost) return false;

        var netId = RunManager.Instance?.NetService?.NetId ?? 0;
        var msg = new MpCheatCommandMessage { Kind = kind, IssuedByNetId = netId };
        MpCheatSync.BroadcastCommand(msg);
        Execute(msg);
        return true;
    }
}
