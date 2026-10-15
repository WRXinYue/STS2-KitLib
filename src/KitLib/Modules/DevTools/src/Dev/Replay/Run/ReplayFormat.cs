using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace KitLib.Replay;

/// <summary>
/// On-disk format for a full-run <c>.replay</c>.
/// <see cref="CoreVersion"/> is the replay engine contract and is independent
/// of <see cref="ModVersion"/>. Bump it when command grammar or playback
/// semantics change enough that files cannot be mixed.
/// </summary>
internal static class ReplayFormat {
    internal const int CoreVersion = 1;
    internal const int MinSupportedCore = 1;

    internal const string CorePrefix = "# ReplayCore: ";
    internal const string CharacterPrefix = "# Character: ";
    internal const string SeedPrefix = "# Seed: ";
    internal const string AscensionPrefix = "# Ascension: ";
    internal const string ActsPrefix = "# Acts: ";
    internal const string GamePrefix = "# Game: ";
    internal const string ModPrefix = "# Mod: ";
    internal const string StartTimePrefix = "# StartTime: ";
    internal const string SavePointPrefix = "# SavePoint: ";

    internal static bool IsPlayable(int fileCore) =>
        fileCore >= MinSupportedCore && fileCore <= CoreVersion;

    internal static ReplayLog Parse(IReadOnlyList<string> lines) {
        int core = CoreVersion;
        bool sawCore = false;
        string seed = "unknown-seed";
        string character = "unknown-character";
        int ascension = 0;
        string[]? acts = null;
        long startTime = 0;
        var savePoints = new Dictionary<long, int>();
        var commands = new List<string>();
        bool inHeader = true;

        foreach (string raw in lines) {
            string line = raw;
            if (inHeader) {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.StartsWith('#')) {
                    if (line.StartsWith(CorePrefix, StringComparison.Ordinal)
                        && int.TryParse(line[CorePrefix.Length..].Trim(), out int parsedCore)) {
                        core = parsedCore;
                        sawCore = true;
                    }
                    else if (line.StartsWith(SeedPrefix, StringComparison.Ordinal))
                        seed = line[SeedPrefix.Length..].Trim();
                    else if (line.StartsWith(CharacterPrefix, StringComparison.Ordinal))
                        character = line[CharacterPrefix.Length..].Trim();
                    else if (line.StartsWith(AscensionPrefix, StringComparison.Ordinal)
                        && int.TryParse(line[AscensionPrefix.Length..].Trim(), out int parsedAsc))
                        ascension = parsedAsc;
                    else if (line.StartsWith(ActsPrefix, StringComparison.Ordinal)) {
                        var parsed = line[ActsPrefix.Length..]
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (parsed.Length > 0)
                            acts = parsed;
                    }
                    else if (line.StartsWith(StartTimePrefix, StringComparison.Ordinal)
                        && long.TryParse(line[StartTimePrefix.Length..].Trim(), out long parsedStart))
                        startTime = parsedStart;
                    else if (line.StartsWith(SavePointPrefix, StringComparison.Ordinal))
                        TryAddSavePoint(savePoints, line[SavePointPrefix.Length..]);
                    continue;
                }
                inHeader = false;
            }

            if (line.Length > 0 && !line.StartsWith('#'))
                commands.Add(line);
        }

        if (!sawCore)
            core = 1;

        return new ReplayLog(core, seed, character, ascension, acts, startTime, savePoints, commands);
    }

    internal static ReplayLog? TryReadFile(string path) {
        try {
            if (!File.Exists(path))
                return null;
            return Parse(File.ReadAllLines(path));
        }
        catch {
            return null;
        }
    }

    internal static string Format(
        int coreVersion,
        string character,
        string seed,
        int ascension,
        string acts,
        string gameVersion,
        long startTime,
        IReadOnlyDictionary<long, int> savePoints,
        IReadOnlyList<string> commands) {
        var sb = new StringBuilder();
        sb.Append(CorePrefix).Append(coreVersion).AppendLine();
        sb.Append(CharacterPrefix).Append(character).AppendLine();
        sb.Append(SeedPrefix).Append(seed).AppendLine();
        sb.Append(AscensionPrefix).Append(ascension).AppendLine();
        if (acts.Length > 0)
            sb.Append(ActsPrefix).Append(acts).AppendLine();
        sb.Append(GamePrefix).Append(gameVersion).AppendLine();
        sb.Append(ModPrefix).Append(ModVersion.Current).AppendLine();
        if (startTime != 0)
            sb.Append(StartTimePrefix).Append(startTime).AppendLine();
        foreach (var kv in savePoints.OrderBy(p => p.Value).ThenBy(p => p.Key)) {
            sb.Append(SavePointPrefix)
                .Append(kv.Key.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(kv.Value.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }
        foreach (string entry in commands)
            sb.AppendLine(entry);
        return sb.ToString();
    }

    /// <summary>
    /// Keep checkpoints on the current timeline. Drop points past
    /// <paramref name="commandCount"/> (abandoned after loading an earlier save).
    /// </summary>
    internal static Dictionary<long, int> MergeSavePoint(
        IReadOnlyDictionary<long, int>? existing,
        long saveTime,
        int commandCount) {
        var next = new Dictionary<long, int>();
        if (existing != null) {
            foreach (var kv in existing) {
                if (kv.Value <= commandCount)
                    next[kv.Key] = kv.Value;
            }
        }
        if (saveTime != 0)
            next[saveTime] = commandCount;
        return next;
    }

    static void TryAddSavePoint(Dictionary<long, int> savePoints, string rest) {
        string[] parts = rest.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return;
        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long saveTime))
            return;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            return;
        if (count < 0)
            return;
        savePoints[saveTime] = count;
    }
}

internal sealed class ReplayLog {
    internal ReplayLog(
        int coreVersion,
        string seed,
        string characterId,
        int ascension,
        string[]? acts,
        long startTime,
        Dictionary<long, int> savePoints,
        List<string> commands) {
        CoreVersion = coreVersion;
        Seed = seed;
        CharacterId = characterId;
        Ascension = ascension;
        Acts = acts;
        StartTime = startTime;
        SavePoints = savePoints;
        Commands = commands;
    }

    internal int CoreVersion { get; }
    internal string Seed { get; }
    internal string CharacterId { get; }
    internal int Ascension { get; }
    internal string[]? Acts { get; }
    internal long StartTime { get; }
    internal IReadOnlyDictionary<long, int> SavePoints { get; }
    internal IReadOnlyList<string> Commands { get; }

    /// <summary>
    /// Command prefix that matches this save. Missing checkpoints (legacy files)
    /// keep the full log; loading an earlier save of a later timeline uses the
    /// recorded count so later actions are not kept.
    /// </summary>
    internal int CommandCountForSave(long saveTime) {
        if (saveTime != 0 && SavePoints.TryGetValue(saveTime, out int count))
            return Math.Clamp(count, 0, Commands.Count);
        return Commands.Count;
    }
}
