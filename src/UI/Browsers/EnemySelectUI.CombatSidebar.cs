using System.Collections.Generic;
using System.Linq;
using DevMode.Actions;
using DevMode.EnemyIntent;
using DevMode.Icons;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

internal static partial class EnemySelectUI {
    internal const string CombatToolsContextId = "enemy.combatTools";

    private static CombatEnemySidebarPanel? _combatSidebar;
    private static DevPanelSidebarHost? _combatGameHost;

    internal static void EnsureGameContextPane(DevPanelSidebarHost host) {
        DevPanelUI.EnsureContextProvider(
            ref _combatGameHost,
            host,
            ref _combatSidebar,
            CombatToolsContextId,
            () => new CombatEnemySidebarPanel());
    }

    internal static void RefreshCombatContext() {
        _combatSidebar?.Refresh();
    }

    internal sealed partial class CombatEnemySidebarPanel : CombatContextSidebarBase {
        private static readonly Color KillTint = new(1f, 0.45f, 0.45f);
        private static readonly Color KillAllTint = new(1f, 0.3f, 0.3f);

        public CombatEnemySidebarPanel() : base("EnemyCombatToolsSidebar") { }

        public override string Title => I18N.T("enemy.combatSidebar.title", "Combat");

        public override string Hint => I18N.T("enemy.combatSidebar.hint",
            "Add or remove enemies in the current fight.");

        protected override string ComputeSnapshotKey() {
            if (!IsCombatVisible)
                return "hidden";

            var enemies = CombatEnemyActions.GetCurrentEnemies().Where(e => !e.IsDead).ToList();
            var identityKeys = new List<string>(enemies.Count);
            foreach (var enemy in enemies)
                identityKeys.Add(MonsterIntentOverrides.BuildEnemyKey(enemy));

            return $"{enemies.Count}|{string.Join(';', identityKeys)}";
        }

        protected override void RebuildFixedActions(VBoxContainer host) {
            AddActions(host, [
                new ContextRailAction(
                    MdiIcon.Plus,
                    I18N.T("enemy.combatSidebar.addMenu", "Add enemies to combat"),
                    OpenAddMenu),
            ]);
        }

        protected override void RebuildDynamicActions(VBoxContainer host) {
            var enemies = CombatEnemyActions.GetCurrentEnemies().Where(e => !e.IsDead).ToList();
            var killActions = new List<ContextRailAction>(enemies.Count + 1);

            foreach (var enemy in enemies) {
                string name = enemy.Monster?.Title?.GetFormattedText() ?? I18N.T("enemy.unknownName", "???");
                string hp = $"{enemy.CurrentHp}/{enemy.MaxHp}";
                var captured = enemy;
                killActions.Add(new ContextRailAction(
                    MdiIcon.Skull,
                    I18N.T("enemy.combatSidebar.killOne", "Kill {0} ({1} HP)", name, hp),
                    () => DevPanelUI.RunCombatAction(() => CombatEnemyActions.KillEnemy(captured)),
                    KillTint));
            }

            if (enemies.Count > 0) {
                killActions.Add(new ContextRailAction(
                    MdiIcon.Skull,
                    I18N.T("enemy.combatSidebar.killAll", "Kill all enemies"),
                    () => DevPanelUI.RunCombatAction(CombatEnemyActions.KillAllEnemies),
                    KillAllTint));
            }

            AddActions(host, killActions);
        }

        private static void OpenAddMenu() {
            var globalUi = NRun.Instance?.GlobalUi;
            if (globalUi == null)
                return;

            const string menuName = "DevModeCombatAddMenu";
            ((Node)globalUi).GetNodeOrNull<Control>(menuName)?.QueueFree();

            var backdrop = new ColorRect {
                Name = menuName,
                Color = new Color(0f, 0f, 0f, 0.25f),
                MouseFilter = Control.MouseFilterEnum.Stop,
                AnchorRight = 1,
                AnchorBottom = 1,
                ZIndex = 1290,
            };
            backdrop.GuiInput += ev => {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    backdrop.QueueFree();
            };

            var panel = new PanelContainer {
                AnchorLeft = 1,
                AnchorRight = 1,
                AnchorTop = 0.5f,
                AnchorBottom = 0.5f,
                OffsetLeft = -(DevPanelUI.ContextPaneW + 8),
                OffsetRight = -8,
                OffsetTop = -72,
                OffsetBottom = 72,
            };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                BgColor = DevModeTheme.PanelBg,
                BorderColor = DevModeTheme.PanelBorder,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 8,
                ContentMarginBottom = 8,
            });

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            panel.AddChild(vbox);

            vbox.AddChild(CreateAddMenuButton(
                I18N.T("enemy.combatSidebar.addEncounter", "Add encounter to combat"),
                () => {
                    backdrop.QueueFree();
                    ShowEncounterOverlay(globalUi, null, enc => {
                        DevPanelUI.RunCombatAction(() => CombatEnemyActions.AddEncounterMonsters(enc));
                    });
                }));

            vbox.AddChild(CreateAddMenuButton(
                I18N.T("enemy.combatSidebar.addMonster", "Add monster to combat"),
                () => {
                    backdrop.QueueFree();
                    ShowMonsterSpawnOverlay(globalUi, monster => {
                        DevPanelUI.RunCombatAction(() => CombatEnemyActions.AddMonster(monster));
                    });
                }));

            backdrop.AddChild(panel);
            ((Node)globalUi).AddChild(backdrop);
        }

        private static Button CreateAddMenuButton(string text, System.Action onPressed) {
            var btn = new Button {
                Text = text,
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            btn.AddThemeColorOverride("font_hover_color", DevModeTheme.TextPrimary);
            btn.AddThemeColorOverride("font_pressed_color", DevModeTheme.TextPrimary);
            btn.Pressed += onPressed;
            return btn;
        }
    }
}
