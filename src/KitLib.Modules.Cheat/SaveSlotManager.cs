using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HarmonyLib;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace KitLib;

internal static class SaveSlotManager {
    internal const int QuickSlotId = 0;

    private static string SnapshotDir => DataPaths.SnapshotsDir;

    private static readonly Regex SlotMetaPattern = new(@"^slot(\d+)_meta\.json$", RegexOptions.Compiled);

    // ──────── Slot discovery ────────

    /// <summary>Returns sorted list of all slot IDs that have save data on disk.</summary>
    public static List<int> GetAllSlotIds() {
        if (!Directory.Exists(SnapshotDir)) return new List<int>();

        return Directory.GetFiles(SnapshotDir, "slot*_meta.json")
            .Select(f => SlotMetaPattern.Match(Path.GetFileName(f)))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .OrderBy(id => id)
            .ToList();
    }

    /// <summary>Returns the next unused slot ID (always >= 1).</summary>
    public static int NextSlotId() {
        var ids = GetAllSlotIds();
        return ids.Count == 0 ? 1 : ids.Max() + 1;
    }

    // ──────── Quick save (slot 0, convenience for hotkey/console) ────────

    internal static string QuickSlotDisplayName =>
        I18N.T("snapshot.quickName", "Quick Save");

    public static bool QuickSave() => SaveToSlot(QuickSlotId, QuickSlotDisplayName);

    public static bool QuickLoad() => LoadFromSlot(QuickSlotId);

    public static bool HasQuickSnapshot => HasSlot(QuickSlotId);

    // ──────── Snapshot files (used by combat checkpoints and slots) ────────

    internal static bool SaveSnapshotToFiles(string savePath, string metaPath, string name) {
        var rm = RunManager.Instance;
        var state = rm?.DebugOnlyGetState();
        if (state == null)
            return false;

        try {
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var save = rm!.ToSave(SnapshotPreFinishedRoom(state.CurrentRoom));
            var json = SaveManager.ToJson(save);
            AtomicWrite(savePath, json);

            var meta = CaptureMetaFromState(state, name);
            AtomicWrite(metaPath, JsonSerializer.Serialize(meta));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SaveSlotManager: Save snapshot failed: {ex.Message}");
            return false;
        }
    }

    internal static bool LoadFromFile(string savePath) {
        try {
            if (!File.Exists(savePath)) {
                MainFile.Logger.Warn($"SaveSlotManager: Snapshot missing: {savePath}");
                return false;
            }

            var json = File.ReadAllText(savePath);
            var result = SaveManager.FromJson<SerializableRun>(json);
            if (result.SaveData == null) {
                MainFile.Logger.Warn("SaveSlotManager: Failed to deserialize snapshot.");
                return false;
            }

            return LoadFromSave(result.SaveData);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SaveSlotManager: Load snapshot failed: {ex.Message}");
            return false;
        }
    }

    internal static SaveSlotMeta? LoadMetaFromFile(string metaPath) {
        if (!File.Exists(metaPath))
            return null;
        try {
            return JsonSerializer.Deserialize<SaveSlotMeta>(File.ReadAllText(metaPath));
        }
        catch {
            return null;
        }
    }

    // ──────── Slot save / load / delete ────────

    public static bool SaveToSlot(int slot, string name = "") =>
        SaveSnapshotToFiles(SlotPath(slot), MetaPath(slot), name);

    public static bool LoadFromSlot(int slot) => LoadFromFile(SlotPath(slot));

    public static bool DeleteSlot(int slot) {
        try {
            var deleted = false;
            var savePath = SlotPath(slot);
            var metaPath = MetaPath(slot);

            if (File.Exists(savePath)) { File.Delete(savePath); deleted = true; }
            if (File.Exists(metaPath)) { File.Delete(metaPath); deleted = true; }

            if (deleted)
                MainFile.Logger.Info($"SaveSlotManager: Deleted slot {slot}.");
            return deleted;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SaveSlotManager: Delete slot {slot} failed: {ex.Message}");
            return false;
        }
    }

    public static bool HasSlot(int slot) => File.Exists(SlotPath(slot));

    public static SaveSlotMeta? LoadMeta(int slot) {
        var path = MetaPath(slot);
        if (!File.Exists(path)) return null;
        try {
            return JsonSerializer.Deserialize<SaveSlotMeta>(File.ReadAllText(path));
        }
        catch {
            return null;
        }
    }

    public static void RenameSlot(int slot, string name) {
        var meta = LoadMeta(slot) ?? new SaveSlotMeta();
        meta.Name = name;
        try { AtomicWrite(MetaPath(slot), JsonSerializer.Serialize(meta)); }
        catch (Exception ex) { MainFile.Logger.Warn($"SaveSlotManager: Rename slot {slot} failed: {ex.Message}"); }
    }

    public static bool SetDebugNotes(int slot, string notes) {
        if (!HasSlot(slot))
            return false;

        var meta = LoadMeta(slot) ?? new SaveSlotMeta();
        meta.DebugNotes = notes ?? "";
        try {
            AtomicWrite(MetaPath(slot), JsonSerializer.Serialize(meta));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SaveSlotManager: Set debug notes for slot {slot} failed: {ex.Message}");
            return false;
        }
    }

    // ──────── Helpers ────────

    // AbstractRoom.FromSerializable only restores combat and event rooms; shop/rest/treasure/map must reload via map coord.
    private static AbstractRoom? SnapshotPreFinishedRoom(AbstractRoom? room) =>
        room != null && IsRestorablePreFinishedRoomType(room.RoomType) ? room : null;

    private static SerializableRoom? LoadPreFinishedRoom(SerializableRoom? room) =>
        room != null && IsRestorablePreFinishedRoomType(room.RoomType) ? room : null;

    private static bool IsRestorablePreFinishedRoomType(RoomType roomType) =>
        roomType is RoomType.Monster or RoomType.Elite or RoomType.Boss or RoomType.Event;

    private static string SlotPath(int slot) => Path.Combine(SnapshotDir, $"slot{slot}.json");
    private static string MetaPath(int slot) => Path.Combine(SnapshotDir, $"slot{slot}_meta.json");

    private static void AtomicWrite(string path, string content) {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    // MissingMethodException has no blame API; stack + ModAssemblyLookup tie assemblies to manifests when possible.
    private static void LogLoadFailure(Exception ex) {
        MainFile.Logger.Warn($"SaveSlotManager: Load save async failed: {ex.Message}");

        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            MainFile.Logger.Warn($"SaveSlotManager: Inner: {inner.GetType().Name}: {inner.Message}");

        const int maxFrames = 48;
        var trace = new StackTrace(ex, fNeedFileInfo: true);
        var n = Math.Min(trace.FrameCount, maxFrames);

        string? culpritSummary = null;
        var manifestModsOnStack = new HashSet<KitLibModInfo>();

        var sb = new StringBuilder();
        sb.Append("SaveSlotManager: Stack (assembly → type.method):");
        for (var i = 0; i < n; i++) {
            var frame = trace.GetFrame(i);
            var method = frame?.GetMethod();
            if (method?.DeclaringType == null) continue;

            var assembly = method.DeclaringType.Assembly;
            var asmName = assembly.GetName().Name ?? "?";

            if (culpritSummary == null
                && !ModAssemblyLookup.IsRuntimeInfrastructureAssembly(asmName)
                && !ModAssemblyLookup.IsGameCoreAssembly(asmName)) {
                var asmVer = ModAssemblyLookup.FormatAssemblyVersion(assembly);
                if (ModAssemblyLookup.TryGetByAssemblySimpleName(asmName, out var mod))
                    culpritSummary =
                        $"{mod.DisplayName} v{mod.Version} (id={mod.Id}) — assembly {asmName} [{asmVer}]";
                else
                    culpritSummary =
                        $"assembly {asmName} [{asmVer}] (no manifest match — dependency or unpackaged DLL)";
            }

            if (ModAssemblyLookup.TryGetByAssemblySimpleName(asmName, out var matched))
                manifestModsOnStack.Add(new KitLibModInfo(matched.Id, matched.DisplayName, matched.Version, matched.Dependencies));

            sb.AppendLine().Append("  ").Append(asmName).Append(" → ")
                .Append(method.DeclaringType.FullName).Append('.').Append(method.Name);
        }

        if (trace.FrameCount > maxFrames)
            sb.AppendLine().Append("  … (truncated)");

        if (culpritSummary != null)
            MainFile.Logger.Warn($"SaveSlotManager: Likely culprit: {culpritSummary}");

        if (manifestModsOnStack.Count > 0) {
            var parts = new List<string>(manifestModsOnStack.Count);
            foreach (var m in manifestModsOnStack)
                parts.Add($"{m.DisplayName} v{m.Version} (id={m.Id})");
            MainFile.Logger.Warn($"SaveSlotManager: Manifest-matched mod(s) on stack: {string.Join("; ", parts)}");
        }

        MainFile.Logger.Warn(sb.ToString());
    }

    private static SaveSlotMeta CaptureMetaFromState(RunState state, string name) {
        var player = state.Players.FirstOrDefault();
        var meta = new SaveSlotMeta {
            Name = name,
            SaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TotalFloor = state.TotalFloor,
            Gold = player?.Gold ?? 0,
            Hp = player?.Creature.CurrentHp ?? 0,
            MaxHp = player?.Creature.MaxHp ?? 0,
            CharacterId = player?.Character.Id.Entry ?? "",
        };

        if (player != null) {
            meta.CardTitles = player.Deck.Cards
                .Select(c => c.Title)
                .ToList();

            meta.RelicTitles = player.Relics
                .Select(r => r.Title.GetFormattedText())
                .ToList();
        }

        meta.Seed = state.Rng?.StringSeed ?? "";

        meta.ModList = ModRuntime.Catalog.GetSnapshot()
            .Select(m => $"{m.DisplayName} v{m.Version}")
            .ToList();

        return meta;
    }

    // ──────── Internal load ────────

    private static bool LoadFromSave(SerializableRun save) {
        try {
            if (RunManager.Instance == null) {
                MainFile.Logger.Warn("SaveSlotManager: No RunManager instance.");
                return false;
            }

            TaskHelper.RunSafely(LoadFromSaveAsync(save));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SaveSlotManager: Load save failed: {ex.Message}");
            return false;
        }
    }

    private static async Task LoadFromSaveAsync(SerializableRun save) {
        try {
            await NGame.Instance!.Transition.FadeOut();

            KitLib.Host.KitLibHost.StopAiPlayLoop?.Invoke();
            KitLibCheatOps.ResetSkipAnim?.Invoke();

            if (RunManager.Instance.IsInProgress)
                RunManager.Instance.CleanUp();

            try {
                // InDevRun = true already implies CheatsInRun and IsActive; no need to change NormalRunMode.
                KitLibState.InDevRun = true;

                var state = RunState.FromSerializable(save);
                await SetUpSavedSinglePlayerCompat(RunManager.Instance, state, save);

                var prop = AccessTools.Property(typeof(RunManager), "ShouldSave");
                prop?.SetValue(RunManager.Instance, false);

                NGame.Instance.ReactionContainer.InitializeNetworking(
                    new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());

                await NGame.Instance.LoadRun(state, LoadPreFinishedRoom(save.PreFinishedRoom));
                MainFile.Logger.Info("SaveSlotManager: Save loaded successfully.");
            }
            catch (Exception ex) {
                LogLoadFailure(ex);
                KitLibState.OnRunEnded();
                throw;
            }
        }
        finally {
            try {
                await NGame.Instance!.Transition.FadeIn();
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"SaveSlotManager: FadeIn failed: {ex.Message}");
            }
        }
    }

    static async Task SetUpSavedSinglePlayerCompat(RunManager runManager, RunState state, SerializableRun save) {
        var method = AccessTools.Method(typeof(RunManager), "SetUpSavedSingleplayer")
            ?? AccessTools.Method(typeof(RunManager), "SetUpSavedSinglePlayer");
        if (method == null)
            throw new MissingMethodException(typeof(RunManager).FullName, "SetUpSavedSingleplayer");

        if (method.Invoke(runManager, [state, save]) is Task task)
            await task;
    }
}
