using System.Threading.Tasks;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Multiplayer.Cheat;

internal static class MpCheatPotionCoordinator {
    public static Task<string> TryHostAddPotionAsync(Player target, PotionModel potion) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.AddPotionPrepare,
            MpCheatCommandKind.AddPotionExecute,
            BuildPayload(MpCheatItemKind.AddPotion, target, potion),
            PotionActions.TryValidateAddPotion,
            PotionActions.ExecuteAddPotionFromMpSync,
            "PotionAdd",
            p => string.Format(I18N.T("mpcheat.potionAdd.success", "Added potion {0}."), p.ItemId));

    public static Task<string> TryClientRequestAddPotionAsync(Player target, PotionModel potion) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildPayload(MpCheatItemKind.AddPotion, target, potion),
            PotionActions.TryValidateAddPotion,
            requireSelfTarget: true,
            "PotionAdd");

    public static Task<string> TryHostDiscardPotionAsync(Player target, int slotIndex) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.RemovePotionPrepare,
            MpCheatCommandKind.RemovePotionExecute,
            BuildDiscardPayload(target, slotIndex),
            PotionActions.TryValidateRemovePotion,
            PotionActions.ExecuteDiscardPotionFromMpSync,
            "PotionDiscard",
            _ => I18N.T("mpcheat.potionRemove.success", "Potion discarded."));

    public static Task<string> TryClientRequestDiscardPotionAsync(Player target, int slotIndex) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildDiscardPayload(target, slotIndex),
            PotionActions.TryValidateRemovePotion,
            requireSelfTarget: true,
            "PotionDiscard");

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (message.Item?.Kind is not (MpCheatItemKind.AddPotion or MpCheatItemKind.RemovePotion)) return;
        var validate = message.Item.Kind == MpCheatItemKind.AddPotion
            ? PotionActions.TryValidateAddPotion
            : (MpCheatItemValidateDelegate)PotionActions.TryValidateRemovePotion;
        MpCheatItemSyncCore.OnPrepareReceived(
            message,
            validate,
            message.Item.Kind == MpCheatItemKind.AddPotion ? "PotionAdd" : "PotionDiscard");
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (message.Item == null) return;
        MpCheatItemExecuteDelegate? execute = message.Item.Kind switch {
            MpCheatItemKind.AddPotion => PotionActions.ExecuteAddPotionFromMpSync,
            MpCheatItemKind.RemovePotion => PotionActions.ExecuteDiscardPotionFromMpSync,
            _ => null,
        };
        if (execute == null) return;
        MpCheatItemSyncCore.OnExecuteReceived(
            message,
            execute,
            message.Item.Kind == MpCheatItemKind.AddPotion ? "PotionAdd" : "PotionDiscard");
    }

    internal static Task<(bool Success, string Message)> TryHostFromPayloadCoreAsync(MpCheatItemPayload payload) =>
        payload.Kind switch {
            MpCheatItemKind.AddPotion => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.AddPotionPrepare,
                MpCheatCommandKind.AddPotionExecute,
                payload,
                PotionActions.TryValidateAddPotion,
                PotionActions.ExecuteAddPotionFromMpSync,
                "PotionAdd",
                p => string.Format(I18N.T("mpcheat.potionAdd.success", "Added potion {0}."), p.ItemId)),
            MpCheatItemKind.RemovePotion => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.RemovePotionPrepare,
                MpCheatCommandKind.RemovePotionExecute,
                payload,
                PotionActions.TryValidateRemovePotion,
                PotionActions.ExecuteDiscardPotionFromMpSync,
                "PotionDiscard",
                _ => I18N.T("mpcheat.potionRemove.success", "Potion discarded.")),
            _ => Task.FromResult((false, MpCheatItemSyncCore.FormatError("unknown potion kind"))),
        };

    private static MpCheatItemPayload BuildPayload(MpCheatItemKind kind, Player target, PotionModel potion) =>
        new() {
            Kind = kind,
            TargetPlayerNetId = target.NetId,
            ItemId = ((AbstractModel)potion).Id.Entry ?? "",
        };

    private static MpCheatItemPayload BuildDiscardPayload(Player target, int slotIndex) =>
        new() {
            Kind = MpCheatItemKind.RemovePotion,
            TargetPlayerNetId = target.NetId,
            SlotIndex = slotIndex,
        };
}
