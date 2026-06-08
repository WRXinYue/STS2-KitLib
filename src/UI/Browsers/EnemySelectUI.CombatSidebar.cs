using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.EnemyIntent;
using KitLib.Icons;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

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
        RefreshMapCombatDetailIfOpen();
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

            var enemies = GetLivingEnemies();
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
                    OpenAddEnemies),
            ]);
        }

        protected override void RebuildDynamicActions(VBoxContainer host) {
            var enemies = GetLivingEnemies();
            var killActions = new List<ContextRailAction>(enemies.Count + 1);

            foreach (var enemy in enemies) {
                string name = enemy.Monster?.Title?.GetFormattedText() ?? I18N.T("enemy.unknownName", "???");
                string hp = $"{enemy.CurrentHp}/{enemy.MaxHp}";
                var captured = enemy;
                killActions.Add(new ContextRailAction(
                    MdiIcon.Skull,
                    I18N.T("enemy.combatSidebar.killOne", "Kill {0} ({1} HP)", name, hp),
                    () => RunSyncedKillEnemy(captured),
                    KillTint));
            }

            if (enemies.Count > 0) {
                killActions.Add(new ContextRailAction(
                    MdiIcon.Skull,
                    I18N.T("enemy.combatSidebar.killAll", "Kill all enemies"),
                    () => RunSyncedKillAll(),
                    KillAllTint));
            }

            AddActions(host, killActions);
        }

        private static void OpenAddEnemies() {
            var globalUi = NRun.Instance?.GlobalUi;
            if (globalUi == null)
                return;

            OpenCombatAddPicker(globalUi);
        }
    }
}
