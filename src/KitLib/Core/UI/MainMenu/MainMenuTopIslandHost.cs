using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// <para xml:lang="en">Main-menu host for the top notification island. Subscribes to
/// <see cref="KitLibNotificationCenter"/> while the menu is up, reveals <see cref="KitLibTopIslandChrome"/> for
/// one notification, then retracts it after a short time to live.</para>
/// <para xml:lang="zh-CN">顶部通知岛的主菜单宿主。菜单存在期间订阅 <see cref="KitLibNotificationCenter"/>，
/// 收到通知时展开 <see cref="KitLibTopIslandChrome"/>，存活时间到后自动收起。</para>
/// </summary>
/// <remarks>
/// <para xml:lang="en">The island lives on the screen frame's layer rather than under the menu, so the menu's
/// own exit is the only teardown path.</para>
/// <para xml:lang="zh-CN">通知岛位于窗口边框图层而非菜单树下，因此菜单自身的退场是唯一的拆除路径。</para>
/// </remarks>
internal static class MainMenuTopIslandHost {
    /// <summary><para xml:lang="en">How long one notification stays up before it retracts.</para>
    /// <para xml:lang="zh-CN">单条通知显示多久后自动收起。</para></summary>
    internal const float TtlSeconds = 3f;

    private const string TimerNodeName = "KitLibTopIslandTtl";

    private static NMainMenu? _menu;
    private static KitLibTopIsland? _island;
    private static Godot.Timer? _timer;
    private static Tween? _tween;
    private static float _presence;

    /// <summary>Whether the island is currently hosted by <paramref name="mainMenu"/>.</summary>
    internal static bool IsAttachedTo(NMainMenu mainMenu) =>
        _menu == mainMenu && _island != null && GodotObject.IsInstanceValid(_island.Root);

    /// <summary>Attach the island to the main menu; an existing attachment is left alone.</summary>
    public static void EnsureAttached(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        if (IsAttachedTo(mainMenu))
            return;

        Detach();
        _menu = mainMenu;
        _island = KitLibScreenFrame.CreateTopIsland();
        _island.Root.AddChild(CreateTtlTimer());
        // Registered so a Ctrl+B wrap retract carries the island out and back in with the window edge.
        KitLibScreenFrame.AddWrapListener(ApplyWrap);
        KitLibNotificationCenter.Raised += OnNotificationRaised;
        mainMenu.TreeExiting += Detach;
        MainFile.Logger.Info("KitLib main-menu top island attached.");
    }

    /// <summary>Attach to the currently loaded main menu, if any.</summary>
    public static void SyncActiveMainMenu() {
        try {
            var mainMenu = MegaCrit.Sts2.Core.Nodes.NGame.Instance?.MainMenu;
            if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu))
                EnsureAttached(mainMenu);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu top island sync failed: {ex.Message}");
        }
    }

    private static Godot.Timer CreateTtlTimer() {
        var timer = new Godot.Timer {
            Name = TimerNodeName,
            WaitTime = TtlSeconds,
            OneShot = true,
        };
        timer.Timeout += Retract;
        _timer = timer;
        return timer;
    }

    private static void Detach() => ResetStatics();

    private static void ResetStatics() {
        KitLibNotificationCenter.Raised -= OnNotificationRaised;
        KitLibScreenFrame.RemoveWrapListener(ApplyWrap);

        var menu = _menu;
        if (menu != null && GodotObject.IsInstanceValid(menu))
            menu.TreeExiting -= Detach;

        KillTween();
        if (_timer != null && GodotObject.IsInstanceValid(_timer))
            _timer.QueueFree();
        _timer = null;

        var island = _island;
        _island = null;
        _presence = 0f;
        _menu = null;
        if (island != null)
            KitLibScreenFrame.RemoveTopIsland(island);
    }

    /// <summary>One notification at a time: it replaces the current content and restarts the time to live.</summary>
    private static void OnNotificationRaised(KitLibNotification notification) {
        var island = _island;
        if (island == null)
            return;

        KitLibTopIslandChrome.SetContent(island, notification);
        island.Root.Visible = true;
        Reveal();
        RestartTtl();
    }

    private static void Reveal() {
        KillTween();

        var island = _island;
        if (island == null)
            return;
        if (!island.Root.IsInsideTree()) {
            SetPresenceProgress(1f);
            return;
        }

        float from = _presence;
        var tween = island.Root.CreateTween();
        // The tween only drives 0..1 progress; SpringEase() shapes it into the island's light overshoot.
        tween.TweenMethod(Callable.From<float>(p => SetPresenceProgress(Mathf.Lerp(from, 1f,
                KitLibRailChrome.SpringEase(p * KitLibRailChrome.SpringSeconds, KitLibRailChrome.SpringZeta)))),
                0f, 1f, KitLibRailChrome.SpringSeconds)
            .SetTrans(Tween.TransitionType.Linear);
        // A spring's last step can land off target; snap so the tab settles cleanly.
        tween.Finished += () => SetPresenceProgress(1f);
        _tween = tween;
    }

    private static void Retract() {
        KillTween();

        var island = _island;
        if (island == null)
            return;
        if (!island.Root.IsInsideTree()) {
            SetPresenceProgress(0f);
            island.Root.Visible = false;
            return;
        }

        float from = _presence;
        var tween = island.Root.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => SetPresenceProgress(Mathf.Lerp(from, 0f, p))), 0f, 1f,
                KitLibScreenFrame.AnimationSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Finished += () => {
            SetPresenceProgress(0f);
            if (_island != null)
                _island.Root.Visible = false;
        };
        _tween = tween;
    }

    private static void RestartTtl() {
        var timer = _timer;
        if (timer == null || !GodotObject.IsInstanceValid(timer))
            return;

        timer.Stop();
        timer.Start();
    }

    /// <summary>
    /// <para xml:lang="en">Take the island with the window edge: its own presence, capped by the frame's wrap, so
    /// Ctrl+B hides it and restoring the wrap regrows it while the notification is still live.</para>
    /// <para xml:lang="zh-CN">通知岛随窗口边一起进退：自身展开度受边框 wrap 封顶，故 Ctrl+B 会一并隐藏；
    /// 恢复 wrap 时若仍在存活时间内则重新长出。</para>
    /// </summary>
    private static void ApplyWrap() {
        var island = _island;
        if (island == null)
            return;

        KitLibTopIslandChrome.SetPresence(island, Mathf.Min(_presence, KitLibScreenFrame.WrapT));
    }

    private static void SetPresenceProgress(float presence) {
        _presence = presence;
        ApplyWrap();
    }

    private static void KillTween() {
        if (_tween != null && GodotObject.IsInstanceValid(_tween))
            _tween.Kill();
        _tween = null;
    }
}
