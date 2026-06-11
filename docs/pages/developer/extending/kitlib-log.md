---
title:
  en: KitLib logging API
  zh-CN: KitLib 日志 API
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en

Content mods reference NuGet **`STS2.KitLib.Abstractions`** and call **`KitLib.Logging.KitLibLog`**. When KitLib Core is loaded at runtime, logs use a unified line format, appear in the game logger, and (with **KitLib.User**) land in `mod_data/KitLib/instances/{pid}/session.log` and the in-game log viewer.

**You do not pass your manifest id on every call.** KitLib resolves it from the **calling assembly** via `ModAssemblyLookup` (same map used for save-slot blame and content attribution). Satellite DLLs in your mod folder are registered automatically.

**Sub-module / scope** uses a second bracket segment: **`[mod][scope]`**. The first segment is always the manifest id (used for log-viewer mod filters); the second is a logical area inside your mod (Combat, Save, UI, …).

See also: [Mod runtime API](/developer/extending/mod-runtime) for catalog/timing hooks.
:::

::: zh-CN

内容 mod 引用 NuGet **`STS2.KitLib.Abstractions`**，调用 **`KitLib.Logging.KitLibLog`**。运行时加载 KitLib Core 后，日志走统一行格式，进入游戏 logger；安装 **KitLib.User** 时还会写入 `mod_data/KitLib/instances/{pid}/session.log` 与游戏内日志查看器。

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

## Dependencies{lang="en"}

## 依赖{lang="zh-CN"}

::: en

| Mode | Compile | Runtime | Behavior |
| --- | --- | --- | --- |
| **Soft** | `STS2.KitLib.Abstractions` only | KitLib optional | `KitLibLog.IsAvailable == false`; calls no-op |
| **Hard** | Abstractions + manifest `"dependencies": ["KitLib"]` | KitLib required | Full pipeline + session.log when User module present |

Abstractions types are safe to reference unconditionally; only **invoke** `KitLibLog` after checking `IsAvailable` unless you hard-depend on KitLib.

Optional sink for tools: implement `IKitLibLogSink` and register via KitLib internals (not part of the content-mod surface today).
:::

::: zh-CN

| 模式 | 编译 | 运行时 | 行为 |
| --- | --- | --- | --- |
| **软依赖** | 仅 `STS2.KitLib.Abstractions` | KitLib 可选 | `KitLibLog.IsAvailable == false`；调用为 no-op |
| **硬依赖** | Abstractions + 清单 `"dependencies": ["KitLib"]` | 必须装 KitLib | 完整管道；有 User 模块时写入 session.log |

Abstractions 类型可无条件引用；除非硬依赖 KitLib，否则应在 `IsAvailable` 为真后再调用 `KitLibLog`。

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

## Session logs & CLI{lang="en"}

## 会话日志与 CLI{lang="zh-CN"}

::: en

With **KitLib.User**, session boundaries use `KitLogMarkers.SessionBoundaryPrefix`. Per-process logs:

`mod_data/KitLib/instances/{pid}/session.log`

External tail:

```bash
kitlog tail --pid <pid> -f
kitlog tail --sync-viewer
```

Filter contract file: `log-viewer-filter.json` (`LogViewerFilterContract`).
:::

::: zh-CN

安装 **KitLib.User** 时，会话边界使用 `KitLogMarkers.SessionBoundaryPrefix`。按进程日志路径：

`mod_data/KitLib/instances/{pid}/session.log`

外部 tail：

```bash
kitlog tail --pid <pid> -f
kitlog tail --sync-viewer
```

过滤器契约文件：`log-viewer-filter.json`（`LogViewerFilterContract`）。
:::
