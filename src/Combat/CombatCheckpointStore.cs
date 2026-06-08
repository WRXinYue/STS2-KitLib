using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.Combat;

/// <summary>Per-combat checkpoint bundle: one index plus node snapshot files.</summary>
internal static class CombatCheckpointStore {
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string SessionDir => Path.Combine(DataPaths.SnapshotsDir, "combat_session");
    private static string IndexPath => Path.Combine(SessionDir, "index.json");
    private static string NodesDir => Path.Combine(SessionDir, "nodes");

    private static CombatCheckpointIndex? _index;
    private static bool _combatStartSaved;

    internal static bool HasActiveSession => _index != null;

    internal static IReadOnlyList<CombatCheckpointNode> GetNodes() {
        EnsureIndexLoaded();
        return _index?.Nodes ?? (IReadOnlyList<CombatCheckpointNode>)Array.Empty<CombatCheckpointNode>();
    }

    internal static void BeginCombat() {
        ResetSession();
    }

    /// <summary>Wipe stale checkpoints — prior combat may not have reached EndCombat after a crash.</summary>
    private static void ResetSession() {
        _index = null;
        _combatStartSaved = false;

        if (Directory.Exists(SessionDir)) {
            try {
                Directory.Delete(SessionDir, recursive: true);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"CombatCheckpointStore: ResetSession cleanup failed: {ex.Message}");
            }
        }

        Directory.CreateDirectory(NodesDir);
        _index = new CombatCheckpointIndex {
            SessionStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        PersistIndex();
    }

    internal static void EndCombat() {
        _index = null;
        _combatStartSaved = false;
        if (!Directory.Exists(SessionDir))
            return;
        try {
            Directory.Delete(SessionDir, recursive: true);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatCheckpointStore: EndCombat cleanup failed: {ex.Message}");
        }
    }

    internal static bool HasNode(CombatCheckpointKind kind) {
        EnsureIndexLoaded();
        if (kind == CombatCheckpointKind.TurnStart)
            return GetCurrentTurnStartNode() != null;
        return _index?.Nodes.Any(n => n.Kind == KindKey(kind)) == true;
    }

    internal static bool SaveNode(CombatCheckpointKind kind, int round = 0) {
        if (_index == null)
            return false;

        if (kind == CombatCheckpointKind.CombatStart) {
            if (_combatStartSaved)
                return true;
            _combatStartSaved = true;
        }

        string nodeId = kind switch {
            CombatCheckpointKind.CombatStart => "combat_start",
            CombatCheckpointKind.TurnStart => $"turn_{round}",
            CombatCheckpointKind.Manual => $"manual_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        string label = LabelFor(kind, round);
        string savePath = NodeSavePath(nodeId);
        string metaPath = NodeMetaPath(nodeId);

        if (!SaveSlotManager.SaveSnapshotToFiles(savePath, metaPath, label))
            return false;

        string combatPath = NodeCombatPath(nodeId);
        if (!CombatSnapshotIO.TryCapture(combatPath, out _))
            MainFile.Logger.Warn($"CombatCheckpointStore: combat snapshot failed for {nodeId}");

        _index.Nodes.RemoveAll(n => n.Id == nodeId);
        _index.Nodes.Add(new CombatCheckpointNode {
            Id = nodeId,
            Kind = KindKey(kind),
            Source = kind == CombatCheckpointKind.Manual ? "manual" : "auto",
            Round = round,
            Label = label,
            SaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        PersistIndex();
        return true;
    }

    internal static bool TryLoadNode(CombatCheckpointKind kind) {
        var node = kind == CombatCheckpointKind.TurnStart
            ? GetCurrentTurnStartNode()
            : GetLatestNode(kind);
        if (node == null)
            return false;
        return TryLoadNodeById(node.Id);
    }

    internal static bool TryLoadNodeById(string nodeId) {
        EnsureIndexLoaded();
        var node = _index?.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null && _index != null && node.SaveTime < _index.SessionStartedAt) {
            MainFile.Logger.Warn($"CombatCheckpointStore: refusing stale node {nodeId} from prior combat session.");
            return false;
        }

        string combatPath = NodeCombatPath(nodeId);
        if (File.Exists(combatPath) && CombatManager.Instance is { IsInProgress: true })
            return CombatCheckpointRestorer.TryRestoreFromFile(combatPath);

        string savePath = NodeSavePath(nodeId);
        if (!File.Exists(savePath))
            return false;
        return SaveSlotManager.LoadFromFile(savePath);
    }

    internal static SaveSlotMeta? LoadNodeMeta(string nodeId) {
        string metaPath = NodeMetaPath(nodeId);
        return SaveSlotManager.LoadMetaFromFile(metaPath);
    }

    private static CombatCheckpointNode? GetLatestNode(CombatCheckpointKind kind) {
        EnsureIndexLoaded();
        if (_index == null)
            return null;
        string key = KindKey(kind);
        return _index.Nodes
            .Where(n => n.Kind == key)
            .OrderBy(n => n.SaveTime)
            .LastOrDefault();
    }

    /// <summary>Turn-start reload should target the current combat round, not the first saved turn.</summary>
    private static CombatCheckpointNode? GetCurrentTurnStartNode() {
        EnsureIndexLoaded();
        if (_index == null)
            return null;

        string key = KindKey(CombatCheckpointKind.TurnStart);
        int round = CombatManager.Instance?.DebugOnlyGetState()?.RoundNumber ?? -1;
        if (round >= 0) {
            var byRound = _index.Nodes
                .Where(n => n.Kind == key && n.Round == round)
                .OrderBy(n => n.SaveTime)
                .LastOrDefault();
            if (byRound != null)
                return byRound;
        }

        return null;
    }

    private static string KindKey(CombatCheckpointKind kind) => kind switch {
        CombatCheckpointKind.CombatStart => "combat_start",
        CombatCheckpointKind.TurnStart => "turn_start",
        CombatCheckpointKind.Manual => "manual",
        _ => kind.ToString()
    };

    private static string LabelFor(CombatCheckpointKind kind, int round) => kind switch {
        CombatCheckpointKind.CombatStart =>
            I18N.T("snapshot.nodeCombatStart", "Combat start"),
        CombatCheckpointKind.TurnStart =>
            I18N.T("snapshot.nodeTurnStart", "Turn {0} start", round),
        CombatCheckpointKind.Manual =>
            I18N.T("snapshot.nodeManual", "Manual checkpoint"),
        _ => kind.ToString()
    };

    private static string NodeSavePath(string nodeId) =>
        Path.Combine(NodesDir, $"{nodeId}.json");

    private static string NodeMetaPath(string nodeId) =>
        Path.Combine(NodesDir, $"{nodeId}_meta.json");

    private static string NodeCombatPath(string nodeId) =>
        Path.Combine(NodesDir, $"{nodeId}.combat.bin");

    private static void PersistIndex() {
        if (_index == null)
            return;
        try {
            Directory.CreateDirectory(SessionDir);
            AtomicWrite(IndexPath, JsonSerializer.Serialize(_index, JsonOpts));
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatCheckpointStore: Persist index failed: {ex.Message}");
        }
    }

    private static void AtomicWrite(string path, string content) {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    private static void EnsureIndexLoaded() {
        if (_index != null)
            return;
        if (!File.Exists(IndexPath))
            return;
        try {
            _index = JsonSerializer.Deserialize<CombatCheckpointIndex>(File.ReadAllText(IndexPath), JsonOpts);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatCheckpointStore: load index failed: {ex.Message}");
        }
    }

    internal static string FormatNodeTime(long saveTime) =>
        saveTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(saveTime).LocalDateTime.ToString("MM/dd HH:mm")
            : "";
}
