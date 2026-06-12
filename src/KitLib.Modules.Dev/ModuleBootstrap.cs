using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.AI.Combat.Simulation;
using KitLib.CombatStats;
using KitLib.EnemyIntent;
using KitLib.Feedback;
using KitLib.Host;
using KitLib.Interop;
using KitLib.Mcp;
using KitLib.Patches;
using KitLib.Scripts;

namespace KitLib.Dev;

/// <summary>Heavy Dev init; runtime wiring runs from <see cref="KitLibProcessNode"/> and <see cref="DevModelDbInitPatch"/>.</summary>
internal static class ModuleBootstrap {
    private static bool _completed;
    private static bool _completing;
    private static bool _harmonyApplied;

    internal static bool IsBootstrapComplete => _completed;
    private static bool _bridgesStarted;

    /// <summary>Starts HTTP bridges on demand when the Scripts panel is opened.</summary>
    internal static void EnsureBridgesStarted() {
        if (_bridgesStarted || !KitLibBootstrapGate.CanStartHttpListener)
            return;
        _bridgesStarted = true;
        Callable.From(StartDeferredBridges).CallDeferred();
    }

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
            KitLibHost.IsDualInstanceActive = KitLibInstanceRegistry.IsDualInstanceActive;

            EnsureHarmonyApplied();
            ApplyManualHookPatches();

            KitLibInstanceRegistry.Register();

            SafeStep("ScriptManager", () => ScriptManager.Initialize());
            SafeStep("FrameworkBridge", () => FrameworkBridge.Initialize());
            SafeStep("CombatStatsTracker", () => CombatStatsTracker.Initialize());
            SafeStep("MonsterIntentOverlayTracker", () => MonsterIntentOverlayTracker.Initialize());
            SafeStep("MonsterIntentOverrides", () => MonsterIntentOverrides.Initialize());

            KitLibHost.CaptureMonsterIntentSteps = (enemy, targets, pressure) =>
                MonsterIntentReader.CaptureIntentSteps(enemy, targets, (CombatState)pressure);

            SafeStep("DevTabRegistration", () => DevTabRegistration.Register());

            CrashRecoveryHooks.RegisterHandlers();
            CrashRecoveryStore.MarkSessionStarted();
            Callable.From(CrashRecoveryHooks.EnsureLifecycleNode).CallDeferred();

            _completed = true;
            KitLibBootstrapGate.EnterInteractive();
        }
        finally {
            _completing = false;
        }
    }

    static void StartDeferredBridges() {
        if (!KitLibBootstrapGate.CanStartHttpListener)
            return;
        SafeStep("ScriptBridge", () => ScriptBridge.StartCore());
        SafeStep("McpBridge", () => McpBridge.StartCore());
    }

    static Harmony ResolveDevHarmony() => KitLibHarmony.GetOrCreate(KitLibModuleIds.Dev);

    static void SafeStep(string name, Action action) {
        try {
            action();
        }
        catch (Exception ex) {
            BootstrapDiagnostics.RecordFailure(name, ex);
        }
    }
}
