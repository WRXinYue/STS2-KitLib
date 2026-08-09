using System;
using Godot;

namespace KitLib.UI;

/// <summary>Shared draggable floating panel helpers for combat overlays.</summary>
internal static class FloatingCombatOverlay {
    internal static StyleBoxFlat CreatePanelStyle() {
        var theme = ThemeManager.Current;
        return new StyleBoxFlat {
            BgColor = theme.RailBg,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = theme.RailBorder,
            ShadowColor = new Color(0, 0, 0, 0.25f),
            ShadowSize = 6,
        };
    }

    /// <summary>Drag title bar → move panel within host bounds.</summary>
    internal sealed class DraggablePanelBinding {
        private readonly Control _host;
        private readonly PanelContainer _panel;
        private readonly float _defaultWidth;
        private readonly Func<bool> _isFreePosition;
        private readonly Action<bool> _setFreePosition;
        private readonly Action<Vector2>? _onPositionCommitted;
        private bool _dragging;
        private Vector2 _dragOffset;

        public DraggablePanelBinding(
            Control host,
            PanelContainer panel,
            float defaultWidth,
            Func<bool> isFreePosition,
            Action<bool> setFreePosition,
            Action<Vector2>? onPositionCommitted = null) {
            _host = host;
            _panel = panel;
            _defaultWidth = defaultWidth;
            _isFreePosition = isFreePosition;
            _setFreePosition = setFreePosition;
            _onPositionCommitted = onPositionCommitted;
        }

        public void WireHandle(Control handle) {
            handle.GuiInput += e => {
                if (e is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left)
                    return;

                if (mb.Pressed) {
                    EnsureFreePosition();
                    var mouseLocal = _host.GetGlobalTransformWithCanvas().AffineInverse()
                        * _host.GetGlobalMousePosition();
                    _dragOffset = mouseLocal - _panel.Position;
                    _dragging = true;
                    handle.AcceptEvent();
                    return;
                }

                if (_dragging) {
                    _dragging = false;
                    ClampAndCommit();
                    handle.AcceptEvent();
                }
            };
        }

        public void ClampAndCommit() {
            ClampPanel();
            if (_isFreePosition())
                _onPositionCommitted?.Invoke(_panel.Position);
        }

        public void Process() {
            if (!_dragging)
                return;

            if (!Input.IsMouseButtonPressed(MouseButton.Left)) {
                _dragging = false;
                ClampAndCommit();
                return;
            }

            var mouseLocal = _host.GetGlobalTransformWithCanvas().AffineInverse()
                * _host.GetGlobalMousePosition();
            _panel.Position = mouseLocal - _dragOffset;
        }

        private void EnsureFreePosition() {
            if (_isFreePosition())
                return;

            var pos = _panel.Position;
            var size = _panel.Size;
            if (size.X <= 0f)
                size.X = _defaultWidth;
            if (size.Y <= 0f)
                size.Y = _panel.GetCombinedMinimumSize().Y;

            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            _panel.Size = size;
            _panel.Position = pos;
            _setFreePosition(true);
        }

        private void ClampPanel() {
            var size = _panel.Size;
            if (size.X <= 0f || size.Y <= 0f)
                return;

            var pos = _panel.Position;
            pos.X = Math.Clamp(pos.X, 0f, Math.Max(0f, _host.Size.X - size.X));
            pos.Y = Math.Clamp(pos.Y, 0f, Math.Max(0f, _host.Size.Y - size.Y));
            _panel.Position = pos;
        }
    }
}
