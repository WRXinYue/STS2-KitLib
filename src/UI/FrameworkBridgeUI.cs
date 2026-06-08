using System;
using KitLib.Interop;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Status panel for RitsuLib (bridge diagnostics).</summary>
internal static class FrameworkBridgeUI {
    private const string RootName = "KitLibFrameworkBridge";
    private const float PanelW = 680f;
    private const double AutoRefreshIntervalSec = 2.0;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 10);

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("bridge.title", "Framework bridge")));
        var subtitle = new Label {
            Text = I18N.T("bridge.subtitle",
                "Runtime diagnostics: RitsuLib manifest, mod settings inventory, and process-wide Harmony."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(inner);
        vbox.AddChild(scroll);

        void Rebuild() {
            foreach (Node c in inner.GetChildren())
                c.QueueFree();

            var snap = FrameworkBridge.CaptureSnapshot();

            inner.AddChild(MakeSectionCard(
                I18N.T("bridge.section.ritsu", "RitsuLib"),
                I18N.T("bridge.section.ritsu.desc",
                    "Framework bootstrap, mod settings UI registry, and manifest constants from Const."),
                section => {
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.name", "Display name"), snap.RitsuDisplayName));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.manifestVersion", "Manifest version (Const)"),
                        snap.RitsuManifestVersion));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.assemblyVersion", "Assembly version"),
                        snap.RitsuLibAssemblyVersion));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.modId", "Framework mod id"),
                        snap.RitsuLibFrameworkModId));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.settingsKey", "User settings root key"),
                        snap.RitsuSettingsRootKey));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.settingsFile", "Settings file name"),
                        snap.RitsuSettingsFileName));
                    section.AddChild(MakeSep());
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.init", "Initialized"),
                        YesNo(snap.RitsuLibInitialized)));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.active", "Active"), YesNo(snap.RitsuLibActive)));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.settings", "Mod settings UI"),
                        YesNo(snap.RitsuLibHasModSettingsPages)));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.pages", "Registered pages"),
                        snap.RitsuLibModSettingsPageCount.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.distinctMods", "Distinct owning mods"),
                        snap.RitsuLibDistinctOwningModCount.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.ritsu.sections", "Total sections (all pages)"),
                        snap.RitsuLibTotalSectionCount.ToString()));
                    section.AddChild(MakeSep());
                    section.AddChild(MakeDetailLabel(I18N.T("bridge.ritsu.inventoryTitle",
                        "Page inventory (owning mod | page id | sections | sort | parent | title)")));
                    section.AddChild(MakeCodeBlock(snap.RitsuLibPagesInventoryLines));
                }));

            var h = snap.HarmonyStats;
            inner.AddChild(MakeSectionCard(
                I18N.T("bridge.section.harmony", "Harmony (process-wide)"),
                I18N.T("bridge.harmony.desc",
                    "Global Harmony state for this process — comparable to common mod-framework patch dumps."),
                section => {
                    section.AddChild(MakeDetailLabel(I18N.T("bridge.harmony.summaryNote",
                        "This panel shows aggregate counts only. Use the Harmony analysis panel for the full per-method report, or export patches to a file from your tooling.")));
                    section.AddChild(MakeSep());
                    section.AddChild(MakeKvRow(I18N.T("bridge.harmony.methods", "Patched methods"),
                        h.PatchedMethodCount.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.harmony.totalOps", "Total patch operations"),
                        h.TotalPatchOperations.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.harmony.prefixes", "Prefixes"), h.PrefixCount.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.harmony.postfixes", "Postfixes"),
                        h.PostfixCount.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.harmony.transpilers", "Transpilers"),
                        h.TranspilerCount.ToString()));
                    section.AddChild(MakeKvRow(I18N.T("bridge.harmony.finalizers", "Finalizers"),
                        h.FinalizerCount.ToString()));
                }));

            inner.AddChild(MakeFootnote(I18N.T("bridge.hint",
                "Read-only. Manual refresh or enable auto-refresh below.")));
        }

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        var autoRefresh = new CheckButton {
            Text = I18N.T("bridge.autoRefresh", "Auto-refresh"),
            ButtonPressed = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        autoRefresh.AddThemeFontSizeOverride("font_size", 11);
        btnRow.AddChild(autoRefresh);
        var refresh = new Button {
            Text = I18N.T("bridge.refresh", "Refresh"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        refresh.Pressed += Rebuild;
        btnRow.AddChild(refresh);
        vbox.AddChild(btnRow);

        var timer = new Godot.Timer {
            WaitTime = AutoRefreshIntervalSec,
            OneShot = false,
            Autostart = true
        };
        timer.Timeout += () => {
            if (autoRefresh.ButtonPressed)
                Rebuild();
        };
        root.AddChild(timer);

        Rebuild();

        ((Node)globalUi).AddChild(root);
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }

    private static Control MakeSep() {
        var line = new ColorRect {
            Color = KitLibTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1)
        };
        line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return line;
    }

    private static Control MakeSectionCard(string title, string description, Action<VBoxContainer> fillBody) {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.55f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 14
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var outer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        outer.AddThemeConstantOverride("separation", 8);

        var head = new Label {
            Text = title,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        head.AddThemeFontSizeOverride("font_size", 13);
        head.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        outer.AddChild(head);

        var desc = new Label {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        desc.AddThemeFontSizeOverride("font_size", 10);
        desc.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        outer.AddChild(desc);

        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        fillBody(body);
        outer.AddChild(body);

        panel.AddChild(outer);
        return panel;
    }

    private static HBoxContainer MakeKvRow(string label, string value) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var left = new Label {
            Text = label + ":",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        left.AddThemeFontSizeOverride("font_size", 11);
        left.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        var right = new Label {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Right,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        right.AddThemeFontSizeOverride("font_size", 11);
        right.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        right.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        right.CustomMinimumSize = new Vector2(220, 0);
        row.AddChild(left);
        row.AddChild(right);
        return row;
    }

    private static Label MakeDetailLabel(string text) {
        var l = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        l.AddThemeFontSizeOverride("font_size", 10);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return l;
    }

    private static Control MakeCodeBlock(string text) {
        var wrap = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var innerStyle = new StyleBoxFlat {
            BgColor = new Color(0f, 0f, 0f, 0.28f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        wrap.AddThemeStyleboxOverride("panel", innerStyle);

        var lbl = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        lbl.AddThemeFontSizeOverride("font_size", 10);
        lbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        wrap.AddChild(lbl);
        return wrap;
    }

    private static Label MakeFootnote(string text) {
        var l = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        l.AddThemeFontSizeOverride("font_size", 10);
        l.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return l;
    }

    private static string YesNo(bool v) => v ? I18N.T("bridge.yes", "Yes") : I18N.T("bridge.no", "No");
}
