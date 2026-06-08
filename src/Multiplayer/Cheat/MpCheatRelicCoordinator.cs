using System.Threading.Tasks;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Multiplayer.Cheat;

internal static class MpCheatRelicCoordinator {
    public static Task<string> TryHostAddRelicAsync(Player target, RelicModel relic) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.AddRelicPrepare,
            MpCheatCommandKind.AddRelicExecute,
            BuildPayload(MpCheatItemKind.AddRelic, target, relic),
            RelicActions.TryValidateAddRelic,
            RelicActions.ExecuteAddRelicFromMpSync,
            "RelicAdd",
            p => string.Format(I18N.T("mpcheat.relicAdd.success", "Added relic {0}."), p.ItemId));

    public static Task<string> TryClientRequestAddRelicAsync(Player target, RelicModel relic) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildPayload(MpCheatItemKind.AddRelic, target, relic),
            RelicActions.TryValidateAddRelic,
            requireSelfTarget: true,
            "RelicAdd");

    public static Task<string> TryHostRemoveRelicAsync(Player target, string relicId) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.RemoveRelicPrepare,
            MpCheatCommandKind.RemoveRelicExecute,
            BuildRemovePayload(target, relicId),
            RelicActions.TryValidateRemoveRelic,
            RelicActions.ExecuteRemoveRelicFromMpSync,
            "RelicRemove",
            p => string.Format(I18N.T("mpcheat.relicRemove.success", "Removed relic {0}."), p.ItemId));

    public static Task<string> TryClientRequestRemoveRelicAsync(Player target, string relicId) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildRemovePayload(target, relicId),
            RelicActions.TryValidateRemoveRelic,
            requireSelfTarget: true,
            "RelicRemove");

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (message.Item?.Kind is not (MpCheatItemKind.AddRelic or MpCheatItemKind.RemoveRelic)) return;
        var validate = message.Item.Kind == MpCheatItemKind.AddRelic
            ? RelicActions.TryValidateAddRelic
            : (MpCheatItemValidateDelegate)RelicActions.TryValidateRemoveRelic;
        MpCheatItemSyncCore.OnPrepareReceived(
            message,
            validate,
            message.Item.Kind == MpCheatItemKind.AddRelic ? "RelicAdd" : "RelicRemove");
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (message.Item == null) return;
        MpCheatItemExecuteDelegate? execute = message.Item.Kind switch {
            MpCheatItemKind.AddRelic => RelicActions.ExecuteAddRelicFromMpSync,
            MpCheatItemKind.RemoveRelic => RelicActions.ExecuteRemoveRelicFromMpSync,
            _ => null,
        };
        if (execute == null) return;
        MpCheatItemSyncCore.OnExecuteReceived(
            message,
            execute,
            message.Item.Kind == MpCheatItemKind.AddRelic ? "RelicAdd" : "RelicRemove");
    }

    internal static Task<(bool Success, string Message)> TryHostFromPayloadCoreAsync(MpCheatItemPayload payload) =>
        payload.Kind switch {
            MpCheatItemKind.AddRelic => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.AddRelicPrepare,
                MpCheatCommandKind.AddRelicExecute,
                payload,
                RelicActions.TryValidateAddRelic,
                RelicActions.ExecuteAddRelicFromMpSync,
                "RelicAdd",
                p => string.Format(I18N.T("mpcheat.relicAdd.success", "Added relic {0}."), p.ItemId)),
            MpCheatItemKind.RemoveRelic => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.RemoveRelicPrepare,
                MpCheatCommandKind.RemoveRelicExecute,
                payload,
                RelicActions.TryValidateRemoveRelic,
                RelicActions.ExecuteRemoveRelicFromMpSync,
                "RelicRemove",
                p => string.Format(I18N.T("mpcheat.relicRemove.success", "Removed relic {0}."), p.ItemId)),
            _ => Task.FromResult((false, MpCheatItemSyncCore.FormatError("unknown relic kind"))),
        };

    private static MpCheatItemPayload BuildPayload(MpCheatItemKind kind, Player target, RelicModel relic) =>
        new() {
            Kind = kind,
            TargetPlayerNetId = target.NetId,
            ItemId = ((AbstractModel)relic).Id.Entry ?? "",
        };

    private static MpCheatItemPayload BuildRemovePayload(Player target, string relicId) =>
        new() {
            Kind = MpCheatItemKind.RemoveRelic,
            TargetPlayerNetId = target.NetId,
            ItemId = relicId,
        };
}
