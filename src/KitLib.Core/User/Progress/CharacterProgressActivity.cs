using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Progress;

internal static class CharacterProgressActivity {
    public static bool HasActivity(CharacterStats stats) =>
        stats.TotalWins > 0
        || stats.TotalLosses > 0
        || stats.Playtime > 0
        || stats.MaxAscension > 0;

    public static bool IsMissingOrDegraded(
        IReadOnlyDictionary<ModelId, CharacterStats> loaded,
        ModelId id,
        CharacterStats backupStat) {
        if (!HasActivity(backupStat))
            return false;

        if (!loaded.TryGetValue(id, out var loadedStat))
            return true;

        if (!HasActivity(loadedStat))
            return true;

        return loadedStat.MaxAscension < backupStat.MaxAscension
               || loadedStat.TotalWins < backupStat.TotalWins
               || loadedStat.TotalLosses < backupStat.TotalLosses
               || loadedStat.Playtime < backupStat.Playtime;
    }
}
