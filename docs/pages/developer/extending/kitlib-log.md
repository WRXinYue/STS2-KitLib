---
title:
  en: KitLib logging API
  zh-CN: KitLib 日志 API
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en

Content mods reference NuGet **`STS2.KitLib.Abstractions`** and call **`KitLib.Logging.KitLibLog`**. When KitLib Core is loaded at runtime, logs use a unified line format, appear in the game logger and `user://logs/godot.log`, and (with **KitLib.User**) stream to the in-game log viewer and `kitlog attach` pipe.

**You do not pass your manifest id on every call.** KitLib resolves it from the **calling assembly** via `ModAssemblyLookup` (same map used for save-slot blame and content attribution). Satellite DLLs in your mod folder are registered automatically.

**Sub-module / scope** uses a second bracket segment: **`[mod][scope]`**. The first segment is always the manifest id (used for log-viewer mod filters); the second is a logical area inside your mod (Combat, Save, UI, …).

See also: [Mod runtime API](/developer/extending/mod-runtime) for catalog/timing hooks.
:::

::: zh-CN

内容 mod 引用 NuGet **`STS2.KitLib.Abstractions`**，调用 **`KitLib.Logging.KitLibLog`**。运行时加载 KitLib Core 后，日志走统一行格式，进入游戏 logger 与 `user://logs/godot.log`；安装 **KitLib.User** 时还可进入游戏内日志查看器与 `kitlog attach` 管道。

**不必每次传入 manifest id。** KitLib 通过 **调用方程序集** 经 `ModAssemblyLookup` 解析（与存档归因、内容归属同一套映射）。mod 目录下的卫星 DLL 会在启动时自动登记。

**二级模块 / scope** 使用第二段方括号：**`[mod][scope]`**。第一段始终是 manifest id（供日志查看器按 mod 过滤）；第二段是 mod 内部逻辑分区（Combat、Save、UI 等）。

另见：[Mod 运行时 API](/developer/extending/mod-runtime)（目录与初始化时机）。
:::

## Line format{lang="en"}

## 行格式{lang="zh-CN"}

::: en

| Shape | Example |
| --- | --- |
| Mod only | `[my-mod] deck loaded` |
| Mod + scope | `[my-mod][Combat] played Strike for 6` |

Rules:

- First `[…]` must match a loaded manifest **id** (or alias) for log-viewer mod filtering.
- Second `[…]` is optional scope text; filter with text search or `kitlog --filter Combat`.
- Levels: `Debug`, `Info`, `Warn`, `Error` (`KitLogLevel` enum).
:::

::: zh-CN

| 形态 | 示例 |
| --- | --- |
| 仅 mod | `[my-mod] deck loaded` |
| mod + scope | `[my-mod][Combat] played Strike for 6` |

规则：

- 第一个 `[…]` 须匹配已加载清单 **id**（或别名），供日志查看器按 mod 过滤。
- 第二个 `[…]` 为可选 scope；可用文本搜索或 `kitlog --filter Combat` 过滤。
- 级别：`Debug`、`Info`、`Warn`、`Error`（`KitLogLevel` 枚举）。
:::

## API{lang="en"}

## API{lang="zh-CN"}

::: en

Namespace: `KitLib.Logging` (NuGet `STS2.KitLib.Abstractions`).

```csharp
using KitLib.Logging;

// Auto mod id from caller assembly
KitLibLog.Info("deck loaded");
// → [my-mod] deck loaded

// Explicit scope (second bracket)
KitLibLog.Warn("Save", "checksum mismatch");
// → [my-mod][Save] checksum mismatch

// Reuse a scope handle
var combat = KitLibLog.Scope("Combat");
combat.Info("turn start");

// Soft dependency guard
if (KitLibLog.IsAvailable)
    KitLibLog.Debug("trace");
```

When the call comes from a **satellite assembly** (e.g. `MyMod.Combat.dll`) and you omit scope, KitLib may derive scope from the assembly name (e.g. `Combat`). Explicit scope always wins.

`KitLibLog.Bind` is for KitLib Core only — do not call from content mods.
:::

::: zh-CN

命名空间：`KitLib.Logging`（NuGet `STS2.KitLib.Abstractions`）。

```csharp
using KitLib.Logging;

// 从调用程序集自动解析 mod id
KitLibLog.Info("deck loaded");
// → [my-mod] deck loaded

// 显式 scope（第二段方括号）
KitLibLog.Warn("Save", "checksum mismatch");
// → [my-mod][Save] checksum mismatch

// 固定 scope 句柄
var combat = KitLibLog.Scope("Combat");
combat.Info("turn start");

// 软依赖判断
if (KitLibLog.IsAvailable)
    KitLibLog.Debug("trace");
```

若调用来自 **卫星程序集**（如 `MyMod.Combat.dll`）且未写 scope，KitLib 可能从程序集名推导 scope（如 `Combat`）。显式 scope 优先。

`KitLibLog.Bind` 仅供 KitLib Core 使用 — 内容 mod 请勿调用。
:::

## Recommended: ModLog helper{lang="en"}

## 推荐：ModLog 助手{lang="zh-CN"}

::: en

For most content mods, prefer **`ModLog`** over hand-rolling a `Logging` class. It bundles:

- **Local level gate** (`Func<KitLogLevel>` — e.g. from your mod config)
- **KitLib pipeline** when Core is loaded (`KitLibLog.Write`)
- **Formatted fallback** when KitLib is absent (`KitLibLogFormat.FormatLine(modId, scope, body)`)
- **Caller attribution on Debug only** (`file:line member | message`)

```csharp
using KitLib.Logging;

static readonly ModLog Log = new(
    modId: Main.ModID,
    minimumLevel: () => Config.MinKitLogLevel,
    fallback: WriteFallback);

static void WriteFallback(KitLogLevel level, string line) {
    switch (level) {
        case KitLogLevel.Error: Main.Logger.Error(line); break;
        case KitLogLevel.Warn: Main.Logger.Warn(line); break;
        case KitLogLevel.Debug: Main.Logger.Debug(line); break;
        default: Main.Logger.Info(line); break;
    }
}

// Usage
Log.Info("deck loaded");
Log.Warn("Save", "checksum mismatch");
Log.Scope("Combat").Info("turn start");
```

Notes:

- **`modId`** is used for the **fallback** line only. When KitLib is bound, mod id is still resolved from the **caller assembly** (same as direct `KitLibLog` calls).
- **`Error`** always emits regardless of minimum level.
- Optional thin facade: `public static ModLogScope Scope(string s) => Log.Scope(s);`
- Low-level control: use `KitLibLog` / `KitLibLogScope` directly (previous section).
:::

::: zh-CN

多数内容 mod 推荐使用 **`ModLog`**，不必手写整份 `Logging` 类。它整合：

- **本地级别过滤**（`Func<KitLogLevel>`，例如来自 mod 配置）
- **KitLib 已加载时**走统一管道（`KitLibLog.Write`）
- **KitLib 未加载时**走格式化 fallback（`KitLibLogFormat.FormatLine(modId, scope, body)`）
- **仅 Debug 带调用点**（`file:line member | message`）

```csharp
using KitLib.Logging;

static readonly ModLog Log = new(
    modId: Main.ModID,
    minimumLevel: () => Config.MinKitLogLevel,
    fallback: WriteFallback);

static void WriteFallback(KitLogLevel level, string line) {
    switch (level) {
        case KitLogLevel.Error: Main.Logger.Error(line); break;
        case KitLogLevel.Warn: Main.Logger.Warn(line); break;
        case KitLogLevel.Debug: Main.Logger.Debug(line); break;
        default: Main.Logger.Info(line); break;
    }
}

// 用法
Log.Info("deck loaded");
Log.Warn("Save", "checksum mismatch");
Log.Scope("Combat").Info("turn start");
```

说明：

- **`modId`** 仅用于 **fallback** 行；KitLib 已绑定时 mod id 仍从 **调用程序集** 解析（与直接调用 `KitLibLog` 相同）。
- **`Error`** 不受最低级别限制，始终输出。
- 可选薄封装：`public static ModLogScope Scope(string s) => Log.Scope(s);`
- 需要更底层控制时，仍可直接使用上一节的 `KitLibLog` / `KitLibLogScope`。
:::

## Mod settings: log level row{lang="en"}

## Mod 设置页：日志级别行{lang="zh-CN"}

::: en

When **KitLib.ModPanel** is loaded, add a standard log-level dropdown to your native settings page via **`KitLibModSettingsUiOps.BuildLogLevelRow`** (namespace `KitLib.Abstractions.Modding`). Wire the same getter into **`ModLog`** or **`ModLogSettings`**.

Optional JSON helper:

```csharp
using KitLib.Abstractions.Modding;
using KitLib.Logging;

static readonly string LogSettingsPath = Path.Combine(modDataDir, "log-settings.json");
static ModLogSettings _logSettings = ModLogSettings.Load(LogSettingsPath);
static readonly ModLog Log = _logSettings.CreateModLog(Main.ModID, WriteFallback);
```

In `KitLibModSettingsRegistry.Register` → `BuildBody`:

```csharp
using Godot;
using KitLib.Abstractions.Modding;

static object BuildLoggingPage() {
    var stack = new VBoxContainer();
    var row = KitLibModSettingsUiOps.BuildLogLevelRow?.Invoke(
        I18N.T("modlog.settings.level.title", "Minimum log level"),
        I18N.T("modlog.settings.level.desc",
            "Local filter before KitLib or fallback logging."),
        () => _logSettings.MinimumLevel,
        level => {
            _logSettings.MinimumLevel = level;
            _logSettings.Save(LogSettingsPath);
        });
    if (row is Control c)
        stack.AddChild(c);
    return stack;
}
```

Notes:

- Delegate is **`null`** when ModPanel is not loaded — use `?.Invoke` and skip or provide your own UI.
- Level labels use shared keys `modlog.level.*` (Debug / Info / Warn / Error).
- Changing the level takes effect on the **next log call**; no restart required.
- If you already have a config type, pass `() => Config.MinKitLogLevel` / `level => { …; Config.Save(); }` instead of `ModLogSettings`.
:::

::: zh-CN

加载 **KitLib.ModPanel** 后，可通过 **`KitLibModSettingsUiOps.BuildLogLevelRow`**（命名空间 `KitLib.Abstractions.Modding`）在 native 设置页加入标准日志级别下拉框，并与 **`ModLog`** 或 **`ModLogSettings`** 共用同一 getter。

可选 JSON 辅助类：

```csharp
using KitLib.Abstractions.Modding;
using KitLib.Logging;

static readonly string LogSettingsPath = Path.Combine(modDataDir, "log-settings.json");
static ModLogSettings _logSettings = ModLogSettings.Load(LogSettingsPath);
static readonly ModLog Log = _logSettings.CreateModLog(Main.ModID, WriteFallback);
```

在 `KitLibModSettingsRegistry.Register` → `BuildBody` 中：

```csharp
using Godot;
using KitLib.Abstractions.Modding;

static object BuildLoggingPage() {
    var stack = new VBoxContainer();
    var row = KitLibModSettingsUiOps.BuildLogLevelRow?.Invoke(
        I18N.T("modlog.settings.level.title", "最低日志级别"),
        I18N.T("modlog.settings.level.desc",
            "低于此级别的消息在 KitLib 或 fallback 输出前于本地过滤。"),
        () => _logSettings.MinimumLevel,
        level => {
            _logSettings.MinimumLevel = level;
            _logSettings.Save(LogSettingsPath);
        });
    if (row is Control c)
        stack.AddChild(c);
    return stack;
}
```

说明：

- 未加载 ModPanel 时 delegate 为 **`null`** — 请用 `?.Invoke`，或自行实现 UI。
- 下拉项标签使用共享键 `modlog.level.*`（Debug / Info / Warn / Error）。
- 修改级别在 **下一次写日志** 时生效，无需重启。
- 若已有配置类型，可改用 `() => Config.MinKitLogLevel` / `level => { …; Config.Save(); }`，不必使用 `ModLogSettings`。
:::

## Dependencies{lang="en"}

## 依赖{lang="zh-CN"}

::: en

| Mode | Compile | Runtime | Behavior |
| --- | --- | --- | --- |
| **Soft** | `STS2.KitLib.Abstractions` only | KitLib optional | `KitLibLog.IsAvailable == false`; calls no-op |
| **Hard** | Abstractions + manifest `"dependencies": ["KitLib"]` | KitLib required | Full pipeline + log viewer / pipe when User module present |

Abstractions types are safe to reference unconditionally; **`ModLog`** handles the KitLib vs fallback split for you. Use direct `KitLibLog` only when you need manual control.

Optional sink for tools: implement `IKitLibLogSink` and register via KitLib internals (not part of the content-mod surface today).
:::

::: zh-CN

| 模式 | 编译 | 运行时 | 行为 |
| --- | --- | --- | --- |
| **软依赖** | 仅 `STS2.KitLib.Abstractions` | KitLib 可选 | `KitLibLog.IsAvailable == false`；调用为 no-op |
| **硬依赖** | Abstractions + 清单 `"dependencies": ["KitLib"]` | 必须装 KitLib | 完整管道；有 User 模块时接入日志查看器与 pipe |

Abstractions 类型可无条件引用；**`ModLog`** 会自动处理 KitLib 与 fallback 分支。仅在需要手动控制时使用底层 `KitLibLog`。

工具向可选 sink：实现 `IKitLibLogSink`（当前未作为内容 mod 公开注册入口）。
:::

## Internal KitLib usage{lang="en"}

## KitLib 内部用法{lang="zh-CN"}

::: en

KitLib modules call **`KitLog.Info(scope, message)`** in Core (same `[KitLib][scope]` shape). Prefer scoped overloads over hand-written `[Tag]` prefixes in `MainFile.Logger` strings.

Legacy single-bracket tags such as `[KitLibHost]` should use scope **`Host`**; `[KitLib.CombatAdd]` → **`CombatAdd`**.
:::

::: zh-CN

KitLib 各模块在 Core 内调用 **`KitLog.Info(scope, message)`**（同样为 `[KitLib][scope]` 形态）。请使用带 scope 的重载，不要在 `MainFile.Logger` 字符串里手写 `[Tag]` 前缀。

旧式单段方括号标签（如 `[KitLibHost]`）应改用 scope **`Host`**；`[KitLib.CombatAdd]` → **`CombatAdd`**。
:::

## Logs & CLI{lang="en"}

## 日志与 CLI{lang="zh-CN"}

::: en

With **KitLib.User**, session boundaries use `KitLogMarkers.SessionBoundaryPrefix`. Disk output uses the official `user://logs/godot.log`.

Live per-process stream (recommended for dual-instance):

```bash
kitlog attach --pid <pid> -f --sync-viewer
```

Legacy file tail:

```bash
kitlog tail -f --sync-viewer
```

Filter updates stream over the log pipe when using `kitlog attach --sync-viewer` (no disk file).

**Mod settings:** KitLib → General → **Open live log terminal on startup** (`LaunchKitlogOnStartup`, default off). When enabled, KitLib.User opens `kitlog attach` after startup; silently skips if KitLog.Cli (kitlog) is not installed.
:::

::: zh-CN

安装 **KitLib.User** 时，会话边界使用 `KitLogMarkers.SessionBoundaryPrefix`。磁盘日志使用官方 `user://logs/godot.log`。

按进程实时流（双开推荐）：

```bash
kitlog attach --pid <pid> -f --sync-viewer
```

传统文件 tail：

```bash
kitlog tail -f --sync-viewer
```

筛选更新经日志管道发送给 `kitlog attach --sync-viewer`（不再写磁盘文件）。

**Mod 设置：** KitLib → 常规 → **启动时打开实时日志终端**（`LaunchKitlogOnStartup`，默认关）。开启后 KitLib.User 在启动后打开 `kitlog attach`；未安装 KitLog.Cli（kitlog）时静默跳过。
:::
