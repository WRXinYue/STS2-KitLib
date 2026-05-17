using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevMode.Actions;
using DevMode.Actions.CardModes;
using DevMode.Icons;
using DevMode.Navigation;
using DevMode.Panels;
using DevMode.UI;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode;

/// <summary>
/// Facade / coordinator — delegates to specialized modules:
///   UI       → <see cref="DevPanelUI"/>
///   Cards    → <see cref="CardActions"/>
///   Relics   → <see cref="RelicActions"/>
///   Nav      → <see cref="NavigationHelper"/>
///   Context  → <see cref="RunContext"/>
/// </summary>
internal static class DevPanel {
    // ──────── ActionSession ────────
    // Runs one async action at a time. Calling Cancel() before a new Run()
    // prevents the old action's completion callback from firing, eliminating
    // the race between TryDismissCurrent and in-flight async wrappers.

    internal sealed class ActionSession {
        private int _gen;
        public bool IsBusy { get; private set; }

        /// <summary>Invalidate the in-flight action (if any).</summary>
        public void Cancel() {
            _gen++;
            IsBusy = false;
        }

        /// <summary>
        /// Run <paramref name="work"/> asynchronously.
        /// <paramref name="onCompleted"/> is only called when the action finishes
        /// naturally — not when it was superseded by Cancel() or a newer Run().
        /// </summary>
        public void Run(Func<Task> work, string label, Action onCompleted) {
            IsBusy = true;
            int myGen = ++_gen;
            TaskHelper.RunSafely(Execute(work, label, myGen, onCompleted));
        }

        private async Task Execute(Func<Task> work, string label, int myGen, Action onCompleted) {
            try { await work(); }
            catch (Exception ex) { MainFile.Logger.Warn($"DevPanel: {label} failed: {ex.Message}"); }
            finally {
                IsBusy = false;
                if (_gen == myGen)
                    onCompleted();
            }
        }
    }

    // ──────── State ────────

    private static readonly ActionSession _session = new();
    private static NGlobalUi? _globalUi;

    private static readonly Dictionary<CardMode, ICardModeHandler> _cardHandlers = new() {
        [CardMode.View] = new ViewModeHandler(),
        [CardMode.Add] = new AddModeHandler(),
        [CardMode.Upgrade] = new UpgradeModeHandler(),
        [CardMode.Delete] = new DeleteModeHandler(),
        [CardMode.Edit] = new EditModeHandler(),
    };

    private static ICardModeHandler CurrentCardHandler
        => _cardHandlers[DevModeState.CardMode];

    // ──────── Lifecycle ────────

    public static void Attach(NGlobalUi globalUi) {
        try {
            _globalUi = globalUi;

            var actions = new DevPanelActions {
                OnNewTest = StartNewTest,
                OnRefreshPanel = RefreshPanel,
                OnCycleGameSpeed = SpeedControl.CycleSpeed,
                GetGameSpeedLabel = SpeedControl.GetLabel,
                OnToggleSkipAnim = SkipAnimControl.Toggle,
                GetSkipAnimLabel = SkipAnimControl.GetLabel,
            };

            RegisterBuiltInTabs(globalUi, actions);

            DevPanelUI.Attach(globalUi, actions);
            ((Node)globalUi).TreeExiting += () => Detach(globalUi);

            MainFile.Logger.Info("DevPanel: Sidebar attached.");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"DevPanel: Failed to attach sidebar: {ex.Message}");
        }
    }

    public static void Detach(NGlobalUi globalUi) {
        try {
            DevPanelRegistry.DeactivateAll(globalUi);
            DevPanelUI.Detach(globalUi);
            ClearState();
            SpeedControl.Reset();
            SkipAnimControl.Reset();
            _globalUi = null;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"DevPanel: Failed to detach: {ex.Message}");
        }
    }

    // ──────── Built-in Tab Registration ────────

    private static void RegisterBuiltInTabs(NGlobalUi globalUi, DevPanelActions actions) {
        // Primary group — main feature panels
        DevPanelRegistry.Register("devmode.cards", MdiIcon.Cards, I18N.T("panel.cards", "Cards"), 100, DevPanelTabGroup.Primary, _ => OpenCards());
        DevPanelRegistry.Register("devmode.relics", MdiIcon.From("diamond-stone"), I18N.T("panel.relics", "Relics"), 200, DevPanelTabGroup.Primary, _ => OpenRelics());
        DevPanelRegistry.Register("devmode.enemies", MdiIcon.Skull, I18N.T("panel.enemies", "Enemies"), 300, DevPanelTabGroup.Primary, _ => OpenEnemies());
        DevPanelRegistry.Register("devmode.powers", MdiIcon.Flash, I18N.T("panel.powers", "Powers"), 400, DevPanelTabGroup.Primary, _ => OpenPowers());
        DevPanelRegistry.Register("devmode.potions", MdiIcon.From("flask-outline"), I18N.T("panel.potions", "Potions"), 500, DevPanelTabGroup.Primary, _ => OpenPotions());
        DevPanelRegistry.Register("devmode.events", MdiIcon.CalendarStar, I18N.T("panel.events", "Events"), 600, DevPanelTabGroup.Primary, _ => OpenEvents());
        DevPanelRegistry.Register("devmode.rooms", MdiIcon.MapMarker, I18N.T("panel.rooms", "Rooms"), 650, DevPanelTabGroup.Primary, _ => OpenRooms());
        DevPanelRegistry.Register("devmode.console", MdiIcon.Console, I18N.T("panel.console", "Console"), 700, DevPanelTabGroup.Primary, _ => OpenConsole());
        DevPanelRegistry.Register("devmode.cheats", MdiIcon.Star, I18N.T("panel.cheats", "Cheats"), 750, DevPanelTabGroup.Primary, gui => DevPanelUI.ShowCheatsOverlay(gui, actions));
        DevPanelRegistry.Register("devmode.presets", MdiIcon.From("book-open-variant"), I18N.T("panel.presets", "Presets"), 800, DevPanelTabGroup.Primary, _ => OpenPresets());
        DevPanelRegistry.Register("devmode.hooks", MdiIcon.LightningBolt, I18N.T("panel.hooks", "Hooks"), 900, DevPanelTabGroup.Primary, _ => OpenHooks());
        DevPanelRegistry.Register("devmode.scripts", MdiIcon.PuzzleOutline, I18N.T("panel.scripts", "Scripts"), 950, DevPanelTabGroup.Primary, _ => OpenScripts());
        DevPanelRegistry.Register("devmode.logs", MdiIcon.TextBoxOutline, I18N.T("panel.logs", "Logs"), 960, DevPanelTabGroup.Primary, _ => OpenLogs(), null,
            DevPanelTabKind.Developer);
        DevPanelRegistry.Register("devmode.harmonyAnalysis", MdiIcon.Magnify, I18N.T("panel.harmonyAnalysis", "Harmony analysis"), 962, DevPanelTabGroup.Primary,
            _ => OpenHarmonyAnalysis(), null, DevPanelTabKind.Developer);
        DevPanelRegistry.Register("devmode.frameworks", MdiIcon.FilterVariant, I18N.T("panel.frameworks", "Frameworks"), 965, DevPanelTabGroup.Primary,
            _ => OpenFrameworks(), null, DevPanelTabKind.Developer);
        DevPanelRegistry.Register("devmode.feedback", MdiIcon.BugOutline, I18N.T("panel.feedback", "Mod Feedback"), 970, DevPanelTabGroup.Primary,
            _ => OpenFeedback(), null, DevPanelTabKind.Developer);

        // Utility group — settings / tools
        DevPanelRegistry.Register("devmode.save", MdiIcon.ContentSave, I18N.T("panel.save", "Save / Load"), 100, DevPanelTabGroup.Utility, gui => DevPanelUI.ShowSaveLoadOverlay(gui, actions));
        DevPanelRegistry.Register("devmode.settings", MdiIcon.Cog, I18N.T("panel.settings", "Settings"), 200, DevPanelTabGroup.Utility, gui => DevPanelUI.ShowSettingsOverlay(gui, actions));

    }

    // ──────── Panel Openers ────────

    private static void OpenCards() {
        if (!TryDismissCurrent()) return;
        DevModeState.ActivePanel = ActivePanel.Cards;
        DevPanelUI.UpdateTopBar(_globalUi!, CardTopBarConfig.None);

        if (_globalUi == null) return;
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player)) return;
        CardBrowserUI.Show(_globalUi, state, player);
    }

    private static void OpenRelics() {
        if (!TryDismissCurrent()) return;
        DevModeState.ActivePanel = ActivePanel.Relics;
        DevPanelUI.UpdateTopBar(_globalUi!, CardTopBarConfig.None);

        if (_globalUi == null) return;
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player)) return;
        RelicBrowserUI.Show(_globalUi, state, player);
    }

    private static void OpenEnemies() {
        if (_globalUi == null) return;
        DevModeState.ActivePanel = ActivePanel.Enemies;
        UpdateTopBar();

        // Wire up the combat kill callback
        DevPanelUI.SetCombatKillCallback(() => {
            if (_globalUi != null)
                EnemySelectUI.ShowEnemyKillPicker(_globalUi);
        });

        // Check if we're in combat — if so, default to showing the monster picker
        var combatState = CombatEnemyActions.GetCombatState();

        switch (DevModeState.EnemyMode) {
            case EnemyMode.Global:
                if (combatState != null) {
                    // In combat: show encounter picker to add enemies
                    EnemySelectUI.Show(_globalUi, null, enc => {
                        TaskHelper.RunSafely(CombatEnemyActions.AddEncounterMonsters(enc));
                    });
                }
                else {
                    EnemySelectUI.Show(_globalUi, null, enc => {
                        EnemyActions.SetGlobalOverride(enc);
                        UpdateTopBar();
                    });
                }
                break;

            case EnemyMode.PerType:
                ShowRoomTypePicker();
                break;

            case EnemyMode.Off:
                if (combatState != null) {
                    // In combat with no override mode: show encounter picker
                    EnemySelectUI.Show(_globalUi, null, enc => {
                        TaskHelper.RunSafely(CombatEnemyActions.AddEncounterMonsters(enc));
                    });
                }
                else {
                    EnemySelectUI.ShowFloorPicker(_globalUi);
                }
                break;
        }
    }

    private static void ShowRoomTypePicker() {
        if (_globalUi == null) return;

        // Show encounter selector filtered by each room type in sequence
        // For simplicity, show the full selector with filter tabs
        EnemySelectUI.Show(_globalUi, RoomType.Monster, enc => {
            EnemyActions.SetRoomTypeOverride(enc.RoomType, enc);
            UpdateTopBar();
        });
    }

    private static void OpenPowers() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Powers;
        UpdateTopBar();

        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return;

        PowerSelectUI.Show(_globalUi, player);
    }

    private static void OpenPotions() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Potions;
        UpdateTopBar();

        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return;

        PotionSelectUI.Show(_globalUi, player);
    }

    private static void OpenEvents() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Events;
        UpdateTopBar();

        EventSelectUI.Show(_globalUi, evt => {
            EventActions.TryForceEnterEvent(evt);
        });
    }

    private static void OpenRooms() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Rooms;
        UpdateTopBar();

        RoomSelectUI.Show(_globalUi);
    }

    private static void OpenConsole() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Console;
        UpdateTopBar();

        ConsoleUI.Show(_globalUi);
    }

    private static void OpenPresets() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Presets;
        UpdateTopBar();

        PresetUI.Show(_globalUi);
    }

    private static void OpenHooks() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Hooks;
        UpdateTopBar();

        HookConfigUI.Show(_globalUi);
    }

    private static void OpenScripts() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Scripts;
        UpdateTopBar();

        ScriptUI.Show(_globalUi);
    }

    private static void OpenLogs() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Logs;
        UpdateTopBar();

        LogViewerUI.Show(_globalUi);
    }

    private static void OpenHarmonyAnalysis() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.HarmonyAnalysis;
        UpdateTopBar();

        HarmonyAnalysisUI.Show(_globalUi);
    }

    private static void OpenFrameworks() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Frameworks;
        UpdateTopBar();

        FrameworkBridgeUI.Show(_globalUi);
    }

    private static void OpenFeedback() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        DevModeState.ActivePanel = ActivePanel.Feedback;
        UpdateTopBar();

        FeedbackReportUI.Show(_globalUi);
    }

    private static void StartNewTest() {
        try {
            var game = NGame.Instance;
            var rm = RunManager.Instance;
            if (game == null || rm == null) return;

            TaskHelper.RunSafely(StartNewTestAsync(game, rm));
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"DevPanel: StartNewTest failed: {ex.Message}");
        }
    }

    private static async Task StartNewTestAsync(NGame game, RunManager rm) {
        await game.Transition.FadeOut();

        if (rm.IsInProgress)
            rm.CleanUp();

        await game.ReturnToMainMenu();
        await game.Transition.FadeIn();

        MainFile.Logger.Info("DevPanel: Returned to main menu for new test run.");
    }

    private static void RefreshPanel() {
        switch (DevModeState.ActivePanel) {
            case ActivePanel.Cards: OpenCards(); break;
            case ActivePanel.Relics: OpenRelics(); break;
            case ActivePanel.Enemies: OpenEnemies(); break;
            case ActivePanel.Powers: OpenPowers(); break;
            case ActivePanel.Potions: OpenPotions(); break;
            case ActivePanel.Events: OpenEvents(); break;
            case ActivePanel.Rooms: OpenRooms(); break;
            case ActivePanel.Console: OpenConsole(); break;
            case ActivePanel.Presets: OpenPresets(); break;
            case ActivePanel.Hooks: OpenHooks(); break;
            case ActivePanel.Scripts: OpenScripts(); break;
            case ActivePanel.Logs: OpenLogs(); break;
            case ActivePanel.HarmonyAnalysis: OpenHarmonyAnalysis(); break;
            case ActivePanel.Frameworks: OpenFrameworks(); break;
            case ActivePanel.Feedback: OpenFeedback(); break;
            case ActivePanel.CardEdit: break;
        }
    }

    // ──────── Panel Switching ────────

    private static bool TryDismissCurrent() {
        if (_session.IsBusy) RunContext.Clear();
        _session.Cancel();                     // invalidate before closing overlays
        NavigationHelper.CloseCapstone();
        NavigationHelper.CloseOverlays();

        // Dismiss all DevMode overlay panels in one shot
        if (_globalUi != null)
            DevPanelUI.CloseAllOverlays(_globalUi);

        return true;
    }

    // ──────── Interception Handlers (called from Harmony patches) ────────

    /// <summary>
    /// Card selection is now handled entirely by CardBrowserUI's self-drawn grid.
    /// This handler remains for backward compatibility with existing Harmony patches.
    /// </summary>
    public static bool TryHandleCardSelection(NCardHolder holder) {
        return false;
    }

    public static bool TryHandleRelicSelection(NRelicCollectionEntry entry) {
        if (DevModeState.ActivePanel != ActivePanel.Relics || DevModeState.RelicMode != RelicMode.Add)
            return false;

        if (entry?.relic == null) return true;

        if (!RunContext.TryResolvePending(out _, out var player)) {
            ClearState();
            return true;
        }

        TaskHelper.RunSafely(RelicActions.AddRelic(entry.relic.CanonicalInstance, player));
        return true;
    }

    // ──────── Lifecycle Notifications ────────

    /// <summary>
    /// Called when the official NCardLibrary closes. Since CardBrowserUI is now
    /// self-drawn this is mostly a no-op, but kept for safety in case the user
    /// opens the official library through the pause menu.
    /// </summary>
    public static void NotifyCardLibraryClosed() {
        // Only reset if we were actively using the Cards panel
        if (DevModeState.ActivePanel != ActivePanel.Cards) return;
        ResetPanel();
    }

    public static void NotifyRelicCollectionClosed() {
        if (DevModeState.ActivePanel != ActivePanel.Relics) return;
        ResetPanel();
        ClearState();
    }

    // ──────── Private ────────

    internal static void ResetPanel() {
        DevModeState.ActivePanel = ActivePanel.None;
        UpdateTopBar();
    }

    private static void UpdateTopBar() {
        if (_globalUi == null) return;
        // CardBrowserUI has its own integrated UI; TopBar not needed for cards
        DevPanelUI.UpdateTopBar(_globalUi, CardTopBarConfig.None);
    }

    private static void ClearState() {
        RunContext.Clear();
    }
}
