using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace KitLib.UI;

/// <summary>
/// Geometry and motion shared by the two left-edge rails in <c>KitLib.Hud</c>: the in-run rail
/// (DevPanelUI) and the main-menu rail. The rails differ in width
/// (icon-only in run, icon + tool name on the main menu) but share the same widened black left contour
/// band, column offset and spring, so the chrome lines up and moves alike across screens.
/// </summary>
internal static class KitLibRailChrome {
    /// <summary>Left padding inside the left contour band, measured from the screen edge (x=0). The band
    /// itself is the widened black left border of the screen frame and hosts the rail column.</summary>
    internal const float RailLeft = 24f;
    /// <summary>Rail row height; the drawable icon is smaller (see the Hud main-menu rail).</summary>
    internal const float IconButtonSize = 36f;
    /// <summary>Left rail chrome — above KitLib browser panels, below tool modals.</summary>
    internal const int ChromeZIndex = 1260;

    /// <summary>Spring stiffness shared by the rails' motion, in rad/s.</summary>
    internal const float SpringOmega = 15.7f;
    /// <summary>Duration of a rail spring, in seconds.</summary>
    internal const float SpringSeconds = 0.45f;
    /// <summary>Damping ratio of a rail spring; below 1 gives a single light overshoot.</summary>
    internal const float SpringZeta = 0.72f;

    /// <summary>Underdamped spring easing: one light overshoot, then a settle.</summary>
    /// <remarks>
    /// <c>y(t) = 1 - e^(-ζωt) · (cos(ωd·t) + (ζ/√(1-ζ²)) · sin(ωd·t))</c>, with <c>ωd = ω√(1-ζ²)</c>.
    /// </remarks>
    internal static float SpringEase(float seconds, float zeta) {
        float sqrtTerm = Mathf.Sqrt(1f - zeta * zeta);
        float dampedOmega = SpringOmega * sqrtTerm;
        return 1f - Mathf.Exp(-zeta * SpringOmega * seconds) *
               (Mathf.Cos(dampedOmega * seconds) + (zeta / sqrtTerm) * Mathf.Sin(dampedOmega * seconds));
    }
}

/// <summary>
/// <para xml:lang="en">Eased 0..1 progress integrated per frame on a node's scene tree. Scene loads arrive as one very long frame, which a tween would swallow in a single step, so stalled frames are skipped and the slide stays smooth.</para>
/// <para xml:lang="zh-CN">逐帧积分的 0..1 缓动进度。场景加载会带来极长的单帧，补间会一步吃掉整段动画，因此这里跳过卡顿帧，保证划入划出平滑。</para>
/// </summary>
internal sealed class KitLibSlide {
    /// <summary>Frames longer than this are scene-load stalls rather than animation.</summary>
    private const float StallSeconds = 1f / 20f;

    private readonly Action<float> _apply;
    private readonly float _seconds;
    private SceneTree? _tree;
    private float _from;
    private float _target = 1f;
    private float _elapsed;

    internal KitLibSlide(float value, Action<float> apply, float seconds) {
        _from = _target = value;
        _apply = apply;
        _seconds = seconds;
    }

    /// <summary>Current progress, 0 off screen to 1 docked.</summary>
    internal float Value { get; private set; }

    /// <summary>Slide to <paramref name="target"/>, or jump there when animation is off.</summary>
    internal void To(Node? host, float target, bool animated) {
        // Already heading there: a periodic re-sync must not restart the ease.
        if (_tree != null && Mathf.IsEqualApprox(_target, target))
            return;

        if (!animated || host == null || !GodotObject.IsInstanceValid(host)) {
            Snap(target);
            return;
        }

        if (_tree == null) {
            var tree = host.GetTree();
            if (tree == null) {
                Snap(target);
                return;
            }
            _tree = tree;
            tree.ProcessFrame += Step;
        }

        _from = Value;
        _target = target;
        _elapsed = 0f;
    }

    /// <summary>Jump to <paramref name="value"/> and report it once.</summary>
    internal void Snap(float value) {
        Stop();
        Value = _from = _target = value;
        _apply(value);
    }

    /// <summary>Stop any running slide without reporting a new value.</summary>
    internal void Stop() {
        _elapsed = 0f;
        _from = _target = Value;
        if (_tree == null)
            return;
        _tree.ProcessFrame -= Step;
        _tree = null;
    }

    private void Step() {
        float delta = _tree != null && _tree.Root != null
            ? (float)_tree.Root.GetProcessDeltaTime()
            : 0f;
        // A scene-load stall is not animation; hold the current position instead of jumping.
        if (delta <= 0f || delta > StallSeconds)
            return;

        _elapsed += delta;
        float t = Mathf.Min(_elapsed / _seconds, 1f);
        Value = Mathf.Lerp(_from, _target, 1f - Mathf.Pow(1f - t, 3f));
        _apply(Value);

        if (t < 1f)
            return;
        float target = _target;
        Stop();
        Value = target;
        _apply(target);
    }
}

/// <summary>
/// <para xml:lang="en">Runs a callback once the game's screen transition has finished. A run scene is built behind a fade, so anything animated at attach time would play where nobody can see it.</para>
/// <para xml:lang="zh-CN">等游戏屏幕过渡结束后再执行回调。局内场景是在淡入过渡后面建好的，挂载时立刻播的动画会被过渡盖住。</para>
/// </summary>
internal sealed class KitLibRevealWait {
    private SceneTree? _tree;
    private Action? _done;

    internal void Begin(Node host, Action done) {
        Cancel();
        var tree = host.GetTree();
        var transition = NGame.Instance?.Transition;
        if (tree == null || transition == null || !GodotObject.IsInstanceValid(transition)
            || !transition.InTransition) {
            done();
            return;
        }

        _tree = tree;
        _done = done;
        tree.ProcessFrame += Step;
    }

    internal void Cancel() {
        if (_tree == null)
            return;
        _tree.ProcessFrame -= Step;
        _tree = null;
        _done = null;
    }

    private void Step() {
        var transition = NGame.Instance?.Transition;
        if (transition != null && GodotObject.IsInstanceValid(transition) && transition.InTransition)
            return;

        var done = _done;
        Cancel();
        done?.Invoke();
    }
}
