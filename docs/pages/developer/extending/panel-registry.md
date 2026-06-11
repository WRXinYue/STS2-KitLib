---
title:
  en: Dev panel registry
  zh-CN: 开发者面板注册
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
`DevPanelRegistry` lets other mods add tabs to the DevMode vertical rail. Each tab gets an icon, a display name, an order value, and two callbacks — one when the tab is opened (`onActivate`) and one when it is closed (`onDeactivate`).

The full flow is:

1. Your `[ModInitializer]` calls `DevPanelRegistry.RegisterPanelWhenReady(callback)`.
2. DevMode queues the callback and runs it after every mod initializer has finished (before `LocManager.Initialize`).
3. Inside the callback, call `DevPanelRegistry.Register(...)` to add the tab.
4. When the user clicks your tab, `onActivate` is called with the current `NGlobalUi`.
5. Inside `onActivate`, build a Godot `Control` tree and add it to `globalUi`.
6. When the user switches away, `onDeactivate` fires — remove your root node.
:::

::: zh-CN
`DevPanelRegistry` 允许其他 mod 向 DevMode 竖向轨道添加标签页。每个标签页有图标、显示名称、排序值，以及两个回调——标签被打开时（`onActivate`）和关闭时（`onDeactivate`）各触发一次。

完整流程如下：

1. 你的 `[ModInitializer]` 调用 `DevPanelRegistry.RegisterPanelWhenReady(callback)`。
2. DevMode 将回调入队，在所有 mod 初始化器执行完毕后统一触发（在 `LocManager.Initialize` 之前）。
3. 在回调内调用 `DevPanelRegistry.Register(...)` 完成标签注册。
4. 用户点击你的标签时，`onActivate` 被调用，参数为当前 `NGlobalUi`。
5. 在 `onActivate` 内构建 Godot `Control` 树并挂载到 `globalUi`。
6. 用户切换到其他标签时，`onDeactivate` 触发——移除你的根节点。
:::

## File organization{lang="en"}

## 文件组织{lang="zh-CN"}

::: en
Keep DevMode integration in a dedicated file, isolated with a compile-time symbol. This way your mod loads normally even when DevMode is not installed.

```
src/
├── Main.cs                         # [ModInitializer] — calls RegisterPanelWhenReady
└── Integrations/
    └── DevMode/
        └── DevModeRegistration.cs  # panel logic, guarded by #if YOUR_MOD_DEVMODE
```
:::

::: zh-CN
将 DevMode 集成代码放在专属文件中，并用编译期符号隔离。这样即使未安装 DevMode，你的 mod 也能正常加载。

```
src/
├── Main.cs                         # [ModInitializer] — 调用 RegisterPanelWhenReady
└── Integrations/
    └── DevMode/
        └── DevModeRegistration.cs  # 面板逻辑，用 #if YOUR_MOD_DEVMODE 隔离
```
:::

## Registration{lang="en"}

## 注册{lang="zh-CN"}

::: en
In your `[ModInitializer]`, call `RegisterPanelWhenReady` with a method reference:

```csharp
// Main.cs
using HarmonyLib;
#if YOUR_MOD_DEVMODE
using DevMode.UI;
using YourMod.Integrations.DevMode;
#endif
using MegaCrit.Sts2.Core.Modding;

[ModInitializer(nameof(Initialize))]
public class Main
{
    public static void Initialize()
    {
        // ... your normal mod setup ...

#if YOUR_MOD_DEVMODE
        DevPanelRegistry.RegisterPanelWhenReady(DevModeRegistration.TryRegister);
#endif
    }
}
```

Wrap the actual `Register` call in a try/catch so a failure logs a warning instead of crashing the mod:

```csharp
// Integrations/DevMode/DevModeRegistration.cs
#if YOUR_MOD_DEVMODE
using DevMode.Icons;
using DevMode.UI;

namespace YourMod.Integrations.DevMode;

internal static class DevModeRegistration
{
    internal static void TryRegister()
    {
        try
        {
            DevPanelRegistry.Register(
                id:           "yourmod.debug",
                icon:         MdiIcon.Bug,
                name:         "Your Panel",
                order:        500,
                group:        DevPanelTabGroup.Primary,
                onActivate:   OnActivate,
                onDeactivate: OnDeactivate);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[DevMode] Failed to register panel: {ex.Message}");
        }
    }

    private static void OnActivate(NGlobalUi globalUi) { /* build UI */ }
    private static void OnDeactivate(NGlobalUi globalUi) { /* cleanup */ }
}
#else
namespace YourMod.Integrations.DevMode;

internal static class DevModeRegistration
{
    internal static void TryRegister() { }  // no-op when DevMode absent
}
#endif
```
:::

::: zh-CN
在 `[ModInitializer]` 中用方法引用调用 `RegisterPanelWhenReady`：

```csharp
// Main.cs
using HarmonyLib;
#if YOUR_MOD_DEVMODE
using DevMode.UI;
using YourMod.Integrations.DevMode;
#endif
using MegaCrit.Sts2.Core.Modding;

[ModInitializer(nameof(Initialize))]
public class Main
{
    public static void Initialize()
    {
        // ... 你的 mod 初始化逻辑 ...

#if YOUR_MOD_DEVMODE
        DevPanelRegistry.RegisterPanelWhenReady(DevModeRegistration.TryRegister);
#endif
    }
}
```

实际的 `Register` 调用包裹在 try/catch 里，失败时记录警告而不是让 mod 崩溃：

```csharp
// Integrations/DevMode/DevModeRegistration.cs
#if YOUR_MOD_DEVMODE
using DevMode.Icons;
using DevMode.UI;

namespace YourMod.Integrations.DevMode;

internal static class DevModeRegistration
{
    internal static void TryRegister()
    {
        try
        {
            DevPanelRegistry.Register(
                id:           "yourmod.debug",
                icon:         MdiIcon.Bug,
                name:         "你的面板",
                order:        500,
                group:        DevPanelTabGroup.Primary,
                onActivate:   OnActivate,
                onDeactivate: OnDeactivate);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[DevMode] 注册面板失败: {ex.Message}");
        }
    }

    private static void OnActivate(NGlobalUi globalUi) { /* 构建 UI */ }
    private static void OnDeactivate(NGlobalUi globalUi) { /* 清理 */ }
}
#else
namespace YourMod.Integrations.DevMode;

internal static class DevModeRegistration
{
    internal static void TryRegister() { }  // 未安装 DevMode 时为空操作
}
#endif
```
:::

## Building the panel UI{lang="en"}

## 构建面板 UI{lang="zh-CN"}

::: en
`onActivate` must follow the **browser rail** pattern. The structure is always the same: a full-screen root `Control` → backdrop → browser panel → your content `VBoxContainer`.

```csharp
private const string RootName = "DevModeYourModDebug";  // must start with "DevMode"
private const float PanelWidth = 520f;

private static void Remove(NGlobalUi globalUi) =>
    ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();

private static void OnActivate(NGlobalUi globalUi)
{
    Remove(globalUi);  // remove any stale instance first

    DevPanelModApi.PinRail();
    DevPanelModApi.SpliceRail(globalUi, joined: true);

    var root = new Control
    {
        Name = RootName,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        ZIndex = 1250,
    };
    root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    root.TreeExiting += () =>
    {
        DevPanelModApi.UnpinRail();
        DevPanelModApi.SpliceRail(globalUi, joined: false);
    };

    // Backdrop closes the panel when clicked outside
    root.AddChild(DevPanelModApi.CreateBrowserBackdrop(() => Remove(globalUi)));

    // The panel card with a "Content" VBoxContainer child
    var panel = DevPanelModApi.CreateBrowserPanel(PanelWidth);
    root.AddChild(panel);

    // Build your content inside the VBoxContainer
    var content = panel.GetNode<VBoxContainer>("Content");
    content.AddThemeConstantOverride("separation", 10);

    // --- add your Godot nodes here ---
    BuildContent(content, globalUi);
    // ---------------------------------

    ((Node)globalUi).AddChild(root);
}

private static void OnDeactivate(NGlobalUi globalUi) => Remove(globalUi);
```

**Key constraints:**
- Root `Control.Name` **must start with `DevMode`** — `CloseAllOverlays` uses this prefix to clean up when switching tabs.
- Always call `Remove(globalUi)` at the top of `onActivate` to discard any leftover node from a previous activation.
- Wire up `TreeExiting` on root to call `UnpinRail` + `SpliceRail(..., joined: false)` — this restores the rail when your node is freed for any reason.
:::

::: zh-CN
`onActivate` 必须遵循 **browser rail** 模式。结构固定：全屏根 `Control` → 遮罩层 → 浏览器面板 → 你的内容 `VBoxContainer`。

```csharp
private const string RootName = "DevModeYourModDebug";  // 必须以 "DevMode" 开头
private const float PanelWidth = 520f;

private static void Remove(NGlobalUi globalUi) =>
    ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();

private static void OnActivate(NGlobalUi globalUi)
{
    Remove(globalUi);  // 先清理可能残留的旧实例

    DevPanelModApi.PinRail();
    DevPanelModApi.SpliceRail(globalUi, joined: true);

    var root = new Control
    {
        Name = RootName,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        ZIndex = 1250,
    };
    root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    root.TreeExiting += () =>
    {
        DevPanelModApi.UnpinRail();
        DevPanelModApi.SpliceRail(globalUi, joined: false);
    };

    // 遮罩层：点击面板外侧时关闭面板
    root.AddChild(DevPanelModApi.CreateBrowserBackdrop(() => Remove(globalUi)));

    // 面板卡片，含名为 "Content" 的 VBoxContainer 子节点
    var panel = DevPanelModApi.CreateBrowserPanel(PanelWidth);
    root.AddChild(panel);

    // 在 VBoxContainer 内构建内容
    var content = panel.GetNode<VBoxContainer>("Content");
    content.AddThemeConstantOverride("separation", 10);

    // --- 在此添加你的 Godot 节点 ---
    BuildContent(content, globalUi);
    // --------------------------------

    ((Node)globalUi).AddChild(root);
}

private static void OnDeactivate(NGlobalUi globalUi) => Remove(globalUi);
```

**关键约束：**
- 根节点 `Control.Name` **必须以 `DevMode` 开头** — `CloseAllOverlays` 通过此前缀清理切换标签时的残留面板。
- 每次 `onActivate` 开头调用 `Remove(globalUi)`，丢弃上次激活遗留的节点。
- 在 root 的 `TreeExiting` 中调用 `UnpinRail` + `SpliceRail(..., joined: false)`——无论节点因何被释放，都能正确还原轨道状态。
:::

## Adding controls and live state{lang="en"}

## 添加控件与状态刷新{lang="zh-CN"}

::: en
Common Godot nodes for panel content:

| Node | Use |
| --- | --- |
| `Label` | Read-only text, status display |
| `Button` | Trigger an action |
| `SpinBox` | Numeric input |
| `HBoxContainer` | Lay out label + input side by side |
| `CheckBox` | Boolean toggle |

For panels that show live game state, extract status into a `RefreshStatus` method and call it after every action and once on open:

```csharp
private static void BuildContent(VBoxContainer content, NGlobalUi globalUi)
{
    // Status label — refreshed after every action
    var status = new Label { Name = "StatusLabel" };
    content.AddChild(status);

    // Numeric input row
    var row = new HBoxContainer();
    row.AddChild(new Label { Text = "Value" });
    var spin = new SpinBox { MinValue = 0, MaxValue = 999, Step = 1, Value = 0,
                             SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    row.AddChild(spin);
    content.AddChild(row);

    // Action button
    var btn = new Button { Text = "Apply" };
    btn.Pressed += () =>
    {
        ApplyValue((int)spin.Value);
        RefreshStatus(status);
    };
    content.AddChild(btn);

    // Refresh button
    var btnRefresh = new Button { Text = "Refresh" };
    btnRefresh.Pressed += () => RefreshStatus(status);
    content.AddChild(btnRefresh);

    RefreshStatus(status);  // populate on open
}

private static void RefreshStatus(Label status)
{
    // Read current game state and write to label
    status.Text = $"Current value: {ReadGameState()}";
}
```
:::

::: zh-CN
面板内容常用的 Godot 节点：

| 节点 | 用途 |
| --- | --- |
| `Label` | 只读文本、状态展示 |
| `Button` | 触发操作 |
| `SpinBox` | 数值输入 |
| `HBoxContainer` | 标签 + 输入并排布局 |
| `CheckBox` | 布尔开关 |

对于需要显示实时游戏状态的面板，将状态读取抽成 `RefreshStatus` 方法，在每次操作后以及初次打开时调用：

```csharp
private static void BuildContent(VBoxContainer content, NGlobalUi globalUi)
{
    // 状态标签 — 每次操作后刷新
    var status = new Label { Name = "StatusLabel" };
    content.AddChild(status);

    // 数值输入行
    var row = new HBoxContainer();
    row.AddChild(new Label { Text = "数值" });
    var spin = new SpinBox { MinValue = 0, MaxValue = 999, Step = 1, Value = 0,
                             SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    row.AddChild(spin);
    content.AddChild(row);

    // 操作按钮
    var btn = new Button { Text = "应用" };
    btn.Pressed += () =>
    {
        ApplyValue((int)spin.Value);
        RefreshStatus(status);
    };
    content.AddChild(btn);

    // 刷新按钮
    var btnRefresh = new Button { Text = "刷新状态" };
    btnRefresh.Pressed += () => RefreshStatus(status);
    content.AddChild(btnRefresh);

    RefreshStatus(status);  // 打开时立即填充
}

private static void RefreshStatus(Label status)
{
    // 读取当前游戏状态并写入 label
    status.Text = $"当前值: {ReadGameState()}";
}
```
:::

## Icons{lang="en"}

## 图标{lang="zh-CN"}

::: en
DevMode bundles [Material Design Icons](https://pictogrammers.com/library/mdi/) via the `MdiIcon` struct (`DevMode.Icons` namespace). Pre-defined fields use PascalCase:

```csharp
MdiIcon.Bug           // "bug"
MdiIcon.Star          // "star"
MdiIcon.Flash         // "flash"
MdiIcon.Cards         // "cards"
```

To get a Godot texture: `MdiIcon.Bug.Texture(size: 20, color: Colors.White)`.

For icons not pre-defined, use the kebab-case name:

```csharp
MdiIcon.Get("account-check", size: 24);
```

> **Tree-shaking:** Only icons referenced as `MdiIcon.XxxYyy` in source are bundled at build time. Icons used only via `MdiIcon.Get("...")` must already be bundled by a static reference, or they will not be available at runtime.

See [`src/Icons/MdiIcon.cs`](src/Icons/MdiIcon.cs) for the full list of pre-defined icons.
:::

::: zh-CN
DevMode 通过 `MdiIcon` 结构体（`DevMode.Icons` 命名空间）内置了 [Material Design Icons](https://pictogrammers.com/library/mdi/) 图标集。预定义字段使用 PascalCase 命名：

```csharp
MdiIcon.Bug           // "bug"
MdiIcon.Star          // "star"
MdiIcon.Flash         // "flash"
MdiIcon.Cards         // "cards"
```

获取 Godot 纹理：`MdiIcon.Bug.Texture(size: 20, color: Colors.White)`。

对于未预定义的图标，可使用 kebab-case 名称：

```csharp
MdiIcon.Get("account-check", size: 24);
```

> **Tree-shaking 机制：** 构建时仅打包源码中以 `MdiIcon.XxxYyy` 方式静态引用的图标。若通过 `MdiIcon.Get("...")` 使用动态名称，该图标必须已被某处静态引用，否则运行时不可用。

完整预定义图标列表见 [`src/Icons/MdiIcon.cs`](src/Icons/MdiIcon.cs)。
:::

## Dependencies{lang="en"}

## 依赖{lang="zh-CN"}

::: en
There are two patterns depending on how central the DevMode panel is to your mod.

**Hard dependency** — DevMode is required for your mod to function:

Add `DevMode` to your mod manifest `dependencies`. The engine guarantees DevMode loads before your mod, so DevMode types are always available at startup.

```json
{
  "dependencies": ["DevMode"]
}
```

**Soft / optional dependency** — DevMode panel is an optional debug feature:

Do **not** declare `DevMode` in `dependencies`. Instead, gate all DevMode code behind a compile-time symbol or a runtime assembly check so your mod loads cleanly without DevMode installed.

```csharp
// Conditional compilation example
#if YOUR_MOD_DEVMODE
DevPanelRegistry.RegisterPanelWhenReady(Register);
#endif
```

`RegisterPanelWhenReady` resolves the initialization timing in both cases, but it does **not** replace a manifest dependency — if you reference DevMode types unconditionally at startup and DevMode is absent, the CLR will throw at load time.

Document the optional dependency for your users: *"Install DevMode to enable the developer panel."*
:::

::: zh-CN
根据 DevMode 面板对你的 mod 的重要程度，有两种模式可选。

**硬依赖** — DevMode 是 mod 正常运行的必要条件：

在 mod 清单的 `dependencies` 中加入 `DevMode`，引擎会保证先加载 DevMode，你的 mod 在启动时始终可以访问 DevMode 类型。

```json
{
  "dependencies": ["DevMode"]
}
```

**软依赖 / 可选集成** — DevMode 面板只是可选的调试功能：

**不**在 `dependencies` 中声明 DevMode。所有 DevMode 相关代码需通过编译期符号或运行时程序集检测进行隔离，确保未安装 DevMode 时 mod 也能正常加载。

```csharp
// 条件编译示例
#if YOUR_MOD_DEVMODE
DevPanelRegistry.RegisterPanelWhenReady(Register);
#endif
```

`RegisterPanelWhenReady` 解决的是初始化时机问题，并不能替代 manifest 依赖声明——若在启动时无条件引用 DevMode 类型而 DevMode 未安装，CLR 会在加载阶段抛出异常。

建议在 mod 说明中告知用户：*「安装 DevMode 即可启用开发者面板。」*
:::
