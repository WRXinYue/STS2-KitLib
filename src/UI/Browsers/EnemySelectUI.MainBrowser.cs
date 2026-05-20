using System;
using System.Linq;
using DevMode.Actions;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace DevMode.UI;

internal static partial class EnemySelectUI {
    private enum EnemyNavTab {
        Global,
        ByType,
        ByFloor,
        Kill,
    }

    private sealed class MainBrowserState {
        public required NGlobalUi GlobalUi;
        public required VBoxContainer ContentHost;
        public EnemyNavTab ActiveTab;
        public RoomType? EncounterFilter;
        public Label StatusLabel = null!;
    }

    public static void ShowMain(NGlobalUi globalUi) {
        Hide(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, 0f, () => Hide(globalUi), contentSeparation: 8, backdropWhenFullWidth: true);

        var state = new MainBrowserState {
            GlobalUi = globalUi,
            ContentHost = new VBoxContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            },
            ActiveTab = ResolveInitialTab(),
            EncounterFilter = null,
        };
        state.ContentHost.AddThemeConstantOverride("separation", 8);

        BuildMainNav(vbox, state);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        vbox.AddChild(state.ContentHost);

        state.StatusLabel = new Label { Text = "" };
        state.StatusLabel.AddThemeFontSizeOverride("font_size", 11);
        state.StatusLabel.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        state.StatusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(state.StatusLabel);

        SwitchMainTab(state);
        ((Node)globalUi).AddChild(root);
    }

    private static EnemyNavTab ResolveInitialTab() {
        if (DevModeState.FloorOverrides.Count > 0)
            return EnemyNavTab.ByFloor;

        return DevModeState.EnemyMode switch {
            EnemyMode.Global => EnemyNavTab.Global,
            EnemyMode.PerType => EnemyNavTab.ByType,
            _ => EnemyNavTab.Global,
        };
    }

    private static void BuildMainNav(VBoxContainer vbox, MainBrowserState state) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var title = new Label {
            Text = I18N.T("panel.enemies", "Enemies"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", DevModeTheme.Accent);
        row.AddChild(title);

        row.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });

        AddNavChip(row, state, EnemyNavTab.Global, I18N.T("topbar.enemy.global", "Global"));
        AddNavChip(row, state, EnemyNavTab.ByType, I18N.T("topbar.enemy.byType", "By Type"));
        AddNavChip(row, state, EnemyNavTab.ByFloor, I18N.T("topbar.enemy.byFloor", "By Floor"));

        if (CombatEnemyActions.GetCombatState() != null) {
            row.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });
            AddNavChip(row, state, EnemyNavTab.Kill, I18N.T("topbar.enemy.killEnemy", "Kill Enemy"));
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
    }

    private static void AddNavChip(HBoxContainer row, MainBrowserState state, EnemyNavTab tab, string label) {
        var chip = DevPanelUI.CreateFilterChip(label, active: state.ActiveTab == tab);
        chip.Pressed += () => {
            if (state.ActiveTab == tab)
                return;
            state.ActiveTab = tab;
            RefreshNavChips(row, state);
            SwitchMainTab(state);
        };
        chip.SetMeta("enemy_nav_tab", (int)tab);
        row.AddChild(chip);
    }

    private static void RefreshNavChips(HBoxContainer row, MainBrowserState state) {
        foreach (var child in row.GetChildren()) {
            if (child is not Button chip || !chip.HasMeta("enemy_nav_tab"))
                continue;
            chip.ButtonPressed = (EnemyNavTab)(int)chip.GetMeta("enemy_nav_tab") == state.ActiveTab;
        }
    }

    private static void SwitchMainTab(MainBrowserState state) {
        foreach (var child in state.ContentHost.GetChildren())
            ((Node)child).QueueFree();

        state.StatusLabel.Text = "";

        switch (state.ActiveTab) {
            case EnemyNavTab.Global:
                BuildGlobalTab(state);
                break;
            case EnemyNavTab.ByType:
                BuildByTypeTab(state);
                break;
            case EnemyNavTab.ByFloor:
                BuildFloorPicker(state.ContentHost, state.GlobalUi, embedded: true,
                    onStatusChanged: text => state.StatusLabel.Text = text);
                state.StatusLabel.Text = I18N.T(
                    "enemy.byFloorHint",
                    "Floor overrides apply on top of global / per-type settings.");
                break;
            case EnemyNavTab.Kill:
                BuildKillTab(state);
                break;
        }
    }

    private static void BuildGlobalTab(MainBrowserState state) {
        bool inCombat = CombatEnemyActions.GetCombatState() != null;
        state.StatusLabel.Text = inCombat
            ? I18N.T("enemy.globalCombatHint", "Pick an encounter to spawn in the current combat.")
            : I18N.T("enemy.globalHint", "Sets the same encounter for all combat rooms in the run.");

        BuildEncounterPicker(
            state.ContentHost,
            state.GlobalUi,
            state.EncounterFilter,
            enc => {
                if (inCombat)
                    TaskHelper.RunSafely(CombatEnemyActions.AddEncounterMonsters(enc));
                else
                    EnemyActions.SetGlobalOverride(enc);
                state.StatusLabel.Text = I18N.T(
                    "enemy.appliedGlobal",
                    "Applied global override: {0}",
                    EnemyActions.GetShortName(enc));
            },
            new EncounterPickerOptions {
                CloseOnSelect = false,
                ShowTitle = false,
                OnFilterChanged = filter => {
                    state.EncounterFilter = filter;
                    SwitchMainTab(state);
                },
            });

        GrabEncounterSearchFocus(state.ContentHost);
    }

    private static void BuildByTypeTab(MainBrowserState state) {
        state.StatusLabel.Text = I18N.T(
            "enemy.byTypeHint",
            "Pick an encounter to override its room type (Normal / Elite / Boss).");

        BuildEncounterPicker(
            state.ContentHost,
            state.GlobalUi,
            state.EncounterFilter,
            enc => {
                EnemyActions.SetRoomTypeOverride(enc.RoomType, enc);
                state.StatusLabel.Text = I18N.T(
                    "enemy.appliedByType",
                    "Applied {0} override: {1}",
                    enc.RoomType,
                    EnemyActions.GetShortName(enc));
            },
            new EncounterPickerOptions {
                CloseOnSelect = false,
                ShowTitle = false,
                OnFilterChanged = filter => {
                    state.EncounterFilter = filter;
                    SwitchMainTab(state);
                },
            });

        GrabEncounterSearchFocus(state.ContentHost);
    }

    private static void BuildKillTab(MainBrowserState state) {
        var enemies = CombatEnemyActions.GetCurrentEnemies();
        if (enemies.Count == 0) {
            state.ContentHost.AddChild(new Label {
                Text = I18N.T("enemy.noLiveEnemies", "No live enemies in combat."),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return;
        }

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var listBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listBox.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(listBox);
        state.ContentHost.AddChild(scroll);

        foreach (var enemy in enemies) {
            if (enemy.IsDead) continue;
            var enemyName = enemy.Monster?.Title?.GetFormattedText() ?? I18N.T("enemy.unknownName", "???");
            var hp = $"{enemy.CurrentHp}/{enemy.MaxHp}";
            var btn = DevPanelUI.CreateListItemButton(
                I18N.T("enemy.killEntry", "{0}  HP: {1}", enemyName, hp));
            var captured = enemy;
            btn.Pressed += () => {
                TaskHelper.RunSafely(CombatEnemyActions.KillEnemy(captured));
                SwitchMainTab(state);
            };
            listBox.AddChild(btn);
        }

        var killAllBtn = DevPanelUI.CreateListItemButton(I18N.T("enemy.killAll", "Kill All"));
        killAllBtn.Alignment = HorizontalAlignment.Center;
        killAllBtn.Pressed += () => {
            TaskHelper.RunSafely(CombatEnemyActions.KillAllEnemies());
            SwitchMainTab(state);
        };
        state.ContentHost.AddChild(killAllBtn);
    }
}
