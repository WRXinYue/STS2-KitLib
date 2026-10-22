using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.UI;

/// <summary>
/// <para xml:lang="en">Bridges the game's runtime mod-detected event into <see cref="KitLibNotificationCenter"/>,
/// so a mod appearing mid-session announces itself wherever notifications are shown.</para>
/// <para xml:lang="zh-CN">把游戏的运行期"检测到模组"事件桥接到 <see cref="KitLibNotificationCenter"/>，
/// 让会话中途出现的模组在通知出现处自动播报。</para>
/// </summary>
/// <remarks>
/// <para xml:lang="en">Subscribed after KitLib's own initializer, so the startup mod scan has already run and only
/// mods detected later arrive here.</para>
/// <para xml:lang="zh-CN">订阅发生在 KitLib 自身初始化之后，此时开机扫描已完成，这里只会收到之后新检测到的模组。</para>
/// </remarks>
internal static class KitLibModNotificationBridge {
    private static bool _subscribed;

    /// <summary>
    /// <para xml:lang="en">Subscribe to <c>ModManager.OnModDetected</c>; idempotent.</para>
    /// <para xml:lang="zh-CN">订阅 <c>ModManager.OnModDetected</c>；幂等。</para>
    /// </summary>
    internal static void Initialize() {
        if (_subscribed)
            return;

        _subscribed = true;
        ModManager.OnModDetected += OnModDetected;
    }

    private static void OnModDetected(Mod mod) {
        var name = ResolveName(mod);
        // Detection can fire off the main thread; defer so the tab's nodes are touched on the main thread only.
        Callable.From(() => KitLibNotificationCenter.Raise(new KitLibNotification(
            I18N.T("kitlib.notify.modDetected", "New mod detected: {0}", name)))).CallDeferred();
    }

    /// <summary>Manifest name, falling back to the manifest id.</summary>
    private static string ResolveName(Mod? mod) {
        var manifest = mod?.manifest;
        if (manifest == null)
            return "";

        string? name = manifest.name;
        if (string.IsNullOrWhiteSpace(name))
            name = manifest.id;
        return name ?? "";
    }
}
