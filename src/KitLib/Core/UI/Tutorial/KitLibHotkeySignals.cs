using System;

namespace KitLib.UI;

/// <summary>
/// <para xml:lang="en">Process-wide "a KitLib shortcut just ran" signal. Modules that own the bindings report here,
/// so Core-side listeners (the main-menu top island) can react without referencing those modules.</para>
/// <para xml:lang="zh-CN">全局的"某个 KitLib 快捷键刚被执行"信号。持有绑定的模块在此上报，Core 侧监听方
/// （主菜单通知岛）因此无需引用这些模块。</para>
/// </summary>
internal static class KitLibHotkeySignals {
    /// <summary>
    /// <para xml:lang="en">Fired with the <see cref="KitLib.Settings.HotkeyActionId"/> of the action that ran.</para>
    /// <para xml:lang="zh-CN">携带刚执行动作的 <see cref="KitLib.Settings.HotkeyActionId"/> 触发。</para>
    /// </summary>
    internal static event Action<string>? Used;

    internal static void ReportUsed(string actionId) => Used?.Invoke(actionId);
}
