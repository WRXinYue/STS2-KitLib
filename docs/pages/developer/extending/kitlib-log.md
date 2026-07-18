---
title:
  en: Logging
  zh-CN: 日志
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en

Content mods use the **official STS2 logger** (`MegaCrit.Sts2.Core.Logging.Logger`). KitLib does not ship a separate logging API or wrapper types.

When **KitLib.User** is installed, all lines from every `Logger` instance are captured via `Log.LogCallback`, shown in the in-game log viewer, written to `user://logs/godot.log`, and streamed to the browser dev console.

Reference implementation: [LustTravel2](https://github.com/WRXinYue/LustTravel2) (`Main.Logger`).
:::

::: zh-CN

内容 mod 使用 **STS2 官方 logger**（`MegaCrit.Sts2.Core.Logging.Logger`）。KitLib 不再提供单独的日志 API 或封装类型。

安装 **KitLib.User** 后，所有 `Logger` 实例的输出经 `Log.LogCallback` 采集，进入游戏内日志查看器、`user://logs/godot.log`，并推送到浏览器开发者控制台。

参考实现：[LustTravel2](https://github.com/WRXinYue/LustTravel2)（`Main.Logger`）。
:::

## Recommended pattern{lang="en"}

## 推荐写法{lang="zh-CN"}

::: en

```csharp
using MegaCrit.Sts2.Core.Logging;

public class Main {
    public const string ModID = "my-mod";

    internal static Logger Logger { get; } = new(ModID, LogType.Generic);

    public static void Initialize() {
        ApplyLogEnvOverrides();
        Logger.Info("[Bootstrap] Mod initializing...");
    }

    static void ApplyLogEnvOverrides() {
        var debugEnv = Environment.GetEnvironmentVariable("MY_MOD_DEBUG");
        if (debugEnv is not null && IsTruthy(debugEnv))
            Logger.SetLogLevelForType(LogType.Generic, LogLevel.Debug);
    }
}
```

- Create one `Logger` per mod id (constructor tags lines with your manifest id).
- Use bracket tags inside the message for sub-areas, e.g. `[Combat]`, `[Save]` — same style as LustTravel2.
- Gate expensive Harmony patches with `Logger.WillLog(LogLevel.Debug)` in `Prepare()`.
- Control verbosity with `Logger.SetLogLevelForType` from mod settings or an env var (see LustTravel2 `LUSTTRAVEL_MOD_DEBUG`).
:::

::: zh-CN

```csharp
using MegaCrit.Sts2.Core.Logging;

public class Main {
    public const string ModID = "my-mod";

    internal static Logger Logger { get; } = new(ModID, LogType.Generic);

    public static void Initialize() {
        ApplyLogEnvOverrides();
        Logger.Info("[Bootstrap] Mod initializing...");
    }

    static void ApplyLogEnvOverrides() {
        var debugEnv = Environment.GetEnvironmentVariable("MY_MOD_DEBUG");
        if (debugEnv is not null && IsTruthy(debugEnv))
            Logger.SetLogLevelForType(LogType.Generic, LogLevel.Debug);
    }
}
```

- 每个 mod id 创建一个 `Logger`（构造函数会把行标记为你的清单 id）。
- 在消息内用方括号标签区分子模块，如 `[Combat]`、`[Save]` — 与 LustTravel2 相同风格。
- 高开销 Harmony 补丁在 `Prepare()` 中用 `Logger.WillLog(LogLevel.Debug)` 门控。
- 在 mod 设置或环境变量中调用 `Logger.SetLogLevelForType` 控制级别（参见 LustTravel2 的 `LUSTTRAVEL_MOD_DEBUG`）。
:::

## KitLib internal logging{lang="en"}

## KitLib 内部日志{lang="zh-CN"}

::: en

KitLib modules call **`KitLog.Info(scope, message)`** in Core (scoped `[KitLib] [scope]` shape). This is internal to KitLib — content mods should not use it.
:::

::: zh-CN

KitLib 各模块在 Core 内调用 **`KitLog.Info(scope, message)`**（`[KitLib][scope]` 形态）。仅供 KitLib 内部使用 — 内容 mod 请勿调用。
:::

## Logs & dev viewer{lang="en"}

## 日志与开发者控制台{lang="zh-CN"}

::: en

With **KitLib.User**, session boundaries use `KitLogMarkers.SessionBoundaryPrefix`. Disk output uses the official `user://logs/godot.log`.

Live tail (recommended):

```text
http://127.0.0.1:9878/#/logs
```

Open from the in-game log viewer **Dev viewer** button, or enable **Auto-open developer console on startup** in Mod settings → KitLib → General (`LaunchKitlogOnStartup`, default off). Filter sync works when **Sync with game** is on in the log viewer.

Dual-instance: each STS2 process serves its own dev console on port 9878 (one window at a time unless you use separate log files via `make launch-mp` with `--log-file`).
:::

::: zh-CN

安装 **KitLib.User** 时，会话边界使用 `KitLogMarkers.SessionBoundaryPrefix`。磁盘日志使用官方 `user://logs/godot.log`。

实时 tail（推荐）：

```text
http://127.0.0.1:9878/#/logs
```

从局内日志查看器点击 **Dev viewer**，或在 Mod 设置 → KitLib → 常规 开启 **自启动开发者控制台**（`LaunchKitlogOnStartup`，默认关）。日志查看器开启 **与游戏同步** 时筛选会同步。

双开：每个 STS2 进程在 9878 端口提供独立的开发者控制台（同时只开一个窗口时；也可用 `make launch-mp` 配合 `--log-file` 分文件日志）。
:::
