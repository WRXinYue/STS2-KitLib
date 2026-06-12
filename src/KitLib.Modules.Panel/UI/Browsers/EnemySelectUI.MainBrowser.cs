using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    internal sealed class MainBrowserState {
        public required NGlobalUi GlobalUi;
        public required VBoxContainer ContentHost;
        public required DevPanelUI.DualColumnOverlayHandle Dual;
        public RoomType? EncounterFilter;
        public Label StatusLabel = null!;
    }

    public static void ShowMain(NGlobalUi globalUi) {
        Hide(globalUi);

        var dual = DevPanelUI.CreateDualColumnOverlay(new DevPanelUI.DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = RootName,
            DualMetaKey = DualMetaKey,
            CarrierNodeName = CarrierNodeName,
            MainDefaultWidth = DefaultMainWidth,
            ExtDefaultWidth = DefaultExtWidth,
            FallbackClose = () => Hide(globalUi),
        });

        _mainDual = dual;
        _mainGlobalUi = globalUi;

        _mapDetailScroll = new ScrollContainer {
            Name = "EnemyMapDetailScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        _mapDetailHost = new VBoxContainer {
            Name = "EnemyMapDetailHost",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _mapDetailHost.AddThemeConstantOverride("separation", 8);
        _mapDetailScroll.AddChild(_mapDetailHost);

        _extensionHost = new VBoxContainer {
            Name = "EnemyExtensionHost",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
        };
        _extensionHost.AddThemeConstantOverride("separation", 8);

        var extLayout = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        extLayout.AddChild(_mapDetailScroll);
        extLayout.AddChild(_extensionHost);
        dual.ExtContent.AddChild(extLayout);

        dual.Root.TreeExiting += () => {
            if (_mainDual?.Root != dual.Root)
                return;
            _mainDual = null;
            _mainGlobalUi = null;
            _mapDetailScroll = null;
            _mapDetailHost = null;
            _extensionHost = null;
            _activeMapSession = null;
        };

        var state = new MainBrowserState {
            GlobalUi = globalUi,
            Dual = dual,
            ContentHost = new VBoxContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            },
            EncounterFilter = null,
        };
        state.ContentHost.AddThemeConstantOverride("separation", 8);

        BuildMainNav(dual.MainContent);
        dual.MainContent.AddChild(DevPanelUI.CreateOverlaySeparator());
        dual.MainContent.AddChild(state.ContentHost);

        state.StatusLabel = new Label { Text = "" };
        state.StatusLabel.AddThemeFontSizeOverride("font_size", 11);
        state.StatusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        state.StatusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        dual.MainContent.AddChild(state.StatusLabel);

        SwitchMainView(state);
        dual.AttachToScene();
        Callable.From(() => {
            dual.PrepareExtensionVisible();
            dual.AnimateExtensionSlideIn();
        }).CallDeferred();
    }

    private static void BuildMainNav(VBoxContainer vbox) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var title = new Label {
            Text = I18N.T("panel.enemies", "Enemies"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        row.AddChild(title);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
    }

    internal static void SwitchMainView(MainBrowserState state) {
        foreach (var child in state.ContentHost.GetChildren())
            ((Node)child).QueueFree();

        BuildMapTab(state);
        state.StatusLabel.Text = I18N.T(
            "enemy.mapHint",
            "Click combat nodes on the map to edit. Run rules apply to this run only.");
    }
}
