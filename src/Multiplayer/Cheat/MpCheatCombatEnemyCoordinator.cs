using System.Threading.Tasks;
using DevMode.Actions;
using MegaCrit.Sts2.Core.Models;

namespace DevMode.Multiplayer.Cheat;

internal static class MpCheatCombatEnemyCoordinator {
    public static Task<string> TryHostAddMonsterAsync(MonsterModel monster) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.AddMonsterPrepare,
            MpCheatCommandKind.AddMonsterExecute,
            BuildPayload(MpCheatItemKind.AddMonster, monster),
            CombatEnemyActions.TryValidateAddMonster,
            CombatEnemyActions.ExecuteAddMonsterFromMpSync,
            "CombatAddMonster",
            p => string.Format(I18N.T("mpcheat.combatAdd.monsterSuccess", "Added monster {0}."), p.ItemId));

    public static Task<string> TryClientRequestAddMonsterAsync(MonsterModel monster) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildPayload(MpCheatItemKind.AddMonster, monster),
            CombatEnemyActions.TryValidateAddMonster,
            requireSelfTarget: false,
            "CombatAddMonster");

    public static Task<string> TryHostAddEncounterAsync(EncounterModel encounter) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.AddEncounterPrepare,
            MpCheatCommandKind.AddEncounterExecute,
            BuildPayload(MpCheatItemKind.AddEncounter, encounter),
            CombatEnemyActions.TryValidateAddEncounter,
            CombatEnemyActions.ExecuteAddEncounterFromMpSync,
            "CombatAddEncounter",
            p => string.Format(I18N.T("mpcheat.combatAdd.encounterSuccess", "Added encounter {0}."), p.ItemId));

    public static Task<string> TryClientRequestAddEncounterAsync(EncounterModel encounter) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildPayload(MpCheatItemKind.AddEncounter, encounter),
            CombatEnemyActions.TryValidateAddEncounter,
            requireSelfTarget: false,
            "CombatAddEncounter");

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (message.Item?.Kind is not (MpCheatItemKind.AddMonster or MpCheatItemKind.AddEncounter)) return;
        var validate = message.Item.Kind == MpCheatItemKind.AddMonster
            ? CombatEnemyActions.TryValidateAddMonster
            : (MpCheatItemValidateDelegate)CombatEnemyActions.TryValidateAddEncounter;
        MpCheatItemSyncCore.OnPrepareReceived(
            message,
            validate,
            message.Item.Kind == MpCheatItemKind.AddMonster ? "CombatAddMonster" : "CombatAddEncounter");
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (message.Item == null) return;
        MpCheatItemExecuteDelegate? execute = message.Item.Kind switch {
            MpCheatItemKind.AddMonster => CombatEnemyActions.ExecuteAddMonsterFromMpSync,
            MpCheatItemKind.AddEncounter => CombatEnemyActions.ExecuteAddEncounterFromMpSync,
            _ => null,
        };
        if (execute == null) return;
        MpCheatItemSyncCore.OnExecuteReceived(
            message,
            execute,
            message.Item.Kind == MpCheatItemKind.AddMonster ? "CombatAddMonster" : "CombatAddEncounter");
    }

    internal static Task<(bool Success, string Message)> TryHostFromPayloadCoreAsync(MpCheatItemPayload payload) =>
        payload.Kind switch {
            MpCheatItemKind.AddMonster => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.AddMonsterPrepare,
                MpCheatCommandKind.AddMonsterExecute,
                payload,
                CombatEnemyActions.TryValidateAddMonster,
                CombatEnemyActions.ExecuteAddMonsterFromMpSync,
                "CombatAddMonster",
                p => string.Format(I18N.T("mpcheat.combatAdd.monsterSuccess", "Added monster {0}."), p.ItemId)),
            MpCheatItemKind.AddEncounter => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.AddEncounterPrepare,
                MpCheatCommandKind.AddEncounterExecute,
                payload,
                CombatEnemyActions.TryValidateAddEncounter,
                CombatEnemyActions.ExecuteAddEncounterFromMpSync,
                "CombatAddEncounter",
                p => string.Format(I18N.T("mpcheat.combatAdd.encounterSuccess", "Added encounter {0}."), p.ItemId)),
            _ => Task.FromResult((false, MpCheatItemSyncCore.FormatError("unknown combat kind"))),
        };

    private static MpCheatItemPayload BuildPayload(MpCheatItemKind kind, MonsterModel monster) =>
        new() {
            Kind = kind,
            ItemId = ((AbstractModel)monster).Id.Entry ?? "",
        };

    private static MpCheatItemPayload BuildPayload(MpCheatItemKind kind, EncounterModel encounter) =>
        new() {
            Kind = kind,
            ItemId = ((AbstractModel)encounter).Id.Entry ?? "",
        };
}
