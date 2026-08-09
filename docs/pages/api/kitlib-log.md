---
title:
  en: Logging
  zh-CN: 日志
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Two audiences:

| Audience | Use |
| --- | --- |
| **Content mods (write)** | Official STS2 `Logger` — KitLib captures automatically |
| **Tools / viewers (listen)** | `LogStreamHub` + `LogStreamFilters` — subscribe, replay, filter |

Do **not** invent a second write API. Reuse the stream + filter helpers when building custom viewers, MCP bridges, or side panels.
:::

::: zh-CN
两类用法：

| 对象 | 用法 |
| --- | --- |
| **内容 mod（写）** | 官方 STS2 `Logger` — KitLib 自动采集 |
| **工具 / 查看器（听）** | `LogStreamHub` + `LogStreamFilters` — 订阅、回放、筛选 |

**不要**再造一套写日志 API。做自定义查看器、MCP、旁路面板时复用流 + 筛选助手。
:::

## Write logs (content mods){lang="en"}

## 写日志（内容 mod）{lang="zh-CN"}

::: en
```csharp
using MegaCrit.Sts2.Core.Logging;

public class Main {
    public const string ModID = "my-mod";
    internal static Logger Logger { get; } = new(ModID, LogType.Generic);

    public static void Initialize() {
        Logger.Info("[Bootstrap] Mod initializing...");
    }
}
```

- One `Logger` per mod id.
- Bracket tags in the message for sub-areas (`[Combat]`, `[Save]`).
- Gate expensive work with `Logger.WillLog(LogLevel.Debug)`.

With **KitLib.User**, lines are captured via `Log.LogCallback`, shown in the in-game viewer, written to `user://logs/godot.log`, and streamed to the browser console.
:::

::: zh-CN
```csharp
using MegaCrit.Sts2.Core.Logging;

public class Main {
    public const string ModID = "my-mod";
    internal static Logger Logger { get; } = new(ModID, LogType.Generic);

    public static void Initialize() {
        Logger.Info("[Bootstrap] Mod initializing...");
    }
}
```

- 每个 mod id 一个 `Logger`。
- 消息内用方括号标签区分子模块（`[Combat]`、`[Save]`）。
- 高开销逻辑用 `Logger.WillLog(LogLevel.Debug)` 门控。

安装 **KitLib.User** 后，经 `Log.LogCallback` 采集，进入游戏内查看器、`user://logs/godot.log` 与浏览器控制台。
:::

## Listen & replay (tooling){lang="en"}

## 订阅与回放（工具）{lang="zh-CN"}

::: en
**Entry:** `KitLib.Logging.LogStreamHub` (Abstractions).

| Member | Purpose |
| --- | --- |
| `Subscribe(handler)` | Live frames only |
| `SubscribeWithReplay(handler)` | Current filter frame (if any) + buffered history, then live |
| `Unsubscribe(handler)` | Same delegate instance as subscribe |
| `GetHistorySnapshot()` | Copy of ring buffer |
| `CurrentFilter` | Last published `LogViewerFilterSnapshot` |
| `Publish` / `PublishFilter` | Producers (KitLib capture / viewer sync) |

```csharp
using KitLib.Logging;

void OnEntry(LogStreamEntry entry) {
    if (entry.IsFilterFrame) {
        // entry.Filter — viewer sync frame; update local UI state
        return;
    }
    // entry.Lvl, Text, Mod, Scope, Boundary, Ts
}

LogStreamHub.SubscribeWithReplay(OnEntry);
// ...
LogStreamHub.Unsubscribe(OnEntry);
```

History depth: `LogStreamContract.MaxHistoryEntries` (2000). Pipe framing: `LogStreamFraming` + `LogStreamContract.PipeName(pid)`.
:::

::: zh-CN
**入口：** `KitLib.Logging.LogStreamHub`（Abstractions）。

| 成员 | 作用 |
| --- | --- |
| `Subscribe(handler)` | 仅实时帧 |
| `SubscribeWithReplay(handler)` | 当前筛选帧（若有）+ 缓冲历史，再跟实时 |
| `Unsubscribe(handler)` | 须与订阅时同一委托实例 |
| `GetHistorySnapshot()` | 环形缓冲副本 |
| `CurrentFilter` | 最近一次 `LogViewerFilterSnapshot` |
| `Publish` / `PublishFilter` | 生产端（KitLib 采集 / 查看器同步） |

```csharp
using KitLib.Logging;

void OnEntry(LogStreamEntry entry) {
    if (entry.IsFilterFrame) {
        // entry.Filter — 查看器同步帧
        return;
    }
    // entry.Lvl、Text、Mod、Scope、Boundary、Ts
}

LogStreamHub.SubscribeWithReplay(OnEntry);
// ...
LogStreamHub.Unsubscribe(OnEntry);
```

历史深度：`LogStreamContract.MaxHistoryEntries`（2000）。管道分帧：`LogStreamFraming` + `LogStreamContract.PipeName(pid)`。
:::

## Filter helpers{lang="en"}

## 筛选助手{lang="zh-CN"}

::: en
**Entry:** `KitLib.Logging.LogStreamFilters` (+ `LogStreamSourceParser`, `LogViewerFilterSnapshot`).

Aligned with the browser log viewer (`tools/dev-viewer` filter state).

| Helper | Purpose |
| --- | --- |
| `CreateDefaultFilter()` | Snapshot with builtin suppress rules |
| `BuiltinSuppressRules` | Shared noise patterns |
| `ShouldShow(entry, filter, aiPreset?)` | Full visibility check |
| `WhereVisible(entries, filter)` | Batch filter |
| `MeetsMinLevel` / `IsSuppressedByRules` | Building blocks |
| `ParseSource(entry, filter)` | Mod / `KitLib` / `Game` attribution |
| `IsSessionBoundary` | Keep session markers visible |
| `KitLogMarkers` | Session boundary text constants |

```csharp
var defaults = LogStreamFilters.CreateDefaultFilter();
var filter = new LogViewerFilterSnapshot {
    MinLevel = "warn",
    TextFilter = "combat",
    HiddenSources = ["Game"],
    SuppressRules = defaults.SuppressRules,
    LoadedModIds = defaults.LoadedModIds,
    ModIdAliases = defaults.ModIdAliases,
};

void OnEntry(LogStreamEntry entry) {
    if (entry.IsFilterFrame)
        return;
    if (!LogStreamFilters.ShouldShow(entry, filter ?? LogStreamHub.CurrentFilter))
        return;
    // render / forward
}

LogStreamHub.SubscribeWithReplay(OnEntry);
```

Or filter a snapshot:

```csharp
var visible = LogStreamFilters.WhereVisible(
    LogStreamHub.GetHistorySnapshot(),
    LogStreamHub.CurrentFilter);
```

### `LogViewerFilterSnapshot` fields

| Field | Role |
| --- | --- |
| `MinLevel` | `info` / `warn` / `error` (null = all) |
| `TextFilter` | Case-insensitive substring |
| `SuppressRules` | Enabled patterns hide matching lines |
| `HiddenSources` | Hide by source id (`Game`, `KitLib`, mod ids) |
| `LoadedModIds` / `ModIdAliases` | Source attribution for untagged lines |
:::

::: zh-CN
**入口：** `KitLib.Logging.LogStreamFilters`（以及 `LogStreamSourceParser`、`LogViewerFilterSnapshot`）。

与浏览器日志查看器筛选逻辑对齐（`tools/dev-viewer`）。

| 助手 | 作用 |
| --- | --- |
| `CreateDefaultFilter()` | 带内置抑制规则的快照 |
| `BuiltinSuppressRules` | 共用噪音模式 |
| `ShouldShow(entry, filter, aiPreset?)` | 完整可见性判断 |
| `WhereVisible(entries, filter)` | 批量筛选 |
| `MeetsMinLevel` / `IsSuppressedByRules` | 积木方法 |
| `ParseSource(entry, filter)` | 归属到 mod / `KitLib` / `Game` |
| `IsSessionBoundary` | 会话边界保持可见 |
| `KitLogMarkers` | 会话边界常量 |

```csharp
var defaults = LogStreamFilters.CreateDefaultFilter();
var filter = new LogViewerFilterSnapshot {
    MinLevel = "warn",
    TextFilter = "combat",
    HiddenSources = ["Game"],
    SuppressRules = defaults.SuppressRules,
    LoadedModIds = defaults.LoadedModIds,
    ModIdAliases = defaults.ModIdAliases,
};

void OnEntry(LogStreamEntry entry) {
    if (entry.IsFilterFrame)
        return;
    if (!LogStreamFilters.ShouldShow(entry, filter ?? LogStreamHub.CurrentFilter))
        return;
    // 渲染 / 转发
}

LogStreamHub.SubscribeWithReplay(OnEntry);
```

或对快照筛选：

```csharp
var visible = LogStreamFilters.WhereVisible(
    LogStreamHub.GetHistorySnapshot(),
    LogStreamHub.CurrentFilter);
```
:::

## Dev viewer{lang="en"}

## 开发者控制台{lang="zh-CN"}

::: en
Live tail (recommended): `http://127.0.0.1:9878/#/logs`

Open from the in-game log viewer **Dev viewer** button, or enable **Auto-open developer console on startup** in Mod settings → KitLib → General.
:::

::: zh-CN
实时 tail（推荐）：`http://127.0.0.1:9878/#/logs`

从游戏内日志查看器 **Dev viewer** 打开，或在 Mod 设置 → KitLib → 一般 开启启动时自动打开。
:::

## KitLib internal{lang="en"}

## KitLib 内部{lang="zh-CN"}

::: en
`KitLog` in Core is for KitLib modules only — content mods should use the official `Logger`.
:::

::: zh-CN
Core 里的 `KitLog` 仅供 KitLib 模块 — 内容 mod 请用官方 `Logger`。
:::
