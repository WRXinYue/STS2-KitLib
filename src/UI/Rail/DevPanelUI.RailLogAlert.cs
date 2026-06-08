using KitLib.Icons;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private const string LogsTabId = "devmode.logs";

    private static readonly Color LogAlertErrorBright = new(1f, 0.37f, 0.37f);
    private static readonly Color LogAlertErrorDim = new(1f, 0.37f, 0.37f, 0.35f);
    private static readonly Color LogAlertWarnBright = new(1f, 0.78f, 0.25f);
    private static readonly Color LogAlertWarnDim = new(1f, 0.78f, 0.25f, 0.35f);

    private static Tween? _logsAlertBlinkTween;
    private static LogLevel? _logsAlertBlinkLevel;

    internal static void RefreshLogAlertHints() {
        var severity = LogCollector.UnseenAlertSeverity;

        if (severity == null || IsPeekTabVisible) {
            StopLogsButtonAlertBlink();
            return;
        }

        var btn = FindLogsRailButton();
        if (btn == null || !GodotObject.IsInstanceValid(btn)) {
            StopLogsButtonAlertBlink();
            return;
        }

        if (_logsAlertBlinkTween != null
            && GodotObject.IsInstanceValid(_logsAlertBlinkTween)
            && _logsAlertBlinkLevel == severity)
            return;

        StopLogsButtonAlertBlink();
        StartLogsButtonAlertBlink(btn, severity.Value);
    }

    internal static void StopLogAlertBlink() => StopLogsButtonAlertBlink();

    private static void StopLogsButtonAlertBlink() {
        _logsAlertBlinkTween?.Kill();
        _logsAlertBlinkTween = null;
        _logsAlertBlinkLevel = null;

        var btn = FindLogsRailButton();
        if (btn != null && GodotObject.IsInstanceValid(btn))
            btn.Modulate = Colors.White;

        RefreshRailIconTints();
    }

    private static bool IsLogAlertBlinking(Button btn)
        => _logsAlertBlinkTween != null
           && GodotObject.IsInstanceValid(_logsAlertBlinkTween)
           && FindLogsRailButton() == btn;

    private static Button? FindLogsRailButton() {
        foreach (var btn in _railButtons) {
            if (GodotObject.IsInstanceValid(btn) && btn.GetMeta("tab_id").AsString() == LogsTabId)
                return btn;
        }
        return null;
    }

    private static void StartLogsButtonAlertBlink(Button btn, LogLevel severity) {
        bool isError = severity >= LogLevel.Error;
        var bright = isError ? LogAlertErrorBright : LogAlertWarnBright;
        var dim = isError ? LogAlertErrorDim : LogAlertWarnDim;
        float halfCycle = isError ? 0.35f : 0.65f;

        _logsAlertBlinkLevel = severity;
        btn.Modulate = Colors.White;
        btn.Icon = MdiIcon.TextBoxOutline.Texture(20, dim);

        _logsAlertBlinkTween = btn.CreateTween();
        _logsAlertBlinkTween.SetLoops();
        _logsAlertBlinkTween.SetTrans(Tween.TransitionType.Sine);
        _logsAlertBlinkTween.SetEase(Tween.EaseType.InOut);

        _logsAlertBlinkTween.TweenMethod(Callable.From((float t) => {
            if (!GodotObject.IsInstanceValid(btn))
                return;
            btn.Icon = MdiIcon.TextBoxOutline.Texture(20, dim.Lerp(bright, t));
        }), 0f, 1f, halfCycle);
        _logsAlertBlinkTween.Parallel()
            .TweenProperty(btn, "modulate:a", 0.5f, halfCycle);

        _logsAlertBlinkTween.TweenMethod(Callable.From((float t) => {
            if (!GodotObject.IsInstanceValid(btn))
                return;
            btn.Icon = MdiIcon.TextBoxOutline.Texture(20, bright.Lerp(dim, t));
        }), 0f, 1f, halfCycle);
        _logsAlertBlinkTween.Parallel()
            .TweenProperty(btn, "modulate:a", 1f, halfCycle);
    }
}
