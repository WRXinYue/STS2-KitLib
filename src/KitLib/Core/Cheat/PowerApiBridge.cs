using System.Linq;
using System.Threading.Tasks;
using KitLib.Abstractions.Host;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Cheat;

internal static class PowerApiBridge {
    public static void Wire() {
        KitLibPowerApi.IsAvailable = () => true;
        KitLibPowerApi.TryAddPower = TryAddPower;
        KitLibPowerApi.TryRemovePower = TryRemovePower;
        KitLibPowerApi.TryClearPowers = TryClearPowers;
    }

    static async Task<KitLibRunItemResult> TryAddPower(KitLibAddPowerRequest request) {
        if (!CombatManager.Instance.IsInProgress)
            return KitLibRunItemResult.Fail("Powers only work during combat.");
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (string.IsNullOrWhiteSpace(request.PowerId))
            return KitLibRunItemResult.Fail("Missing or invalid power id.");
        if (request.Amount < 1)
            return KitLibRunItemResult.Fail("Amount must be >= 1.");

        var power = PowerActions.FindPowerById(request.PowerId.Trim());
        if (power == null)
            return KitLibRunItemResult.Fail($"Power not found: '{request.PowerId}'.");

        if (!TryMapTarget(request.Target, out var target, out var targetError))
            return KitLibRunItemResult.Fail(targetError);

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatPowerCoordinator.TryAddWithResultAsync(
                player, power, request.Amount, target);
            return ok
                ? KitLibRunItemResult.Success(((AbstractModel)power).Id.Entry)
                : KitLibRunItemResult.Fail(msg);
        }

        await PowerActions.AddPower(player, power, request.Amount, target);
        return KitLibRunItemResult.Success(((AbstractModel)power).Id.Entry);
    }

    static async Task<KitLibRunItemResult> TryRemovePower(KitLibRemovePowerRequest request) {
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (player.Creature == null)
            return KitLibRunItemResult.Fail("No active run.");
        if (string.IsNullOrWhiteSpace(request.PowerId))
            return KitLibRunItemResult.Fail("Missing or invalid power id.");

        var powerId = request.PowerId.Trim();
        var power = PowerActions.FindPowerById(powerId);
        if (power == null)
            return KitLibRunItemResult.Fail($"Power not found: '{powerId}'.");

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatPowerCoordinator.TryRemoveWithResultAsync(player, powerId);
            return ok ? KitLibRunItemResult.Success(powerId) : KitLibRunItemResult.Fail(msg);
        }

        var match = player.Creature.Powers.FirstOrDefault(p => p?.Id == power.Id);
        if (match == null)
            return KitLibRunItemResult.Fail($"Power not on target: '{powerId}'.");

        PowerActions.RemovePower(player.Creature, power);
        return KitLibRunItemResult.Success(powerId);
    }

    static async Task<KitLibRunItemResult> TryClearPowers(KitLibClearPowersRequest request) {
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (player.Creature == null)
            return KitLibRunItemResult.Fail("No active run.");

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatPowerCoordinator.TryClearWithResultAsync(player);
            return ok ? KitLibRunItemResult.Success() : KitLibRunItemResult.Fail(msg);
        }

        PowerActions.RemoveAllPowers(player.Creature);
        return KitLibRunItemResult.Success();
    }

    static bool TryResolvePlayer(ulong? targetPlayerNetId, out Player player, out string error) {
        player = null!;
        error = "";
        if (!RunContext.TryGetRunAndPlayer(out _, out var local) || local == null) {
            error = "No active run.";
            return false;
        }

        if (!targetPlayerNetId.HasValue || targetPlayerNetId.Value == 0 || targetPlayerNetId.Value == local.NetId) {
            player = local;
            return true;
        }

        var found = CardActions.FindPlayerByNetId(targetPlayerNetId.Value);
        if (found == null) {
            error = "Target player not found.";
            return false;
        }

        player = found;
        return true;
    }

    static bool TryMapTarget(KitLibPowerTarget target, out PowerTarget mapped, out string error) {
        error = "";
        switch (target) {
            case KitLibPowerTarget.Self:
                mapped = PowerTarget.Self;
                return true;
            case KitLibPowerTarget.AllEnemies:
                mapped = PowerTarget.AllEnemies;
                return true;
            case KitLibPowerTarget.Allies:
                mapped = PowerTarget.Allies;
                return true;
            default:
                mapped = PowerTarget.Self;
                error = $"Unknown power target '{target}'.";
                return false;
        }
    }
}
