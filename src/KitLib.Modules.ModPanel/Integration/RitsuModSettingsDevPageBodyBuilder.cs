using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;
using KitLib.UI;

namespace KitLib.Integration;
/// <summary>
///     Builds RitsuLib settings page content without <c>ModSettingsUiFactory.CreatePageContent</c> / Ritsu section
///     chrome: walks registered <c>ModSettingsPage</c> (via reflection). Entry rows are built by
///     <see cref="ModSettingsRitsuEntryControls" /> (DevMode widgets); list/keybinding fall back to Ritsu
///     <c>CreateControl</c>.
/// </summary>
internal static class RitsuModSettingsDevPageBodyBuilder {
    private const string ContextTypeName = "STS2RitsuLib.Settings.ModSettingsUiContext";
    private const string LocalizationTypeName = "STS2RitsuLib.Settings.ModSettingsLocalization";
    public static bool TryBuild(Node ritsuSubmenu, string modId, object page, Assembly asm, out Control? root,
        out string? error) {
        root = null;
        error = null;
        if (ritsuSubmenu == null || page == null) {
            error = "null submenu or page";
            return false;
        }
        var contextType = asm.GetType(ContextTypeName);
        if (contextType == null) {
            error = "ModSettingsUiContext type missing";
            return false;
        }
        object? context;
        try {
            context = CreateUiContext(contextType, ritsuSubmenu, modId, page);
        }
        catch (Exception ex) {
            error = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
        if (context == null) {
            error = "ModSettingsUiContext ctor failed";
            return false;
        }
        try {
            root = BuildPageRoot(page, context, contextType, asm);
            return true;
        }
        catch (Exception ex) {
            error = ex.InnerException?.Message ?? ex.Message;
            root = null;
            return false;
        }
    }
    private static object? CreateUiContext(Type contextType, Node ritsuSubmenu, string modId, object page) {
        ConstructorInfo? chosen = null;
        foreach (var c in contextType.GetConstructors(BindingFlags.Instance | BindingFlags.Public |
                                                     BindingFlags.NonPublic)) {
            var p = c.GetParameters();
            if (p.Length == 1 && p[0].ParameterType.IsInstanceOfType(ritsuSubmenu)) {
                chosen = c;
                break;
            }
        }
        if (chosen != null)
            return chosen.Invoke(new object?[] { ritsuSubmenu });
        var pageId = page.GetType().GetProperty("Id")?.GetValue(page) as string ?? "";
        var pageModForKey = page.GetType().GetProperty("ModId")?.GetValue(page) as string;
        if (string.IsNullOrWhiteSpace(pageModForKey))
            pageModForKey = modId;
        var pageKey = $"{pageModForKey}::{pageId}";
        foreach (var c in contextType.GetConstructors(BindingFlags.Instance | BindingFlags.Public |
                                                     BindingFlags.NonPublic)) {
            var p = c.GetParameters();
            if (p.Length != 2)
                continue;
            if (!p[0].ParameterType.IsInstanceOfType(ritsuSubmenu))
                continue;
            if (p[1].ParameterType != typeof(string))
                continue;
            chosen = c;
            break;
        }
        if (chosen != null)
            return chosen.Invoke(new object?[] { ritsuSubmenu, pageKey });
        throw new MissingMethodException(contextType.FullName, "ModSettingsUiContext(submenu[, pageKey])");
    }
    private static Control BuildPageRoot(object page, object context, Type contextType, Assembly asm) {
        var modId = page.GetType().GetProperty("ModId")?.GetValue(page) as string ?? "";
        var pageId = page.GetType().GetProperty("Id")?.GetValue(page) as string ?? "";
        var container = new VBoxContainer {
            Name = $"Page_{SanitizeName(modId)}_{SanitizeName(pageId)}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        container.AddThemeConstantOverride("separation", 8);
        var sections = page.GetType().GetProperty("Sections")?.GetValue(page) as IEnumerable;
        if (sections != null) {
            var index = 0;
            foreach (var section in sections) {
                if (section == null)
                    continue;
                if (index++ > 0)
                    container.AddChild(CreateDivider());
                container.AddChild(BuildSection(section, context, contextType, asm));
            }
        }
        var visibleWhen = page.GetType().GetProperty("VisibleWhen")?.GetValue(page) as Func<bool>;
        return MaybeWrapDynamicVisibility(context, container, visibleWhen);
    }
    private static Control BuildSection(object section, object context, Type contextType, Assembly asm) {
        var id = section.GetType().GetProperty("Id")?.GetValue(section) as string ?? "section";
        var isCollapsible = section.GetType().GetProperty("IsCollapsible")?.GetValue(section) is true;
        var startCollapsed = section.GetType().GetProperty("StartCollapsed")?.GetValue(section) is true;
        var titleObj = section.GetType().GetProperty("Title")?.GetValue(section);
        var descObj = section.GetType().GetProperty("Description")?.GetValue(section);
        Control content;
        if (isCollapsible)
            content = BuildCollapsibleSection(id, titleObj, descObj, startCollapsed, section, context, contextType,
                asm);
        else
            content = BuildFlatSection(id, titleObj, descObj, section, context, contextType, asm);
        var sectionVisible = section.GetType().GetProperty("VisibleWhen")?.GetValue(section) as Func<bool>;
        return MaybeWrapDynamicVisibility(context, content, sectionVisible);
    }
    private static Control BuildFlatSection(string id, object? titleObj, object? descObj, object section,
        object context, Type contextType, Assembly asm) {
        var container = new VBoxContainer {
            Name = $"Section_{SanitizeName(id)}",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        container.AddThemeConstantOverride("separation", 8);
        if (titleObj != null) {
            var title = CreateStaticSectionTitleLabel(context, contextType, titleObj, asm);
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            container.AddChild(title);
        }
        if (descObj != null) {
            var desc = CreateStaticDescriptionLabel(context, contextType, descObj);
            desc.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            container.AddChild(desc);
        }
        foreach (var entryControl in EnumerateEntryControls(section, context, contextType, asm))
            container.AddChild(entryControl);
        return container;
    }
    private static Control BuildCollapsibleSection(string id, object? titleObj, object? descObj, bool startCollapsed,
        object section, object context, Type contextType, Assembly asm) {
        var root = new VBoxContainer {
            Name = $"Section_{SanitizeName(id)}_Collapsible",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddThemeConstantOverride("separation", 6);
        var titleText = titleObj != null
            ? ResolveRitsuUiText(contextType, titleObj)
            : ResolveLocalized(asm, "section.default", "Section");
        var header = new Button {
            ToggleMode = true,
            Flat = true,
            Text = titleText,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        header.AddThemeFontSizeOverride("font_size", 20);
        header.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        var descText = descObj != null ? ResolveRitsuUiText(contextType, descObj) : null;
        Label? descLabel = null;
        if (!string.IsNullOrWhiteSpace(descText)) {
            descLabel = new Label {
                Text = descText,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            descLabel.AddThemeFontSizeOverride("font_size", 14);
            descLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        }
        var content = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 8);
        content.Visible = !startCollapsed;
        header.ButtonPressed = !startCollapsed;
        header.Toggled += expanded => {
            content.Visible = expanded;
            if (descLabel != null)
                descLabel.Visible = expanded;
        };
        root.AddChild(header);
        if (descLabel != null) {
            descLabel.Visible = !startCollapsed;
            root.AddChild(descLabel);
        }
        foreach (var entryControl in EnumerateEntryControls(section, context, contextType, asm))
            content.AddChild(entryControl);
        root.AddChild(content);
        return root;
    }
    private static global::System.Collections.Generic.IEnumerable<Control> EnumerateEntryControls(object section,
        object context,
        Type contextType,
        Assembly asm) {
        var entries = section.GetType().GetProperty("Entries")?.GetValue(section) as IEnumerable;
        if (entries == null)
            yield break;
        foreach (var entry in entries) {
            if (entry == null)
                continue;
            var ctrl = ModSettingsRitsuEntryControls.Create(context, contextType, asm, entry);
            if (ctrl == null)
                continue;
            var visibility = TryGetVisibilityPredicate(entry);
            yield return MaybeWrapDynamicVisibility(context, ctrl, visibility);
        }
    }
    private static Func<bool>? TryGetVisibilityPredicate(object entry) {
        for (var t = entry.GetType(); t != null; t = t.BaseType) {
            var visProp = t.GetProperty("VisibilityPredicate",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (visProp == null)
                continue;
            return visProp.GetValue(entry) as Func<bool>;
        }
        return null;
    }
    private static Label CreateStaticSectionTitleLabel(object context, Type contextType, object titleObj,
        Assembly asm) {
        var text = ResolveEntryLabelDisplay(contextType, titleObj, asm);
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        RegisterTitleRefresh(context, contextType, titleObj, asm, label);
        return label;
    }
    private static Label CreateStaticDescriptionLabel(object context, Type contextType, object descObj) {
        var text = ResolveRitsuUiText(contextType, descObj);
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        RegisterDescriptionRefresh(context, contextType, descObj, label);
        return label;
    }
    private static void RegisterTitleRefresh(object context, Type contextType, object titleObj, Assembly asm,
        Label label) {
        void Apply() {
            if (!GodotObject.IsInstanceValid(label))
                return;
            label.Text = ResolveEntryLabelDisplay(contextType, titleObj, asm);
        }
        RegisterRefreshWhenAlive(context, label, Apply);
    }
    private static void RegisterDescriptionRefresh(object context, Type contextType, object descObj, Label label) {
        void Apply() {
            if (!GodotObject.IsInstanceValid(label))
                return;
            label.Text = ResolveRitsuUiText(contextType, descObj);
            label.Visible = !string.IsNullOrWhiteSpace(label.Text);
        }
        Apply();
        RegisterRefreshWhenAlive(context, label, Apply);
    }
    private static string ResolveEntryLabelDisplay(Type contextType, object titleObj, Assembly asm) {
        var resolved = ResolveRitsuUiText(contextType, titleObj);
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved;
        return ResolveLocalized(asm, "entry.label.empty", "—");
    }
    private static string ResolveLocalized(Assembly asm, string key, string fallback) {
        var loc = asm.GetType(LocalizationTypeName);
        if (loc == null)
            return fallback;
        foreach (var m in loc.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
            if (m.Name != "Get")
                continue;
            var ps = m.GetParameters();
            if (ps.Length != 2 || ps[0].ParameterType != typeof(string) || ps[1].ParameterType != typeof(string))
                continue;
            try {
                var s = m.Invoke(null, new object[] { key, fallback }) as string;
                return string.IsNullOrWhiteSpace(s) ? fallback : s;
            }
            catch {
                return fallback;
            }
        }
        return fallback;
    }
    private static string ResolveRitsuUiText(Type contextType, object? textObj) {
        if (textObj == null)
            return string.Empty;
        foreach (var m in contextType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
            if (m.Name != "Resolve")
                continue;
            var ps = m.GetParameters();
            if (ps.Length == 0)
                continue;
            if (!ps[0].ParameterType.IsInstanceOfType(textObj))
                continue;
            try {
                var args = ps.Length >= 2
                    ? new[] { textObj, string.Empty }
                    : new[] { textObj };
                return m.Invoke(null, args) as string ?? string.Empty;
            }
            catch {
                return string.Empty;
            }
        }
        return string.Empty;
    }
    private static Control MaybeWrapDynamicVisibility(object context, Control inner, Func<bool>? predicate) {
        if (predicate == null)
            return inner;
        var host = new MarginContainer {
            Name = "DynamicVisibilityHost",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        host.AddChild(inner);
        void Apply() {
            if (!GodotObject.IsInstanceValid(host))
                return;
            try {
                host.Visible = predicate();
            }
            catch {
                host.Visible = true;
            }
        }
        Apply();
        RegisterRefreshWhenAlive(context, host, Apply);
        return host;
    }
    private static void RegisterRefreshWhenAlive(object context, GodotObject? node, Action action) {
        ModSettingsRitsuEntryReflection.RegisterRefresh(context, () => {
            if (!GodotObject.IsInstanceValid(node))
                return;
            action();
        });
    }
    private static ColorRect CreateDivider() {
        return new ColorRect {
            CustomMinimumSize = new Vector2(0f, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(0.909804f, 0.862745f, 0.745098f, 0.25098f),
        };
    }
    private static string SanitizeName(string text) {
        return string.Join("_", text.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}