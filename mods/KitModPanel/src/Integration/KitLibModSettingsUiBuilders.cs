using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.UI;

namespace KitLib.Integration;

/// <summary>KitLib-native settings row builders exposed via <see cref="KitLibModSettingsUiApi"/>.</summary>
internal static class KitLibModSettingsUiBuilders {
    static readonly List<(Func<bool> Get, CheckBox Box)> LiveBoolToggles = [];

    internal static void WireApi() {
        KitLibModSettingsUiApi.CreatePageStack = CreatePageStack;
        KitLibModSettingsUiApi.CreateSectionHeader = CreateSectionHeader;
        KitLibModSettingsUiApi.CreateBoolToggle = CreateBoolToggle;
        KitLibModSettingsUiApi.CreateChoiceRow = CreateChoiceRow;
        KitLibModSettingsUiApi.CreateIntSlider = CreateIntSlider;
        KitLibModSettingsUiApi.CreateFloatSlider = CreateFloatSlider;
        KitLibModSettingsUiApi.CreateStringField = CreateStringField;
        KitLibModSettingsUiApi.CreateColorRow = CreateColorRow;
        KitLibModSettingsUiApi.CreateActionButton = CreateActionButton;
        KitLibModSettingsUiApi.RefreshBoolToggles = RefreshBoolToggles;
    }

    internal static void RefreshBoolToggles() {
        for (var i = LiveBoolToggles.Count - 1; i >= 0; i--) {
            var (get, box) = LiveBoolToggles[i];
            if (!GodotObject.IsInstanceValid(box)) {
                LiveBoolToggles.RemoveAt(i);
                continue;
            }

            box.SetPressedNoSignal(get());
        }
    }

    internal static Control CreatePageStack() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);
        return stack;
    }

    internal static Control CreateSectionHeader(string title, string? description) {
        var col = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddThemeConstantOverride("separation", 4);
        col.AddChild(DevModeFormChrome.CreateTitleLabel(string.IsNullOrWhiteSpace(title) ? "—" : title));
        if (!string.IsNullOrWhiteSpace(description))
            col.AddChild(DevModeFormChrome.CreateDescriptionLabel(description));
        return col;
    }

    internal static Control CreateBoolToggle(string title, string? description, Func<bool> get, Action<bool> set) {
        var cb = new CheckBox {
            ButtonPressed = get(),
            FocusMode = Control.FocusModeEnum.All,
        };
        DevModeFormChrome.ApplyToggle(cb);
        cb.Toggled += on => set(on);
        LiveBoolToggles.Add((get, cb));
        cb.TreeExiting += () => LiveBoolToggles.RemoveAll(entry => entry.Box == cb);
        return DevModeFormChrome.CreateLabeledValueRow(title, description, cb);
    }

    internal static Control CreateChoiceRow(
        string title,
        string? description,
        IReadOnlyList<KitLibModSettingsChoice> options,
        Func<int> getId,
        Action<int> setId) {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
            throw new ArgumentException("Choice options must not be empty.", nameof(options));

        var ob = new OptionButton {
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        DevModeFormChrome.ApplyOptionButton(ob);
        var selected = getId();
        var selectIndex = 0;
        for (var i = 0; i < options.Count; i++) {
            var opt = options[i];
            ob.AddItem(opt.Label, opt.Id);
            if (opt.Id == selected)
                selectIndex = i;
        }
        ob.Selected = selectIndex;
        ob.ItemSelected += idx => setId((int)ob.GetItemId((int)idx));
        return DevModeFormChrome.CreateLabeledValueRow(title, description, ob);
    }

    internal static Control CreateIntSlider(
        string title,
        string? description,
        Func<int> get,
        Action<int> set,
        int min,
        int max,
        int step) {
        if (step <= 0)
            step = 1;
        var track = DevModeFormChrome.WithSliderStyle(new HSlider {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.SliderTrackMinWidth, 22f),
            Value = get(),
        });
        var valueLab = DevModeFormChrome.CreateSliderValueCaption();
        valueLab.Text = ((int)Math.Round(track.Value)).ToString(CultureInfo.InvariantCulture);
        track.ValueChanged += v => {
            var iv = (int)Math.Round(v);
            set(iv);
            valueLab.Text = iv.ToString(CultureInfo.InvariantCulture);
        };
        return DevModeFormChrome.CreateLabeledValueRow(title, description,
            DevModeFormChrome.CreateSliderTrackRow(track, valueLab));
    }

    internal static Control CreateFloatSlider(
        string title,
        string? description,
        Func<float> get,
        Action<float> set,
        float min,
        float max,
        float step) {
        if (step <= 0)
            step = 0.01f;
        var track = DevModeFormChrome.WithSliderStyle(new HSlider {
            MinValue = min,
            MaxValue = max,
            Step = step,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.SliderTrackMinWidth, 22f),
            Value = get(),
        });
        var valueLab = DevModeFormChrome.CreateSliderValueCaption();
        valueLab.Text = ((float)track.Value).ToString("0.###", CultureInfo.InvariantCulture);
        track.ValueChanged += v => {
            var fv = (float)v;
            set(fv);
            valueLab.Text = fv.ToString("0.###", CultureInfo.InvariantCulture);
        };
        return DevModeFormChrome.CreateLabeledValueRow(title, description,
            DevModeFormChrome.CreateSliderTrackRow(track, valueLab));
    }

    internal static Control CreateStringField(
        string title,
        string? description,
        Func<string> get,
        Action<string> set,
        bool multiline) {
        if (multiline) {
            var te = new TextEdit {
                Text = get() ?? "",
                CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.StringFieldMinWidth,
                    DevModeFormChrome.Metrics.StringMultilineMinHeight),
                WrapMode = TextEdit.LineWrappingMode.Boundary,
                FocusMode = Control.FocusModeEnum.All,
            };
            DevModeFormChrome.ApplyTextEdit(te);
            te.FocusExited += () => set(te.Text);
            return DevModeFormChrome.CreateStackedField(title, description, te);
        }

        var le = new LineEdit {
            Text = get() ?? "",
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.StringFieldMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
            FocusMode = Control.FocusModeEnum.All,
        };
        DevModeFormChrome.ApplyLineEdit(le);
        le.TextChanged += text => set(text);
        return DevModeFormChrome.CreateLabeledValueRow(title, description, le);
    }

    internal static Control CreateColorRow(
        string title,
        string? description,
        Func<KitLibModSettingsRgb> get,
        Action<KitLibModSettingsRgb> set) {
        var rgb = get();
        var cp = new ColorPickerButton {
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ColorSwatchSize,
                DevModeFormChrome.Metrics.ColorSwatchSize),
            EditAlpha = false,
            Color = new Color(rgb.R, rgb.G, rgb.B),
            FocusMode = Control.FocusModeEnum.All,
        };
        cp.ColorChanged += c => set(new KitLibModSettingsRgb(c.R, c.G, c.B));
        return DevModeFormChrome.CreateLabeledValueRow(title, description, cp);
    }

    internal static Control CreateActionButton(string title, string? description, Action onPressed) {
        ArgumentNullException.ThrowIfNull(onPressed);
        var b = new Button {
            Text = string.IsNullOrWhiteSpace(title) ? "—" : title,
            FocusMode = Control.FocusModeEnum.All,
        };
        DevModeFormChrome.ApplyAccentPillButton(b);
        b.Pressed += onPressed;
        if (string.IsNullOrWhiteSpace(description))
            return b;
        return DevModeFormChrome.CreateLabeledValueRow(title, description, b);
    }
}
