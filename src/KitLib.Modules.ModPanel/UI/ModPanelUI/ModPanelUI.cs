using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using KitLib.Integration;
using KitLib.Modding;
using KitLib.ModPanel.Diagnostics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
namespace KitLib.UI;
/// <summary>
/// Full-screen overlay: root frame, sidebar column, and content column mirror STS2-RitsuLib
/// <c>RitsuModSettingsSubmenu</c> layout (Apr 2026); options body stays DevMode-owned.
/// </summary>
public static partial class ModPanelUI {
    private const int ZOrder = 2000;
    private static ModPanelSubmenu? _root;
    private static NMainMenu? _hostMainMenu;
    private static string? _pendingInitialModId;
    private static string? _pendingInitialPageId;
    private static string? _pendingInitialPageModId;
    private static Action? _themeRefresh;
    private static ModPanelDetailTitleEditor? _detailTitleEditor;

    internal static void RegisterThemeRefresh(Action refresh) => _themeRefresh = refresh;

    internal static void ClearThemeRefresh() => _themeRefresh = null;

    internal static void FlushPendingTitleEdit() {
        if (_detailTitleEditor != null && GodotObject.IsInstanceValid(_detailTitleEditor))
            _detailTitleEditor.CommitEdit();
    }

    internal static void HandleThemeChanged() => _themeRefresh?.Invoke();

    public static bool TryGetScreenContext(out IScreenContext? context) {
        if (_root != null && GodotObject.IsInstanceValid(_root)) {
            context = _root;
            return true;
        }
        context = null;
        return false;
    }
    public static void Show(NMainMenu mainMenu, string? initialModId = null, string? initialPageId = null) {
        var perf = ModPanelPerf.Start();
        MainFile.Logger.Info("KitLib: Opening mod panel…");
        TeardownShell();
        _pendingInitialModId = initialModId;
        _pendingInitialPageId = initialPageId;
        _pendingInitialPageModId = initialModId;
        RitsuModSettingsEmbedHost.Ensure();
        _hostMainMenu = mainMenu;
        _root = mainMenu.SubmenuStack.PushSubmenuType<ModPanelSubmenu>();
        ModPanelPerf.Log("open.pushSubmenu", perf);
        var reportSw = ModPanelPerf.Start();
        var openReport = BuildOpenReport();
        ModPanelDiagnostics.LogOpenReport(openReport);
        ModPanelPerf.Log("open.buildReport", reportSw);
        ModPanelDiagnostics.LogSidebarLayoutDeferred(_root, openReport);
        ModPanelPerf.Log("open.total", perf);
    }
    public static void Hide() => TeardownShell();
    /// <summary>Back button, input forwarder, and re-open <see cref="Show" /> all use this one exit path.</summary>
    private static void TeardownShell() {
        if (_hostMainMenu != null && GodotObject.IsInstanceValid(_hostMainMenu)) {
            if (_hostMainMenu.SubmenuStack.Peek() is ModPanelSubmenu)
                _hostMainMenu.SubmenuStack.Pop();
            _hostMainMenu = null;
        }
        _root = null;
    }
    internal static void OnSubmenuPopped(ModPanelSubmenu submenu) {
        if (_root == submenu)
            _root = null;
        _hostMainMenu = null;
        _detailTitleEditor = null;
    }
    public static bool IsVisible =>
        _root != null && GodotObject.IsInstanceValid(_root) && _root.Visible;

    internal static NMainMenu? TryGetHostMainMenu() =>
        _hostMainMenu != null && GodotObject.IsInstanceValid(_hostMainMenu) ? _hostMainMenu : null;
    private static bool IsSelectableSidebarMod(KitLibModEntry entry) => entry.IsLoaded;

    private static string ResolveInitialSidebarModId(
        ModPanelSidebarPlan sidebarPlan,
        IReadOnlyList<KitLibModEntry> ordered) {
        var pending = _pendingInitialModId;
        _pendingInitialModId = null;
        if (!string.IsNullOrWhiteSpace(pending)
            && ordered.Any(e => string.Equals(e.Id, pending, StringComparison.OrdinalIgnoreCase)))
            return pending;
        return sidebarPlan.InitialSelectedModId;
    }

    private static string ResolveShowcaseModId()
        => ModPanelSidebarPlanner.ResolveShowcaseModId(
            ModRuntime.Registry.GetAllEntries(),
            KitLibModuleIds.Core,
            RitsuModSettingsBridge.IsRitsuFrameworkModId,
            IsSelectableSidebarMod);

    private static ModPanelOpenReport BuildOpenReport() {
        var registry = ModRuntime.Registry.GetAllEntries();
        var plan = ModPanelSidebarPlanner.Plan(
            registry,
            KitLibModuleIds.Core,
            RitsuModSettingsBridge.IsRitsuFrameworkModId,
            IsSelectableSidebarMod);
        var embedProbe = ModPanelEmbedHostProbe.Probe(RitsuModSettingsBridge.TryGetRitsuAssembly());
        return ModPanelDiagnostics.BuildOpenReport(
            plan,
            embedProbe,
            ModPanelDiagnostics.CountRawLoadedMods(),
            registry.Count);
    }
    internal static void BuildInto(ModPanelSubmenu root) {
        // Backdrop: NMainMenu.EnableBackstop (BlurBackstop + shader), same as vanilla settings / submenu stack.
        var frame = new MarginContainer {
            Name = "Frame",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.GrowHorizontal = Control.GrowDirection.Both;
        frame.GrowVertical = Control.GrowDirection.Both;
        frame.AddThemeConstantOverride("margin_left", 160);
        frame.AddThemeConstantOverride("margin_top", 72);
        frame.AddThemeConstantOverride("margin_right", 160);
        frame.AddThemeConstantOverride("margin_bottom", 72);
        root.AddChild(frame);
        // MarginContainer lays out its single child: use size flags only (anchor preset fights the container).
        var outer = new VBoxContainer {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        outer.AddThemeConstantOverride("separation", 18);
        frame.AddChild(outer);
        var hintsRow = CreatePaneHotkeyHintsRow();
        outer.AddChild(hintsRow);
        var body = new HBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        body.AddThemeConstantOverride("separation", 20);
        outer.AddChild(body);
        var (contentPanel, ritsuContentList, pageTabChrome, modBanner) = BuildContentPanel();
        _detailTitleEditor = modBanner.TitleEditor;
        body.AddChild(BuildSidebarPanel(root, hintsRow, ritsuContentList, pageTabChrome, modBanner));
        body.AddChild(contentPanel);
        // Same control as NSubmenu: NBackButton starts off-screen until Enable() (see NSubmenu.OnScreenVisibilityChange).
        var backButton = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/back_button"))
            .Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
        backButton.Name = "BackButton";
        backButton.ZIndex = ZOrder + 50;
        backButton.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(backButton);
        root.AddChild(new ShellInputForwarder(TeardownShell) {
            Name = "ShellInputForwarder",
            ProcessMode = Node.ProcessModeEnum.Always,
        });
    }
    /// <summary>Controller pane hotkeys row; icons filled by <see cref="ModPanelControllerSupport" />.</summary>
    private static HBoxContainer CreatePaneHotkeyHintsRow() {
        var row = new HBoxContainer {
            Name = "PaneHotkeyHints",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(CreatePaneHotkeyIcon("BackHotkeyIcon", true));
        row.AddChild(new Control {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        row.AddChild(CreatePaneHotkeyIcon("TabLeftHotkeyIcon"));
        row.AddChild(CreatePaneHotkeyIcon("TabRightHotkeyIcon"));
        row.AddChild(CreatePaneHotkeyIcon("SelectHotkeyIcon", true));
        return row;
    }
    private static TextureRect CreatePaneHotkeyIcon(string name, bool visible = false) {
        return new TextureRect {
            Name = name,
            Visible = visible,
            CustomMinimumSize = new Vector2(44f, 32f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
    }
    private static Control BuildSidebarPanel(ModPanelSubmenu shell, HBoxContainer hintsRow,
        VBoxContainer ritsuContentList, ModPanelPageTabChrome pageTabChrome, ModDetailBannerControls modBanner) {
        if (!KitLibHost.IsModuleLoaded(KitLibModuleIds.User)) {
            return BuildMissingUserModulePanel();
        }

        var showcaseModId = ResolveShowcaseModId();
        var panel = new Panel {
            Name = "ModPanelSidebarPanel",
            CustomMinimumSize = new Vector2(ModPanelUiMetrics.SidebarPanelMinWidth, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
        var mainVBox = new VBoxContainer {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        mainVBox.AddThemeConstantOverride("separation", 0);
        panel.AddChild(mainVBox);
        var titleEditor = modBanner.TitleEditor;
        var metaChipRow = modBanner.MetaChipRow;
        var modIdLabel = modBanner.ModIdLabel;
        var descScroll = modBanner.DescScroll;
        var descLabel = modBanner.DescLabel;
        var modIcon = modBanner.ModIcon;
        var previewPlaceholder = modBanner.PreviewPlaceholder;
        var previewCaption = modBanner.PreviewCaption;
        var listFrame = new MarginContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        listFrame.AddThemeConstantOverride("margin_left", ModPanelUiMetrics.SidebarContentMarginH);
        listFrame.AddThemeConstantOverride("margin_top", 0);
        listFrame.AddThemeConstantOverride("margin_right", ModPanelUiMetrics.SidebarContentMarginH);
        listFrame.AddThemeConstantOverride("margin_bottom", 0);
        var listHeader = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        listHeader.AddThemeConstantOverride("separation", 0);
        listFrame.AddChild(listHeader);
        var pendingRestartBanner = new Label {
            Name = "ModPanelPendingRestartBanner",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.Word,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left,
            LabelSettings = new LabelSettings {
                FontSize = 12,
                FontColor = new Color(0.95f, 0.78f, 0.42f, 0.98f),
            },
        };
        listHeader.AddChild(pendingRestartBanner);
        var scroll = SidebarModListScrollBuilder.Create(out var scrollInner);
        scrollInner.AddThemeConstantOverride("separation", 0);
        var modButtonList = new VBoxContainer {
            Name = "ModPanelSidebarModList",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        modButtonList.AddThemeConstantOverride("separation", 0);
        scrollInner.AddChild(modButtonList);
        var sidebarPlan = ModPanelSidebarPlanner.Plan(
            ModRuntime.Registry.GetAllEntries(),
            KitLibModuleIds.Core,
            RitsuModSettingsBridge.IsRitsuFrameworkModId,
            IsSelectableSidebarMod);
        var ordered = new List<KitLibModEntry>(sidebarPlan.OrderedMods);
        void RefreshPendingRestartBanner() {
            var pending = ModRuntime.LoadSettings.HasPendingRestartChanges();
            pendingRestartBanner.Visible = pending;
            if (pending) {
                pendingRestartBanner.Text = I18N.T("modpanel.sidebar.pendingRestart",
                    "Restart the game to apply mod enable/disable changes.");
            }
        }
        var modRows = new List<SidebarModRowVm>();
        var initialSelectedId = ResolveInitialSidebarModId(sidebarPlan, ordered);
        var selectedModId = initialSelectedId;
        var contentState = new ModPanelContentState();
        Control? scopeFocusTarget = null;
        ModPanelControllerSupport controllerSupport = new();
        pageTabChrome.PageSelected += id => {
            contentState.PageId = id;
            RebuildRitsuRightPane();
        };
        void RebuildRitsuRightPane() {
            RefreshSettingsContent(ritsuContentList, pageTabChrome, selectedModId, contentState, RebuildRitsuRightPane);
            Callable.From(() => {
                ModPanelFocusWiring.Wire(modRows, selectedModId, contentState.PageId, pageTabChrome, ritsuContentList,
                    scopeFocusTarget);
                pageTabChrome.RefreshTriggerIcons();
                if (GodotObject.IsInstanceValid(controllerSupport))
                    controllerSupport.RefreshHints();
            }).CallDeferred();
        }
        controllerSupport.Configure(pageTabChrome, hintsRow);
        void RefreshModRowChrome(bool animateSelection = true) {
            foreach (var row in modRows) {
                var sel = string.Equals(row.Id, selectedModId, StringComparison.OrdinalIgnoreCase);
                var focused = row.Host.HasFocus();
                if (animateSelection && !row.Pressing && !focused)
                    ModPanelSidebarMotion.AnimateRowStyle(row.InnerStyle, row.BgPanel, sel, row.Pressing, focused);
                else
                    ModPanelSidebarMotion.ApplyRowStyle(row.InnerStyle, row.BgPanel, sel, row.Pressing, focused);
            }
        }
        void RefreshSidebarRowTitle(string modId, ModEntrySource source) {
            foreach (var row in modRows) {
                if (!string.Equals(row.Id, modId, StringComparison.OrdinalIgnoreCase)
                    || row.Entry.Source != source)
                    continue;
                var displayName = ResolveSidebarListTitle(row.Entry);
                row.TitleLabel.Text = displayName;
                break;
            }
        }
        titleEditor.TitleCommitted += RefreshSidebarRowTitle;
        void SelectMod(string id) {
            selectedModId = id;
            RefreshModRowChrome();
            var rowEntry = modRows.Find(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))?.Entry;
            var m = ModPanelModBanner.TryFindMod(id);
            if (m != null) {
                ApplySidebarTexts(m, id, titleEditor, metaChipRow, modIdLabel, descScroll, descLabel);
                var tex = ModPanelModBanner.TryLoadModIcon(m, id);
                ApplyPreviewState(tex, false, modIcon, previewPlaceholder, previewCaption);
            }
            else if (rowEntry != null) {
                ApplySidebarTextsFromEntry(rowEntry.Value, titleEditor, metaChipRow, modIdLabel, descScroll, descLabel);
                ApplyPreviewState(null, true, modIcon, previewPlaceholder, previewCaption);
            }
            else {
                ApplySidebarTexts(null, id, titleEditor, metaChipRow, modIdLabel, descScroll, descLabel);
                ApplyPreviewState(null, true, modIcon, previewPlaceholder, previewCaption);
            }
            var pagesForMod = rowEntry?.IsLoaded == true
                ? ResolveInitialPageId(id)
                : "";
            contentState.PageId = pagesForMod;
            RebuildRitsuRightPane();
            var selectedRow = modRows.Find(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            if (selectedRow != null) {
                shell.SetInitialFocusedControl(selectedRow.Host);
                if (NControllerManager.Instance?.IsUsingController == true)
                    Callable.From(() => selectedRow.Host.TryGrabFocus()).CallDeferred();
            }
        }
        if (ordered.Count == 0) {
            var fallback = ModPanelModBanner.TryFindMod(showcaseModId);
            ApplySidebarTexts(fallback, showcaseModId, titleEditor, metaChipRow, modIdLabel, descScroll, descLabel);
            var tex0 = ModPanelModBanner.TryLoadModIcon(fallback, showcaseModId);
            ApplyPreviewState(tex0, fallback == null, modIcon, previewPlaceholder, previewCaption);
            contentState.PageId = ResolveInitialPageId(showcaseModId);
            Callable.From(RebuildRitsuRightPane).CallDeferred();
        }
        else {
            foreach (var info in ordered) {
                var displayName = ResolveSidebarListTitle(info);
                var isSel = string.Equals(info.Id, initialSelectedId, StringComparison.OrdinalIgnoreCase);
                var captured = info;
                var section = new VBoxContainer {
                    Name = $"SidebarModSection_{info.Id}",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                section.AddThemeConstantOverride("separation", 0);
                modButtonList.AddChild(section);
                var card = new PanelContainer {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ClipContents = true,
                };
                card.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
                section.AddChild(card);
                var cardContent = new VBoxContainer {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                cardContent.AddThemeConstantOverride("separation", 0);
                card.AddChild(cardContent);
                var innerStyle = new StyleBoxFlat();
                var rowHost = new Control {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    MouseFilter = Control.MouseFilterEnum.Stop,
                    MouseDefaultCursorShape = Control.CursorShape.Arrow,
                    CustomMinimumSize = new Vector2(0f, ModPanelUiMetrics.SidebarModRowMinHeight),
                    FocusMode = Control.FocusModeEnum.All,
                };
                var bgPanel = new Panel {
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ClipContents = true,
                };
                bgPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                bgPanel.AddThemeStyleboxOverride("panel", innerStyle);
                ModPanelSidebarMotion.ApplyRowStyle(innerStyle, bgPanel, isSel, false, false);
                rowHost.AddChild(bgPanel);
                var rowContent = new HBoxContainer {
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Alignment = BoxContainer.AlignmentMode.Center,
                };
                rowContent.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                rowContent.AddThemeConstantOverride("separation", 8);
                rowContent.OffsetLeft = 8 + (int)ModPanelUiMetrics.SidebarModAccentBarWidth
                    + ModPanelUiMetrics.SidebarModAccentTextGutter;
                rowContent.OffsetRight = -12;
                rowContent.OffsetTop = 8;
                rowContent.OffsetBottom = -8;
                var enableTickbox = ModPanelEnableTickbox.Create(captured.IsEnabledInSettings);
                enableTickbox.Toggled += tick => {
                    ModRuntime.LoadSettings.SetEnabled(captured.Id, captured.Source, tick.IsTicked);
                    RefreshPendingRestartBanner();
                };
                rowContent.AddChild(enableTickbox);
                var titleLbl = new Label {
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Text = displayName,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    LabelSettings = new LabelSettings {
                        FontSize = 22,
                        FontColor = ResolveSidebarModTitleColor(captured.LoadStatus),
                    },
                };
                rowContent.AddChild(titleLbl);
                var versionChip = CreateSidebarModListVersionChip(captured.Version);
                if (versionChip != null)
                    rowContent.AddChild(versionChip);
                rowContent.AddChild(CreateModSourceChip(captured.Source, captured.InstallPath));
                rowHost.AddChild(rowContent);
                var vm = new SidebarModRowVm {
                    Entry = captured,
                    TitleLabel = titleLbl,
                    InnerStyle = innerStyle,
                    BgPanel = bgPanel,
                    Host = rowHost,
                    EnableTickbox = enableTickbox,
                };
                modRows.Add(vm);
                rowHost.FocusEntered += () => {
                    if (!string.Equals(selectedModId, vm.Id, StringComparison.OrdinalIgnoreCase))
                        SelectMod(vm.Id);
                    else
                        RefreshModRowChrome();
                };
                rowHost.FocusExited += () => RefreshModRowChrome();
                rowHost.GuiInput += ev => {
                    if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left) {
                        if (mb.Pressed) {
                            vm.Pressing = true;
                            RefreshModRowChrome();
                        }
                        else {
                            vm.Pressing = false;
                            SelectMod(vm.Id);
                        }
                    }
                };
                cardContent.AddChild(rowHost);
            }
            var deferredInitial = initialSelectedId;
            Callable.From(() => SelectMod(deferredInitial)).CallDeferred();
        }
        RefreshPendingRestartBanner();
        var dividerLine = CreateSidebarScrollTopDivider();
        dividerLine.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        listHeader.AddChild(dividerLine);
        listHeader.AddChild(scroll);
        SidebarModListScrollBuilder.ResetScrollTopDeferred(scroll);
        var sidebarLower = new VBoxContainer {
            Name = "ModPanelSidebarLower",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        sidebarLower.AddThemeConstantOverride("separation", 8);
        sidebarLower.AddChild(listFrame);
        var scopeStrip = CreateModPanelScopeInfoStrip();
        scopeFocusTarget = scopeStrip.GetNodeOrNull<Button>("ModPanelScopeCollapsed")
            ?? scopeStrip.GetNodeOrNull<Button>("ModPanelScopeCollapse");
        sidebarLower.AddChild(scopeStrip);
        mainVBox.AddChild(sidebarLower);
        shell.AddChild(controllerSupport);
        controllerSupport.BindSubmenu(shell);
        controllerSupport.ConfigureSidebar(modRows, () => selectedModId, SelectMod, ritsuContentList);
        void OnThemeRefresh() {
            RefreshModRowChrome(animateSelection: false);
            RebuildRitsuRightPane();
        }
        RegisterThemeRefresh(OnThemeRefresh);
        shell.TreeExiting += () => ClearThemeRefresh();
        Callable.From(() => {
            if (modRows.Count > 0) {
                ModPanelFocusWiring.Wire(modRows, selectedModId, contentState.PageId, pageTabChrome, ritsuContentList,
                    scopeFocusTarget);
                pageTabChrome.RefreshTriggerIcons();
                var selectedRow = modRows.Find(r =>
                    string.Equals(r.Id, selectedModId, StringComparison.OrdinalIgnoreCase));
                shell.SetInitialFocusedControl(selectedRow?.Host);
            }
            shell.RefreshControllerFocus();
            controllerSupport.RefreshHints();
        }).CallDeferred();
        return panel;
    }
    private static (Panel Frame, TextureRect Icon, Control Placeholder, MegaRichTextLabel Caption) CreateSidebarModPreviewParts() {
        var outer = ModPanelUiMetrics.ModSidebarPreviewOuterSize;
        var previewFrame = new Panel {
            Name = "ModPreviewFrame",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(outer, outer),
            ClipContents = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        previewFrame.AddThemeStyleboxOverride("panel", CreateModSidebarPreviewFrameStyle());
        var inner = new Control {
            Name = "ModPreviewInner",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        inner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        previewFrame.AddChild(inner);
        var modIcon = new TextureRect {
            Name = "ModIcon",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        modIcon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        inner.AddChild(modIcon);
        var previewPlaceholder = new Control {
            Name = "ModPreviewPlaceholder",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = true,
        };
        previewPlaceholder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        inner.AddChild(previewPlaceholder);
        var bg = new ColorRect {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = Colors.Black,
        };
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        previewPlaceholder.AddChild(bg);
        var captionMargin = new MarginContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        captionMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        captionMargin.AddThemeConstantOverride("margin_left", 4);
        captionMargin.AddThemeConstantOverride("margin_right", 4);
        captionMargin.AddThemeConstantOverride("margin_top", 4);
        captionMargin.AddThemeConstantOverride("margin_bottom", 4);
        previewPlaceholder.AddChild(captionMargin);
        var previewCaption = CreateSidebarWrapLabel(13, HorizontalAlignment.Center,
            VerticalAlignment.Center);
        previewCaption.Modulate = Colors.White;
        previewCaption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        previewCaption.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        captionMargin.AddChild(previewCaption);
        return (previewFrame, modIcon, previewPlaceholder, previewCaption);
    }
    private static void ApplySidebarTextsFromEntry(KitLibModEntry entry, ModPanelDetailTitleEditor titleEditor,
        HBoxContainer metaChipRow, MegaRichTextLabel modIdLabel, ScrollContainer descScroll,
        MegaRichTextLabel descLabel) {
        titleEditor.Bind(entry.Id, entry.Source, entry.DisplayName);
        UpdateDetailBannerMetaChips(metaChipRow, entry.Version, entry.Source, entry.LoadStatus, entry.InstallPath,
            entry.Id, _root, entry.DisplayName);
        modIdLabel.SetTextAutoSize(entry.Id);
        descScroll.Visible = false;
        descLabel.Visible = false;
        descLabel.SetTextAutoSize("");
        descLabel.TooltipText = "";
    }
    private static void ApplySidebarTexts(Mod? mod, string modId, ModPanelDetailTitleEditor titleEditor,
        HBoxContainer metaChipRow, MegaRichTextLabel modIdLabel, ScrollContainer descScroll,
        MegaRichTextLabel descLabel) {
        if (mod == null) {
            titleEditor.Bind("", ModEntrySource.ModsDirectory,
                I18N.T("modpanel.sidebar.modHeader.none", "No mod selected"));

            foreach (Node child in metaChipRow.GetChildren())
                child.QueueFree();

            modIdLabel.SetTextAutoSize("");
            descScroll.Visible = false;
            descLabel.SetTextAutoSize("");
            descLabel.Visible = false;
            return;
        }
        var source = ModPanelInstallSource.FromStsSource(mod.modSource);
        titleEditor.Bind(modId, source, ModPanelModBanner.ResolveTitle(mod, modId));
        var loadStatus = ModPanelInstallSource.FromStsLoadState(mod.state);
        UpdateDetailBannerMetaChips(metaChipRow, ModPanelModBanner.ResolveVersion(mod), source, loadStatus, mod.path,
            modId, _root, ModPanelModBanner.ResolveTitle(mod, modId));
        modIdLabel.SetTextAutoSize(modId);
        var descParts = new List<string>();
        var author = ModPanelModBanner.ResolveAuthor(mod);
        if (!string.IsNullOrWhiteSpace(author))
            descParts.Add(author);
        var desc = ModPanelModBanner.ResolveDescription(mod);
        if (!string.IsNullOrWhiteSpace(desc))
            descParts.Add(desc);
        if (descParts.Count == 0) {
            descScroll.Visible = false;
            descLabel.Visible = false;
            descLabel.SetTextAutoSize("");
            descLabel.TooltipText = "";
        }
        else {
            descScroll.Visible = true;
            descLabel.Visible = true;
            descLabel.SetTextAutoSize(string.Join("\n\n", descParts));
            descLabel.TooltipText = "";
            descScroll.ScrollVertical = 0;
        }
    }
    private static void ApplyPreviewState(Texture2D? tex, bool noModSelected, TextureRect modIcon,
        Control previewPlaceholder, MegaRichTextLabel previewCaption) {
        var hasArt = tex != null && !noModSelected;
        if (hasArt) {
            modIcon.Texture = tex;
            modIcon.Visible = true;
            previewPlaceholder.Visible = false;
            modIcon.Modulate = Colors.White;
            return;
        }
        modIcon.Texture = null;
        modIcon.Visible = false;
        previewPlaceholder.Visible = true;
        var caption = noModSelected
            ? I18N.T("modpanel.sidebar.modPreview.empty", "No preview")
            : I18N.T("modpanel.sidebar.modPreview.noImage", "No resources");
        previewCaption.SetTextAutoSize(caption);
    }

    private static string ResolveSidebarListTitle(KitLibModEntry entry)
        => ModTitleStore.Resolve(entry.Id, entry.Source, entry.DisplayName);

    private static (Control Panel, VBoxContainer ContentList, ModPanelPageTabChrome PageTabChrome,
        ModDetailBannerControls Banner) BuildContentPanel() {
        var panel = new Panel {
            Name = "ModPanelContentPanel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
        var frame = new MarginContainer {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        frame.AddThemeConstantOverride("margin_left", 18);
        frame.AddThemeConstantOverride("margin_top", 18);
        frame.AddThemeConstantOverride("margin_right", 18);
        frame.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(frame);
        var root = new VBoxContainer {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        frame.AddChild(root);
        var (modBannerClip, modBanner) = CreateModDetailBanner();
        root.AddChild(modBannerClip);
        var pageTabChrome = new ModPanelPageTabChrome {
            Visible = false,
        };
        root.AddChild(pageTabChrome);
        var scroll = new ScrollContainer {
            Name = "ModPanelContentScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
            FocusMode = Control.FocusModeEnum.None,
        };
        root.AddChild(scroll);
        var contentStack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        contentStack.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(contentStack);
        var contentScrollFrame = new MarginContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        contentScrollFrame.AddThemeConstantOverride("margin_right", ModPanelUiMetrics.ContentScrollRightGutter);
        contentStack.AddChild(contentScrollFrame);
        var contentList = new VBoxContainer {
            Name = "RitsuSettingsSummaryHost",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        contentList.AddThemeConstantOverride("separation", 8);
        contentScrollFrame.AddChild(contentList);
        scroll.SetMeta("modpanel_content_scroll", true);
        return (panel, contentList, pageTabChrome, modBanner);
    }

    private static (Control Clip, ModDetailBannerControls Controls) CreateModDetailBanner() {
        var modHeaderOuter = new MarginContainer {
            Name = "ModPanelDetailBanner",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var headerRoot = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        headerRoot.AddThemeStyleboxOverride("panel", CreateModSidebarPreviewFrameStyle());
        var headerRow = new HBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        headerRow.AddThemeConstantOverride("separation", 12);
        headerRoot.AddChild(headerRow);
        var (previewFrame, modIcon, previewPlaceholder, previewCaption) = CreateSidebarModPreviewParts();
        headerRow.AddChild(previewFrame);

        var textCol = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        textCol.AddThemeConstantOverride("separation", 6);
        headerRow.AddChild(textCol);

        var titleEditor = new ModPanelDetailTitleEditor();
        textCol.AddChild(titleEditor);

        var metaChipRow = new HBoxContainer {
            Name = "ModPanelDetailMetaChips",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        metaChipRow.AddThemeConstantOverride("separation", 8);
        textCol.AddChild(metaChipRow);

        var modIdLabel = CreateSidebarWrapLabel(14, HorizontalAlignment.Left);
        modIdLabel.Modulate = new Color(0.75f, 0.72f, 0.65f, 0.95f);
        modIdLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        textCol.AddChild(modIdLabel);

        var descScroll = new ScrollContainer {
            Name = "ModPanelDetailDescScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        textCol.AddChild(descScroll);

        var descLabel = CreateSidebarWrapLabel(13, HorizontalAlignment.Left);
        descLabel.Modulate = new Color(0.65f, 0.62f, 0.58f, 0.9f);
        descLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        descLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        descLabel.Visible = false;
        descScroll.AddChild(descLabel);

        modHeaderOuter.AddChild(headerRoot);

        var headerClip = new SidebarBannerClip(ModPanelUiMetrics.SidebarModBannerFixedHeight);
        headerClip.AddChild(modHeaderOuter);
        modHeaderOuter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var controls = new ModDetailBannerControls {
            TitleEditor = titleEditor,
            MetaChipRow = metaChipRow,
            ModIdLabel = modIdLabel,
            DescScroll = descScroll,
            DescLabel = descLabel,
            ModIcon = modIcon,
            PreviewPlaceholder = previewPlaceholder,
            PreviewCaption = previewCaption,
        };
        return (headerClip, controls);
    }

    private static void FinishSettingsBodyPresentation(Control body) {
        if (!GodotObject.IsInstanceValid(body))
            return;
        ModSettingsRitsuFormDevTheme.ApplyToSubtree(body);
    }

    private static void PresentNoSettingsContent(VBoxContainer list, ModPanelPageTabChrome pageTabChrome,
        int generation) {
        pageTabChrome.ClearPages();
        ModPanelContentMotion.Present(list, generation, CreateInlineDescription(
            I18N.T("modpanel.content.noSettingsPages", "This mod has no registered settings pages.")));
    }

    private static string ResolveInitialPageId(string modId) {
        var pendingPage = _pendingInitialPageId;
        var pendingMod = _pendingInitialPageModId;
        if (!string.IsNullOrWhiteSpace(pendingPage)
            && !string.IsNullOrWhiteSpace(pendingMod)
            && string.Equals(pendingMod, modId, StringComparison.OrdinalIgnoreCase)
            && PageExistsForMod(modId, pendingPage)) {
            _pendingInitialPageId = null;
            _pendingInitialPageModId = null;
            return pendingPage;
        }
        if (PrefersRitsuSettingsContent(modId)) {
            var ritsuPages = RitsuModSettingsBridge.GetAllPageObjects(modId);
            return RitsuModSettingsBridge.GetPageId(ritsuPages[0]);
        }
        if (KitLibModSettingsRegistry.HasPages(modId)) {
            var pages = KitLibModSettingsRegistry.GetPages(modId);
            return pages.Count > 0 ? pages[0].PageId : "";
        }
        var fallbackRitsuPages = RitsuModSettingsBridge.GetAllPageObjects(modId);
        return fallbackRitsuPages.Count > 0 ? RitsuModSettingsBridge.GetPageId(fallbackRitsuPages[0]) : "";
    }

    /// <summary>
    /// Ritsu-registered pages are the full settings surface for content mods; a lone KitLib-native page
    /// (e.g. log level) must not shadow them in ModPanel.
    /// </summary>
    private static bool PrefersRitsuSettingsContent(string modId) {
        if (!RitsuModSettingsBridge.IsAvailable)
            return false;
        return RitsuModSettingsBridge.GetAllPageObjects(modId).Count > 0;
    }

    private static bool PageExistsForMod(string modId, string pageId) {
        if (KitLibModSettingsRegistry.HasPages(modId)) {
            foreach (var page in KitLibModSettingsRegistry.GetPages(modId)) {
                if (string.Equals(page.PageId, pageId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        foreach (var page in RitsuModSettingsBridge.GetAllPageObjects(modId)) {
            if (string.Equals(RitsuModSettingsBridge.GetPageId(page), pageId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void RefreshSettingsContent(VBoxContainer list, ModPanelPageTabChrome pageTabChrome, string modId,
        ModPanelContentState state, Action rebuild) {
        var perf = ModPanelPerf.Start();
        var generation = ModPanelContentMotion.BeginRefresh(list);
        if (ModPanelModBanner.TryFindMod(modId) == null) {
            ModPanelContentMotion.Present(list, generation,
                CreateModStatusDescription(
                    I18N.T("modpanel.content.modNotLoaded",
                        "This mod is disabled or failed to load. Enable it in the list and restart the game to edit settings here.")));
            ModPanelPerf.Log("refresh.modNotLoaded", perf, $"modId={modId}");
            return;
        }
        if (PrefersRitsuSettingsContent(modId)) {
            RefreshRitsuSettingsContent(list, pageTabChrome, modId, state, perf, generation);
            ModPanelPerf.Log("refresh.total", perf, $"modId={modId} pageId={state.PageId} path=ritsu");
            return;
        }
        if (TryRefreshNativeSettingsContent(list, pageTabChrome, modId, state, perf, generation))
            return;
        RefreshRitsuSettingsContent(list, pageTabChrome, modId, state, perf, generation);
        ModPanelPerf.Log("refresh.total", perf, $"modId={modId} pageId={state.PageId} path=ritsu");
    }

    private static bool TryRefreshNativeSettingsContent(VBoxContainer list, ModPanelPageTabChrome pageTabChrome,
        string modId, ModPanelContentState state, Stopwatch perf, int generation) {
        if (!KitLibModSettingsRegistry.HasPages(modId))
            return false;
        var pages = KitLibModSettingsRegistry.GetPages(modId);
        if (pages.Count == 0)
            return false;

        var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ModPanelPageTabChrome.PageEntry>(pages.Count);
        foreach (var p in pages) {
            validIds.Add(p.PageId);
            entries.Add(new ModPanelPageTabChrome.PageEntry(
                p.PageId, KitLibNativeModSettingsBootstrap.ResolvePageTitle(p)));
        }
        if (string.IsNullOrWhiteSpace(state.PageId) || !validIds.Contains(state.PageId))
            state.PageId = entries[0].Id;
        pageTabChrome.SetPages(entries, state.PageId);

        KitLibModSettingsPageRegistration? active = null;
        foreach (var p in pages) {
            if (string.Equals(p.PageId, state.PageId, StringComparison.OrdinalIgnoreCase)) {
                active = p;
                break;
            }
        }
        if (active == null) {
            PresentNoSettingsContent(list, pageTabChrome, generation);
            return true;
        }

        var buildSw = ModPanelPerf.Start();
        var built = active.BuildBody();
        ModPanelPerf.Log("refresh.nativeBuildBody", buildSw, $"pageId={state.PageId}");
        if (built is not Control body) {
            ModPanelContentMotion.Present(list, generation, CreateInlineDescription(string.Format(
                I18N.T("modpanel.content.buildFailed", "Could not build panel UI: {0}"),
                "Native page body was not a Control.")));
            ModPanelPerf.Log("refresh.total", perf, $"modId={modId} pageId={state.PageId} path=native invalidBody");
            return true;
        }
        FinishSettingsBodyPresentation(body);
        ModPanelContentMotion.Present(list, generation, body);
        ModPanelPerf.Log("refresh.total", perf, $"modId={modId} pageId={state.PageId} path=native");
        return true;
    }

    private static void RefreshRitsuSettingsContent(VBoxContainer list, ModPanelPageTabChrome pageTabChrome, string modId,
        ModPanelContentState state, Stopwatch perf, int generation) {
        if (!RitsuModSettingsBridge.IsAvailable) {
            MainFile.Logger.Warn("KitLib ModPanel: STS2-RitsuLib assembly not loaded.");
            ModPanelContentMotion.Present(list, generation, CreateInlineDescription(
                I18N.T("modpanel.content.ritsuNotLoaded",
                    "STS2-RitsuLib is not loaded. Install/enable it to scan registered mod settings here.")));
            ModPanelPerf.Log("refresh.ritsuMissing", perf, $"modId={modId}");
            return;
        }
        var reflectSw = ModPanelPerf.Start();
        var pages = RitsuModSettingsBridge.GetAllPageObjects(modId);
        ModPanelPerf.Log("refresh.ritsuEnumerate", reflectSw, $"count={pages.Count}");
        if (pages.Count == 0) {
            MainFile.Logger.Info($"KitLib ModPanel: no registered settings pages for mod '{modId}'.");
            PresentNoSettingsContent(list, pageTabChrome, generation);
            return;
        }
        var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ModPanelPageTabChrome.PageEntry>(pages.Count);
        foreach (var p in pages) {
            var pid = RitsuModSettingsBridge.GetPageId(p);
            validIds.Add(pid);
            entries.Add(new ModPanelPageTabChrome.PageEntry(pid, RitsuModSettingsBridge.GetPageTabLabel(p, modId)));
        }
        if (string.IsNullOrWhiteSpace(state.PageId) || !validIds.Contains(state.PageId))
            state.PageId = entries[0].Id;
        pageTabChrome.SetPages(entries, state.PageId);
        object? activePage = null;
        foreach (var p in pages) {
            if (string.Equals(RitsuModSettingsBridge.GetPageId(p), state.PageId, StringComparison.OrdinalIgnoreCase)) {
                activePage = p;
                break;
            }
        }
        if (activePage == null) {
            PresentNoSettingsContent(list, pageTabChrome, generation);
            return;
        }
        var submenu = RitsuModSettingsEmbedHost.TryGetSubmenu();
        if (submenu == null) {
            var embedProbe = ModPanelEmbedHostProbe.Probe(RitsuModSettingsBridge.TryGetRitsuAssembly());
            MainFile.Logger.Warn(
                $"KitLib ModPanel: RitsuModSettingsSubmenu embed host failed for mod '{modId}', page '{state.PageId}' " +
                $"(embed={embedProbe.Status}, detail={embedProbe.Detail ?? "—"}).");
            ModPanelContentMotion.Present(list, generation, CreateInlineDescription(
                I18N.T("modpanel.content.embedHostFailed",
                    "Could not initialize the RitsuLib settings host.")));
            return;
        }
        var ritsuPageModId = RitsuModSettingsBridge.GetPageModId(activePage);
        if (string.IsNullOrWhiteSpace(ritsuPageModId))
            ritsuPageModId = modId;
        RitsuModSettingsEmbedHost.SyncSubmenuSelection(ritsuPageModId, state.PageId);
        var buildSw = ModPanelPerf.Start();
        var body = RitsuModSettingsBridge.TryCreateInteractivePageBody(submenu, ritsuPageModId, activePage, out var err);
        ModPanelPerf.Log("refresh.ritsuBuildBody", buildSw, $"pageId={state.PageId}");
        if (body == null) {
            MainFile.Logger.Warn(
                $"KitLib ModPanel: page body build failed for mod '{ritsuPageModId}', page '{state.PageId}': {err ?? "—"}");
            ModPanelContentMotion.Present(list, generation, CreateInlineDescription(string.Format(
                I18N.T("modpanel.content.buildFailed", "Could not build panel UI: {0}"), err ?? "—")));
            return;
        }
        FinishSettingsBodyPresentation(body);
        ModPanelContentMotion.Present(list, generation, body);
    }

    static Control BuildMissingUserModulePanel() {
        var panel = new Panel {
            Name = "ModPanelMissingUserModule",
            CustomMinimumSize = new Vector2(ModPanelUiMetrics.SidebarPanelMinWidth, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
        var label = new MegaRichTextLabel {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = I18N.T(
                "modpanel.missingUserModule",
                "KitLib could not load a required component (User module). " +
                "Your KitLib installation may be incomplete—reinstall KitLib from the same source you originally used, " +
                "then fully restart the game."),
        };
        label.AddThemeFontSizeOverride("normal_font_size", 14);
        ModPanelUI.ApplyMegaRichTextFontOverrides(label);
        var margin = new MarginContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        margin.AddThemeConstantOverride("margin_left", ModPanelUiMetrics.SidebarContentMarginH);
        margin.AddThemeConstantOverride("margin_right", ModPanelUiMetrics.SidebarContentMarginH);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddChild(label);
        panel.AddChild(margin);
        MainFile.Logger.Error("KitLib ModPanel: KitLib.User module is not loaded; mod list sidebar disabled.");
        return panel;
    }

    private sealed class ModPanelContentState {
        public string PageId = "";
    }

    private sealed class ModDetailBannerControls {
        public required ModPanelDetailTitleEditor TitleEditor { get; init; }
        public required HBoxContainer MetaChipRow { get; init; }
        public required MegaRichTextLabel ModIdLabel { get; init; }
        public required ScrollContainer DescScroll { get; init; }
        public required MegaRichTextLabel DescLabel { get; init; }
        public required TextureRect ModIcon { get; init; }
        public required Control PreviewPlaceholder { get; init; }
        public required MegaRichTextLabel PreviewCaption { get; init; }
    }
    /// <summary>Caps the selected-mod banner to a fixed height; long description scrolls inside the text column.</summary>
    private sealed partial class SidebarBannerClip : Control {
        private readonly float _height;
        public SidebarBannerClip(float height) {
            _height = height;
            ClipContents = true;
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            CustomMinimumSize = new Vector2(0f, height);
        }
        public override Vector2 _GetMinimumSize() => new Vector2(0f, _height);
    }
    private sealed partial class ShellInputForwarder : Node {
        private readonly Action _onClose;
        public ShellInputForwarder(Action onClose) {
            _onClose = onClose;
        }
        public override void _Ready() {
            SetProcessUnhandledInput(true);
        }
        public override void _UnhandledInput(InputEvent @event) {
            if (@event is InputEventKey { Echo: false, Pressed: true } key
                && (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape)) {
                _onClose();
                GetViewport()?.SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("ui_cancel")) {
                _onClose();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }
}
