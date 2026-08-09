using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Multiplayer.Cheat;

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

    public static Task<string> TryHostKillEnemyAsync(Creature enemy) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.KillEnemyPrepare,
            MpCheatCommandKind.KillEnemyExecute,
            BuildKillPayload(enemy),
            CombatEnemyActions.TryValidateKillEnemy,
            CombatEnemyActions.ExecuteKillEnemyFromMpSync,
            "CombatKillEnemy",
            p => string.Format(I18N.T("mpcheat.combatKill.oneSuccess", "Killed {0}."), p.ItemId));

    public static Task<string> TryClientRequestKillEnemyAsync(Creature enemy) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildKillPayload(enemy),
            CombatEnemyActions.TryValidateKillEnemy,
            requireSelfTarget: false,
            "CombatKillEnemy");

    public static Task<string> TryHostKillAllAsync() {
        if (MpCheatSession.IsHost) {
            if (!MpCheatCommandExecutor.TryPublishHostCommand(MpCheatCommandKind.KillAllEnemies))
                return Task.FromResult(I18N.T("mpcheat.combatKill.allFailed", "Kill all failed."));
            return Task.FromResult(I18N.T("mpcheat.combatKill.allSuccess", "Killed all enemies."));
        }
        return MpCheatItemSyncCore.TryClientRequestAsync(
            new MpCheatItemPayload { Kind = MpCheatItemKind.KillAllEnemies },
            CombatEnemyActions.TryValidateKillAll,
            requireSelfTarget: false,
            "CombatKillAll");
    }

    public static Task<string> TryClientRequestKillAllAsync() =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            new MpCheatItemPayload { Kind = MpCheatItemKind.KillAllEnemies },
            CombatEnemyActions.TryValidateKillAll,
            requireSelfTarget: false,
            "CombatKillAll");

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (message.Item == null) return;
        MpCheatItemValidateDelegate? validate = message.Item.Kind switch {
            MpCheatItemKind.AddMonster => CombatEnemyActions.TryValidateAddMonster,
            MpCheatItemKind.AddEncounter => CombatEnemyActions.TryValidateAddEncounter,
            MpCheatItemKind.KillEnemy => CombatEnemyActions.TryValidateKillEnemy,
            _ => null,
        };
        if (validate == null) return;
        var logTag = message.Item.Kind switch {
            MpCheatItemKind.AddMonster => "CombatAddMonster",
            MpCheatItemKind.AddEncounter => "CombatAddEncounter",
            MpCheatItemKind.KillEnemy => "CombatKillEnemy",
            _ => "Combat",
        };
        MpCheatItemSyncCore.OnPrepareReceived(message, validate, logTag);
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (message.Item == null) return;
        MpCheatItemExecuteDelegate? execute = message.Item.Kind switch {
            MpCheatItemKind.AddMonster => CombatEnemyActions.ExecuteAddMonsterFromMpSync,
            MpCheatItemKind.AddEncounter => CombatEnemyActions.ExecuteAddEncounterFromMpSync,
            MpCheatItemKind.KillEnemy => CombatEnemyActions.ExecuteKillEnemyFromMpSync,
            _ => null,
        };
        if (execute == null) return;
        var logTag = message.Item.Kind switch {
            MpCheatItemKind.AddMonster => "CombatAddMonster",
            MpCheatItemKind.AddEncounter => "CombatAddEncounter",
            MpCheatItemKind.KillEnemy => "CombatKillEnemy",
            _ => "Combat",
        };
        MpCheatItemSyncCore.OnExecuteReceived(message, execute, logTag);
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
            MpCheatItemKind.KillEnemy => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.KillEnemyPrepare,
                MpCheatCommandKind.KillEnemyExecute,
                payload,
                CombatEnemyActions.TryValidateKillEnemy,
                CombatEnemyActions.ExecuteKillEnemyFromMpSync,
                "CombatKillEnemy",
                p => string.Format(I18N.T("mpcheat.combatKill.oneSuccess", "Killed {0}."), p.ItemId)),
            MpCheatItemKind.KillAllEnemies => TryHostKillAllCoreAsync(),
            _ => Task.FromResult((false, MpCheatItemSyncCore.FormatError("unknown combat kind"))),
        };

    private static async Task<(bool Success, string Message)> TryHostKillAllCoreAsync() {
        if (!MpCheatCommandExecutor.TryPublishHostCommand(MpCheatCommandKind.KillAllEnemies))
            return (false, I18N.T("mpcheat.combatKill.allFailed", "Kill all failed."));
        return (true, I18N.T("mpcheat.combatKill.allSuccess", "Killed all enemies."));
    }

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

    private static MpCheatItemPayload BuildKillPayload(Creature enemy) =>
        new() {
            Kind = MpCheatItemKind.KillEnemy,
            ItemId = EnemyKey.Build(enemy),
        };
}
