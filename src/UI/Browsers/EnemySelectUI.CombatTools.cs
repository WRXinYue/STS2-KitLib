using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    internal static bool IsCombatToolsVisible =>
        KitLibState.IsActive
        && CombatManager.Instance?.IsInProgress == true
        && CombatEnemyActions.GetCombatState() != null;

    private static IReadOnlyList<Creature> GetLivingEnemies() =>
        CombatEnemyActions.GetCurrentEnemies().Where(e => !e.IsDead).ToList();

    internal static void BuildCurrentCombatDetailSection(
        VBoxContainer host,
        NGlobalUi globalUi,
        Action? onChanged = null) {
        if (!IsCombatToolsVisible)
            return;

        var combatTitle = new Label {
            Text = I18N.T("enemy.currentCombatTitle", "Current combat"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        combatTitle.AddThemeFontSizeOverride("font_size", 13);
        host.AddChild(combatTitle);
        host.AddChild(MakeSubtleLabel(
            I18N.T("enemy.currentCombatHint", "Affects this fight only · not saved to run rules.")));

        var enemies = GetLivingEnemies();
        if (enemies.Count == 0) {
            host.AddChild(MakeSubtleLabel(
                I18N.T("enemy.noLiveEnemies", "No live enemies in combat.")));
        }
        else {
            foreach (var enemy in enemies)
                host.AddChild(CreateCombatEnemyRow(enemy, onChanged));
        }

        var addBtn = new Button {
            Text = I18N.T("enemy.combatSidebar.addMenu", "Add enemies to combat"),
            CustomMinimumSize = new Vector2(0, 32),
            FocusMode = Control.FocusModeEnum.None,
        };
        addBtn.Pressed += () => OpenCombatAddPicker(globalUi, onChanged);
        host.AddChild(addBtn);

        if (enemies.Count > 0) {
            var killAllBtn = new Button {
                Text = I18N.T("enemy.combatSidebar.killAll", "Kill all enemies"),
                CustomMinimumSize = new Vector2(0, 32),
                FocusMode = Control.FocusModeEnum.None,
            };
            killAllBtn.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
            killAllBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.55f, 0.55f));
            killAllBtn.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.35f, 0.35f));
            killAllBtn.Pressed += () => RunSyncedKillAll(onChanged);
            host.AddChild(killAllBtn);
        }

        host.AddChild(new HSeparator());
    }

    private static Control CreateCombatEnemyRow(Creature enemy, Action? onChanged) {
        string name = enemy.Monster?.Title?.GetFormattedText() ?? I18N.T("enemy.unknownName", "???");
        string hp = $"{enemy.CurrentHp}/{enemy.MaxHp}";

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var textBox = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        textBox.AddThemeConstantOverride("separation", 1);
        var nameLabel = new Label {
            Text = name,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        textBox.AddChild(nameLabel);
        var hpLabel = new Label { Text = hp };
        hpLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        hpLabel.AddThemeFontSizeOverride("font_size", 11);
        textBox.AddChild(hpLabel);
        row.AddChild(textBox);

        var captured = enemy;
        var killBtn = new Button {
            Text = I18N.T("enemy.combatKill", "Kill"),
            CustomMinimumSize = new Vector2(56, 30),
            FocusMode = Control.FocusModeEnum.None,
        };
        killBtn.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
        killBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.55f, 0.55f));
        killBtn.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.35f, 0.35f));
        killBtn.Pressed += () => RunSyncedKillEnemy(captured, onChanged);
        row.AddChild(killBtn);

        return row;
    }

    internal static void RunSyncedKillEnemy(Creature enemy, Action? onChanged = null) {
        if (!MpCheatSession.InMultiplayerRun) {
            DevPanelUI.RunCombatAction(() => CombatEnemyActions.KillEnemy(enemy), onChanged);
            return;
        }
        if (!MpCheatSession.CanUseMultiplayerCheats) return;
        TaskHelper.RunSafely(SyncKillEnemyAsync());

        async System.Threading.Tasks.Task SyncKillEnemyAsync() {
            var result = MpCheatSession.IsHost
                ? await MpCheatCombatEnemyCoordinator.TryHostKillEnemyAsync(enemy)
                : await MpCheatCombatEnemyCoordinator.TryClientRequestKillEnemyAsync(enemy);
            MainFile.Logger.Info($"[MpCheat] Combat kill result: {result}");
            RefreshCombatContext();
            onChanged?.Invoke();
        }
    }

    internal static void RunSyncedKillAll(Action? onChanged = null) {
        if (!MpCheatSession.InMultiplayerRun) {
            DevPanelUI.RunCombatAction(CombatEnemyActions.KillAllEnemies, onChanged);
            return;
        }
        if (!MpCheatSession.CanUseMultiplayerCheats) return;
        TaskHelper.RunSafely(SyncKillAllAsync());

        async System.Threading.Tasks.Task SyncKillAllAsync() {
            var result = await MpCheatCombatEnemyCoordinator.TryHostKillAllAsync();
            MainFile.Logger.Info($"[MpCheat] Combat kill all result: {result}");
            RefreshCombatContext();
            onChanged?.Invoke();
        }
    }

    internal static void OpenCombatAddPicker(NGlobalUi globalUi, Action? onChanged = null) {
        Action<EncounterModel> onEncounter = enc => RunSyncedCombatAdd(enc, onChanged);
        Action<MonsterModel> onMonster = mon => RunSyncedCombatAddMonster(mon, onChanged);

        if (IsMainPanelOpen) {
            ShowCombatAddInExtension(globalUi, null, onEncounter, onMonster);
            return;
        }

        ShowCombatAddPickerModal(globalUi, null, onEncounter, onMonster);
    }

    internal static void RunSyncedCombatAddMonster(MonsterModel monster, Action? onChanged = null) {
        MainFile.Logger.Info(
            $"[KitLib.CombatAdd] UI request: {((AbstractModel)monster).Id.Entry} mp={MpCheatSession.InMultiplayerRun}");
        if (!MpCheatSession.InMultiplayerRun) {
            DevPanelUI.RunCombatAction(async () => { await CombatEnemyActions.AddMonster(monster); }, onChanged);
            return;
        }

        if (!MpCheatSession.CanUseMultiplayerCheats) {
            MainFile.Logger.Warn(
                I18N.T(
                    "mpcheat.blocked",
                    "Multiplayer cheat inactive: {0}",
                    MpCheatSession.LastBlockReason ?? "unknown"));
            return;
        }

        TaskHelper.RunSafely(SyncAddMonsterAsync());

        async System.Threading.Tasks.Task SyncAddMonsterAsync() {
            var result = MpCheatSession.IsHost
                ? await MpCheatCombatEnemyCoordinator.TryHostAddMonsterAsync(monster)
                : await MpCheatCombatEnemyCoordinator.TryClientRequestAddMonsterAsync(monster);
            MainFile.Logger.Info($"[MpCheat] Combat add monster result: {result}");
            RefreshCombatContext();
            onChanged?.Invoke();
        }
    }

    internal static void RunSyncedCombatAdd(EncounterModel encounter, Action? onChanged = null) {
        if (!MpCheatSession.InMultiplayerRun) {
            DevPanelUI.RunCombatAction(() => CombatEnemyActions.AddEncounterMonsters(encounter), onChanged);
            return;
        }

        if (!MpCheatSession.CanUseMultiplayerCheats) {
            MainFile.Logger.Warn(
                I18N.T(
                    "mpcheat.blocked",
                    "Multiplayer cheat inactive: {0}",
                    MpCheatSession.LastBlockReason ?? "unknown"));
            return;
        }

        TaskHelper.RunSafely(SyncAsync());

        async System.Threading.Tasks.Task SyncAsync() {
            var result = MpCheatSession.IsHost
                ? await MpCheatCombatEnemyCoordinator.TryHostAddEncounterAsync(encounter)
                : await MpCheatCombatEnemyCoordinator.TryClientRequestAddEncounterAsync(encounter);
            MainFile.Logger.Info($"[MpCheat] Combat add result: {result}");
            RefreshCombatContext();
            onChanged?.Invoke();
        }
    }
}
