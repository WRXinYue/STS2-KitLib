using Godot;
using KitLib.DevPerf;
using KitLib.Icons;
using KitLib.Settings;

namespace KitLib.UI;

internal static class DevPerfOverlayUI {
    internal const string RootName = "KitLibDevPerfOverlay";

    const float MarginRight = 12f;
    const float MarginTop = 10f;
    const float MpOverlayOffset = 220f;
    const float MaxLineWidth = 360f;

    static DevPerfOverlayHost? _overlay;
    static Node? _host;
    static bool _lastVisible;

    internal static bool ShouldShow() {
        if (!KitLibState.IsActive)
            return false;
        return SettingsStore.Current.PerfHudEnabled;
    }

    internal static void SyncVisibility() => _overlay?.SyncVisibility();

    internal static void Attach(Node host) {
        if (host == null || !GodotObject.IsInstanceValid(host))
            return;

        _host = host;
        EnsureAttached();
    }

    static void EnsureAttached() {
        if (_host == null || !GodotObject.IsInstanceValid(_host))
            return;

        var existing = _host.GetNodeOrNull<Control>(RootName);
        if (existing is DevPerfOverlayHost host && GodotObject.IsInstanceValid(host)) {
            _overlay = host;
            host.SyncVisibility();
            return;
        }

        if (_overlay != null && GodotObject.IsInstanceValid(_overlay)
            && _overlay.IsInsideTree() && _overlay.GetParent() == _host)
            return;

        _overlay = new DevPerfOverlayHost();
        _host.AddChild(_overlay);
        _overlay.SyncVisibility();
    }

    sealed partial class DevPerfOverlayHost : Control {
        readonly VBoxContainer _stack;
        readonly Godot.Timer _refreshTimer;

        public DevPerfOverlayHost() {
            Name = RootName;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);

            _stack = new VBoxContainer {
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(MaxLineWidth, 0),
            };
            _stack.AddThemeConstantOverride("separation", 2);
            AddChild(_stack);

            _refreshTimer = new Godot.Timer { WaitTime = 0.25, Autostart = true };
            _refreshTimer.Timeout += RefreshContent;
            AddChild(_refreshTimer);

            var viewport = GetViewport();
            if (viewport != null)
                viewport.SizeChanged += OnViewportSizeChanged;
            TreeExiting += () => {
                if (viewport != null)
                    viewport.SizeChanged -= OnViewportSizeChanged;
                if (_overlay == this)
                    _overlay = null;
            };

            RefreshContent();
        }

        void OnViewportSizeChanged() => ApplyCornerLayout();

        public void SyncVisibility() {
            var show = ShouldShow();
            Visible = show;
            if (show != _lastVisible) {
                _lastVisible = show;
                MainFile.Logger.Info(
                    $"[Perf] Overlay visibility={(show ? "visible" : "hidden")} enabled={SettingsStore.Current.PerfHudEnabled} active={KitLibState.IsActive}");
            }

            if (show) {
                MoveToFront();
                RefreshContent();
            }
        }

        void RefreshContent() {
            if (!ShouldShow()) {
                Visible = false;
                return;
            }

            Visible = true;
            ApplyCornerLayout();
            RebuildLines();
        }

        void ApplyCornerLayout() {
            var viewport = GetViewport();
            var size = viewport?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var top = MarginTop;
            if (CombatStatsUI.IsMultiplayerOverlayActive())
                top += MpOverlayOffset;

            Position = new Vector2(Mathf.Max(MarginRight, size.X - MaxLineWidth - MarginRight), top);
        }

        void RebuildLines() {
            foreach (var child in _stack.GetChildren())
                child.QueueFree();

            var lines = new List<DevPerfLine>();
            DevPerfMetrics.CollectLines(lines);
            if (lines.Count == 0) {
                _stack.AddChild(MakeLabel("perf: —", alert: false));
                return;
            }

            foreach (var line in lines)
                _stack.AddChild(MakeLabel(line.Text, line.Alert));

            MainFile.Logger.Debug($"[Perf] Overlay refreshed lines={lines.Count}");
        }

        static Label MakeLabel(string text, bool alert) {
            var label = new Label {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride(
                "font_color",
                alert ? new Color(1f, 0.35f, 0.35f) : KitLibTheme.TextPrimary);
            return label;
        }
    }
}
