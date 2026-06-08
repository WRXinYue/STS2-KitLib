using System;
using System.Linq;
using KitLib.Scripts;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>SpireScratch script manager panel — shows loaded scripts with enable/disable toggle.</summary>
internal static class ScriptUI {
    private const string RootName = "KitLibScripts";
    private const float PanelW = 680f;

    private static Color ColAccent => KitLibTheme.Accent;
    private static Color ColLight => KitLibTheme.TextPrimary;
    private static Color ColSubtle => KitLibTheme.Subtle;
    private static Color ColBg => KitLibTheme.ButtonBgNormal;
    private static Color ColError => new(0.9f, 0.35f, 0.3f);
    private static Color ColOk => new(0.3f, 0.85f, 0.45f);

    private static int _lastSeenVersion = -1;
    private static bool _refreshing;

    public static void Show(NGlobalUi globalUi) {
        if (_refreshing) return;
        _refreshing = true;
        try {
            Remove(globalUi);
            _lastSeenVersion = ScriptManager.ReloadVersion;

            var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
                globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 8);

            BuildHeader(vbox, globalUi);
            BuildScriptList(vbox);
            BuildVariableSection(vbox);

            var timer = new Godot.Timer { WaitTime = 1.0, Autostart = true, OneShot = false, Name = "ScriptAutoRefresh" };
            timer.Timeout += () => {
                if (ScriptManager.ReloadVersion != _lastSeenVersion)
                    Show(globalUi);
            };
            root.AddChild(timer);

            ((Node)globalUi).AddChild(root);
        }
        finally { _refreshing = false; }
    }

    public static void Remove(NGlobalUi globalUi) {
        var parent = (Node)globalUi;
        // Remove ALL instances to clean up any leftover duplicates
        while (true) {
            var root = parent.GetNodeOrNull<Control>(RootName);
            if (root == null) break;
            // Stop timer immediately so it can't trigger another Show()
            root.GetNodeOrNull<Godot.Timer>("ScriptAutoRefresh")?.Stop();
            // Detach from tree first — prevents QueueFree race where
            // the old node is still findable by name
            parent.RemoveChild(root);
            root.QueueFree();
        }
    }

    // ──────── Header ────────

    private static void BuildHeader(VBoxContainer vbox, NGlobalUi globalUi) {
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 10);

        var title = new Label {
            Text = I18N.T("script.title", "SpireScratch Scripts"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", ColAccent);
        titleRow.AddChild(title);

        titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // Reload button
        var reloadBtn = new Button {
            Text = I18N.T("script.reload", "Reload"),
            FocusMode = Control.FocusModeEnum.None,
        };
        reloadBtn.AddThemeFontSizeOverride("font_size", 12);
        reloadBtn.Pressed += () => {
            ScriptManager.Reload();
            Show(globalUi);
        };
        titleRow.AddChild(reloadBtn);

        // Open folder button
        var openBtn = new Button {
            Text = I18N.T("script.openFolder", "Open Folder"),
            FocusMode = Control.FocusModeEnum.None,
        };
        openBtn.AddThemeFontSizeOverride("font_size", 12);
        openBtn.Pressed += () => {
            try { OS.ShellOpen(ScriptManager.ScriptsDir); }
            catch (Exception ex) { MainFile.Logger.Warn($"[ScriptUI] Open folder failed: {ex.Message}"); }
        };
        titleRow.AddChild(openBtn);

        // Open editor button
        var editorBtn = new Button {
            Text = I18N.T("script.openEditor", "Open Editor"),
            FocusMode = Control.FocusModeEnum.None,
        };
        editorBtn.AddThemeFontSizeOverride("font_size", 12);
        editorBtn.AddThemeColorOverride("font_color", ColAccent);
        editorBtn.Pressed += () => {
            try {
                var editorPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(ScriptManager.ScriptsDir) ?? "", "editor", "index.html");
                if (System.IO.File.Exists(editorPath))
                    OS.ShellOpen(editorPath);
                else
                    MainFile.Logger.Warn($"[ScriptUI] Editor not found at: {editorPath}");
            }
            catch (Exception ex) { MainFile.Logger.Warn($"[ScriptUI] Open editor failed: {ex.Message}"); }
        };
        titleRow.AddChild(editorBtn);

        // Migrate hooks button
        var hooks = KitLib.Settings.SettingsStore.Current.Hooks;
        if (hooks != null && hooks.Count > 0) {
            var migrateBtn = new Button {
                Text = I18N.T("script.migrate", "Migrate Hooks ({0})", hooks.Count),
                FocusMode = Control.FocusModeEnum.None,
            };
            migrateBtn.AddThemeFontSizeOverride("font_size", 12);
            migrateBtn.Pressed += () => {
                int migrated = Scripts.HookMigration.MigrateAll();
                MainFile.Logger.Info($"[ScriptUI] Migrated {migrated} hook(s) to scripts.");
                Show(globalUi);
            };
            titleRow.AddChild(migrateBtn);
        }

        vbox.AddChild(titleRow);

        // Status line
        var statusRow = new HBoxContainer();
        statusRow.AddThemeConstantOverride("separation", 6);

        var statusLabel = new Label {
            Text = string.Format(
                I18N.T("script.status", "{0} script(s) loaded — last reload: {1}"),
                ScriptManager.Scripts.Count,
                ScriptManager.LastReloadTime.ToString("HH:mm:ss")),
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", ColSubtle);
        statusRow.AddChild(statusLabel);

        if (ScriptManager.LastError != null) {
            var errLabel = new Label { Text = ScriptManager.LastError };
            errLabel.AddThemeFontSizeOverride("font_size", 11);
            errLabel.AddThemeColorOverride("font_color", ColError);
            statusRow.AddChild(errLabel);
        }

        vbox.AddChild(statusRow);
        vbox.AddChild(MakeDivider());
    }

    // ──────── Script List ────────

    private static void BuildScriptList(VBoxContainer vbox) {
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 200),
        };
        var listBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listBox.AddThemeConstantOverride("separation", 4);

        var scripts = ScriptManager.Scripts;
        if (scripts.Count == 0) {
            var empty = new Label {
                Text = I18N.T("script.empty", "No scripts found. Open the editor to create one."),
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            empty.AddThemeFontSizeOverride("font_size", 12);
            empty.AddThemeColorOverride("font_color", ColSubtle);
            listBox.AddChild(empty);
        }
        else {
            foreach (var loaded in scripts)
                listBox.AddChild(BuildScriptRow(loaded));
        }

        scroll.AddChild(listBox);
        vbox.AddChild(scroll);
    }

    private static Control BuildScriptRow(ScriptManager.LoadedScript loaded) {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat {
            BgColor = ColBg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        // Status indicator
        var statusDot = new Label {
            Text = loaded.ParseError != null ? "✗" : loaded.Entry.Enabled ? "●" : "○",
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(16, 0),
        };
        statusDot.AddThemeFontSizeOverride("font_size", 12);
        statusDot.AddThemeColorOverride("font_color",
            loaded.ParseError != null ? ColError : loaded.Entry.Enabled ? ColOk : ColSubtle);
        row.AddChild(statusDot);

        // Name
        var nameCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var nameLabel = new Label {
            Text = loaded.ParseError != null
                ? loaded.FileName
                : string.IsNullOrEmpty(loaded.Entry.Name) ? loaded.FileName : loaded.Entry.Name,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        nameLabel.AddThemeColorOverride("font_color", loaded.ParseError != null ? ColError : ColLight);
        nameCol.AddChild(nameLabel);

        // Subtitle: trigger or error
        var subLabel = new Label {
            Text = loaded.ParseError != null
                ? loaded.ParseError
                : $"{loaded.Entry.Trigger}  —  {loaded.FileName}",
        };
        subLabel.AddThemeFontSizeOverride("font_size", 10);
        subLabel.AddThemeColorOverride("font_color", loaded.ParseError != null ? ColError : ColSubtle);
        nameCol.AddChild(subLabel);

        row.AddChild(nameCol);

        // Enable toggle
        if (loaded.ParseError == null) {
            var toggle = new CheckButton {
                ButtonPressed = loaded.Entry.Enabled,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(40, 22),
            };
            var entry = loaded.Entry;
            var filePath = loaded.FilePath;
            toggle.Toggled += on => {
                entry.Enabled = on;
                try {
                    var json = System.Text.Json.JsonSerializer.Serialize(entry, new System.Text.Json.JsonSerializerOptions {
                        WriteIndented = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    });
                    System.IO.File.WriteAllText(filePath, json);
                }
                catch { /* watcher will pick up the change */ }
            };
            row.AddChild(toggle);
        }

        panel.AddChild(row);
        return panel;
    }

    // ──────── Variables Section ────────

    private static void BuildVariableSection(VBoxContainer vbox) {
        var vars = ScriptVariableStore.All;
        if (!vars.Any()) return;

        vbox.AddChild(MakeDivider());

        var hdr = new Label { Text = I18N.T("script.variables", "Script Variables") };
        hdr.AddThemeFontSizeOverride("font_size", 12);
        hdr.AddThemeColorOverride("font_color", ColAccent);
        vbox.AddChild(hdr);

        foreach (var (name, value) in vars) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameL = new Label { Text = name, CustomMinimumSize = new Vector2(120, 0) };
            nameL.AddThemeFontSizeOverride("font_size", 11);
            nameL.AddThemeColorOverride("font_color", ColLight);
            row.AddChild(nameL);

            var valL = new Label { Text = value.ToString() };
            valL.AddThemeFontSizeOverride("font_size", 11);
            valL.AddThemeColorOverride("font_color", ColAccent);
            row.AddChild(valL);

            vbox.AddChild(row);
        }
    }

    // ──────── Helpers ────────

    private static ColorRect MakeDivider() => new() {
        Color = KitLibTheme.Separator,
        CustomMinimumSize = new Vector2(0, 1),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };
}
