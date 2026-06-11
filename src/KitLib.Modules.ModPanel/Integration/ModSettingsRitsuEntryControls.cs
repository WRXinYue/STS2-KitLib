using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Godot;
using KitLib.UI;

namespace KitLib.Integration;
/// <summary>
///     DevMode-built rows for STS2-RitsuLib <c>ModSettingsEntryDefinition</c> instances (reflection over definitions;
///     no Ritsu row chrome). Unknown or heavy types fall back to Ritsu <c>CreateControl</c>.
/// </summary>
internal static class ModSettingsRitsuEntryControls {
    private const string EntryDefinitionTypeName = "STS2RitsuLib.Settings.ModSettingsEntryDefinition";
    private const string LocalizationTypeName = "STS2RitsuLib.Settings.ModSettingsLocalization";
    public static Control? Create(object context, Type contextType, Assembly asm, object entry) {
        var name = entry.GetType().Name;
        try {
            Control? c = name switch {
                "ToggleModSettingsEntryDefinition" => CreateToggle(context, contextType, asm, entry),
                "SliderModSettingsEntryDefinition" => CreateDoubleSlider(context, contextType, asm, entry),
                "FloatSliderModSettingsEntryDefinition" => CreateFloatSlider(context, contextType, asm, entry),
                "IntSliderModSettingsEntryDefinition" => CreateIntSlider(context, contextType, asm, entry),
                "ColorModSettingsEntryDefinition" => CreateColor(context, contextType, asm, entry),
                "StringModSettingsEntryDefinition" => CreateStringLine(context, contextType, asm, entry, multiline: false),
                "MultilineStringModSettingsEntryDefinition" => CreateStringLine(context, contextType, asm, entry,
                    multiline: true),
                "ButtonModSettingsEntryDefinition" => CreateButton(context, contextType, asm, entry, host: false),
                "HostContextButtonModSettingsEntryDefinition" => CreateButton(context, contextType, asm, entry,
                    host: true),
                "HeaderModSettingsEntryDefinition" => CreateHeader(context, contextType, asm, entry),
                "ParagraphModSettingsEntryDefinition" => CreateParagraph(context, contextType, asm, entry),
                "ImageModSettingsEntryDefinition" => CreateImage(context, contextType, asm, entry),
                "SubpageModSettingsEntryDefinition" => CreateSubpage(context, contextType, asm, entry),
                _ when name.StartsWith("ChoiceModSettingsEntryDefinition", StringComparison.Ordinal) =>
                    CreateChoice(context, contextType, asm, entry),
                _ => null,
            };
            // No outer WrapCard: labeled rows and CreateStackedField (multiline) already match intended chrome;
            // a global wrap would double-card stacked fields and add a panel around every form row.
            if (c != null)
                return c;
            return CreateRitsuFallback(context, contextType, asm, entry);
        }
        catch {
            return CreateRitsuFallback(context, contextType, asm, entry);
        }
    }
    private static Control? CreateRitsuFallback(object context, Type contextType, Assembly asm, object entry) {
        var entryBase = asm.GetType(EntryDefinitionTypeName);
        var create = entryBase?.GetMethod("CreateControl",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
            new[] { contextType }, null);
        return create?.Invoke(entry, new[] { context }) as Control;
    }
    private static Control CreateToggle(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding") ?? throw new InvalidOperationException("toggle binding");
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var cb = new CheckBox {
            ButtonPressed = ReadBool(binding),
        };
        DevModeFormChrome.ApplyToggle(cb);
        cb.Toggled += pressed => {
            CallWrite(binding, pressed);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        RegisterRefresh(context, cb, () => {
            if (!GodotObject.IsInstanceValid(cb))
                return;
            cb.ButtonPressed = ReadBool(binding);
        });
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, cb);
    }
    private static Control CreateDoubleSlider(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding")!;
        var min = Convert.ToDouble(GetProp(entry, "MinValue") ?? 0d);
        var max = Convert.ToDouble(GetProp(entry, "MaxValue") ?? 1d);
        var step = Convert.ToDouble(GetProp(entry, "Step") ?? 0.01d);
        var formatter = GetProp(entry, "ValueFormatter");
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var track = DevModeFormChrome.WithSliderStyle(new HSlider {
            MinValue = min,
            MaxValue = max,
            Step = step <= 0 ? 0.0001 : step,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.SliderTrackMinWidth, 22f),
            Value = ReadDouble(binding),
        });
        var valueLab = DevModeFormChrome.CreateSliderValueCaption();
        valueLab.Text = FormatSlider(binding, formatter, track.Value);
        track.ValueChanged += v => {
            CallWrite(binding, v);
            valueLab.Text = FormatSlider(binding, formatter, v);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        var right = DevModeFormChrome.CreateSliderTrackRow(track, valueLab);
        RegisterRefresh(context, track, () => {
            if (!GodotObject.IsInstanceValid(track))
                return;
            var v = ReadDouble(binding);
            track.Value = v;
            valueLab.Text = FormatSlider(binding, formatter, v);
        });
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, right);
    }
    private static Control CreateFloatSlider(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding")!;
        var min = Convert.ToSingle(GetProp(entry, "MinValue") ?? 0f);
        var max = Convert.ToSingle(GetProp(entry, "MaxValue") ?? 1f);
        var step = Convert.ToSingle(GetProp(entry, "Step") ?? 0.01f);
        var formatter = GetProp(entry, "ValueFormatter");
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var track = DevModeFormChrome.WithSliderStyle(new HSlider {
            MinValue = min,
            MaxValue = max,
            Step = step <= 0 ? 0.0001 : step,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.SliderTrackMinWidth, 22f),
            Value = ReadFloat(binding),
        });
        var valueLab = DevModeFormChrome.CreateSliderValueCaption();
        valueLab.Text = FormatSliderFloat(binding, formatter, (float)track.Value);
        track.ValueChanged += v => {
            var fv = (float)v;
            CallWrite(binding, fv);
            valueLab.Text = FormatSliderFloat(binding, formatter, fv);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        var right = DevModeFormChrome.CreateSliderTrackRow(track, valueLab);
        RegisterRefresh(context, track, () => {
            if (!GodotObject.IsInstanceValid(track))
                return;
            var fv = ReadFloat(binding);
            track.Value = fv;
            valueLab.Text = FormatSliderFloat(binding, formatter, fv);
        });
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, right);
    }
    private static Control CreateIntSlider(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding")!;
        var min = Convert.ToInt32(GetProp(entry, "MinValue") ?? 0);
        var max = Convert.ToInt32(GetProp(entry, "MaxValue") ?? 100);
        var step = Convert.ToInt32(GetProp(entry, "Step") ?? 1);
        if (step <= 0)
            step = 1;
        var formatter = GetProp(entry, "ValueFormatter");
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var track = DevModeFormChrome.WithSliderStyle(new HSlider {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.SliderTrackMinWidth, 22f),
            Value = ReadInt(binding),
        });
        var valueLab = DevModeFormChrome.CreateSliderValueCaption();
        valueLab.Text = FormatSliderInt(binding, formatter, (int)Math.Round(track.Value));
        track.ValueChanged += v => {
            var iv = (int)Math.Round(v);
            CallWrite(binding, iv);
            valueLab.Text = FormatSliderInt(binding, formatter, iv);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        var right = DevModeFormChrome.CreateSliderTrackRow(track, valueLab);
        RegisterRefresh(context, track, () => {
            if (!GodotObject.IsInstanceValid(track))
                return;
            var iv = ReadInt(binding);
            track.Value = iv;
            valueLab.Text = FormatSliderInt(binding, formatter, iv);
        });
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, right);
    }
    private static Control CreateChoice(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding")!;
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var optionsObj = GetProp(entry, "Options");
        var presentation = GetProp(entry, "Presentation");
        var isDropdown = presentation == null || presentation.ToString() == "Dropdown";
        var options = FlattenOptions(contextType, asm, optionsObj);
        if (options.Count == 0)
            throw new InvalidOperationException("choice options");
        if (isDropdown)
            return CreateChoiceDropdown(context, contextType, asm, title, desc, binding, options);
        return CreateChoiceStepper(context, contextType, asm, title, desc, binding, options);
    }
    private static List<(object Value, string Label)> FlattenOptions(Type contextType, Assembly asm,
        object? optionsObj) {
        var list = new List<(object, string)>();
        if (optionsObj is not IEnumerable en)
            return list;
        foreach (var item in en) {
            if (item == null)
                continue;
            var v = item.GetType().GetProperty("Value")?.GetValue(item);
            var labelObj = item.GetType().GetProperty("Label")?.GetValue(item);
            var label = ResolveText(contextType, labelObj, asm);
            list.Add((v ?? "", label));
        }
        return list;
    }
    private static Control CreateChoiceDropdown(object context, Type contextType, Assembly asm, string title,
        string? desc, object binding, List<(object Value, string Label)> options) {
        var ob = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        ob.CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
            DevModeFormChrome.Metrics.ValueColumnMinHeight);
        DevModeFormChrome.ApplyOptionButton(ob);
        var current = CallRead(binding);
        var sel = 0;
        for (var i = 0; i < options.Count; i++) {
            ob.AddItem(options[i].Label);
            if (ValuesEqual(current, options[i].Value))
                sel = i;
        }
        ob.Select(sel);
        ob.ItemSelected += idx => {
            var v = options[(int)idx].Value;
            CallWrite(binding, v);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        RegisterRefresh(context, ob, () => {
            if (!GodotObject.IsInstanceValid(ob))
                return;
            var cur = CallRead(binding);
            for (var i = 0; i < options.Count; i++) {
                if (!ValuesEqual(cur, options[i].Value))
                    continue;
                ob.Select(i);
                return;
            }
        });
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, ob);
    }
    private static Control CreateChoiceStepper(object context, Type contextType, Assembly asm, string title,
        string? desc, object binding, List<(object Value, string Label)> options) {
        var row = new HBoxContainer {
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        row.AddThemeConstantOverride("separation", 8);
        var prev = new Button {
            Text = "‹",
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.MiniIconButtonSize,
                DevModeFormChrome.Metrics.MiniIconButtonSize),
        };
        var next = new Button {
            Text = "›",
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.MiniIconButtonSize,
                DevModeFormChrome.Metrics.MiniIconButtonSize),
        };
        DevModeFormChrome.ApplyMiniIconButton(prev);
        DevModeFormChrome.ApplyMiniIconButton(next);
        var center = new Label {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceCenterMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        center.AddThemeFontSizeOverride("font_size", 14);
        center.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        void SyncDisplay() {
            var cur = CallRead(binding);
            var idx = IndexOfValue(options, cur);
            center.Text = options.Count > 0 ? options[idx].Label : "";
        }
        SyncDisplay();
        void Move(int delta) {
            var cur = CallRead(binding);
            var idx = IndexOfValue(options, cur);
            idx = Mathf.Clamp(idx + delta, 0, Math.Max(0, options.Count - 1));
            CallWrite(binding, options[idx].Value);
            SyncDisplay();
            MarkDirty(context, binding);
            RequestRefresh(context);
        }
        prev.Pressed += () => Move(-1);
        next.Pressed += () => Move(1);
        row.AddChild(prev);
        row.AddChild(center);
        row.AddChild(next);
        RegisterRefresh(context, row, SyncDisplay);
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, row);
    }
    private static int IndexOfValue(List<(object Value, string Label)> options, object? current) {
        for (var i = 0; i < options.Count; i++) {
            if (ValuesEqual(current, options[i].Value))
                return i;
        }
        return 0;
    }
    private static Control CreateColor(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding")!;
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var cp = new ColorPickerButton {
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ColorSwatchSize,
                DevModeFormChrome.Metrics.ColorSwatchSize),
            EditAlpha = true,
        };
        cp.Color = ParseColor(ReadString(binding));
        cp.ColorChanged += c => {
            CallWrite(binding, c.ToHtml(true));
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        RegisterRefresh(context, cp, () => {
            if (!GodotObject.IsInstanceValid(cp))
                return;
            cp.Color = ParseColor(ReadString(binding));
        });
        var wrap = new CenterContainer();
        wrap.CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ColorRowMinWidth,
            DevModeFormChrome.Metrics.ValueColumnMinHeight);
        wrap.AddChild(cp);
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, wrap);
    }
    private static Color ParseColor(string? s) {
        if (string.IsNullOrWhiteSpace(s))
            return Colors.White;
        if (Color.HtmlIsValid(s))
            return new Color(s);
        return Colors.White;
    }
    private static Control CreateStringLine(object context, Type contextType, Assembly asm, object entry,
        bool multiline) {
        var binding = GetProp(entry, "Binding")!;
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        if (multiline) {
            var te = new TextEdit {
                Text = ReadString(binding),
                CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.StringFieldMinWidth,
                    DevModeFormChrome.Metrics.StringMultilineMinHeight),
                WrapMode = TextEdit.LineWrappingMode.Boundary,
            };
            DevModeFormChrome.ApplyTextEdit(te);
            te.TextChanged += () => {
                CallWrite(binding, te.Text);
                MarkDirty(context, binding);
                RequestRefresh(context);
            };
            RegisterRefresh(context, te, () => {
                if (!GodotObject.IsInstanceValid(te))
                    return;
                te.Text = ReadString(binding);
            });
            return DevModeFormChrome.CreateStackedField(title, desc, te);
        }
        var le = new LineEdit {
            Text = ReadString(binding),
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.StringFieldMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        DevModeFormChrome.ApplyLineEdit(le);
        le.TextSubmitted += t => {
            CallWrite(binding, t);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        le.FocusExited += () => {
            CallWrite(binding, le.Text);
            MarkDirty(context, binding);
            RequestRefresh(context);
        };
        RegisterRefresh(context, le, () => {
            if (!GodotObject.IsInstanceValid(le))
                return;
            le.Text = ReadString(binding);
        });
        return DevModeFormChrome.CreateStackedField(title, desc, le);
    }
    private static Control CreateButton(object context, Type contextType, Assembly asm, object entry, bool host) {
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var b = new Button { Text = title };
        b.CustomMinimumSize = new Vector2(200f, DevModeFormChrome.Metrics.ValueColumnMinHeight);
        DevModeFormChrome.ApplyAccentPillButton(b);
        b.Pressed += () => {
            var del = host
                ? GetProp(entry, "Action") ?? GetProp(entry, "HostAction")
                : GetProp(entry, "Action");
            TryInvokeAction(del, context);
            RequestRefresh(context);
        };
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, b);
    }
    private static void TryInvokeAction(object? del, object context) {
        if (del is not Delegate d)
            return;
        try {
            var ps = d.Method.GetParameters();
            if (ps.Length == 0)
                d.DynamicInvoke();
            else if (ps.Length == 1 && ps[0].ParameterType.IsInstanceOfType(context))
                d.DynamicInvoke(context);
            else
                d.DynamicInvoke();
        }
        catch {
            try {
                (del as Action)?.Invoke();
            }
            catch {
                // ignored
            }
        }
    }
    private static Control CreateHeader(object context, Type contextType, Assembly asm, object entry) {
        var t = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var l = new Label {
            Text = t,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        l.AddThemeFontSizeOverride("font_size", 20);
        l.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        RegisterRefresh(context, l, () => {
            if (!GodotObject.IsInstanceValid(l))
                return;
            l.Text = ResolveText(contextType, GetProp(entry, "Label"), asm);
        });
        var wrap = new MarginContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        wrap.AddThemeConstantOverride("margin_top", 4);
        wrap.AddThemeConstantOverride("margin_bottom", 2);
        wrap.AddChild(l);
        return wrap;
    }
    private static Control CreateParagraph(object context, Type contextType, Assembly asm, object entry) {
        var t = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var l = new Label {
            Text = t,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        l.AddThemeFontSizeOverride("font_size", 14);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        var maxH = GetProp(entry, "MaxBodyHeight");
        if (maxH is int ih && ih > 0)
            l.CustomMinimumSize = new Vector2(0f, ih);
        RegisterRefresh(context, l, () => {
            if (!GodotObject.IsInstanceValid(l))
                return;
            l.Text = ResolveText(contextType, GetProp(entry, "Label"), asm);
        });
        return l;
    }
    private static Control CreateImage(object context, Type contextType, Assembly asm, object entry) {
        var tr = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var h = Convert.ToSingle(GetProp(entry, "PreviewHeight") ?? 120f);
        tr.CustomMinimumSize = new Vector2(0f, h);
        void ApplyTex() {
            try {
                var p = entry.GetType().GetProperty("TextureProvider");
                if (p?.GetValue(entry) is Delegate del) {
                    tr.Texture = del.DynamicInvoke() as Texture2D;
                    return;
                }
                var m = entry.GetType().GetMethod("TextureProvider",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                tr.Texture = m?.Invoke(entry, null) as Texture2D;
            }
            catch {
                tr.Texture = null;
            }
        }
        ApplyTex();
        RegisterRefresh(context, tr, ApplyTex);
        return tr;
    }
    private static Control CreateSubpage(object context, Type contextType, Assembly asm, object entry) {
        var binding = GetProp(entry, "Binding");
        var title = ResolveText(contextType, GetProp(entry, "Label"), asm);
        var desc = ResolveTextNullable(contextType, GetProp(entry, "Description"));
        var pageId = ResolveSubpageTargetId(entry);
        var b = new Button { Text = title };
        b.CustomMinimumSize = new Vector2(220f, DevModeFormChrome.Metrics.ValueColumnMinHeight);
        DevModeFormChrome.ApplyAccentPillButton(b);
        b.Pressed += () => {
            NavigateToPage(context, pageId);
            if (binding != null)
                MarkDirty(context, binding);
            RequestRefresh(context);
        };
        return DevModeFormChrome.CreateLabeledValueRow(title, desc, b);
    }
    private static void NavigateToPage(object context, string pageId) {
        if (string.IsNullOrWhiteSpace(pageId))
            return;
        var m = context.GetType().GetMethod("NavigateToPage", BindingFlags.Public | BindingFlags.Instance);
        m?.Invoke(context, new object[] { pageId });
    }
    private static bool ReadBool(object binding) => Convert.ToBoolean(CallRead(binding) ?? false);
    private static double ReadDouble(object binding) => Convert.ToDouble(CallRead(binding) ?? 0d);
    private static float ReadFloat(object binding) => Convert.ToSingle(CallRead(binding) ?? 0f);
    private static int ReadInt(object binding) => Convert.ToInt32(CallRead(binding) ?? 0);
    private static string ReadString(object binding) => CallRead(binding)?.ToString() ?? string.Empty;
    private static object? CallRead(object binding) {
        var m = binding.GetType().GetMethod("Read", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes,
            null);
        return m?.Invoke(binding, null);
    }
    private static void CallWrite(object binding, object? value) {
        foreach (var mi in binding.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
            if (mi.Name != "Write" || mi.GetParameters().Length != 1)
                continue;
            var p = mi.GetParameters()[0].ParameterType;
            object? arg;
            try {
                arg = value == null
                    ? null
                    : p.IsInstanceOfType(value)
                        ? value
                        : Convert.ChangeType(value, p, CultureInfo.InvariantCulture);
            }
            catch {
                arg = value;
            }
            mi.Invoke(binding, new[] { arg });
            return;
        }
    }
    private static void MarkDirty(object context, object binding) {
        context.GetType().GetMethod("MarkDirty", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(context, new[] { binding });
    }
    private static void RequestRefresh(object context) {
        context.GetType().GetMethod("RequestRefresh", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(context, null);
    }
    private static void RegisterRefresh(object context, GodotObject node, Action apply)
        => ModSettingsRitsuEntryReflection.RegisterRefresh(context, node, apply);
    private static string ResolveSubpageTargetId(object entry) {
        foreach (var name in new[] { "TargetPageId", "ChildPageId", "PageId", "NestedPageId" }) {
            if (GetProp(entry, name) is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        foreach (var p in entry.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (p.PropertyType != typeof(string))
                continue;
            if (!p.Name.Contains("Page", StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.GetValue(entry) is string s2 && !string.IsNullOrWhiteSpace(s2))
                return s2;
        }
        return "";
    }
    private static object? GetProp(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
    private static string ResolveText(Type contextType, object? textObj, Assembly asm) {
        if (textObj == null)
            return ResolveLocalized(asm, "entry.label.empty", "—");
        foreach (var m in contextType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
            if (m.Name != "Resolve")
                continue;
            var ps = m.GetParameters();
            if (ps.Length == 0 || !ps[0].ParameterType.IsInstanceOfType(textObj))
                continue;
            try {
                var args = ps.Length >= 2 ? new[] { textObj, string.Empty } : new[] { textObj };
                var s = m.Invoke(null, args) as string;
                return string.IsNullOrWhiteSpace(s) ? ResolveLocalized(asm, "entry.label.empty", "—") : s;
            }
            catch {
                return "—";
            }
        }
        return "—";
    }
    private static string? ResolveTextNullable(Type contextType, object? textObj) {
        if (textObj == null)
            return null;
        foreach (var m in contextType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
            if (m.Name != "Resolve")
                continue;
            var ps = m.GetParameters();
            if (ps.Length == 0 || !ps[0].ParameterType.IsInstanceOfType(textObj))
                continue;
            try {
                var args = ps.Length >= 2 ? new[] { textObj, string.Empty } : new[] { textObj };
                return m.Invoke(null, args) as string;
            }
            catch {
                return null;
            }
        }
        return null;
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
    private static bool ValuesEqual(object? a, object? b) {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;
        if (a.Equals(b))
            return true;
        try {
            return Convert.ChangeType(a, b.GetType(), CultureInfo.InvariantCulture).Equals(b);
        }
        catch {
            return false;
        }
    }
    private static string FormatSlider(object? _, object? formatter, double v) {
        if (formatter is Delegate d) {
            try {
                var r = d.DynamicInvoke(v);
                if (r != null)
                    return r.ToString() ?? v.ToString("0.##", CultureInfo.InvariantCulture);
            }
            catch {
                // ignored
            }
        }
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }
    private static string FormatSliderFloat(object? _, object? formatter, float v) {
        if (formatter is Delegate d) {
            try {
                var r = d.DynamicInvoke(v);
                if (r != null)
                    return r.ToString() ?? v.ToString("0.##", CultureInfo.InvariantCulture);
            }
            catch {
                // ignored
            }
        }
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }
    private static string FormatSliderInt(object? _, object? formatter, int v) {
        if (formatter is Delegate d) {
            try {
                var r = d.DynamicInvoke(v);
                if (r != null)
                    return r.ToString() ?? v.ToString(CultureInfo.InvariantCulture);
            }
            catch {
                // ignored
            }
        }
        return v.ToString(CultureInfo.InvariantCulture);
    }
}
