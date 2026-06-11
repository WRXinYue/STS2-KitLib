---
title:
  en: Mod runtime API
  zh-CN: Mod 运行时 API
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## vs Panel Registry{lang="en"}

## 与面板注册的区别{lang="zh-CN"}

::: en

| | Mod Runtime API | Panel Registry |
| --- | --- | --- |
| Purpose | Utility — mod metadata and timing hooks | UI — add tabs to the DevMode rail |
| Has UI | No | Yes |
| Typical use | Logging, save metadata, mod filters | In-game developer panels |
| Available | Any time after mods load | Only when DevMode rail is attached |

Use `ModRuntime` when you need mod metadata or a safe post-init timing point, regardless of whether you also register a panel.
:::

::: zh-CN

| | Mod 运行时 API | 面板注册 |
| --- | --- | --- |
| 用途 | 工具层 — mod 元数据与时机钩子 | UI 层 — 向 DevMode 轨道添加标签页 |
| 是否涉及 UI | 否 | 是 |
| 典型场景 | 日志、存档元数据、mod 过滤 | 游戏内开发者面板 |
| 可用时机 | mod 加载完成后任意时刻 | 仅当 DevMode 轨道已附着时 |

需要 mod 元数据或安全的初始化后时机时，使用 `ModRuntime`，与是否同时注册面板无关。
:::

## `DevMode.Modding.ModRuntime`

::: en
Public hooks for other mods:

| Member | Purpose |
| --- | --- |
| `ModRuntime.Catalog` (`IModCatalog`) | Snapshot of loaded mods (`GetSnapshot`, `GetIdSet`), using DevMode’s runtime enumeration over `ModManager` (works across STS2 builds that expose `GetLoadedMods()`, `LoadedMods`, or `Mods`). |
| `ModRuntime.RegisterAfterAllModsLoaded(Action)` | Same queue and timing as `DevPanelRegistry.RegisterPanelWhenReady`. |

`DevModeModInfo` exposes **Id**, **DisplayName**, and **Version** from each manifest.

Use this instead of re-implementing scans over `ModManager` when you need a consistent view (e.g. logging, save metadata, or UI filters).
:::

::: zh-CN
面向其他 mod 的公开钩子：

| 成员 | 作用 |
| --- | --- |
| `ModRuntime.Catalog` (`IModCatalog`) | 已加载 mod 的快照 (`GetSnapshot`、`GetIdSet`)，由 DevMode 在运行时枚举 `ModManager`（兼容不同 STS2 版本中 `GetLoadedMods()`、`LoadedMods` 或 `Mods` 等形态）。 |
| `ModRuntime.RegisterAfterAllModsLoaded(Action)` | 与 `DevPanelRegistry.RegisterPanelWhenReady` 相同的队列与时机。 |

`DevModeModInfo` 暴露各清单中的 **Id**、**DisplayName**、**Version**。

在需要一致视图（例如日志、存档元数据、UI 过滤）时，使用本 API，而不是自行反复扫描 `ModManager`。
:::

## Dependencies{lang="en"}

## 依赖{lang="zh-CN"}

::: en
The same hard / soft dependency rules from [Dev panel registry](/developer/extending/panel-registry) apply here: if you reference `ModRuntime` types unconditionally at startup and DevMode is absent, the CLR throws at load time.

- **Hard dependency** (`"dependencies": ["DevMode"]`): suitable when mod catalog or timing hooks are core to your mod's functionality.
- **Soft dependency** (conditional compilation / runtime assembly check): suitable when `ModRuntime` usage is an optional enhancement. Guard all references behind `#if YOUR_MOD_DEVMODE` or an equivalent runtime check.
:::

::: zh-CN
与[开发者面板注册](/developer/extending/panel-registry)中描述的硬依赖 / 软依赖规则相同：若在启动时无条件引用 `ModRuntime` 类型而 DevMode 未安装，CLR 会在加载阶段抛出异常。

- **硬依赖**（`"dependencies": ["DevMode"]`）：适合将 mod 目录或时机钩子作为核心功能的情况。
- **软依赖**（条件编译 / 运行时程序集检测）：适合将 `ModRuntime` 用作可选增强的情况。所有引用需置于 `#if YOUR_MOD_DEVMODE` 或等效的运行时检测之后。
:::
