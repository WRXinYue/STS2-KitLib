using System;
using System.Collections.Generic;
using KitLib.CombatStats;
using KitLib.EnemyIntent;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string ContextPaneRootName = "KitLibContextPaneRoot";

    /// <summary>Same width as the left icon rail — browser panels reserve this on the right.</summary>
    public static float ContextPaneW => BrowserRailW;

    /// <summary>Right margin for browser panels — leaves room for the game context rail.</summary>
    public static float BrowserContentRight => BrowserPanelRight + BrowserRailW;

    /// <summary>Effective right inset for full-width browser panels (0 when context rail is hidden).</summary>
    public static float EffectiveBrowserContentRight =>
        _contextPaneShown ? BrowserContentRight : BrowserPanelRight;

    private static PanelContainer? _contextPane;
    private static StyleBoxFlat? _contextPaneStyle;
    private static DevPanelSidebarHost? _contextHost;
    private static readonly Dictionary<string, IDevPanelSidebarProvider> _contextProviders = new();
    private static string[] _defaultContextIds = ["default.players"];
    private static bool _contextPaneJoined;
    private static bool _contextRefreshPending;
    private static bool _contextRefreshNeedsIntent;
    private static ulong _lastIntentRefreshFrame;
    private static ulong _lastStatsRefreshFrame;
    private static NGlobalUi? _contextGlobalUi;
    private static bool _contextPaneShown;
    private static Tween? _contextPaneTween;

    private const float ContextPaneVisibleOffsetLeft = -(BrowserRailLeft + BrowserRailW);
    private const float ContextPaneVisibleOffsetRight = -BrowserRailLeft;
    private const float ContextPaneHiddenOffsetLeft = -BrowserRailLeft;
    private const float ContextPaneHiddenOffsetRight = BrowserRailLeft;

    internal static void AttachContextPane(NGlobalUi globalUi) {
        if (((Node)globalUi).GetNodeOrNull<Control>(ContextPaneRootName) != null)
            return;

        var root = new Control {
            Name = ContextPaneRootName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1200,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _contextPane = new PanelContainer {
            Name = "ContextPane",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetLeft = ContextPaneHiddenOffsetLeft,
            OffsetRight = ContextPaneHiddenOffsetRight,
            OffsetTop = 0,
            OffsetBottom = 0,
            Modulate = new Color(1, 1, 1, 0),
        };
        _contextPaneStyle = CreateMirroredRailStyle();
        _contextPane.AddThemeStyleboxOverride("panel", _contextPaneStyle);

        _contextHost = new DevPanelSidebarHost("game.context.host", railCompact: true);
        _contextPane.AddChild(_contextHost);
        root.AddChild(_contextPane);

        CombatStatsUI.EnsureGameContextPane(_contextHost);
        EnemyIntentUI.EnsureGameContextPane(_contextHost);
        EnemySelectUI.EnsureGameContextPane(_contextHost);
        CombatStatsUI.AttachMultiplayerOverlay(globalUi);
        MonsterIntentOverlayUI.Attach(globalUi);

        CombatStatsTracker.Changed += OnCombatStatsTrackerChanged;
        MonsterIntentOverlayTracker.Changed += OnMonsterIntentTrackerChanged;

        _contextGlobalUi = globalUi;
        _contextPaneShown = false;

        root.TreeExiting += () => {
            CombatStatsTracker.Changed -= OnCombatStatsTrackerChanged;
            MonsterIntentOverlayTracker.Changed -= OnMonsterIntentTrackerChanged;
            _contextPaneTween?.Kill();
            _contextPaneTween = null;
            _contextPane = null;
            _contextPaneStyle = null;
            _contextHost = null;
            _contextProviders.Clear();
            _contextPaneJoined = false;
            _contextGlobalUi = null;
            _contextPaneShown = false;
        };

        ((Node)globalUi).AddChild(root);
        SetContextPaneActiveMany(_defaultContextIds);
        CombatStatsUI.RefreshDefaultGameContext();
        EnemyIntentUI.RefreshDefaultContext();
        MonsterIntentOverlayUI.SyncState(globalUi);
        UpdateContextPaneVisibility();
    }

    internal static void DetachContextPane(NGlobalUi globalUi) {
        CombatStatsUI.DetachMultiplayerOverlay(globalUi);
        MonsterIntentOverlayUI.Detach(globalUi);
        CombatStatsTracker.Changed -= OnCombatStatsTrackerChanged;
        MonsterIntentOverlayTracker.Changed -= OnMonsterIntentTrackerChanged;
        _contextPaneTween?.Kill();
        _contextPaneTween = null;
        _contextPane = null;
        _contextPaneStyle = null;
        _contextHost = null;
        _contextProviders.Clear();
        _contextPaneJoined = false;
        _contextGlobalUi = null;
        _contextPaneShown = false;
        ((Node)globalUi).GetNodeOrNull<Control>(ContextPaneRootName)?.QueueFree();
    }

    internal static void RegisterContextProvider(string id, IDevPanelSidebarProvider provider) {
        _contextProviders[id] = provider;
        if (_contextHost != null)
            _contextHost.Register(id, provider);
    }

    internal static void EnsureContextProvider<T>(
        ref DevPanelSidebarHost? cachedHost,
        DevPanelSidebarHost host,
        ref T? panel,
        string id,
        Func<T> factory) where T : class, IDevPanelSidebarProvider {
        if (cachedHost == host && panel != null)
            return;

        cachedHost = host;
        panel = factory();
        RegisterContextProvider(id, panel);
    }

    internal static void RunCombatAction(Func<System.Threading.Tasks.Task> action, Action? onCompleted = null) {
        TaskHelper.RunSafely(WrapCombatAction(action, onCompleted));
    }

    private static async System.Threading.Tasks.Task WrapCombatAction(
        Func<System.Threading.Tasks.Task> action,
        Action? onCompleted) {
        MainFile.Logger.Info("[KitLib.CombatAdd] RunCombatAction starting");
        try {
            await action();
            MainFile.Logger.Info("[KitLib.CombatAdd] RunCombatAction finished");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib.CombatAdd] RunCombatAction failed: {ex}");
            throw;
        }
        finally {
            RefreshContextPane();
            EnemySelectUI.RefreshMapCombatDetailIfOpen();
            onCompleted?.Invoke();
        }
    }

    internal static void SetDefaultContextIds(params string[] ids) {
        if (ids.Length > 0)
            _defaultContextIds = ids;
    }

    internal static void SetContextPaneActive(string id) {
        _contextHost?.SetActive(id);
        UpdateContextPaneVisibility();
    }

    internal static void SetContextPaneActiveMany(params string[] ids) {
        _contextHost?.SetActiveMany(ids);
        UpdateContextPaneVisibility();
    }

    internal static bool IsContextPaneActive(string id) {
        var ids = _contextHost?.ActiveIds;
        if (ids == null)
            return false;
        foreach (string activeId in ids) {
            if (activeId == id)
                return true;
        }
        return false;
    }

    internal static bool HasActiveContext(params string[] ids) {
        var active = _contextHost?.ActiveIds;
        if (active == null || ids.Length == 0)
            return false;
        foreach (string id in ids) {
            bool found = false;
            foreach (string activeId in active) {
                if (activeId == id) {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    internal static void RefreshContextPaneChrome() => _contextHost?.RefreshChrome();

    internal static void RefreshContextProviders(params string[] ids) {
        _contextHost?.RefreshProviders(ids);
        UpdateContextPaneVisibility();
    }

    internal static void RefreshContextPane() {
        _contextHost?.RefreshActive();
        UpdateContextPaneVisibility();
    }

    internal static void UpdateContextPaneVisibility() {
        if (!GodotObject.IsInstanceValid(_contextPane) || _contextHost == null)
            return;
        bool show = SettingsStore.Current.GameContextPaneEnabled && _contextHost.ActiveHasContent;
        SlideContextPane(show);
    }

    internal static void OnGameContextPaneSettingChanged() {
        if (SettingsStore.Current.GameContextPaneEnabled) {
            CombatStatsUI.RefreshDefaultGameContext();
            EnemyIntentUI.RefreshDefaultContext();
            EnemySelectUI.RefreshCombatContext();
        }
        UpdateContextPaneVisibility();
        if (_contextGlobalUi != null)
            NotifyBrowserContextLayoutChanged(_contextGlobalUi);
    }

    internal static void ResetContextPaneToDefault() {
        SetContextPaneActiveMany(_defaultContextIds);
        CombatStatsUI.RefreshDefaultGameContext();
        EnemyIntentUI.RefreshDefaultContext();
        EnemySelectUI.RefreshCombatContext();
        UpdateContextPaneVisibility();
    }

    /// <summary>Flush inner-left edge when a browser panel is open (mirrors left-rail splice).</summary>
    public static void SpliceContextPane(bool joined) {
        _contextPaneJoined = joined;
        ApplyContextPaneSplice();
    }

    private static StyleBoxFlat CreateMirroredRailStyle() {
        return new StyleBoxFlat {
            BgColor = ColRailBg,
            CornerRadiusTopLeft = Radius,
            CornerRadiusBottomLeft = Radius,
            CornerRadiusTopRight = Radius,
            CornerRadiusBottomRight = Radius,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = ColRailBorder,
            ShadowColor = new Color(0, 0, 0, 0.25f),
            ShadowSize = 8,
        };
    }

    private static void ApplyContextPaneSplice() {
        if (_contextPaneStyle == null)
            return;
        int r = _contextPaneJoined ? 0 : Radius;
        _contextPaneStyle.CornerRadiusTopLeft = r;
        _contextPaneStyle.CornerRadiusBottomLeft = r;
        _contextPaneStyle.BorderWidthLeft = _contextPaneJoined ? 0 : 1;
        if (_contextPaneJoined) {
            _contextPaneStyle.ShadowSize = 0;
            _contextPaneStyle.ShadowOffset = Vector2.Zero;
        }
        else {
            _contextPaneStyle.ShadowSize = 8;
            _contextPaneStyle.ShadowOffset = Vector2.Zero;
        }
    }

    private static void ApplyContextPaneTheme() {
        if (_contextPaneStyle == null)
            return;
        _contextPaneStyle.BgColor = ColRailBg;
        _contextPaneStyle.BorderColor = ColRailBorder;
        ApplyContextPaneSplice();
    }

    private static void OnCombatStatsTrackerChanged() {
        if (!SettingsStore.Current.GameContextPaneEnabled)
            return;
        EnqueueContextRefresh(intent: false);
    }

    private static void OnMonsterIntentTrackerChanged() {
        if (!SettingsStore.Current.GameContextPaneEnabled)
            return;
        EnqueueContextRefresh(intent: true);
    }

    private static void EnqueueContextRefresh(bool intent) {
        if (!GodotObject.IsInstanceValid(_contextHost))
            return;
        if (intent)
            _contextRefreshNeedsIntent = true;
        if (_contextRefreshPending)
            return;
        _contextRefreshPending = true;
        Callable.From(() => {
            _contextRefreshPending = false;
            if (!GodotObject.IsInstanceValid(_contextHost))
                return;

            bool needsIntent = _contextRefreshNeedsIntent;
            _contextRefreshNeedsIntent = false;

            if (needsIntent)
                RunIntentContextRefresh();
            else if (Engine.GetProcessFrames() != _lastStatsRefreshFrame) {
                _lastStatsRefreshFrame = Engine.GetProcessFrames();
                CombatStatsUI.RefreshStatsContextOnly();
            }

            UpdateContextPaneVisibility();

            if (_contextRefreshNeedsIntent) {
                if (!GodotObject.IsInstanceValid(_contextHost))
                    _contextRefreshNeedsIntent = false;
                else
                    EnqueueContextRefresh(intent: true);
            }
        }).CallDeferred();
    }

    private static void RunIntentContextRefresh() {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _lastIntentRefreshFrame)
            return;
        _lastIntentRefreshFrame = frame;

        if (CombatStatsUI.IsPanelOpen) {
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        if (EnemyIntentUI.IsPanelOpen) {
            EnemyIntentUI.OnContextChanged();
            return;
        }

        CombatStatsUI.RefreshIntentGameContext();
    }

    private static void SlideContextPane(bool show) {
        if (!GodotObject.IsInstanceValid(_contextPane))
            return;
        if (_contextPaneShown == show)
            return;

        _contextPaneShown = show;
        if (GodotObject.IsInstanceValid(_contextPane)) {
            _contextPane.MouseFilter = show
                ? Control.MouseFilterEnum.Stop
                : Control.MouseFilterEnum.Ignore;
        }
        ReconcileContextPaneBrowserMargin(_contextGlobalUi);
        if (_contextGlobalUi != null)
            ScheduleBrowserContextMerge(_contextGlobalUi);

        bool browserOpen = (_browserOverlayCount + _browserRailHoldCount) > 0;
        _contextPaneTween?.Kill();
        _contextPaneTween = _contextPane.CreateTween();

        float targetAlpha = show ? 1f : 0f;

        if (browserOpen) {
            _contextPaneTween
                .TweenProperty(_contextPane, "modulate:a", targetAlpha, 0.15f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        else {
            float targetLeft = show ? ContextPaneVisibleOffsetLeft : ContextPaneHiddenOffsetLeft;
            float targetRight = show ? ContextPaneVisibleOffsetRight : ContextPaneHiddenOffsetRight;

            _contextPane.AnchorLeft = 1;
            _contextPane.AnchorTop = ContextPaneDefaultTop;
            _contextPane.AnchorRight = 1;
            _contextPane.AnchorBottom = ContextPaneDefaultBottom;
            _contextPane.OffsetTop = 0;
            _contextPane.OffsetBottom = 0;

            _contextPaneTween.TweenProperty(_contextPane, "offset_left", targetLeft, 0.2f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _contextPaneTween.Parallel()
                .TweenProperty(_contextPane, "offset_right", targetRight, 0.2f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _contextPaneTween.Parallel()
                .TweenProperty(_contextPane, "modulate:a", targetAlpha, 0.15f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        _contextPaneTween.Chain().TweenCallback(Callable.From(() => {
            if (_contextGlobalUi != null)
                NotifyBrowserContextLayoutChanged(_contextGlobalUi);
        }));
    }

    private static void ReconcileContextPaneBrowserMargin(NGlobalUi? globalUi) {
        if (globalUi == null)
            return;

        float rightInset = EffectiveBrowserContentRight;
        var parent = (Node)globalUi;
        foreach (var child in parent.GetChildren()) {
            if (child is not Control root)
                continue;
            string name = root.Name.ToString();
            if (!name.StartsWith("KitLib", StringComparison.Ordinal))
                continue;

            var clipHost = root.GetNodeOrNull<Control>("BrowserPanelClipHost");
            var panel = clipHost?.GetNodeOrNull<PanelContainer>("BrowserPanel");
            if (panel == null || panel.AnchorRight < 0.5f)
                continue;

            panel.OffsetRight = -rightInset;
            root.GetNodeOrNull<BrowserPanelWidthGrip>("PanelWidthGrip")?.Sync();
        }
    }
}
