using System.Collections.Generic;
using KitLib.EnemyIntent;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>DevPanel browser for enemy intent preview and floating overlay toggle.</summary>
internal static partial class EnemyIntentUI {
    private const string RootName = "KitLibEnemyIntent";
    private const float PanelW = 720f;

    private static VBoxContainer? _browserPreviewList;
    private static Label? _browserStatus;
    private static NGlobalUi? _globalUi;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);
        _panelOpen = true;
        _globalUi = globalUi;

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 10);

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("enemyIntent.title", "Enemy intents")));
        var subtitle = new Label {
            Text = I18N.T("enemyIntent.subtitle",
                "Click a turn in the intent chain, then pick a move to override that turn."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        _browserStatus = new Label { Text = "" };
        _browserStatus.AddThemeFontSizeOverride("font_size", 11);
        _browserStatus.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        vbox.AddChild(_browserStatus);

        var previewScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _browserPreviewList = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _browserPreviewList.AddThemeConstantOverride("separation", 10);
        previewScroll.AddChild(_browserPreviewList);
        vbox.AddChild(previewScroll);

        var overlayToggle = new CheckButton {
            Text = I18N.T("enemyIntent.overlay.enabled", "Show floating intent panel"),
            ButtonPressed = SettingsStore.Current.CombatStatsMonsterIntentOverlayEnabled,
        };
        overlayToggle.AddThemeFontSizeOverride("font_size", 11);
        overlayToggle.Pressed += () => {
            SettingsStore.SetCombatStatsMonsterIntentOverlayEnabled(overlayToggle.ButtonPressed);
            MonsterIntentOverlayUI.SyncState(globalUi);
        };
        vbox.AddChild(overlayToggle);

        void OnIntentChanged() {
            if (!GodotObject.IsInstanceValid(root))
                return;
            RefreshBrowserPreview(preserveSelection: true);
            MonsterIntentOverlayUI.SyncState(globalUi);
        }

        MonsterIntentOverlayTracker.Changed += OnIntentChanged;
        root.TreeExiting += () => {
            if (((Node)globalUi).GetNodeOrNull<Control>(RootName) != root)
                return;
            MonsterIntentOverlayTracker.Changed -= OnIntentChanged;
            _panelOpen = false;
            _browserPreviewList = null;
            _browserStatus = null;
            if (_globalUi == globalUi)
                _globalUi = null;
            DevPanelUI.ResetContextPaneToDefault();
        };

        ((Node)globalUi).AddChild(root);
        DevPanelUI.SetContextPaneActive(PanelContextId);
        RefreshBrowserPreview();
        MonsterIntentOverlayUI.SyncState(globalUi);
        DevPanelUI.RefreshContextPane();
    }

    public static void Remove(NGlobalUi globalUi) {
        _panelOpen = false;
        _browserPreviewList = null;
        _browserStatus = null;
        if (_globalUi == globalUi)
            _globalUi = null;
        DevPanelUI.ResetContextPaneToDefault();
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        MonsterIntentOverlayUI.SyncState(globalUi);
    }

    internal static void RefreshAfterApply(MonsterIntentEntry appliedEntry) {
        RefreshBrowserPreview(preserveSelection: true);
        MonsterIntentOverlayUI.SyncState(_globalUi);
        DevPanelUI.RefreshContextPane();
    }

    private static void RefreshBrowserPreview(bool preserveSelection = false) {
        if (_browserPreviewList == null || _browserStatus == null)
            return;

        if (!KitLibState.IsActive
            || CombatManager.Instance?.IsInProgress != true
            || CombatManager.Instance.DebugOnlyGetState() is not { } state) {
            ClearPreviewList(_browserPreviewList);
            _browserStatus.Text = I18N.T("enemyIntent.empty", "No active enemies.");
            return;
        }

        var entries = MonsterIntentReader.CaptureCurrent(state);
        if (entries.Count == 0) {
            ClearPreviewList(_browserPreviewList);
            _browserStatus.Text = I18N.T("enemyIntent.empty", "No active enemies.");
            return;
        }

        _browserStatus.Text = I18N.T("enemyIntent.status.live", "Live — {0} enemies", entries.Count);
        IntentEditorRows.Sync(_browserPreviewList, entries, preserveSelection);
    }

    private static void ClearPreviewList(VBoxContainer list) {
        foreach (var child in list.GetChildren())
            child.QueueFree();
    }
}
