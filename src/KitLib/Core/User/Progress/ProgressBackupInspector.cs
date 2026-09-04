using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Progress;

internal sealed class ProgressBackupCharacterSummary {
    public string CharacterName { get; init; } = "";
    public int MaxAscension { get; init; }
    public int PreferredAscension { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public long Playtime { get; init; }
    public long BestWinStreak { get; init; }
    public long CurrentWinStreak { get; init; }
}

internal sealed class ProgressBackupDetails {
    public ProfileBackupMeta? Meta { get; init; }
    public bool LoadFailed { get; init; }
    public string? LoadError { get; init; }

    public string UniqueId { get; init; } = "";
    public int SchemaVersion { get; init; }
    public long TotalPlaytime { get; init; }
    public int TotalUnlocks { get; init; }
    public int CurrentScore { get; init; }
    public long FloorsClimbed { get; init; }
    public int MaxMultiplayerAscension { get; init; }
    public int TotalWins { get; init; }
    public int TotalLosses { get; init; }

    public int DiscoveredCards { get; init; }
    public int DiscoveredRelics { get; init; }
    public int DiscoveredPotions { get; init; }
    public int DiscoveredEvents { get; init; }
    public int DiscoveredActs { get; init; }

    public int EpochTotal { get; init; }
    public int EpochRevealed { get; init; }
    public int EpochObtained { get; init; }
    public IReadOnlyList<string> ModEpochIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ProgressBackupCharacterSummary> Characters { get; init; } =
        Array.Empty<ProgressBackupCharacterSummary>();

    public bool HasPrefs { get; init; }
    public bool HasCurrentRun { get; init; }
}

internal static class ProgressBackupInspector {
    private const int MaxModEpochLines = 24;

    private static readonly ModelId RandomCharacterId = ModelDb.GetId<RandomCharacter>();
    private static readonly ModelId DeprivedCharacterId = ModelDb.GetId<Deprived>();
    private static readonly ModelId DeprecatedCharacterId = ModelDb.GetId<DeprecatedCharacter>();

    public static ProgressBackupDetails Inspect(string backupDir) {
        var meta = ProfileProgressBackupService.TryLoadMeta(backupDir);
        var details = new ProgressBackupDetails {
            Meta = meta,
            HasPrefs = File.Exists(Path.Combine(backupDir, "prefs.save")),
            HasCurrentRun = File.Exists(Path.Combine(backupDir, "current_run.save")),
        };

        var progressPath = Path.Combine(backupDir, "progress.save");
        if (!File.Exists(progressPath)) {
            return new ProgressBackupDetails {
                Meta = meta,
                LoadFailed = true,
                LoadError = "progress.save missing",
                HasPrefs = details.HasPrefs,
                HasCurrentRun = details.HasCurrentRun,
            };
        }

        try {
            var json = File.ReadAllText(progressPath);
            var result = SaveManager.FromJson<SerializableProgress>(json);
            if (!result.Success || result.SaveData == null) {
                return new ProgressBackupDetails {
                    Meta = meta,
                    LoadFailed = true,
                    LoadError = result.ErrorMessage ?? result.Status.ToString(),
                    HasPrefs = details.HasPrefs,
                    HasCurrentRun = details.HasCurrentRun,
                };
            }

            return BuildFromProgress(result.SaveData, meta, details.HasPrefs, details.HasCurrentRun);
        }
        catch (Exception ex) {
            return new ProgressBackupDetails {
                Meta = meta,
                LoadFailed = true,
                LoadError = ex.Message,
                HasPrefs = details.HasPrefs,
                HasCurrentRun = details.HasCurrentRun,
            };
        }
    }

    private static ProgressBackupDetails BuildFromProgress(
        SerializableProgress progress,
        ProfileBackupMeta? meta,
        bool hasPrefs,
        bool hasCurrentRun) {
        var characters = BuildCharacterSummaries(progress.CharStats);

        var modIds = meta?.Mods
            .Select(m => m.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList() ?? [];

        var modEpochIds = progress.Epochs
            .Where(e => !string.IsNullOrEmpty(e.Id) && MatchesAnyMod(e.Id, modIds))
            .Select(e => $"{e.Id} ({e.State})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Take(MaxModEpochLines)
            .ToList();

        var revealed = 0;
        var obtained = 0;
        foreach (var epoch in progress.Epochs) {
            switch (epoch.State) {
                case EpochState.Revealed:
                    revealed++;
                    break;
                case EpochState.Obtained:
                case EpochState.ObtainedNoSlot:
                    obtained++;
                    break;
            }
        }

        return new ProgressBackupDetails {
            Meta = meta,
            UniqueId = progress.UniqueId ?? "",
            SchemaVersion = progress.SchemaVersion,
            TotalPlaytime = progress.TotalPlaytime,
            TotalUnlocks = progress.TotalUnlocks,
            CurrentScore = progress.CurrentScore,
            FloorsClimbed = progress.FloorsClimbed,
            MaxMultiplayerAscension = progress.MaxMultiplayerAscension,
            TotalWins = progress.Wins,
            TotalLosses = progress.Losses,
            DiscoveredCards = progress.DiscoveredCards.Count,
            DiscoveredRelics = progress.DiscoveredRelics.Count,
            DiscoveredPotions = progress.DiscoveredPotions.Count,
            DiscoveredEvents = progress.DiscoveredEvents.Count,
            DiscoveredActs = progress.DiscoveredActs.Count,
            EpochTotal = progress.Epochs.Count,
            EpochRevealed = revealed,
            EpochObtained = obtained,
            ModEpochIds = modEpochIds,
            Characters = characters,
            HasPrefs = hasPrefs,
            HasCurrentRun = hasCurrentRun,
        };
    }

    private static List<ProgressBackupCharacterSummary> BuildCharacterSummaries(
        IReadOnlyList<CharacterStats> charStats) {
        var statsById = new Dictionary<ModelId, CharacterStats>();
        foreach (var stat in charStats) {
            if (stat.Id is not { } id || id == ModelId.none)
                continue;
            statsById[id] = stat;
        }

        var orderedIds = new List<ModelId>();
        var included = new HashSet<ModelId>();

        void TryAdd(ModelId id) {
            if (id == ModelId.none || !included.Add(id))
                return;
            orderedIds.Add(id);
        }

        foreach (var character in ModelDb.AllCharacters)
            TryAdd(character.Id);

        foreach (var character in EnumerateModCharacters())
            TryAdd(character.Id);

        foreach (var id in statsById.Keys
                     .Where(id => id != RandomCharacterId && !included.Contains(id))
                     .Where(id => !IsHiddenInternalCharacter(id) || HasCharacterActivity(statsById, id))
                     .OrderBy(ResolveCharacterName, StringComparer.OrdinalIgnoreCase))
            TryAdd(id);

        TryAdd(RandomCharacterId);

        return orderedIds
            .Select(id => CreateCharacterSummary(id, statsById))
            .ToList();
    }

    private static bool IsHiddenInternalCharacter(ModelId id) =>
        id == DeprivedCharacterId || id == DeprecatedCharacterId;

    private static bool HasCharacterActivity(IReadOnlyDictionary<ModelId, CharacterStats> statsById, ModelId id) {
        if (!statsById.TryGetValue(id, out var stats))
            return false;
        return CharacterProgressActivity.HasActivity(stats);
    }

    private static IEnumerable<CharacterModel> EnumerateModCharacters() => ModCharacterCatalog.EnumerateModCharacters();

    private static ProgressBackupCharacterSummary CreateCharacterSummary(
        ModelId id,
        IReadOnlyDictionary<ModelId, CharacterStats> statsById) {
        statsById.TryGetValue(id, out var stats);
        return new ProgressBackupCharacterSummary {
            CharacterName = ResolveCharacterName(id),
            MaxAscension = stats?.MaxAscension ?? 0,
            PreferredAscension = stats?.PreferredAscension ?? 0,
            Wins = stats?.TotalWins ?? 0,
            Losses = stats?.TotalLosses ?? 0,
            Playtime = stats?.Playtime ?? 0,
            BestWinStreak = stats?.BestWinStreak ?? 0,
            CurrentWinStreak = stats?.CurrentWinStreak ?? 0,
        };
    }

    private static string GetCharacterDisplayName(CharacterModel character) =>
        ModCharacterCatalog.GetDisplayName(character);

    private static string ResolveCharacterName(ModelId? id) => ModCharacterCatalog.ResolveCharacterName(id);

    private static bool MatchesAnyMod(string epochId, IReadOnlyList<string> modIds) {
        foreach (var modId in modIds) {
            if (epochId.Contains(modId, StringComparison.OrdinalIgnoreCase))
                return true;

            var upper = modId.Replace('-', '_').ToUpperInvariant();
            if (epochId.Contains(upper, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    internal static string FormatPlaytime(long value) {
        if (value <= 0)
            return "0m";

        // Heuristic: large values are likely milliseconds.
        var seconds = value > 1_000_000 ? value / 1000 : value;
        if (seconds < 60)
            return $"{seconds}s";

        var minutes = seconds / 60;
        if (minutes < 60)
            return $"{minutes}m";

        var hours = minutes / 60;
        minutes %= 60;
        return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
    }
}
