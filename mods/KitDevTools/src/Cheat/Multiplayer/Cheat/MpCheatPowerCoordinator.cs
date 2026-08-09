using System.Threading.Tasks;
using KitLib;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Multiplayer.Cheat;

internal static class MpCheatPowerCoordinator {
    public static Task<string> TryHostAddPowerAsync(Player target, PowerModel power, int amount, PowerTarget targetKind) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.AddPowerPrepare,
            MpCheatCommandKind.AddPowerExecute,
            BuildAddPayload(target, power, amount, targetKind),
            PowerActions.TryValidateAddPower,
            PowerActions.ExecuteAddPowerFromMpSync,
            "PowerAdd",
            p => string.Format(I18N.T("mpcheat.powerAdd.success", "Applied power {0}."), p.ItemId));

    public static Task<string> TryClientRequestAddPowerAsync(Player target, PowerModel power, int amount, PowerTarget targetKind) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildAddPayload(target, power, amount, targetKind),
            PowerActions.TryValidateAddPower,
            requireSelfTarget: true,
            "PowerAdd");

    public static Task<string> TryHostRemovePowerAsync(Player target, string powerId) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.RemovePowerPrepare,
            MpCheatCommandKind.RemovePowerExecute,
            BuildTargetPayload(MpCheatItemKind.RemovePower, target, powerId),
            PowerActions.TryValidateRemovePower,
            PowerActions.ExecuteRemovePowerFromMpSync,
            "PowerRemove",
            p => string.Format(I18N.T("mpcheat.powerRemove.success", "Removed power {0}."), p.ItemId));

    public static Task<string> TryClientRequestRemovePowerAsync(Player target, string powerId) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildTargetPayload(MpCheatItemKind.RemovePower, target, powerId),
            PowerActions.TryValidateRemovePower,
            requireSelfTarget: true,
            "PowerRemove");

    public static Task<string> TryHostClearPowersAsync(Player target) =>
        MpCheatItemSyncCore.TryHostAsync(
            MpCheatCommandKind.ClearPowersPrepare,
            MpCheatCommandKind.ClearPowersExecute,
            BuildTargetPayload(MpCheatItemKind.ClearPowers, target, ""),
            PowerActions.TryValidateClearPowers,
            PowerActions.ExecuteClearPowersFromMpSync,
            "PowerClear",
            _ => I18N.T("mpcheat.powerClear.success", "Cleared powers."));

    public static Task<string> TryClientRequestClearPowersAsync(Player target) =>
        MpCheatItemSyncCore.TryClientRequestAsync(
            BuildTargetPayload(MpCheatItemKind.ClearPowers, target, ""),
            PowerActions.TryValidateClearPowers,
            requireSelfTarget: true,
            "PowerClear");

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (message.Item?.Kind is not (MpCheatItemKind.AddPower or MpCheatItemKind.RemovePower or MpCheatItemKind.ClearPowers))
            return;
        var validate = message.Item.Kind switch {
            MpCheatItemKind.AddPower => (MpCheatItemValidateDelegate)PowerActions.TryValidateAddPower,
            MpCheatItemKind.RemovePower => PowerActions.TryValidateRemovePower,
            _ => PowerActions.TryValidateClearPowers,
        };
        var logTag = message.Item.Kind switch {
            MpCheatItemKind.AddPower => "PowerAdd",
            MpCheatItemKind.RemovePower => "PowerRemove",
            _ => "PowerClear",
        };
        MpCheatItemSyncCore.OnPrepareReceived(message, validate, logTag);
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (message.Item == null) return;
        MpCheatItemExecuteDelegate? execute = message.Item.Kind switch {
            MpCheatItemKind.AddPower => PowerActions.ExecuteAddPowerFromMpSync,
            MpCheatItemKind.RemovePower => PowerActions.ExecuteRemovePowerFromMpSync,
            MpCheatItemKind.ClearPowers => PowerActions.ExecuteClearPowersFromMpSync,
            _ => null,
        };
        if (execute == null) return;
        var logTag = message.Item.Kind switch {
            MpCheatItemKind.AddPower => "PowerAdd",
            MpCheatItemKind.RemovePower => "PowerRemove",
            _ => "PowerClear",
        };
        MpCheatItemSyncCore.OnExecuteReceived(message, execute, logTag);
    }

    internal static Task<(bool Success, string Message)> TryHostFromPayloadCoreAsync(MpCheatItemPayload payload) =>
        payload.Kind switch {
            MpCheatItemKind.AddPower => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.AddPowerPrepare,
                MpCheatCommandKind.AddPowerExecute,
                payload,
                PowerActions.TryValidateAddPower,
                PowerActions.ExecuteAddPowerFromMpSync,
                "PowerAdd",
                p => string.Format(I18N.T("mpcheat.powerAdd.success", "Applied power {0}."), p.ItemId)),
            MpCheatItemKind.RemovePower => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.RemovePowerPrepare,
                MpCheatCommandKind.RemovePowerExecute,
                payload,
                PowerActions.TryValidateRemovePower,
                PowerActions.ExecuteRemovePowerFromMpSync,
                "PowerRemove",
                p => string.Format(I18N.T("mpcheat.powerRemove.success", "Removed power {0}."), p.ItemId)),
            MpCheatItemKind.ClearPowers => MpCheatItemSyncCore.TryHostWithResultAsync(
                MpCheatCommandKind.ClearPowersPrepare,
                MpCheatCommandKind.ClearPowersExecute,
                payload,
                PowerActions.TryValidateClearPowers,
                PowerActions.ExecuteClearPowersFromMpSync,
                "PowerClear",
                _ => I18N.T("mpcheat.powerClear.success", "Cleared powers.")),
            _ => Task.FromResult((false, MpCheatItemSyncCore.FormatError("unknown power kind"))),
        };

    private static MpCheatItemPayload BuildAddPayload(Player target, PowerModel power, int amount, PowerTarget targetKind) =>
        new() {
            Kind = MpCheatItemKind.AddPower,
            TargetPlayerNetId = target.NetId,
            ItemId = ((AbstractModel)power).Id.Entry ?? "",
            Amount = amount,
            PowerTarget = (int)targetKind,
        };

    private static MpCheatItemPayload BuildTargetPayload(MpCheatItemKind kind, Player target, string powerId) =>
        new() {
            Kind = kind,
            TargetPlayerNetId = target.NetId,
            ItemId = powerId,
        };
}
