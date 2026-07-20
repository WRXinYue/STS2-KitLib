using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.AI.Combat.Simulation;
using KitLib.CombatStats;
using KitLib.Diagnostics;
using KitLib.EnemyIntent;
using KitLib.Feedback;
using KitLib.Host;
using KitLib.Interop;
using KitLib.Mcp;
using KitLib.Patches;

namespace KitLib.Dev;

/// <summary>Heavy Dev init; runtime wiring runs from <see cref="KitLibProcessNode"/> and <see cref="DevModelDbInitPatch"/>.</summary>
internal static class ModuleBootstrap {
    private static bool _completed;
    private static bool _completing;
    private static bool _harmonyApplied;

    internal static bool IsBootstrapComplete => _completed;

    /// <summary>Receives Core-pinned mod_data path; Dev cannot read Core static fields across KitLib copies.</summary>
    internal static void AdoptPinnedModDataDir(string path) {
        if (string.IsNullOrWhiteSpace(path))
            return;
        DevModDataPaths.SetRoot(path);
    }

    /// <summary>Core satellite loader applies Dev Harmony in its ALC; Dev copy of <see cref="KitLibHarmony"/> cannot see that flag.</summary>
    internal static void MarkHarmonyAppliedByHost() => _harmonyApplied = true;

    /// <summary>Apply Dev Harmony during mod init so <see cref="DevModelDbInitPatch"/> is active before <c>ModelDb.Init</c>.</summary>
    internal static void EnsureHarmonyApplied() {
        if (_harmonyApplied)
            return;

        SafeStep("Harmony", () => KitLibHarmony.Apply(typeof(ModuleBootstrap).Assembly, KitLibModuleIds.Dev));
        _harmonyApplied = true;
    }

    internal static void ApplyManualHookPatches() {
        var harmony = ResolveDevHarmony();
        SafeStep("ScriptCardPlayedPatch", () => {
            ScriptCardPlayedPatch.TryApply(harmony);
        });
        SafeStep("ScriptShufflePatch", () => {
            ScriptShufflePatch.TryApply(harmony);
        });
    }

    internal static void Complete() {
        if (_completed)
            return;
        if (_completing) {
            return;
        }

        KitLibBootstrapGate.EnterSceneReadyBootstrap();
        _completing = true;
        try {
            KitLibHost.IsDualInstanceActive = KitLibProcessScope.IsDualInstanceActive;

            EnsureHarmonyApplied();
            ApplyManualHookPatches();

            SafeStep("FrameworkBridge", () => FrameworkBridge.Initialize());
            SafeStep("CombatStatsTracker", () => CombatStatsTracker.Initialize());
            SafeStep("MonsterIntentOverlayTracker", () => MonsterIntentOverlayTracker.Initialize());
            SafeStep("MonsterIntentOverrides", () => MonsterIntentOverrides.Initialize());

            KitLibHost.CaptureMonsterIntentSteps = (enemy, targets, pressure) =>
                MonsterIntentReader.CaptureIntentSteps(enemy, targets, (CombatState)pressure);

            SafeStep("DevTabRegistration", () => DevTabRegistration.Register());

            Callable.From(StartDeferredMcpBridge).CallDeferred();
            WireDevViewerOps();

            _completed = true;
            KitLibBootstrapGate.EnterInteractive();
            KitLibStartupAudit.LogReport("initialization");
        }
        finally {
            _completing = false;
        }
    }

    static void WireDevViewerOps() {
        KitLibDevOps.TryOpenDevViewerLogs = query => {
            try {
                DevViewerServer.OpenLogsInBrowser(query, force: true);
                return true;
            }
            catch (Exception ex) {
                KitLog.Warn("DevViewer", $"Open logs failed: {ex.Message}");
                return false;
            }
        };
        KitLibDevOps.TryScheduleDevViewerLogsOnStartup = query => {
            try {
                DevViewerServer.ScheduleOpenLogsIfNoClient(query);
                return true;
            }
            catch (Exception ex) {
                KitLog.Warn("DevViewer", $"Startup open schedule failed: {ex.Message}");
                return false;
            }
        };
    }

    static void StartDeferredMcpBridge() {
        if (!KitLibBootstrapGate.CanStartHttpListener)
            return;
        SafeStep("McpBridge", () => McpBridge.StartCore());
    }

    static Harmony ResolveDevHarmony() => KitLibHarmony.GetOrCreate(KitLibModuleIds.Dev);

    static void SafeStep(string name, Action action) {
        try {
            KitLibStartupAudit.Measure($"dev.{name}", action);
        }
        catch (Exception ex) {
            BootstrapDiagnostics.RecordFailure(name, ex);
        }
    }
}
