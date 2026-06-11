using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Modding;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ModCompatStartupBannerUI {
    internal const string RootNodeName = "KitLibCompatStartupBannerRoot";
    internal const string NodeName = "KitLibCompatStartupBanner";
    internal const string HoverNodeName = "KitLibCompatStartupBannerHover";

    const string NinePatchPath = "res://images/ui/tiny_nine_patch.png";
    const int HoverPadding = 12;
    const float MaxHoverHeight = 360f;

    public static void TryAttach(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        mainMenu.GetNodeOrNull(RootNodeName)?.QueueFree();

        var moddedWarning = mainMenu.GetNodeOrNull<MegaLabel>("%ModdedWarning");
        if (moddedWarning == null)
            return;

        var issues = ModPanelCompatProbe.CollectStartupIssues();
        if (issues.Count == 0) {
            KitLog.Info($"compat startup: no issues to show");
            return;
        }

        var hoverStyle = mainMenu.GetNodeOrNull<MegaRichTextLabel>("%ModWarningLabel");
        var hoverWidthRef = mainMenu.GetNodeOrNull<Control>("%ModWarningContainer");
        var detailText = FormatDetails(issues);

        var root = new Control {
            Name = RootNodeName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };

        var hoverSection = CreateHoverSection(hoverStyle, detailText, out var detailLabel);
        root.AddChild(hoverSection);

        var banner = new MegaLabel {
            Name = NodeName,
            Text = FormatSummary(issues),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MaxLinesVisible = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            AutoSizeEnabled = false,
            Modulate = StsColors.redGlow,
        };
        CopyLabelTheme(moddedWarning, banner);
        root.AddChild(banner);

        mainMenu.AddChild(root);
        mainMenu.MoveChild(root, moddedWarning.GetIndex());

        float panelWidth = 0f;
        float cachedHoverHeight = 0f;
        var layoutAttempts = 0;

        void RefreshHoverHeight() {
            if (panelWidth <= 0f)
                return;
            cachedHoverHeight = MeasureHoverHeight(detailLabel, panelWidth, detailText);
        }

        bool MirrorsOfficialStatusLine() =>
            GodotObject.IsInstanceValid(moddedWarning)
            && moddedWarning.Visible
            && moddedWarning.IsVisibleInTree();

        bool ApplyLayout() {
            if (!GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(moddedWarning))
                return false;

            var lineHeight = moddedWarning.OffsetBottom - moddedWarning.OffsetTop;
            var bannerWidth = moddedWarning.OffsetRight - moddedWarning.OffsetLeft;
            if (lineHeight <= 0f || bannerWidth <= 0f)
                return false;

            if (panelWidth <= 0f)
                panelWidth = ResolveHoverPanelWidth(hoverWidthRef, bannerWidth);

            var hoverHeight = 0f;
            if (hoverSection.Visible) {
                if (cachedHoverHeight <= 0f)
                    RefreshHoverHeight();
                hoverHeight = cachedHoverHeight;
            }

            root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
            root.GrowHorizontal = Control.GrowDirection.Begin;
            root.GrowVertical = Control.GrowDirection.Begin;
            root.OffsetLeft = -panelWidth;
            root.OffsetRight = 0f;
            root.OffsetBottom = moddedWarning.OffsetTop;
            root.OffsetTop = moddedWarning.OffsetTop - lineHeight - hoverHeight;

            banner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
            banner.GrowHorizontal = Control.GrowDirection.Begin;
            banner.OffsetLeft = -panelWidth;
            banner.OffsetRight = 0f;
            banner.OffsetTop = -lineHeight;
            banner.OffsetBottom = 0f;

            if (hoverSection.Visible) {
                hoverSection.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
                hoverSection.GrowHorizontal = Control.GrowDirection.Begin;
                hoverSection.OffsetLeft = -panelWidth;
                hoverSection.OffsetRight = 0f;
                hoverSection.OffsetTop = -(lineHeight + hoverHeight);
                hoverSection.OffsetBottom = -lineHeight;
            }

            root.Visible = MirrorsOfficialStatusLine();
            return root.Visible;
        }

        void SyncWithOfficialStatusLine() {
            if (!MirrorsOfficialStatusLine()) {
                root.Visible = false;
                if (hoverSection.Visible)
                    hoverSection.Visible = false;
                return;
            }
            ApplyLayout();
        }

        void EnsureLayout() {
            if (!GodotObject.IsInstanceValid(root))
                return;
            if (ApplyLayout() || ++layoutAttempts > 240)
                return;
            Callable.From(EnsureLayout).CallDeferred();
        }

        void ShowHover() {
            if (!MirrorsOfficialStatusLine())
                return;
            hoverSection.Visible = true;
            ApplyLayout();
        }

        void HideHover() {
            hoverSection.Visible = false;
            ApplyLayout();
        }

        root.MouseEntered += ShowHover;
        root.MouseExited += HideHover;
        moddedWarning.Resized += OnModdedWarningResized;
        moddedWarning.VisibilityChanged += OnModdedWarningVisibilityChanged;

        void OnModdedWarningResized() => SyncWithOfficialStatusLine();
        void OnModdedWarningVisibilityChanged() {
            layoutAttempts = 0;
            EnsureLayout();
        }

        Callable.From(EnsureLayout).CallDeferred();

        void OnMainMenuExiting() {
            root.MouseEntered -= ShowHover;
            root.MouseExited -= HideHover;
            moddedWarning.Resized -= OnModdedWarningResized;
            moddedWarning.VisibilityChanged -= OnModdedWarningVisibilityChanged;
            if (GodotObject.IsInstanceValid(root))
                root.QueueFree();
        }

        mainMenu.TreeExiting += OnMainMenuExiting;
        root.TreeExiting += () => mainMenu.TreeExiting -= OnMainMenuExiting;

        LogIssues(issues);
    }

    static float MeasureHoverHeight(MegaRichTextLabel label, float panelWidth, string detailText) {
        var fontSize = label.GetThemeFontSize("normal_font_size");
        if (fontSize <= 0)
            fontSize = 12;
        var textWidth = Mathf.Max(80f, panelWidth - HoverPadding * 4);
        label.Size = new Vector2(textWidth, 10f);
        var measured = label.GetContentHeight();
        float contentHeight;
        if (measured > 0f && measured < MaxHoverHeight)
            contentHeight = measured;
        else {
            var lines = string.IsNullOrEmpty(detailText) ? 1 : detailText.Split('\n').Length;
            contentHeight = lines * (fontSize + 4f);
        }
        return Mathf.Clamp(contentHeight + HoverPadding * 4, 40f, MaxHoverHeight);
    }

    static float ResolveHoverPanelWidth(Control? hoverWidthRef, float statusWidth) {
        if (hoverWidthRef != null) {
            var width = hoverWidthRef.Size.X;
            if (width > 0f)
                return width;
            width = hoverWidthRef.GetGlobalRect().Size.X;
            if (width > 0f)
                return width;
        }
        return Mathf.Max(statusWidth, 320f);
    }

    static Control CreateHoverSection(MegaRichTextLabel? styleSource, string detailText,
        out MegaRichTextLabel detailLabel) {
        var section = new Control {
            Name = HoverNodeName,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var ninePatch = new NinePatchRect {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SelfModulate = new Color(0.21f, 0.21f, 0.21f, 0.812f),
        };
        ninePatch.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        if (ResourceLoader.Exists(NinePatchPath))
            ninePatch.Texture = GD.Load<Texture2D>(NinePatchPath);
        ninePatch.SetPatchMargin((Side)0, HoverPadding);
        ninePatch.SetPatchMargin((Side)1, HoverPadding);
        ninePatch.SetPatchMargin((Side)2, HoverPadding);
        ninePatch.SetPatchMargin((Side)3, HoverPadding);
        section.AddChild(ninePatch);

        var margin = new MarginContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", HoverPadding);
        margin.AddThemeConstantOverride("margin_top", HoverPadding);
        margin.AddThemeConstantOverride("margin_right", HoverPadding);
        margin.AddThemeConstantOverride("margin_bottom", HoverPadding);
        section.AddChild(margin);

        detailLabel = new MegaRichTextLabel {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutoSizeEnabled = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White,
            Text = detailText,
        };
        CopyRichTextTheme(styleSource, detailLabel);
        margin.AddChild(detailLabel);

        return section;
    }

    static void CopyLabelTheme(Label source, Label target) {
        var font = source.GetThemeFont("font");
        if (font != null)
            target.AddThemeFontOverride("font", font);
        var fontSize = source.GetThemeFontSize("font_size");
        if (fontSize > 0)
            target.AddThemeFontSizeOverride("font_size", fontSize);
        else
            target.AddThemeFontSizeOverride("font_size", 14);
    }

    static void CopyRichTextTheme(MegaRichTextLabel? source, MegaRichTextLabel target) {
        if (source != null) {
            var font = source.GetThemeFont("normal_font");
            if (font != null)
                target.AddThemeFontOverride("normal_font", font);
            var fontSize = source.GetThemeFontSize("normal_font_size");
            if (fontSize > 0)
                target.AddThemeFontSizeOverride("normal_font_size", fontSize);
        }
        if (target.GetThemeFontSize("normal_font_size") <= 0)
            target.AddThemeFontSizeOverride("normal_font_size", 12);

        var fallback = target.GetThemeFont("normal_font") ?? ThemeDB.FallbackFont;
        target.AddThemeFontOverride("bold_font", fallback);
        target.AddThemeFontOverride("italics_font", fallback);
        target.AddThemeFontOverride("bold_italics_font", fallback);
        target.AddThemeFontOverride("mono_font", fallback);
    }

    internal static string FormatSummary(IReadOnlyList<KitLibCompatIssue> issues) {
        if (issues.Count == 0)
            return string.Empty;
        var orSep = I18N.T("modpanel.userText.or", " or ");
        var nameSep = I18N.T("modpanel.startup.nameSeparator", ", ");
        if (issues.Count == 1)
            return ModPanelCompatProbe.FormatStartupIssueSummary(issues[0], orSep);
        var names = KitLibCompatStartupScan.JoinDisplayNames(issues, nameSep);
        return string.Format(
            I18N.T("modpanel.startup.compatMany",
                "{0} mod(s) have a version mismatch ({1}), open Mods for details"),
            issues.Count,
            names);
    }

    internal static string FormatDetails(IReadOnlyList<KitLibCompatIssue> issues) {
        var orSep = I18N.T("modpanel.userText.or", " or ");
        return string.Join(
            "\n",
            issues.Select(issue => ModPanelCompatProbe.FormatStartupIssueSummary(issue, orSep, richText: true)));
    }

    static void LogIssues(IReadOnlyList<KitLibCompatIssue> issues) {
        var sb = new StringBuilder();
        sb.Append("[KitLib] compat startup: ");
        for (var i = 0; i < issues.Count; i++) {
            if (i > 0)
                sb.Append("; ");
            var issue = issues[i];
            sb.Append(issue.ModId);
            sb.Append(" (");
            sb.Append(ModPanelCompatProbe.FormatStartupIssueDetailTechnical(issue.Result));
            sb.Append(')');
        }
        MainFile.Logger.Info(sb.ToString());
    }
}
