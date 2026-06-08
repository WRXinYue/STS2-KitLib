using System;
using System.Collections.Generic;
using KitLib.Actions;
using KitLib.Icons;
using Godot;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.UI;

internal readonly record struct ContextRailAction(
    MdiIcon Icon,
    string Tooltip,
    Action OnClick,
    Color? Tint = null);

internal abstract class CombatContextSidebarBase : IDevPanelSidebarProvider {
    private readonly VBoxContainer _root;
    private readonly VBoxContainer _actions;
    private readonly Control _divider;
    private readonly VBoxContainer _dynamic;
    private bool _hasContent;
    private string _lastSnapshotKey = "";

    protected CombatContextSidebarBase(string rootName) {
        _root = new VBoxContainer {
            Name = rootName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _root.AddThemeConstantOverride("separation", 4);
        _root.Alignment = BoxContainer.AlignmentMode.Center;

        _actions = new VBoxContainer();
        _actions.AddThemeConstantOverride("separation", 4);
        _actions.Alignment = BoxContainer.AlignmentMode.Center;

        _divider = ContextRailWidgets.CreateRailDivider();
        _divider.Visible = false;

        _dynamic = new VBoxContainer();
        _dynamic.AddThemeConstantOverride("separation", 4);
        _dynamic.Alignment = BoxContainer.AlignmentMode.Center;

        _root.AddChild(_actions);
        _root.AddChild(_divider);
        _root.AddChild(_dynamic);
    }

    public Control Root => _root;

    public abstract string Title { get; }

    public abstract string Hint { get; }

    public bool HasContent => _hasContent;

    protected static bool IsCombatVisible =>
        KitLibState.IsActive
        && CombatManager.Instance?.IsInProgress == true
        && CombatEnemyActions.GetCombatState() != null;

    public void Refresh() {
        string snapshotKey = ComputeSnapshotKey();
        if (snapshotKey == _lastSnapshotKey)
            return;

        _lastSnapshotKey = snapshotKey;
        ContextRailWidgets.ClearChildren(_actions);
        ContextRailWidgets.ClearChildren(_dynamic);
        _divider.Visible = false;

        if (!IsCombatVisible) {
            _hasContent = false;
            return;
        }

        _hasContent = true;
        RebuildFixedActions(_actions);
        RebuildDynamicActions(_dynamic);
        _divider.Visible = _dynamic.GetChildCount() > 0 && _actions.GetChildCount() > 0;
    }

    protected abstract string ComputeSnapshotKey();

    protected abstract void RebuildFixedActions(VBoxContainer host);

    protected abstract void RebuildDynamicActions(VBoxContainer host);

    protected static void AddActions(VBoxContainer host, IEnumerable<ContextRailAction> actions) {
        foreach (var action in actions)
            host.AddChild(ContextRailWidgets.CreateContextIconButton(
                action.Icon,
                action.Tooltip,
                action.OnClick,
                action.Tint));
    }
}
