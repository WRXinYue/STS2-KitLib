using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

internal static class RelicActions {
    public static async Task AddRelic(RelicModel relic, Player player) {
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn(
                $"RelicActions: Cannot add {((AbstractModel)relic).Id.Entry} locally in multiplayer — use host relic sync.");
            return;
        }

        await RelicCmd.Obtain(relic.ToMutable(), player, -1);
        MainFile.Logger.Info($"RelicActions: Added relic {((AbstractModel)relic).Id.Entry}");
    }

    public static async Task RemoveRelics(Player player) {
        await Task.Yield();
        var relics = player.Relics.ToList();
        if (relics.Count == 0) {
            MainFile.Logger.Info("RelicActions: No relics to remove.");
            return;
        }

        var screen = NChooseARelicSelection.ShowScreen((IReadOnlyList<RelicModel>)relics);
        if (screen == null) return;

        var selected = (await screen.RelicsSelected())
            .Where(r => r != null).ToList();

        if (selected.Count == 0) return;

        foreach (var relic in selected) {
            var owned = player.Relics.FirstOrDefault(r => r == relic)
                ?? player.GetRelicById(((AbstractModel)relic).Id);
            if (owned == null) continue;

            await RelicCmd.Remove(owned);
        }

        MainFile.Logger.Info($"RelicActions: Removed {selected.Count} relic(s)");
    }

    internal static RelicModel? FindRelicById(string relicId) {
        if (string.IsNullOrEmpty(relicId)) return null;
        return ModelDb.AllRelics.FirstOrDefault(r => ((AbstractModel)r).Id.Entry == relicId);
    }

    internal static bool TryValidateAddRelic(MpCheatItemPayload payload, out string? error) {
        error = null;
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) {
            error = "target player not found";
            return false;
        }

        if (FindRelicById(payload.ItemId) == null) {
            error = "relic not found";
            return false;
        }

        return true;
    }

    internal static bool TryValidateRemoveRelic(MpCheatItemPayload payload, out string? error) {
        error = null;
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) {
            error = "target player not found";
            return false;
        }

        var relic = FindRelicById(payload.ItemId);
        if (relic == null) {
            error = "relic not found";
            return false;
        }

        var owned = player.GetRelicById(((AbstractModel)relic).Id);
        if (owned == null) {
            error = "relic not owned";
            return false;
        }

        return true;
    }

    internal static async Task ExecuteAddRelicFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        var relic = FindRelicById(payload.ItemId);
        if (player == null || relic == null) return;

        await RelicCmd.Obtain(relic.ToMutable(), player, -1);
        MainFile.Logger.Info($"RelicActions: MP sync added relic {payload.ItemId} to {player.NetId}");
    }

    internal static async Task ExecuteRemoveRelicFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        var relic = FindRelicById(payload.ItemId);
        if (player == null || relic == null) return;

        var owned = player.GetRelicById(((AbstractModel)relic).Id);
        if (owned == null) return;

        await RelicCmd.Remove(owned);
        MainFile.Logger.Info($"RelicActions: MP sync removed relic {payload.ItemId} from {player.NetId}");
    }
}
