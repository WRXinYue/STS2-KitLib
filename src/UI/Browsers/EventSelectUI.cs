using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.Modding;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Event picker — spliced to the DevMode rail, matching card / relic browser layout.</summary>
internal static class EventSelectUI {
    private const string RootName = "KitLibEventSelect";
    private const string ExtensionWidthKey = "KitLibEventSelect_ext";
    private const string DualMetaKey = "dm_dual_event_select";
    private const string CarrierNodeName = "EventSelectDualCarrier";
    private const float PanelW = 520f;
    private const float DefaultExtWidth = 420f;

    public static void Show(NGlobalUi globalUi, Func<EventModel, AncientEventEnterRequest?, bool> enterEvent) {
        Remove(globalUi);

        var dual = DevPanelUI.CreateDualColumnOverlay(new DevPanelUI.DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = RootName,
            DualMetaKey = DualMetaKey,
            CarrierNodeName = CarrierNodeName,
            MainWidthKey = RootName,
            ExtWidthKey = ExtensionWidthKey,
            MainDefaultWidth = PanelW,
            ExtDefaultWidth = DefaultExtWidth,
            FallbackClose = () => Remove(globalUi),
        });

        var mainVbox = dual.MainContent;
        BuildNavTab(mainVbox, I18N.T("event.nav.title", "Events"));

        var (searchRow, search) = DevPanelUI.CreateSearchRow(I18N.T("event.search", "Search events..."));
        mainVbox.AddChild(searchRow);

        var allEvents = EventActions.GetAllEvents().OrderBy(e => EventActions.GetEventDisplayName(e)).ToList();
        var activeModSourceFilters = new HashSet<string>(StringComparer.Ordinal);
        var excludedModSourceFilters = new HashSet<string>(StringComparer.Ordinal);

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(list);

        var statusLabel = BuildStatusLabel();
        var extStatusLabel = BuildStatusLabel();

        var backBtn = BuildExtensionHeader(dual.ExtContent, out var extTitleBtn);
        var extBodyHost = new VBoxContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        extBodyHost.AddThemeConstantOverride("separation", 3);
        dual.ExtContent.AddChild(extBodyHost);
        dual.ExtContent.AddChild(extStatusLabel);

        backBtn.Pressed += () => dual.CloseExtension();

        void OpenAncientExtension(EventModel evt) {
            foreach (var child in extBodyHost.GetChildren())
                ((Node)child).QueueFree();

            var name = EventActions.GetEventDisplayName(evt);
            extTitleBtn.Text = I18N.T("ancient.options.title", "{0} — pin option", name);

            var scroll = new ScrollContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            var choicesHost = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            choicesHost.AddThemeConstantOverride("separation", 3);
            scroll.AddChild(choicesHost);
            extBodyHost.AddChild(scroll);

            Callable.From(() => {
                AncientEventEnterUI.PopulateChoices(evt, choicesHost, request => {
                    if (enterEvent(evt, request))
                        RequestClose(globalUi);
                    else
                        extStatusLabel.Text = I18N.T("room.error", "Failed to enter room.");
                });
            }).CallDeferred();

            Callable.From(() => {
                bool alreadyOpen = dual.ExtSlot.Visible;
                dual.KillExtCloseTween();
                if (!alreadyOpen) {
                    dual.PrepareExtensionVisible();
                    dual.AnimateExtensionSlideIn();
                }
                else {
                    dual.PrepareExtensionVisible();
                }
                DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
            }).CallDeferred();
        }

        void Rebuild(string filter) {
            foreach (var child in list.GetChildren()) ((Node)child).QueueFree();
            var filtered = allEvents.Where(e => {
                if (!ContentModResolver.MatchesModSourceFilter(
                        ContentModResolver.Resolve(e),
                        activeModSourceFilters,
                        excludedModSourceFilters))
                    return false;
                if (string.IsNullOrWhiteSpace(filter))
                    return true;
                return EventActions.GetEventDisplayName(e)
                    .Contains(filter, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            foreach (var evt in filtered) {
                var displayName = EventActions.GetEventDisplayName(evt);
                var modSource = ContentModResolver.Resolve(evt);
                var captured = evt;
                list.AddChild(CreateEventListRow(displayName, modSource, () => {
                    if (AncientEventActions.NeedsOptionPicker(captured)) {
                        OpenAncientExtension(captured);
                        return;
                    }

                    if (enterEvent(captured, null)) {
                        RequestClose(globalUi);
                        return;
                    }

                    statusLabel.Text = I18N.T("room.error", "Failed to enter room.");
                }));
            }
            statusLabel.Text = I18N.T("event.count", "{0} events", filtered.Count);
        }

        var modSourceRow = BrowserDetailHelpers.TryCreateModSourceFilterRow(
            ContentModResolver.BuildFilterEntries(allEvents.Cast<AbstractModel>()),
            activeModSourceFilters,
            excludedModSourceFilters,
            () => Rebuild(search.Text ?? ""));
        if (modSourceRow != null)
            mainVbox.AddChild(modSourceRow);

        mainVbox.AddChild(scroll);
        mainVbox.AddChild(statusLabel);

        search.TextChanged += Rebuild;
        Rebuild("");

        dual.AttachToScene();
        search.GrabFocus();
    }

    private static Button BuildExtensionHeader(VBoxContainer extVbox, out Button titleBtn) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var backBtn = new Button {
            Text = I18N.T("room.ancients.back", "Back"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 6,
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            backBtn.AddThemeStyleboxOverride(s, flat);
        backBtn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        backBtn.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(backBtn);

        titleBtn = new Button {
            Text = I18N.T("ancient.options.title", "{0} — pin option", ""),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            titleBtn.AddThemeStyleboxOverride(s, flat);
        titleBtn.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        titleBtn.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(titleBtn);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        extVbox.AddChild(row);
        extVbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        return backBtn;
    }

    private static Control CreateEventListRow(string displayName, ContentModSource modSource, Action onPressed) {
        var panel = new PanelContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 44)
        };
        ApplyListItemPanelStyles(panel);

        var margin = new MarginContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 5);
        margin.AddThemeConstantOverride("margin_bottom", 5);

        var col = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        col.AddThemeConstantOverride("separation", 1);

        var nameLabel = new Label {
            Text = displayName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        nameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        col.AddChild(nameLabel);

        var sourceLabel = new Label {
            Text = string.Format(I18N.T("browser.modSource.label", "Source: {0}"), modSource.DisplayLabel),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        sourceLabel.AddThemeFontSizeOverride("font_size", 10);
        sourceLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        sourceLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        col.AddChild(sourceLabel);

        margin.AddChild(col);
        panel.AddChild(margin);
        panel.TooltipText = modSource.ModId ?? modSource.Key;

        panel.GuiInput += evt => {
            if (evt is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                return;
            onPressed();
            panel.AcceptEvent();
        };

        return panel;
    }

    private static void ApplyListItemPanelStyles(PanelContainer panel) {
        StyleBoxFlat MakeStyle(Color bg, Color border) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = border
        };

        var accent = KitLibTheme.Accent;
        var bgNormal = KitLibTheme.ButtonBgNormal;
        var borderNormal = new Color(bgNormal.R, bgNormal.G, bgNormal.B, bgNormal.A * 0.8f);
        var borderHover = new Color(accent.R, accent.G, accent.B, 0.30f);

        panel.AddThemeStyleboxOverride("panel", MakeStyle(bgNormal, borderNormal));
        panel.MouseEntered += () =>
            panel.AddThemeStyleboxOverride("panel", MakeStyle(KitLibTheme.ButtonBgHover, borderHover));
        panel.MouseExited += () =>
            panel.AddThemeStyleboxOverride("panel", MakeStyle(bgNormal, borderNormal));
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }

    internal static void RequestClose(NGlobalUi globalUi) =>
        DevPanelUI.RequestCloseBrowserOverlay(globalUi, RootName, () => Remove(globalUi));

    private static void BuildNavTab(VBoxContainer vbox, string title) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var tab = new Button { Text = title, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        var flat = new StyleBoxFlat { BgColor = Colors.Transparent, ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 4, ContentMarginBottom = 6 };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" }) tab.AddThemeStyleboxOverride(s, flat);
        tab.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        tab.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(tab);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
        vbox.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = KitLibTheme.Separator, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
    }

    private static Label BuildStatusLabel() {
        var lbl = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return lbl;
    }
}
