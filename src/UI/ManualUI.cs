using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>In-game DevMode manual — topic list and embedded Markdown pages.</summary>
internal static class ManualUI {
    private const string RootName = "KitLibManual";
    private const float PanelW = 780f;
    private const int NavW = 168;
    private const int NavItemH = 28;

    private static Color ColAccent => KitLibTheme.Accent;
    private static Color ColSubtle => KitLibTheme.Subtle;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var (root, _, outer) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 8);

        BuildHeader(outer);
        BuildBody(outer);

        ((Node)globalUi).AddChild(root);
    }

    public static void Remove(NGlobalUi globalUi) {
        var parent = (Node)globalUi;
        var existing = parent.GetNodeOrNull<Control>(RootName);
        if (existing == null) return;
        parent.RemoveChild(existing);
        existing.QueueFree();
    }

    private static void BuildHeader(VBoxContainer outer) {
        var title = new Label {
            Text = I18N.T("manual.title", "KitLib Manual"),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", ColAccent);
        outer.AddChild(title);

        var subtitle = new Label {
            Text = I18N.T("manual.subtitle", "What each rail panel can do."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", ColSubtle);
        outer.AddChild(subtitle);
    }

    private static void BuildBody(VBoxContainer outer) {
        var split = new HSplitContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffset = NavW
        };
        split.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Hidden;
        outer.AddChild(split);

        var navPanel = new PanelContainer {
            CustomMinimumSize = new Vector2(NavW, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        DevPanelUI.ApplySidebarPanelStyle(navPanel, mergeLeft: false);
        split.AddChild(navPanel);

        var nav = new ItemList {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SameColumnWidth = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        nav.AddThemeFontSizeOverride("font_size", 11);
        navPanel.AddChild(nav);

        var contentScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        split.AddChild(contentScroll);

        var content = KitLibTheme.CreateGameBbcodeLabel();
        content.FitContent = true;
        content.ScrollActive = false;
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.AddThemeFontSizeOverride("normal_font_size", 11);
        content.AddThemeFontSizeOverride("bold_font_size", 11);
        content.AddThemeFontSizeOverride("mono_font_size", 11);
        contentScroll.AddChild(content);

        foreach (var topic in ManualContent.Topics)
            nav.AddItem(I18N.T(topic.TitleKey, topic.Id));

        nav.CustomMinimumSize = new Vector2(NavW - 16, ManualContent.Topics.Count * NavItemH);

        void ShowTopic(int index) {
            if (index < 0 || index >= ManualContent.Topics.Count) return;
            var topic = ManualContent.Topics[index];
            var md = ManualContent.LoadMarkdown(topic.Id);
            if (string.IsNullOrWhiteSpace(md)) {
                content.Text = I18N.T("manual.empty", "(No content for this topic.)");
                return;
            }
            content.Text = ManualMarkdown.ToBbcode(md);
        }

        nav.ItemSelected += index => ShowTopic((int)index);
        nav.ItemActivated += index => ShowTopic((int)index);

        void ApplySplit() {
            if (!GodotObject.IsInstanceValid(split)) return;
            var total = split.Size.X;
            if (total < NavW + 120f) return;
            split.SplitOffset = NavW;
        }

        split.Resized += ApplySplit;
        Callable.From(ApplySplit).CallDeferred();

        if (nav.ItemCount > 0) {
            nav.Select(0);
            ShowTopic(0);
        }
    }
}
