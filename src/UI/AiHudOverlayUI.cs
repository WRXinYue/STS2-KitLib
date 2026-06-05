using System.Text.Json.Nodes;
using DevMode.AI;
using DevMode.AI.AutoPlay;
using DevMode.AI.Core.Schema;
using DevMode.AI.Sts2;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

/// <summary>In-game AI hosting HUD — plain text stack at top-left.</summary>
internal static partial class AiHudOverlayUI {
    internal const string RootName = "DevModeAiHudOverlay";

    static AiHudOverlayHost? _overlay;
    static NGlobalUi? _globalUi;

    internal static bool IsEnabled() =>
        SettingsStore.Current.AiHudEnabled;

    internal static bool ShouldShow() {
        if (!DevModeState.IsActive)
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
        if (globalUi != null)
            _globalUi = globalUi;
        EnsureAttached();
        _overlay?.SyncVisibility();
    }

    internal static void Attach(NGlobalUi globalUi) {
        _globalUi = globalUi;
        EnsureAttached();
    }

    internal static void Detach(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        _overlay = null;
        if (_globalUi == globalUi)
            _globalUi = null;
    }

    static void EnsureAttached() {
        if (_globalUi == null)
            return;

        var parent = (Node)_globalUi;
        var existing = parent.GetNodeOrNull<Control>(RootName);
        if (existing is AiHudOverlayHost host && GodotObject.IsInstanceValid(host)) {
            _overlay = host;
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
        const float MarginTop = 100f;
        const float MaxWidth = 520f;
        const int TitleFontSize = 20;
        static readonly Color TitleColor = new(1f, 0.84f, 0.35f);

        readonly VBoxContainer _stack;
        readonly Label _titleLabel;
        readonly Label _phaseLabel;
        readonly Label _strategyLabel;
        readonly Label _nextLabel;
        readonly Label _paramsLabel;
        readonly Label _scoreLabel;
        readonly Timer _refreshTimer;
        string? _lastDecisionKey;

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
            _phaseLabel = MakeLabel(12, DevModeTheme.TextSecondary);
            _strategyLabel = MakeLabel(12, DevModeTheme.TextPrimary);
            _nextLabel = MakeLabel(12, DevModeTheme.TextPrimary);
            _paramsLabel = MakeLabel(11, DevModeTheme.TextSecondary);
            _scoreLabel = MakeLabel(10, DevModeTheme.TextSecondary);

            _stack.AddChild(_titleLabel);
            _stack.AddChild(_phaseLabel);
            _stack.AddChild(_strategyLabel);
            _stack.AddChild(_nextLabel);
            _stack.AddChild(_paramsLabel);
            _stack.AddChild(_scoreLabel);
            AddChild(_stack);

            _refreshTimer = new Timer { WaitTime = 0.4, Autostart = true };
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

            JsonObject snapshot;
            try {
                snapshot = AiPlayServices.StateProvider.TakeSnapshot();
            }
            catch {
                snapshot = new JsonObject();
            }

            var decision = AiHudState.Last;
            var decisionKey = decision == null
                ? ""
                : $"{decision.Utc.Ticks}:{decision.Action}:{decision.TargetIndex}:{decision.Reason}";

            _strategyLabel.Text = I18N.T(
                "ai.hud.strategy.prefix",
                "Plan: {0}",
                AiHudModel.BuildStrategyLine(snapshot, phase));

            if (decisionKey != _lastDecisionKey) {
                _lastDecisionKey = decisionKey;
                _nextLabel.Text = AiHudModel.BuildNextActionLine(decision, snapshot);
            }

            if (SettingsStore.Current.AiHudShowParams) {
                _paramsLabel.Text = AiHudModel.BuildParamStrip(snapshot, phase) ?? "";
                _paramsLabel.Visible = !string.IsNullOrWhiteSpace(_paramsLabel.Text);
            }
            else {
                _paramsLabel.Visible = false;
            }

            if (SettingsStore.Current.AiHudShowScoreTerms && phase == GamePhase.Combat) {
                var terms = AiHudModel.BuildScoreTerms(decision);
                _scoreLabel.Text = string.IsNullOrWhiteSpace(terms) ? "" : $"[{terms}]";
                _scoreLabel.Visible = !string.IsNullOrWhiteSpace(_scoreLabel.Text);
            }
            else {
                _scoreLabel.Visible = false;
            }

            ApplyTheme();
        }

        void ApplyTheme() {
            _titleLabel.AddThemeColorOverride("font_color", TitleColor);
            _titleLabel.AddThemeFontSizeOverride("font_size", TitleFontSize);
            _phaseLabel.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            _strategyLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            _nextLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            _paramsLabel.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            _scoreLabel.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
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
