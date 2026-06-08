using System.Text.Json.Nodes;
using KitLib.AI;
using KitLib.AI.AutoPlay;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>In-game AI hosting HUD — sim-derived telemetry at top-left.</summary>
internal static partial class AiHudOverlayUI {
    internal const string RootName = "KitLibAiHudOverlay";

    static AiHudOverlayHost? _overlay;
    static NGlobalUi? _globalUi;

    internal static bool IsEnabled() =>
        SettingsStore.Current.AiHudEnabled;

    internal static bool ShouldShow() {
        if (!KitLibState.IsActive)
            return false;
        if (!IsEnabled())
            return false;
        if (MpCheatSession.InMultiplayerRun)
            return false;
        if (!SettingsStore.Current.AutoPlayEnabled)
            return false;
        return AiPlayModule.Instance.IsRunning;
    }

    internal static void SyncState(NGlobalUi? globalUi = null) {
        if (IsUiAlive(globalUi))
            _globalUi = globalUi;
        else if (!IsUiAlive(_globalUi))
            TryResolveGlobalUi();

        if (!IsUiAlive(_globalUi))
            return;

        EnsureAttached();
        _overlay?.SyncVisibility();
    }

    internal static void Attach(NGlobalUi globalUi) {
        if (!IsUiAlive(globalUi))
            return;

        _globalUi = globalUi;
        EnsureAttached();
    }

    internal static void Detach(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        _overlay = null;
        if (_globalUi == globalUi)
            _globalUi = null;
    }

    static bool IsUiAlive(NGlobalUi? ui) =>
        ui != null && GodotObject.IsInstanceValid(ui);

    static bool TryResolveGlobalUi() {
        if (IsUiAlive(_globalUi))
            return true;

        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            return false;

        foreach (var node in tree.Root.FindChildren("*", recursive: true, owned: false)) {
            if (node is not NGlobalUi ui || !GodotObject.IsInstanceValid(ui))
                continue;
            _globalUi = ui;
            return true;
        }

        return false;
    }

    static void EnsureAttached() {
        if (!IsUiAlive(_globalUi)) {
            _globalUi = null;
            return;
        }

        var parent = (Node)_globalUi;
        var existing = parent.GetNodeOrNull<Control>(RootName);
        if (existing is AiHudOverlayHost host && GodotObject.IsInstanceValid(host)) {
            _overlay = host;
            host.SyncVisibility();
            return;
        }

        if (_overlay != null && GodotObject.IsInstanceValid(_overlay)
            && _overlay.IsInsideTree() && _overlay.GetParent() == parent)
            return;

        _overlay = null;
        var overlay = new AiHudOverlayHost();
        _overlay = overlay;
        overlay.TreeExiting += () => {
            if (_overlay == overlay)
                _overlay = null;
        };

        Callable.From(() => {
            if (_globalUi == null || !GodotObject.IsInstanceValid(_globalUi))
                return;
            var attachParent = (Node)_globalUi;
            if (!GodotObject.IsInstanceValid(overlay) || overlay.GetParent() != null)
                return;
            if (attachParent.GetNodeOrNull<Control>(RootName) != null)
                return;
            attachParent.AddChild(overlay);
            overlay.SyncVisibility();
        }).CallDeferred();
    }

    sealed partial class AiHudOverlayHost : Control {
        const int LayoutZIndex = 1310;
        const float MarginLeft = 40f;
        const float MarginTop = 180f;
        const float MaxWidth = 520f;
        const int TitleFontSize = 20;
        static readonly Color TitleColor = new(1f, 0.84f, 0.35f);

        readonly VBoxContainer _stack;
        readonly Label _titleLabel;
        readonly Label _phaseLabel;
        readonly Label _telemetryLabel;
        readonly Label _nextLabel;
        readonly Label _auxLabel;
        readonly Godot.Timer _refreshTimer;
        string? _lastDecisionKey;
        JsonObject? _cachedSnapshot;

        public AiHudOverlayHost() {
            Name = RootName;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = LayoutZIndex;
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            Position = new Vector2(MarginLeft, MarginTop);

            _stack = new VBoxContainer {
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(MaxWidth, 0),
            };
            _stack.AddThemeConstantOverride("separation", 2);

            _titleLabel = MakeLabel(TitleFontSize, TitleColor);
            _phaseLabel = MakeLabel(12, KitLibTheme.TextSecondary);
            _telemetryLabel = MakeLabel(12, KitLibTheme.TextPrimary);
            _nextLabel = MakeLabel(12, KitLibTheme.TextPrimary);
            _auxLabel = MakeLabel(11, KitLibTheme.TextSecondary);

            _stack.AddChild(_titleLabel);
            _stack.AddChild(_phaseLabel);
            _stack.AddChild(_telemetryLabel);
            _stack.AddChild(_nextLabel);
            _stack.AddChild(_auxLabel);
            AddChild(_stack);

            _refreshTimer = new Godot.Timer { WaitTime = 2.0, Autostart = true };
            _refreshTimer.Timeout += RefreshContent;
            AddChild(_refreshTimer);

            TreeExiting += () => _refreshTimer.QueueFree();
            ThemeManager.OnThemeChanged += OnThemeChanged;
            TreeExiting += () => ThemeManager.OnThemeChanged -= OnThemeChanged;
            I18N.LanguageChanged += OnLanguageChanged;
            TreeExiting += () => I18N.LanguageChanged -= OnLanguageChanged;

            RefreshContent();
        }

        void OnThemeChanged() => ApplyTheme();

        void OnLanguageChanged() {
            _lastDecisionKey = null;
            RefreshContent();
        }

        public void SyncVisibility() {
            Visible = ShouldShow();
            if (Visible)
                RefreshContent();
        }

        void RefreshContent() {
            if (!ShouldShow()) {
                Visible = false;
                return;
            }

            Visible = true;
            var phase = AiPlayServices.StateProvider.CurrentPhase;
            _titleLabel.Text = I18N.T("ai.hud.badge", "AI hosting");
            _phaseLabel.Text = AiHudModel.PhaseShortLabel(phase);

            var decision = AiHudState.Last;
            var decisionKey = decision == null
                ? ""
                : $"{decision.Utc.Ticks}:{decision.Action}:{decision.TargetIndex}:{decision.Reason}";

            JsonObject snapshot;
            if (_cachedSnapshot != null && decisionKey == _lastDecisionKey) {
                snapshot = _cachedSnapshot;
            }
            else {
                try {
                    snapshot = AiPlayServices.StateProvider.TakeSnapshot();
                }
                catch {
                    snapshot = _cachedSnapshot ?? new JsonObject();
                }

                _cachedSnapshot = snapshot;
            }

            _telemetryLabel.Text = AiHudModel.BuildTelemetryLine(snapshot, phase);

            if (decisionKey != _lastDecisionKey) {
                _lastDecisionKey = decisionKey;
                _nextLabel.Text = AiHudModel.BuildNextActionLine(decision, snapshot);
            }

            var aux = AiHudModel.BuildAuxLine(decision, phase);
            _auxLabel.Text = aux ?? "";
            _auxLabel.Visible = !string.IsNullOrWhiteSpace(aux);

            ApplyTheme();
        }

        void ApplyTheme() {
            _titleLabel.AddThemeColorOverride("font_color", TitleColor);
            _titleLabel.AddThemeFontSizeOverride("font_size", TitleFontSize);
            _phaseLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            _telemetryLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            _nextLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            _auxLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        }

        static Label MakeLabel(int size, Color color) {
            var label = new Label {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(MaxWidth, 0),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }
    }
}
