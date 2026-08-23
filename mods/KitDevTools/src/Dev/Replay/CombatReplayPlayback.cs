using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Replay.Commands;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Replay;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Replay;

/// <summary>
/// Playback for official <c>.mcr</c> combat replays and KitLib <c>.replay</c> full-run command logs.
/// Combat auto waits for the action executor plus a short settle so playback matches real speed.
/// Manual steps one recorded sequence (combat) or one command (run) at a time.
/// </summary>
internal readonly record struct ReplayRoomSegment(
    MapPointType PointType,
    RoomType RoomType,
    ModelId? ModelId,
    bool IsStartingBonus = false);

internal static class CombatReplayPlayback {
    internal const string RunReplayExtension = ".replay";
    internal const string LegacyRunReplayExtension = ".sts2replay";
    internal const string RunReplayFileName = "actions.replay";
    internal const string LegacyRunReplayFileName = "actions.sts2replay";

    static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f };
    static readonly string[] SpeedLabels = { "0.5×", "1×", "2×", "4×" };
    const float AutoSettleSeconds = 0.55f;
    static readonly List<ReplayRoomSegment> _rooms = new();
    static readonly PropertyInfo? RunStateProp = AccessTools.Property(typeof(RunManager), "State");

    static bool _active;
    static bool _paused;
    static bool _finished;
    static bool _ready;
    static bool _manual;
    static bool _runSession;
    static int _speedIndex = 1;
    static int _eventIndex;
    static int _eventCount;
    static int _session;
    static int _stepToken;
    static double _savedTimeScale = 1d;
    static string? _path;
    static SceneTree? _tree;
    static bool _seeking;
    static int _seekPastCommandIndex = -1;

    internal static Action<SceneTree>? ShowHud { get; set; }
    internal static Action? HideHud { get; set; }

    internal static bool IsRunSession => _runSession;
    internal static bool IsActive => _runSession
        ? ReplayEngine.IsActive || ReplayEngine.IsReplayRun
        : _active;
    internal static bool IsPaused => _runSession ? ReplayDispatcher.Paused : _paused;
    internal static bool IsFinished => _runSession
        ? ReplayEngine.IsReplayRun && !ReplayEngine.IsActive
        : _finished;
    internal static bool IsManual => _runSession ? ReplayDispatcher.Paused : _manual;
    internal static bool CanRestart => _path != null && (_runSession || _ready || _finished);
    internal static bool CanStep => _runSession
        ? ReplayDispatcher.Paused && ReplayEngine.IsActive
        : _manual && _active && _ready && !_finished;
    internal static int EventIndex => _runSession
        ? Math.Max(0, ReplayEngine._loadedCommands.Count - ReplayEngine._pending.Count)
        : _eventIndex;
    internal static int EventCount => _runSession
        ? ReplayEngine._loadedCommands.Count
        : _eventCount;
    internal static string SpeedLabel => SpeedLabels[_speedIndex];
    internal static IReadOnlyList<ReplayRoomSegment> Rooms {
        get {
            if (_runSession)
                RefreshRunRooms();
            return _rooms;
        }
    }
    internal static int CurrentRoomIndex {
        get {
            if (!_runSession)
                return _rooms.Count == 0 ? 0 : _rooms.Count - 1;
            if (_rooms.Count == 0)
                return 0;
            if (IsFinished)
                return _rooms.Count - 1;
            int moves = 0;
            int consumed = EventIndex;
            var cmds = ReplayEngine._loadedCommands;
            for (int i = 0; i < consumed && i < cmds.Count; i++) {
                if (cmds[i] is MapMoveCommand)
                    moves++;
            }
            return Math.Clamp(moves, 0, _rooms.Count - 1);
        }
    }
    internal static float RoomProgress {
        get {
            if (IsFinished)
                return 1f;
            if (EventCount <= 0)
                return 0f;
            if (!_runSession)
                return Mathf.Clamp(EventIndex / (float)EventCount, 0f, 1f);
            int current = CurrentRoomIndex;
            int start = CommandIndexForRoom(current) + 1;
            int end = current + 1 < _rooms.Count
                ? CommandIndexForRoom(current + 1)
                : EventCount;
            if (end <= start)
                return 0f;
            return Mathf.Clamp((EventIndex - start) / (float)(end - start), 0f, 1f);
        }
    }

    internal static event Action? Changed;

    internal static bool IsRunReplayPath(string path) {
        string ext = Path.GetExtension(path);
        return ext.Equals(RunReplayExtension, StringComparison.OrdinalIgnoreCase)
            || ext.Equals(LegacyRunReplayExtension, StringComparison.OrdinalIgnoreCase);
    }

    internal static string RunReplayRootDirectory() =>
        Path.Combine(OS.GetUserDataDir(), "KitLib", "run-replays");

    internal static string DefaultRunLogsDirectory() {
        string root = RunReplayRootDirectory();
        if (Directory.Exists(root))
            return root;
        string legacy = Path.Combine(root, "logs");
        return Directory.Exists(legacy) ? legacy : OS.GetUserDataDir();
    }

    internal static string FilePathForRun(string seed, string character, long startTime) =>
        Path.Combine(
            RunReplayRootDirectory(),
            SanitizeFileName($"{startTime}_{character}_{seed}") + RunReplayExtension);

    /// <summary>Canonical one-file-per-run path, then the newest legacy per-floor snapshot.</summary>
    internal static string? FindExistingRunReplay(string seed, string character, long startTime) {
        string canonical = FilePathForRun(seed, character, startTime);
        if (File.Exists(canonical))
            return canonical;

        string seedDir = Path.Combine(RunReplayRootDirectory(), "logs", SanitizeFileName(seed));
        if (!Directory.Exists(seedDir))
            return null;

        string? latest = null;
        int bestFloor = -1;
        foreach (string dir in Directory.EnumerateDirectories(seedDir, "floor_*")) {
            string name = Path.GetFileName(dir);
            if (name.Length <= 6 || !int.TryParse(name.AsSpan(6), out int floor) || floor < bestFloor)
                continue;
            string? found = FirstExisting(
                Path.Combine(dir, RunReplayFileName),
                Path.Combine(dir, LegacyRunReplayFileName),
                Path.Combine(dir, "actions.minimal.log"));
            if (found == null)
                continue;
            bestFloor = floor;
            latest = found;
        }
        return latest;
    }

    static string? FirstExisting(params string[] paths) {
        foreach (string path in paths) {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    internal static string SanitizeFileName(string value) {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }

    internal static bool TryPlay(string path, SceneTree tree, out string error) =>
        IsRunReplayPath(path) ? TryPlayRun(path, tree, out error) : TryPlayCombat(path, tree, out error);

    static bool TryPlayCombat(string path, SceneTree tree, out string error) {
        if (!TryLoad(path, out var replay, out error))
            return false;
        Stop();
        _path = path;
        _tree = tree;
        TaskHelper.RunSafely(PlayAsync(replay, tree));
        return true;
    }

    internal static bool TryLoad(string path, out CombatReplay replay, out string error) {
        replay = null!;
        error = "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            error = I18N.T("devmenu.replay.err.notFound", "Replay file not found.");
            return false;
        }

        try {
            var reader = new PacketReader();
            reader.Reset(File.ReadAllBytes(path));
            replay = reader.Read<CombatReplay>();
            return true;
        }
        catch (Exception ex) {
            error = I18N.T("devmenu.replay.err.load", "Could not read this .mcr: {0}", ex.Message);
            return false;
        }
    }

    static bool TryPlayRun(string path, SceneTree tree, out string error) {
        error = "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            error = I18N.T("devmenu.runReplay.err.notFound", "DevTools replay file not found.");
            return false;
        }

        string[] lines;
        try {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) {
            error = I18N.T("devmenu.runReplay.err.load", "Could not read this replay: {0}", ex.Message);
            return false;
        }

        var log = ReplayFormat.Parse(lines);
        if (!ReplayFormat.IsPlayable(log.CoreVersion)) {
            error = log.CoreVersion > ReplayFormat.CoreVersion
                ? I18N.T(
                    "devmenu.runReplay.err.coreNewer",
                    "This replay is ReplayCore {0}; this build is {1}.",
                    log.CoreVersion,
                    ReplayFormat.CoreVersion)
                : I18N.T(
                    "devmenu.runReplay.err.coreOlder",
                    "This replay is ReplayCore {0}; this build supports {1}–{2}.",
                    log.CoreVersion,
                    ReplayFormat.MinSupportedCore,
                    ReplayFormat.CoreVersion);
            return false;
        }

        var commands = log.Commands.ToList();
        if (commands.Count == 0) {
            error = I18N.T("devmenu.runReplay.err.empty", "This replay has no commands.");
            return false;
        }

        CharacterModel? character =
            ModelDb.AllCharacters.FirstOrDefault(c => c.Id.Entry == log.CharacterId)
            ?? ModelDb.AllCharacters.FirstOrDefault();
        if (character == null || NGame.Instance == null) {
            error = I18N.T("devmenu.runReplay.err.start", "Could not start a run for this replay.");
            return false;
        }

        Stop();
        var rm = RunManager.Instance;
        if (rm is { IsInProgress: true })
            rm.CleanUp();

        _path = path;
        _tree = tree;
        _speedIndex = 1;
        _runSession = true;
        ReplayEngine.ContextChanged -= OnRunContextChanged;
        ReplayEngine.ContextChanged += OnRunContextChanged;
        ReplayDispatcher.GameSpeed = Speeds[_speedIndex];
        ReplayDispatcher.Load(commands);
        ReplayDispatcher.StartDispatchPoll();
        ReplayEngine.ActiveSeed = log.Seed;
        ReplayEngine.ActiveActs = log.Acts;
        ReplayEngine.IsReplayRun = true;
        _rooms.Clear();
        _rooms.Add(new ReplayRoomSegment(MapPointType.Ancient, RoomType.Event, null, IsStartingBonus: true));
        if (_seeking)
            ReplayDispatcher.SetSeeking(true);
        ShowHud?.Invoke(tree);
        FinishSeekIfReached();
        Changed?.Invoke();

        NAudioManager.Instance?.StopMusic();
        TaskHelper.RunSafely(
            NGame.Instance.StartNewSingleplayerRun(
                character,
                shouldSave: false,
                ActModel.GetDefaultList(),
                [],
                log.Seed,
                GameMode.Standard,
                log.Ascension));
        return true;
    }

    static void OnRunContextChanged() {
        FinishSeekIfReached();
        Changed?.Invoke();
    }

    static void RefreshRunRooms() {
        _rooms.Clear();
        // Neow / starting bonus is not a map-history entry; it happens before the first MapMove.
        _rooms.Add(new ReplayRoomSegment(MapPointType.Ancient, RoomType.Event, null, IsStartingBonus: true));
        if (RunStateProp?.GetValue(RunManager.Instance) is not RunState runState)
            return;
        foreach (var act in runState.MapPointHistory) {
            foreach (var entry in act) {
                var room = entry.Rooms.Count > 0 ? entry.Rooms[0] : null;
                bool useModel = entry.MapPointType is MapPointType.Boss or MapPointType.Ancient;
                _rooms.Add(new ReplayRoomSegment(
                    entry.MapPointType,
                    room?.RoomType ?? RoomType.Unassigned,
                    useModel ? room?.ModelId : null));
            }
        }
    }

    internal static void TogglePause() {
        if (!IsActive || IsFinished)
            return;
        if (_runSession) {
            ReplayDispatcher.Paused = !ReplayDispatcher.Paused;
            Changed?.Invoke();
            return;
        }
        SetPaused(!_paused);
    }

    internal static void CycleSpeed() {
        if (!IsActive || IsFinished)
            return;
        if (_runSession && IsManual)
            return;
        _speedIndex = (_speedIndex + 1) % Speeds.Length;
        if (_runSession && !_seeking)
            ReplayDispatcher.GameSpeed = Speeds[_speedIndex];
        else if (!_runSession)
            ApplyTimeScale();
        Changed?.Invoke();
    }

    internal static void ToggleMode() {
        if (_runSession) {
            ReplayDispatcher.Paused = !ReplayDispatcher.Paused;
            Changed?.Invoke();
            return;
        }
        SetManual(!_manual);
    }

    internal static bool IsLive => ReplayLiveMode.Enabled;

    internal static void ToggleLive() {
        ReplayLiveMode.Enabled = !ReplayLiveMode.Enabled;
        ReplayDispatcher.RefreshPacing();
        Changed?.Invoke();
    }

    internal static void SetManual(bool manual) {
        if (_manual == manual)
            return;
        _manual = manual;
        if (!manual)
            _stepToken++;
        ApplyTimeScale();
        Changed?.Invoke();
    }

    /// <summary>Manual mode: play the next recorded sequence (combat) or command (run).</summary>
    internal static void StepNext() {
        if (!CanStep)
            return;
        if (_runSession) {
            ReplayDispatcher.Step();
            Changed?.Invoke();
            return;
        }
        _stepToken++;
        Changed?.Invoke();
    }

    /// <summary>
    /// Jump the timeline to a room. Run replays restart and fast-forward when the
    /// target is already behind the playhead; combat <c>.mcr</c> files restart this fight.
    /// </summary>
    internal static void SeekToRoom(int roomIndex) {
        if (roomIndex < 0)
            return;
        if (!_runSession) {
            if (CanRestart)
                RestartFromBeginning();
            return;
        }
        if (_path == null)
            return;

        int past = CommandIndexForRoom(roomIndex);
        bool alreadyEntered = EventIndex > past;
        bool atStartOfRoom0 = roomIndex == 0 && EventIndex == 0 && !IsFinished;

        if (roomIndex == 0) {
            _seeking = false;
            ReplayDispatcher.SetSeeking(false);
            ReplayDispatcher.Paused = false;
            var neowPath = _path;
            var neowTree = _tree ?? NGame.Instance?.GetTree();
            if (neowTree == null || !TryPlay(neowPath, neowTree, out _))
                return;
            return;
        }

        _seekPastCommandIndex = past;
        _seeking = true;
        ReplayDispatcher.Paused = false;
        ReplayDispatcher.SetSeeking(true);

        if (alreadyEntered && !atStartOfRoom0) {
            var path = _path;
            var tree = _tree ?? NGame.Instance?.GetTree();
            if (tree == null || !TryPlay(path, tree, out _)) {
                FinishSeek();
                return;
            }
            return;
        }

        FinishSeekIfReached();
    }

    static int CommandIndexForRoom(int roomIndex) {
        if (roomIndex <= 0)
            return -1;
        int seen = 0;
        var cmds = ReplayEngine._loadedCommands;
        for (int i = 0; i < cmds.Count; i++) {
            if (cmds[i] is not MapMoveCommand)
                continue;
            seen++;
            if (seen == roomIndex)
                return i;
        }
        return cmds.Count > 0 ? cmds.Count - 1 : -1;
    }

    static void FinishSeekIfReached() {
        if (!_seeking)
            return;
        bool reached = EventIndex > _seekPastCommandIndex;
        if (reached)
            FinishSeek();
    }

    static void FinishSeek() {
        _seeking = false;
        ReplayDispatcher.SetSeeking(false);
        ReplayDispatcher.GameSpeed = Speeds[_speedIndex];
        Changed?.Invoke();
    }

    internal static void RestartFromBeginning() {
        if (!CanRestart || _path == null)
            return;
        _seeking = false;
        ReplayDispatcher.SetSeeking(false);
        var path = _path;
        var tree = _tree ?? NGame.Instance?.GetTree();
        Stop();
        if (tree != null)
            TryPlay(path, tree, out _);
    }

    internal static void ExitToMainMenu() {
        _seeking = false;
        ReplayDispatcher.SetSeeking(false);
        Stop();
        KitLibState.OnRunEnded();
        var game = NGame.Instance;
        if (game == null)
            return;
        TaskHelper.RunSafely(game.ReturnToMainMenu());
    }

    internal static void Stop() {
        bool wasRun = _runSession;
        if (_runSession) {
            ReplayEngine.ContextChanged -= OnRunContextChanged;
            ReplayDispatcher.Clear();
            _runSession = false;
        }
        _session++;
        _active = false;
        _paused = false;
        _ready = false;
        _rooms.Clear();
        if (!wasRun)
            RestoreTimeScale();
        Changed?.Invoke();
    }

    static void SetPaused(bool paused) {
        _paused = paused;
        try {
            var rm = RunManager.Instance;
            if (paused)
                rm?.ActionExecutor.Pause();
            else
                rm?.ActionExecutor.Unpause();
        }
        catch (Exception) {
        }

        try {
            var combat = CombatManager.Instance;
            if (combat == null)
                return;
            if (paused)
                combat.Pause();
            else
                combat.Unpause();
        }
        catch (Exception) {
        }

        ApplyTimeScale();
        Changed?.Invoke();
    }

    static void ApplyTimeScale() {
        if (_paused) {
            Engine.TimeScale = 0d;
            return;
        }
        Engine.TimeScale = _manual ? 1d : Speeds[_speedIndex];
    }

    static void RestoreTimeScale() {
        Engine.TimeScale = _savedTimeScale;
    }

    static async Task PlayAsync(CombatReplay replay, SceneTree tree) {
        int session = _session;
        _active = true;
        _paused = false;
        _finished = false;
        _ready = false;
        _speedIndex = 1;
        _eventIndex = 0;
        _eventCount = replay.events?.Count ?? 0;
        _savedTimeScale = Engine.TimeScale;
        Changed?.Invoke();

        var game = NGame.Instance;
        if (game == null) {
            Stop();
            return;
        }

        RunManager rm;
        RunState runState;
        try {
            await game.Transition.FadeOut();
            if (Cancelled(session))
                return;
            KitLibHost.StopAiPlayLoop?.Invoke();
            KitLibCheatApi.ResetSkipAnim?.Invoke();

            rm = RunManager.Instance
                ?? throw new InvalidOperationException("No RunManager instance.");
            if (rm.IsInProgress)
                rm.CleanUp();

            var localCommit = ReleaseInfoManager.Instance.ReleaseInfo?.Commit;
            if (!string.IsNullOrEmpty(localCommit) && replay.gitCommit != localCommit)
                MainFile.Logger.Warn($"Combat replay git commit {replay.gitCommit} != {localCommit}.");

            runState = RunState.FromSerializable(replay.serializableRun);
            if (runState.Players.Count == 0)
                throw new InvalidOperationException("Replay has no players.");
            CaptureRooms(runState);
            Changed?.Invoke();

            rm.SetUpReplay(runState, replay, runState.Players[0].NetId);
            rm.CombatStateSynchronizer.IsDisabled = true;
            AccessTools.Property(typeof(RunManager), "ShouldSave")?.SetValue(rm, false);

            await PreloadManager.LoadRunAssets(runState.Players.Select(p => p.Character));
            if (Cancelled(session))
                return;
            await PreloadManager.LoadActAssets(runState.Act);
            if (Cancelled(session))
                return;
            // Launch postfix injects InDevRun cheats; keep that off until after Launch.
            rm.Launch();
            if (Cancelled(session))
                return;
            KitLibState.InDevRun = true;
            AccessTools.Property(typeof(RunManager), "ShouldSave")?.SetValue(rm, false);
            NAudioManager.Instance?.StopMusic();
            game.RootSceneContainer.SetCurrentScene(NRun.Create(runState));
            await rm.GenerateMap();
            if (Cancelled(session))
                return;
            rm.ActionQueueSet.FastForwardNextActionId(replay.nextActionId);
            rm.ActionQueueSynchronizer.FastForwardHookId(replay.nextHookId);
            if (replay.checksumData is { Count: > 0 })
                rm.ChecksumTracker.LoadReplayChecksums(replay.checksumData, replay.nextChecksumId);
            rm.PlayerChoiceSynchronizer.FastForwardChoiceIds(replay.choiceIds);
            rm.RewardsSetSynchronizer.FastForwardRewardIds(replay.rewardIds);
            await rm.LoadIntoLatestMapCoord(
                AbstractRoom.FromSerializable(replay.serializableRun.PreFinishedRoom, runState));
            if (Cancelled(session))
                return;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"Combat replay failed: {ex.Message}");
            KitLibState.OnRunEnded();
            Stop();
            try {
                await game.Transition.FadeIn();
            }
            catch (Exception fadeEx) {
                MainFile.Logger.Warn($"Combat replay FadeIn failed: {fadeEx.Message}");
            }
            return;
        }

        try {
            await game.Transition.FadeIn();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"Combat replay FadeIn failed: {ex.Message}");
        }
        if (Cancelled(session))
            return;

        _ready = true;
        ApplyTimeScale();
        Changed?.Invoke();

        while (_active && session == _session && rm.ActionExecutor.IsPaused)
            await tree.Root.AwaitProcessFrame();

        var events = replay.events ?? [];
        int i = 0;
        while (i < events.Count) {
            if (Cancelled(session))
                break;
            await WaitIfPaused(tree, rm, session);
            if (Cancelled(session))
                break;

            if (IsSequenceStart(events[i].eventType)) {
                await WaitForManualStep(tree, session);
                if (Cancelled(session))
                    break;
                if (ReplayLiveMode.Enabled)
                    await WaitScaled(tree, session, 0.85f);
                if (Cancelled(session))
                    break;
            }

            bool pumpedAction = false;
            do {
                _eventIndex = i + 1;
                Changed?.Invoke();
                pumpedAction |= await PumpEvent(tree, rm, runState, events[i], session);
                i++;
            } while (i < events.Count && IsInternalFollowUp(events[i].eventType) && !Cancelled(session));

            if (Cancelled(session))
                break;
            if (pumpedAction)
                await rm.ActionExecutor.FinishedExecutingActions();
            if (!_manual)
                await WaitScaled(tree, session, AutoSettleSeconds);
        }

        if (Cancelled(session))
            return;
        _finished = true;
        _paused = false;
        RestoreTimeScale();
        Changed?.Invoke();
    }

    static void CaptureRooms(RunState runState) {
        _rooms.Clear();
        foreach (var act in runState.MapPointHistory) {
            foreach (var entry in act) {
                var room = entry.Rooms.Count > 0 ? entry.Rooms[0] : null;
                bool useModel = entry.MapPointType is MapPointType.Boss or MapPointType.Ancient;
                _rooms.Add(new ReplayRoomSegment(
                    entry.MapPointType,
                    room?.RoomType ?? RoomType.Unassigned,
                    useModel ? room?.ModelId : null));
            }
        }

        if (_rooms.Count == 0) {
            var current = runState.CurrentRoom;
            _rooms.Add(new ReplayRoomSegment(
                MapPointType.Monster,
                current?.RoomType ?? RoomType.Monster,
                null));
        }
    }

    static bool Cancelled(int session) => !_active || session != _session;

    static bool IsSequenceStart(CombatReplayEventType type) =>
        type is CombatReplayEventType.GameAction or CombatReplayEventType.PlayerChoice;

    static bool IsInternalFollowUp(CombatReplayEventType type) =>
        type is CombatReplayEventType.HookAction or CombatReplayEventType.ResumeAction;

    static async Task WaitIfPaused(SceneTree tree, RunManager rm, int session) {
        while (!Cancelled(session) && (_paused || rm.ActionExecutor.IsPaused))
            await tree.Root.AwaitProcessFrame();
    }

    static async Task WaitForManualStep(SceneTree tree, int session) {
        if (!_manual)
            return;
        int seen = _stepToken;
        while (!Cancelled(session) && _manual && _stepToken == seen)
            await tree.Root.AwaitProcessFrame();
    }

    static async Task WaitScaled(SceneTree tree, int session, float seconds) {
        if (seconds <= 0f)
            return;
        var timer = tree.CreateTimer(seconds);
        await tree.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        if (Cancelled(session))
            return;
    }

    static async Task<bool> PumpEvent(
        SceneTree tree,
        RunManager rm,
        RunState runState,
        CombatReplayEvent replayEvent,
        int session) {
        switch (replayEvent.eventType) {
            case CombatReplayEventType.GameAction:
                await PumpGameAction(tree, runState, replayEvent, session);
                return true;
            case CombatReplayEventType.HookAction:
                rm.ActionQueueSet.EnqueueWithoutSynchronizing(
                    rm.ActionQueueSynchronizer.GetHookActionForId(
                        replayEvent.hookId!.Value,
                        replayEvent.playerId!.Value,
                        replayEvent.gameActionType!.Value));
                return true;
            case CombatReplayEventType.ResumeAction:
                rm.ActionQueueSet.ResumeActionWithoutSynchronizing(replayEvent.actionId!.Value);
                return true;
            case CombatReplayEventType.PlayerChoice: {
                    var player = runState.GetPlayer(replayEvent.playerId!.Value);
                    rm.PlayerChoiceSynchronizer.ReceiveReplayChoice(
                        player, replayEvent.choiceId!.Value, replayEvent.playerChoiceResult!.Value);
                    return false;
                }
            default:
                return false;
        }
    }

    static async Task PumpGameAction(SceneTree tree, RunState runState, CombatReplayEvent replayEvent, int session) {
        var combat = CombatManager.Instance;
        while (!Cancelled(session) && combat is { EndingPlayerTurnPhaseOne: true } or { EndingPlayerTurnPhaseTwo: true })
            await tree.Root.AwaitProcessFrame();
        if (Cancelled(session))
            return;

        var player = runState.GetPlayer(replayEvent.playerId!.Value);
        var action = replayEvent.action!.ToGameAction(player);
        if (action.ActionType == GameActionType.CombatPlayPhaseOnly) {
            while (!Cancelled(session) && CombatManager.Instance?.DebugOnlyGetState()?.CurrentSide == CombatSide.Enemy)
                await tree.Root.AwaitProcessFrame();
            if (Cancelled(session))
                return;
        }

        RunManager.Instance.ActionQueueSet.EnqueueWithoutSynchronizing(action);
    }
}
