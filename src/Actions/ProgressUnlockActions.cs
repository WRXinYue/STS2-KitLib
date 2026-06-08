using System.Linq;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline;

namespace KitLib.Actions;

/// <summary>Progress-file unlocks mirroring the dev console <c>unlock all</c> command.</summary>
internal static class ProgressUnlockActions {
    public static void UnlockAll() {
        var progress = SaveManager.Instance.Progress;
        var save = SaveManager.Instance;

        foreach (var card in ModelDb.AllCards)
            progress.MarkCardAsSeen(card.Id);
        foreach (var relic in ModelDb.AllRelics)
            progress.MarkRelicAsSeen(relic.Id);
        foreach (var potion in ModelDb.AllPotions)
            progress.MarkPotionAsSeen(potion.Id);
        foreach (var ev in ModelDb.AllEvents)
            progress.MarkEventAsSeen(ev.Id);
        foreach (var act in ModelDb.Acts)
            progress.MarkActAsSeen(act.Id);

        foreach (var monster in ModelDb.Monsters) {
            var stats = progress.GetOrCreateEnemyStats(monster.Id);
            if (stats.FightStats.Count != 0) continue;
            stats.FightStats.Add(new FightStats {
                Character = ModelDb.GetId<Ironclad>(),
                Wins = 1,
            });
        }

        var revealed = progress.Epochs
            .Where(e => e.State == EpochState.Revealed)
            .Select(e => e.Id)
            .ToHashSet();
        foreach (var epochId in EpochModel.AllEpochIds) {
            if (revealed.Contains(epochId)) continue;
            save.ObtainEpochOverride(epochId, EpochState.Revealed);
        }

        progress.MaxMultiplayerAscension = AscensionManager.maxAscensionAllowed;
        foreach (var character in ModelDb.AllCharacters)
            progress.GetOrCreateCharacterStats(character.Id).MaxAscension = AscensionManager.maxAscensionAllowed;

        save.SaveProgressFile();
        MainFile.Logger.Info("[KitLib] Unlocked all save progress (timeline, ascension, compendium).");
    }
}
