using Godot;

namespace KitLib.UI;

/// <summary>
/// iOS-style swipe actions for sidebar mod rows. At most one row is open at a time;
/// its content slides left to reveal a right-aligned action bar. Mouse and touch share
/// the gesture pipeline; vertical drags are left to the ScrollContainer.
/// </summary>
internal static class ModPanelSidebarSwipe {
    private const float ActivateThreshold = 12f;
    private const string ContentTweenMeta = "modpanel_row_swipe_tw";

    private static SidebarModRowVm? _openRow;
    private static SidebarModRowVm? _pressRow;
    private static Vector2 _pressPos;
    private static bool _pressed;
    private static bool _touchSource;
    private static bool _dragging;
    private static float _dragBaseOffset;

    internal static bool IsOpen(SidebarModRowVm row) => ReferenceEquals(_openRow, row);

    /// <summary>Clears transient state when the sidebar is rebuilt.</summary>
    internal static void Reset() {
        if (_openRow != null) {
            if (GodotObject.IsInstanceValid(_openRow.Content))
                SetOffset(_openRow, 0f);
            if (GodotObject.IsInstanceValid(_openRow.ActionsBar))
                _openRow.ActionsBar.Visible = false;
        }
        _openRow = null;
        _pressRow = null;
        _pressed = false;
        _dragging = false;
    }

    internal static void OpenRow(SidebarModRowVm row) {
        if (ReferenceEquals(_openRow, row)) {
            row.ActionsBar.Visible = true;
            AnimateTo(row, row.SwipeMaxOffset);
            return;
        }
        CloseRow(_openRow);
        _openRow = row;
        row.ActionsBar.Visible = true;
        AnimateTo(row, row.SwipeMaxOffset);
    }

    internal static void RetractAll() => CloseRow(_openRow);

    internal static void HandleRightClick(SidebarModRowVm row) => OpenRow(row);

    internal static void HandlePressStart(SidebarModRowVm row, Vector2 position, bool touch) {
        if (_pressed) {
            if (ReferenceEquals(_pressRow, row)) {
                if (touch) {
                    _touchSource = true;
                    _pressPos = position;
                }
                return;
            }
            // Previous press was released outside any row; snap it before starting over.
            FinishPress(_pressRow);
        }
        _pressed = true;
        _touchSource = touch;
        _pressRow = row;
        _pressPos = position;
        _dragging = false;
    }

    /// <summary>Returns true when the motion was claimed as a horizontal drag.</summary>
    internal static bool HandleMove(SidebarModRowVm row, Vector2 position, bool touch) {
        if (!_pressed || !ReferenceEquals(_pressRow, row) || _touchSource != touch)
            return _dragging;
        var dx = position.X - _pressPos.X;
        if (!_dragging) {
            var dy = position.Y - _pressPos.Y;
            if (Mathf.Abs(dx) < ActivateThreshold && Mathf.Abs(dy) < ActivateThreshold)
                return false;
            if (Mathf.Abs(dy) > Mathf.Abs(dx))
                return false;
            _dragging = true;
            _dragBaseOffset = CurrentOffset(row);
            if (GodotObject.IsInstanceValid(row.ActionsBar))
                row.ActionsBar.Visible = true;
        }
        SetOffset(row, Mathf.Clamp(_dragBaseOffset - dx, 0f, row.SwipeMaxOffset));
        return true;
    }

    /// <summary>Returns true when the press ended as a swipe; the caller then skips click selection.</summary>
    internal static bool HandlePressEnd(SidebarModRowVm row, Vector2 position, bool touch) {
        if (!_pressed || !ReferenceEquals(_pressRow, row))
            return _dragging;
        _pressed = false;
        if (_touchSource != touch)
            return _dragging;
        _pressRow = null;
        var wasDragging = _dragging;
        FinishPress(row);
        return wasDragging;
    }

    /// <summary>Safety net: a press released outside any row still snaps its swipe.</summary>
    internal static void HandleGlobalRelease() {
        if (!_pressed)
            return;
        _pressed = false;
        var row = _pressRow;
        _pressRow = null;
        FinishPress(row);
    }

    private static void FinishPress(SidebarModRowVm? row) {
        if (row == null || !GodotObject.IsInstanceValid(row.Host))
            return;
        var wasDragging = _dragging;
        _dragging = false;
        if (!wasDragging)
            return;
        var offset = CurrentOffset(row);
        if (offset >= row.SwipeMaxOffset * 0.4f)
            OpenRow(row);
        else
            CloseRow(row);
    }

    private static void CloseRow(SidebarModRowVm? row) {
        if (row == null || !GodotObject.IsInstanceValid(row.Host))
            return;
        if (ReferenceEquals(_openRow, row))
            _openRow = null;
        if (GodotObject.IsInstanceValid(row.ActionsBar))
            row.ActionsBar.Visible = false;
        AnimateTo(row, 0f);
    }

    private static float CurrentOffset(SidebarModRowVm row) => row.ContentBaseLeft - row.Content.OffsetLeft;

    private static void SetOffset(SidebarModRowVm row, float offset) {
        if (!GodotObject.IsInstanceValid(row.Content))
            return;
        ModPanelUiMotion.KillTween(row.Content, ContentTweenMeta);
        row.Content.OffsetLeft = row.ContentBaseLeft - offset;
        row.Content.OffsetRight = row.ContentBaseRight - offset;
    }

    private static void AnimateTo(SidebarModRowVm row, float target) {
        if (!GodotObject.IsInstanceValid(row.Content))
            return;
        ModPanelUiMotion.KillTween(row.Content, ContentTweenMeta);
        var start = CurrentOffset(row);
        if (Mathf.IsEqualApprox(start, target))
            return;
        var tw = row.Content.CreateTween();
        row.Content.SetMeta(ContentTweenMeta, tw);
        tw.SetTrans(target > start ? Tween.TransitionType.Back : Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(offset => {
            if (!GodotObject.IsInstanceValid(row.Content))
                return;
            row.Content.OffsetLeft = row.ContentBaseLeft - offset;
            row.Content.OffsetRight = row.ContentBaseRight - offset;
        }), start, target, ModPanelUiMotion.SidebarTransitionDuration);
        tw.TweenCallback(Callable.From(() => {
            if (GodotObject.IsInstanceValid(row.Content) && row.Content.HasMeta(ContentTweenMeta))
                row.Content.RemoveMeta(ContentTweenMeta);
        }));
    }

    /// <summary>Builds an action-bar button with the shared slate/info style family.</summary>
    internal static Button CreateActionButton(StyleBoxFlat normal, StyleBoxFlat hover, StyleBoxFlat disabled) {
        var btn = new Button();
        btn.AddThemeColorOverride("font_color", new Color(0.98f, 0.965f, 0.93f, 1f));
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeStyleboxOverride("disabled", disabled);
        return btn;
    }

    internal static StyleBoxFlat SlateTint(bool hover = false, bool disabled = false) {
        var color = hover ? new Color(0.30f, 0.33f, 0.39f, 0.96f)
            : disabled ? new Color(0.115f, 0.125f, 0.15f, 0.96f)
            : new Color(0.23f, 0.25f, 0.30f, 0.96f);
        return FlatStyle(color);
    }

    internal static StyleBoxFlat InfoTint(bool hover = false, bool disabled = false) {
        var color = hover ? new Color(0.21f, 0.49f, 0.82f, 0.96f)
            : disabled ? new Color(0.08f, 0.21f, 0.36f, 0.96f)
            : new Color(0.16f, 0.42f, 0.72f, 0.96f);
        return FlatStyle(color);
    }

    private static StyleBoxFlat FlatStyle(Color color) {
        var style = new StyleBoxFlat { BgColor = color };
        style.ContentMarginLeft = 12f;
        style.ContentMarginTop = 4f;
        style.ContentMarginRight = 12f;
        style.ContentMarginBottom = 4f;
        return style;
    }
}
