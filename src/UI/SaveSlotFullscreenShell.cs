using System;
using Godot;

namespace DevMode.UI;

/// <summary>Fullscreen dim + centered slot picker. Hosts a single <see cref="SaveSlotPanel"/> — layout is not duplicated.</summary>
internal sealed partial class SaveSlotFullscreenShell : Control, ISaveSlotDialogRoot {
    private ColorRect? _bg;
    private SaveSlotPanel? _panel;

    internal SaveSlotFullscreenShell(
        bool saveMode,
        Action<int> onConfirm,
        Action? onEmbeddedCancel,
        Action? onEmbeddedAfterLoadClose) {
        ZIndex = 200;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = Control.MouseFilterEnum.Stop;

        _bg = new ColorRect { Color = SaveSlotPanel.OverlayScrimColor() };
        _bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        _panel = new SaveSlotPanel(saveMode, onConfirm, embedded: false, onEmbeddedCancel, onEmbeddedAfterLoadClose);
        center.AddChild(_panel);
    }

    public override void _Ready() {
        if (_bg == null || _panel == null) return;

        ApplyViewportMinHeight();
        GetViewport().SizeChanged += OnViewportSizeChanged;

        _bg.Color = SaveSlotPanel.OverlayScrimColor() with { A = 0f };
        var bgTween = CreateTween();
        bgTween.TweenProperty(_bg, "color", SaveSlotPanel.OverlayScrimColor(), 0.18f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        _panel.Scale = new Vector2(0.93f, 0.93f);
        _panel.Modulate = new Color(1, 1, 1, 0f);
        var panelTween = CreateTween().SetParallel();
        panelTween.TweenProperty(_panel, "scale", Vector2.One, 0.18f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        panelTween.TweenProperty(_panel, "modulate", Colors.White, 0.14f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    public override void _ExitTree() {
        var vp = GetViewport();
        if (vp != null)
            vp.SizeChanged -= OnViewportSizeChanged;
    }

    public void HideFromFacade() {
        if (!GodotObject.IsInstanceValid(this)) return;

        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0f), 0.12f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        if (_panel != null)
            tween.TweenProperty(_panel, "scale", new Vector2(0.95f, 0.95f), 0.12f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

        tween.Chain().TweenCallback(Callable.From(() => {
            if (GodotObject.IsInstanceValid(this))
                QueueFree();
        }));
    }

    void OnViewportSizeChanged() => ApplyViewportMinHeight();

    void ApplyViewportMinHeight() {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel)) return;
        float vh = GetViewport().GetVisibleRect().Size.Y;
        float h = Mathf.Clamp(vh * 0.76f, 480f, 920f);
        _panel.CustomMinimumSize = new Vector2(860f, h);
    }
}
