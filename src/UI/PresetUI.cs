using System;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Presets;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Preset manager — two-pane browser with selective save/load scope.</summary>
internal static class PresetUI {
    private const string RootName = "KitLibPresets";
    private const float PanelW = 800f;

    private static readonly Color ColCards = new(0.35f, 0.58f, 0.95f);
    private static readonly Color ColRelics = new(0.88f, 0.72f, 0.22f);
    private static readonly Color ColStats = new(0.32f, 0.76f, 0.50f);
    private static Color ColLight => KitLibTheme.TextPrimary;
    private static Color ColDetailBg => KitLibTheme.ButtonBgNormal;

    // ─────────────────────────────── State ───────────────────────────────

    private sealed class State {
        public string? SelectedName;
        public LoadoutPreset? SelectedPreset;

        // Left pane UI
        public LineEdit NameInput = null!;
        public CheckButton SaveCards = null!;
        public CheckButton SaveRelics = null!;
        public CheckButton SaveStats = null!;
        public CheckButton SaveSnapshot = null!;
        public Button SaveBtn = null!;
        public Label CountLabel = null!;
        public VBoxContainer PresetList = null!;

        // Right pane UI
        public Label DetailName = null!;
        public HBoxContainer DetailBadges = null!;
        public Label DetailSummary = null!;
        public VBoxContainer ApplyScopeBox = null!;
        public CheckButton ApplyCards = null!;
        public CheckButton ApplyRelics = null!;
        public CheckButton ApplyStats = null!;
        public Button ApplyBtn = null!;
        public Button ExportBtn = null!;
        public Button DeleteBtn = null!;
        public Label StatusLabel = null!;
        public Label HintLabel = null!;
        public Control DetailContent = null!;

        // Per-session handlers stored in State (not statics) so they bind to the correct button instances
        public Action? ApplyHandler;
        public Action? ExportHandler;
        public Action? DeleteHandler;
    }

    // ─────────────────────────────── Public API ───────────────────────────────

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 8);

        var s = new State();

        // ── Title bar ──
        BuildTitleBar(vbox);
        vbox.AddChild(MakeDivider());

        // ── Two-column body ──
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(body);

        BuildLeftPane(body, s, () => RebuildPresetList(s));
        BuildRightPane(body, s);

        RebuildPresetList(s);

        ((Node)globalUi).AddChild(root);
        s.NameInput.GrabFocus();
    }

    public static void Remove(NGlobalUi globalUi)
        => ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();

    // ─────────────────────────────── Title bar ───────────────────────────────

    private static void BuildTitleBar(VBoxContainer vbox) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var title = new Label { Text = I18N.T("preset.title", "Preset Manager") };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        row.AddChild(title);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
    }

    // ─────────────────────────────── Left pane ───────────────────────────────

    private static void BuildLeftPane(HBoxContainer body, State s, Action rebuildList) {
        var pane = new VBoxContainer {
            CustomMinimumSize = new Vector2(290f, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        pane.AddThemeConstantOverride("separation", 8);

        // ── New preset card ──
        var newCard = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        newCard.AddThemeStyleboxOverride("panel", MakeCardStyle(KitLibTheme.Separator));

        var newInner = new VBoxContainer();
        newInner.AddThemeConstantOverride("separation", 8);

        var newHdr = new Label { Text = I18N.T("preset.newPreset", "New Preset") };
        newHdr.AddThemeFontSizeOverride("font_size", 11);
        newHdr.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        newInner.AddChild(newHdr);

        s.NameInput = new LineEdit {
            PlaceholderText = I18N.T("preset.name", "Preset name..."),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        newInner.AddChild(s.NameInput);

        // Scope toggles
        var includeHdr = new Label { Text = I18N.T("preset.include", "Include:") };
        includeHdr.AddThemeFontSizeOverride("font_size", 11);
        includeHdr.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        newInner.AddChild(includeHdr);

        var toggleRow = new HBoxContainer();
        toggleRow.AddThemeConstantOverride("separation", 12);
        s.SaveCards = MakeScopeToggle(I18N.T("preset.scope.cards", "Cards"), ColCards, true);
        s.SaveRelics = MakeScopeToggle(I18N.T("preset.scope.relics", "Relics"), ColRelics, true);
        s.SaveStats = MakeScopeToggle(I18N.T("preset.scope.stats", "Stats"), ColStats, true);
        toggleRow.AddChild(s.SaveCards);
        toggleRow.AddChild(s.SaveRelics);
        toggleRow.AddChild(s.SaveStats);
        newInner.AddChild(toggleRow);

        bool showSnapshot = MegaCrit.Sts2.Core.Combat.CombatManager.Instance?.IsInProgress == true;
        s.SaveSnapshot = MakeScopeToggle(
            I18N.T("preset.scope.snapshot", "Combat Snapshot"), new Color(0.85f, 0.55f, 0.25f), false);
        s.SaveSnapshot.Visible = showSnapshot;
        s.SaveSnapshot.Disabled = !showSnapshot;
        s.SaveCards.Toggled += on => { s.SaveSnapshot.Visible = on && showSnapshot; };
        newInner.AddChild(s.SaveSnapshot);

        s.SaveBtn = new Button {
            Text = I18N.T("preset.save", "Save Current"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 32),
        };
        ApplyAccentBtnStyle(s.SaveBtn);
        s.SaveBtn.Pressed += () => DoSave(s, rebuildList);
        newInner.AddChild(s.SaveBtn);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddChild(newInner);
        newCard.AddChild(margin);
        pane.AddChild(newCard);

        // ── Import ──
        var importBtn = new Button {
            Text = I18N.T("preset.import", "Import (Clipboard)"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        importBtn.AddThemeFontSizeOverride("font_size", 12);
        importBtn.Pressed += () => DoImport(s, rebuildList);
        pane.AddChild(importBtn);

        pane.AddChild(MakeDivider());

        // ── Preset count label ──
        s.CountLabel = new Label { HorizontalAlignment = HorizontalAlignment.Left };
        s.CountLabel.AddThemeFontSizeOverride("font_size", 11);
        s.CountLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        pane.AddChild(s.CountLabel);

        // ── Scrollable list ──
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        s.PresetList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        s.PresetList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(s.PresetList);
        pane.AddChild(scroll);

        body.AddChild(pane);
    }

    // ─────────────────────────────── Right detail pane ───────────────────────────────

    private static void BuildRightPane(HBoxContainer body, State s) {
        var pane = new PanelContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var bgStyle = new StyleBoxFlat {
            BgColor = ColDetailBg,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = KitLibTheme.PanelBorder,
        };
        pane.AddThemeStyleboxOverride("panel", bgStyle);

        var margin = new MarginContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);

        var inner = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 10);

        // Hint (shown when nothing selected)
        s.HintLabel = new Label {
            Text = I18N.T("preset.selectHint", "← Select a preset"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        s.HintLabel.AddThemeFontSizeOverride("font_size", 13);
        s.HintLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        inner.AddChild(s.HintLabel);

        // Detail content (hidden until a preset is selected)
        s.DetailContent = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false };
        ((VBoxContainer)s.DetailContent).AddThemeConstantOverride("separation", 10);

        // Name
        s.DetailName = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        s.DetailName.AddThemeFontSizeOverride("font_size", 16);
        s.DetailName.AddThemeColorOverride("font_color", ColLight);
        ((VBoxContainer)s.DetailContent).AddChild(s.DetailName);

        // Badges
        s.DetailBadges = new HBoxContainer();
        s.DetailBadges.AddThemeConstantOverride("separation", 8);
        ((VBoxContainer)s.DetailContent).AddChild(s.DetailBadges);

        // Summary
        s.DetailSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        s.DetailSummary.AddThemeFontSizeOverride("font_size", 11);
        s.DetailSummary.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        ((VBoxContainer)s.DetailContent).AddChild(s.DetailSummary);

        ((VBoxContainer)s.DetailContent).AddChild(MakeDivider());

        // Apply scope
        var applyScopeHdr = new Label { Text = I18N.T("preset.applyScope", "Apply scope") };
        applyScopeHdr.AddThemeFontSizeOverride("font_size", 11);
        applyScopeHdr.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        ((VBoxContainer)s.DetailContent).AddChild(applyScopeHdr);

        s.ApplyScopeBox = new VBoxContainer();
        s.ApplyScopeBox.AddThemeConstantOverride("separation", 6);

        var applyToggleRow = new HBoxContainer();
        applyToggleRow.AddThemeConstantOverride("separation", 14);
        s.ApplyCards = MakeScopeToggle(I18N.T("preset.scope.cards", "Cards"), ColCards, true);
        s.ApplyRelics = MakeScopeToggle(I18N.T("preset.scope.relics", "Relics"), ColRelics, true);
        s.ApplyStats = MakeScopeToggle(I18N.T("preset.scope.stats", "Stats"), ColStats, true);
        applyToggleRow.AddChild(s.ApplyCards);
        applyToggleRow.AddChild(s.ApplyRelics);
        applyToggleRow.AddChild(s.ApplyStats);
        s.ApplyScopeBox.AddChild(applyToggleRow);

        ((VBoxContainer)s.DetailContent).AddChild(s.ApplyScopeBox);
        ((VBoxContainer)s.DetailContent).AddChild(MakeDivider());

        // Action buttons
        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);

        s.ApplyBtn = MakeActionBtn(I18N.T("preset.load", "Apply"), new Color(0.22f, 0.50f, 0.34f, 0.92f));
        s.ExportBtn = MakeActionBtn(I18N.T("preset.export", "Export"), new Color(0.22f, 0.30f, 0.48f, 0.92f));
        s.DeleteBtn = MakeActionBtn(I18N.T("preset.delete", "Delete"), new Color(0.48f, 0.18f, 0.18f, 0.92f));

        s.ApplyBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        s.ExportBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        s.DeleteBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        actionRow.AddChild(s.ApplyBtn);
        actionRow.AddChild(s.ExportBtn);
        actionRow.AddChild(s.DeleteBtn);
        ((VBoxContainer)s.DetailContent).AddChild(actionRow);

        // Spacer to push status to bottom
        ((VBoxContainer)s.DetailContent).AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // Status
        s.StatusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        s.StatusLabel.AddThemeFontSizeOverride("font_size", 11);
        s.StatusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        ((VBoxContainer)s.DetailContent).AddChild(s.StatusLabel);

        inner.AddChild(s.DetailContent);

        margin.AddChild(inner);
        pane.AddChild(margin);
        body.AddChild(pane);
    }

    // ─────────────────────────────── Preset list rebuild ───────────────────────────────

    private static void RebuildPresetList(State s) {
        foreach (var child in s.PresetList.GetChildren())
            ((Node)child).QueueFree();

        var all = PresetManager.Loadouts.All.OrderBy(k => k.Key).ToList();
        s.CountLabel.Text = I18N.T("preset.count", "{0} preset(s)", all.Count);

        if (all.Count == 0) {
            var empty = new Label {
                Text = I18N.T("preset.empty", "No saved presets."),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            empty.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            s.PresetList.AddChild(empty);
            return;
        }

        foreach (var kvp in all) {
            var name = kvp.Key;
            var preset = kvp.Value;
            var isSelected = name == s.SelectedName;

            var row = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var rowStyle = MakeCardStyle(isSelected
                ? new Color(0.24f, 0.34f, 0.50f, 0.90f)
                : KitLibTheme.Separator);
            if (isSelected) {
                rowStyle.BorderWidthLeft = rowStyle.BorderWidthRight = rowStyle.BorderWidthTop = rowStyle.BorderWidthBottom = 1;
                rowStyle.BorderColor = KitLibTheme.AccentAlpha;
            }
            row.AddThemeStyleboxOverride("panel", rowStyle);
            row.MouseFilter = Control.MouseFilterEnum.Stop;

            var rowInner = new VBoxContainer();
            rowInner.AddThemeConstantOverride("separation", 4);

            // Name + badges row
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 6);

            var nameLabel = new Label {
                Text = name,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                ClipText = true,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 13);
            nameLabel.AddThemeColorOverride("font_color", ColLight);
            topRow.AddChild(nameLabel);

            // Small content badges
            if (preset.Contents.HasFlag(PresetContents.Cards))
                topRow.AddChild(MakeSmallBadge("C", ColCards));
            if (preset.Contents.HasFlag(PresetContents.Relics))
                topRow.AddChild(MakeSmallBadge("R", ColRelics));
            if (preset.Contents.HasFlag(PresetContents.Stats))
                topRow.AddChild(MakeSmallBadge("S", ColStats));

            rowInner.AddChild(topRow);

            // Summary sub-line
            var summary = BuildSummaryLine(preset);
            var subLabel = new Label { Text = summary };
            subLabel.AddThemeFontSizeOverride("font_size", 11);
            subLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            rowInner.AddChild(subLabel);

            var innerMargin = new MarginContainer();
            innerMargin.AddThemeConstantOverride("margin_left", 10);
            innerMargin.AddThemeConstantOverride("margin_right", 10);
            innerMargin.AddThemeConstantOverride("margin_top", 8);
            innerMargin.AddThemeConstantOverride("margin_bottom", 8);
            innerMargin.MouseFilter = Control.MouseFilterEnum.Ignore;
            rowInner.MouseFilter = Control.MouseFilterEnum.Ignore;
            innerMargin.AddChild(rowInner);
            row.AddChild(innerMargin);

            var capturedName = name;
            var capturedPreset = preset;
            row.GuiInput += evt => {
                if (evt is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                    SelectPreset(s, capturedName, capturedPreset, () => RebuildPresetList(s));
            };

            s.PresetList.AddChild(row);
        }
    }

    // ─────────────────────────────── Detail update ───────────────────────────────

    private static void SelectPreset(State s, string name, LoadoutPreset preset, Action rebuildList) {
        s.SelectedName = name;
        s.SelectedPreset = preset;

        s.HintLabel.Visible = false;
        s.DetailContent.Visible = true;

        s.DetailName.Text = name;

        // Rebuild badge row
        foreach (var child in s.DetailBadges.GetChildren())
            ((Node)child).QueueFree();
        if (preset.Contents.HasFlag(PresetContents.Cards))
            s.DetailBadges.AddChild(MakeFullBadge(I18N.T("preset.scope.cards", "Cards"), ColCards));
        if (preset.Contents.HasFlag(PresetContents.Relics))
            s.DetailBadges.AddChild(MakeFullBadge(I18N.T("preset.scope.relics", "Relics"), ColRelics));
        if (preset.Contents.HasFlag(PresetContents.Stats))
            s.DetailBadges.AddChild(MakeFullBadge(I18N.T("preset.scope.stats", "Stats"), ColStats));

        // Summary
        s.DetailSummary.Text = BuildDetailSummary(preset);

        // Apply scope — pre-tick to what's in the preset, hide unavailable
        s.ApplyCards.ButtonPressed = preset.Contents.HasFlag(PresetContents.Cards);
        s.ApplyRelics.ButtonPressed = preset.Contents.HasFlag(PresetContents.Relics);
        s.ApplyStats.ButtonPressed = preset.Contents.HasFlag(PresetContents.Stats);
        s.ApplyCards.Disabled = !preset.Contents.HasFlag(PresetContents.Cards);
        s.ApplyRelics.Disabled = !preset.Contents.HasFlag(PresetContents.Relics);
        s.ApplyStats.Disabled = !preset.Contents.HasFlag(PresetContents.Stats);

        // Swap action button handlers for this selection
        ReconnectApply(s, preset, rebuildList);
        ReconnectExport(s, name, preset);
        ReconnectDelete(s, name, rebuildList);

        s.StatusLabel.Text = "";

        rebuildList();
    }

    private static void ReconnectApply(State s, LoadoutPreset preset, Action rebuildList) {
        if (s.ApplyHandler != null) s.ApplyBtn.Pressed -= s.ApplyHandler;
        s.ApplyHandler = () => {
            var scope = PresetContents.None;
            if (s.ApplyCards.ButtonPressed) scope |= PresetContents.Cards;
            if (s.ApplyRelics.ButtonPressed) scope |= PresetContents.Relics;
            if (s.ApplyStats.ButtonPressed) scope |= PresetContents.Stats;
            if (scope == PresetContents.None) {
                s.StatusLabel.Text = I18N.T("preset.noSelection", "Select at least one scope.");
                return;
            }
            MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(PresetManager.ApplyToRunAsync(preset, scope));
            s.StatusLabel.Text = I18N.T("preset.applied", "Preset applied: {0}", s.SelectedName ?? "");
        };
        s.ApplyBtn.Pressed += s.ApplyHandler;
    }

    private static void ReconnectExport(State s, string name, LoadoutPreset preset) {
        if (s.ExportHandler != null) s.ExportBtn.Pressed -= s.ExportHandler;
        s.ExportHandler = () => {
            PresetManager.ExportToClipboard(name, preset);
            s.StatusLabel.Text = I18N.T("preset.exported", "Exported to clipboard: {0}", name);
        };
        s.ExportBtn.Pressed += s.ExportHandler;
    }

    private static void ReconnectDelete(State s, string name, Action rebuildList) {
        if (s.DeleteHandler != null) s.DeleteBtn.Pressed -= s.DeleteHandler;
        s.DeleteHandler = () => {
            PresetManager.Loadouts.Delete(name);
            s.SelectedName = null;
            s.SelectedPreset = null;
            s.HintLabel.Visible = true;
            s.DetailContent.Visible = false;
            s.StatusLabel.Text = I18N.T("preset.deleted", "Deleted: {0}", name);
            rebuildList();
        };
        s.DeleteBtn.Pressed += s.DeleteHandler;
    }

    // ─────────────────────────────── Save / Import ───────────────────────────────

    private static void DoSave(State s, Action rebuildList) {
        var n = s.NameInput.Text?.Trim();
        if (string.IsNullOrEmpty(n)) {
            SetStatus(s, I18N.T("preset.error.noName", "Enter a name first."));
            return;
        }

        var scope = PresetContents.None;
        if (s.SaveCards.ButtonPressed) scope |= PresetContents.Cards;
        if (s.SaveRelics.ButtonPressed) scope |= PresetContents.Relics;
        if (s.SaveStats.ButtonPressed) scope |= PresetContents.Stats;

        if (scope == PresetContents.None) {
            SetStatus(s, I18N.T("preset.noSelection", "Select at least one scope."));
            return;
        }

        bool snapshot = s.SaveSnapshot.ButtonPressed && s.SaveCards.ButtonPressed;
        var p = PresetManager.CaptureFromRun(scope, snapshot);
        if (p == null) {
            SetStatus(s, I18N.T("preset.error.noRun", "No active run."));
            return;
        }

        PresetManager.Loadouts.Set(n, p);
        SetStatus(s, I18N.T("preset.saved", "Saved: {0}", n));
        s.NameInput.Text = "";
        rebuildList();
    }

    private static void DoImport(State s, Action rebuildList) {
        var (n, p) = PresetManager.ImportFromClipboard();
        if (n == null || p == null) {
            SetStatus(s, I18N.T("preset.error.import", "Invalid clipboard data."));
            return;
        }
        PresetManager.Loadouts.Set(n, p);
        SetStatus(s, I18N.T("preset.imported", "Imported: {0}", n));
        rebuildList();
    }

    private static void SetStatus(State s, string msg) {
        // StatusLabel lives in the right pane; if nothing is selected show it on CountLabel instead
        if (s.DetailContent.Visible)
            s.StatusLabel.Text = msg;
        else
            s.CountLabel.Text = msg;
    }

    // ─────────────────────────────── Summary helpers ───────────────────────────────

    private static string BuildSummaryLine(LoadoutPreset preset) {
        var parts = new System.Collections.Generic.List<string>();
        if (preset.Contents.HasFlag(PresetContents.Cards) && preset.Cards.Count > 0) {
            var tag = preset.HasCombatSnapshot ? "c*" : "c";
            parts.Add($"{preset.Cards.Sum(c => c.Count)}{tag}");
        }
        if (preset.Contents.HasFlag(PresetContents.Relics) && preset.Relics.Count > 0)
            parts.Add($"{preset.Relics.Count}r");
        if (preset.Contents.HasFlag(PresetContents.Stats))
            parts.Add($"{preset.Gold}g");
        return parts.Count > 0 ? string.Join("  ", parts) : "—";
    }

    private static string BuildDetailSummary(LoadoutPreset preset) {
        var lines = new System.Collections.Generic.List<string>();
        if (preset.Contents.HasFlag(PresetContents.Cards)) {
            lines.Add(I18N.T("preset.detail.cards", "Cards: {0}", preset.Cards.Sum(c => c.Count)));
            if (preset.HasCombatSnapshot) {
                int hand = preset.HandCards?.Sum(c => c.Count) ?? 0;
                int draw = preset.DrawCards?.Sum(c => c.Count) ?? 0;
                int disc = preset.DiscardCards?.Sum(c => c.Count) ?? 0;
                lines.Add($"  {I18N.T("preset.detail.hand", "Hand: {0}", hand)}" +
                          $"  {I18N.T("preset.detail.draw", "Draw: {0}", draw)}" +
                          $"  {I18N.T("preset.detail.discard", "Discard: {0}", disc)}");
                lines.Add(I18N.T("preset.detail.snapshot", "(combat snapshot)"));
            }
        }
        if (preset.Contents.HasFlag(PresetContents.Relics))
            lines.Add(I18N.T("preset.detail.relics", "Relics: {0}", preset.Relics.Count));
        if (preset.Contents.HasFlag(PresetContents.Stats)) {
            lines.Add(I18N.T("preset.detail.gold", "Gold: {0}", preset.Gold));
            lines.Add(I18N.T("preset.detail.hp", "HP: {0} / {1}", preset.CurrentHp, preset.MaxHp));
            lines.Add(I18N.T("preset.detail.energy", "Max Energy: {0}", preset.MaxEnergy));
        }
        return string.Join("   ·   ", lines);
    }

    // ─────────────────────────────── Widget helpers ───────────────────────────────

    private static CheckButton MakeScopeToggle(string label, Color col, bool defaultOn) {
        var cb = new CheckButton {
            Text = label,
            ButtonPressed = defaultOn,
            FocusMode = Control.FocusModeEnum.None,
        };
        cb.AddThemeFontSizeOverride("font_size", 12);
        cb.AddThemeColorOverride("font_color", col);
        return cb;
    }

    private static Label MakeSmallBadge(string text, Color col) {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 10);
        lbl.AddThemeColorOverride("font_color", col);
        return lbl;
    }

    private static Label MakeFullBadge(string text, Color col) {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", col);
        return lbl;
    }

    private static Button MakeActionBtn(string text, Color bg) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        StyleBoxFlat MakeStyle(Color c) => new() {
            BgColor = c,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        btn.AddThemeStyleboxOverride("normal", MakeStyle(bg));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(bg.Lightened(0.12f)));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(bg.Lightened(0.18f)));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(bg));
        btn.AddThemeColorOverride("font_color", ColLight);
        btn.AddThemeFontSizeOverride("font_size", 12);
        return btn;
    }

    private static void ApplyAccentBtnStyle(Button btn) {
        StyleBoxFlat MakeStyle(Color c) => new() {
            BgColor = c,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        };
        var bg = new Color(0.28f, 0.48f, 0.72f, 0.90f);
        btn.AddThemeStyleboxOverride("normal", MakeStyle(bg));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(bg.Lightened(0.10f)));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(bg.Lightened(0.15f)));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(bg));
        btn.AddThemeColorOverride("font_color", ColLight);
        btn.AddThemeFontSizeOverride("font_size", 13);
    }

    private static StyleBoxFlat MakeCardStyle(Color bg) => new() {
        BgColor = bg,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderColor = KitLibTheme.PanelBorder,
    };

    private static ColorRect MakeDivider() => new() {
        Color = KitLibTheme.Separator,
        CustomMinimumSize = new Vector2(0, 1),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };
}
