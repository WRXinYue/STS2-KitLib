using System;
using System.Linq;
using DevMode.Actions;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

internal static partial class EnemySelectUI {
    internal enum EnemyPanelMode {
        Map,
        Kill,
    }

    internal sealed class MainBrowserState {
        public required NGlobalUi GlobalUi;
        public required VBoxContainer ContentHost;
        public EnemyPanelMode Mode = EnemyPanelMode.Map;
        public RoomType? EncounterFilter;
        public Label StatusLabel = null!;
        public HBoxContainer? NavRow;
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
            Mode = EnemyPanelMode.Map,
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

        SwitchMainView(state);
        ((Node)globalUi).AddChild(root);
    }

    private static void BuildMainNav(VBoxContainer vbox, MainBrowserState state) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        state.NavRow = row;

        var title = new Label {
            Text = I18N.T("panel.enemies", "Enemies"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", DevModeTheme.Accent);
        row.AddChild(title);

        if (CombatEnemyActions.GetCombatState() != null) {
            row.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });
            AddModeChip(row, state, EnemyPanelMode.Kill, I18N.T("topbar.enemy.killEnemy", "Kill Enemy"));
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
    }

    private static void AddModeChip(HBoxContainer row, MainBrowserState state, EnemyPanelMode mode, string label) {
        var chip = DevPanelUI.CreateFilterChip(label, active: state.Mode == mode);
        chip.Pressed += () => {
            if (state.Mode == mode)
                return;
            state.Mode = mode;
            RefreshModeChips(row, state);
            SwitchMainView(state);
        };
        chip.SetMeta("enemy_panel_mode", (int)mode);
        row.AddChild(chip);
    }

    private static void RefreshModeChips(HBoxContainer row, MainBrowserState state) {
        foreach (var child in row.GetChildren()) {
            if (child is not Button chip || !chip.HasMeta("enemy_panel_mode"))
                continue;
            chip.ButtonPressed = (EnemyPanelMode)(int)chip.GetMeta("enemy_panel_mode") == state.Mode;
        }
    }

    internal static void SwitchMainView(MainBrowserState state) {
        foreach (var child in state.ContentHost.GetChildren())
            ((Node)child).QueueFree();

        state.StatusLabel.Text = "";

        if (state.Mode == EnemyPanelMode.Kill) {
            BuildKillTab(state);
            return;
        }

        BuildMapTab(state);
        state.StatusLabel.Text = I18N.T(
            "enemy.mapHint",
            "Click combat nodes on the map to edit. Run rules apply to this run only.");
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
                SwitchMainView(state);
            };
            listBox.AddChild(btn);
        }

        var killAllBtn = DevPanelUI.CreateListItemButton(I18N.T("enemy.killAll", "Kill All"));
        killAllBtn.Alignment = HorizontalAlignment.Center;
        killAllBtn.Pressed += () => {
            TaskHelper.RunSafely(CombatEnemyActions.KillAllEnemies());
            SwitchMainView(state);
        };
        state.ContentHost.AddChild(killAllBtn);
    }
}
