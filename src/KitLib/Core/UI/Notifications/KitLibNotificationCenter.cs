using System;
using Godot;

namespace KitLib.UI;

/// <summary>
/// <para xml:lang="en">Process-wide notification channel for KitLib chrome. Raising is always safe: with no
/// subscriber the notification is dropped, which is how a channel that only the main menu listens to stays
/// menu-only.</para>
/// <para xml:lang="zh-CN">KitLib 界面的全局通知通道。发送总是安全的：无订阅者时直接丢弃，因此"只有主菜单监听"
/// 即天然限定在主菜单。</para>
/// </summary>
internal static class KitLibNotificationCenter {
    /// <summary>
    /// <para xml:lang="en">Fired for every raised notification; subscribers are expected to be hosts that are
    /// currently attached.</para>
    /// <para xml:lang="zh-CN">每次发送通知时触发；订阅方应为当前已挂载的宿主。</para>
    /// </summary>
    internal static event Action<KitLibNotification>? Raised;

    /// <summary>
    /// <para xml:lang="en">Broadcast one notification to every current subscriber.</para>
    /// <para xml:lang="zh-CN">向当前所有订阅者广播一条通知。</para>
    /// </summary>
    internal static void Raise(KitLibNotification notification) => Raised?.Invoke(notification);
}

/// <summary>
/// <para xml:lang="en">One notification: a short message plus an optional icon, which falls back to a theme
/// accent dot when null.</para>
/// <para xml:lang="zh-CN">一条通知：一段短文案加可选图标；图标为 null 时改用主题色圆点。</para>
/// </summary>
internal readonly record struct KitLibNotification(string Message, Texture2D? Icon = null);
