---
title:
  en: STS2 version compatibility
  zh-CN: STS2 版本兼容
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Target game line{lang="en"}

## 目标游戏版本{lang="zh-CN"}

::: en
KitLib targets the **Steam public-beta** STS2 line. The repo pins one compile reference under `eng/sts2-refs/beta/<version>/` (currently **0.109.0**). MSBuild uses `Sts2Profile=beta` by default (`Directory.Build.props`).

`make init` writes `local.props` with your `Sts2Dir`. `make sync-full` builds against the pinned beta ref and deploys to `mods/KitLib/`.
:::

::: zh-CN
KitLib 面向 **Steam public-beta** 分支的 STS2。仓库在 `eng/sts2-refs/beta/<version>/` 固定一份编译引用（当前 **0.109.0**）。MSBuild 默认 `Sts2Profile=beta`（见根目录 `Directory.Build.props`）。

`make init` 会写入 `Sts2Dir`；`make sync-full` 按固定 beta 引用构建并部署到 `mods/KitLib/`。
:::

## Runtime compatibility checks{lang="en"}

## 运行时兼容检查{lang="zh-CN"}

::: en
At launch, KitLib maps the game’s `release_info.json` version through **`Sts2ProfileMap`** (`KitLib.Abstractions.Compat`). PC builds with `0.106+` are treated as **supported**; older or unknown versions may show a startup banner.

Other mods can reuse the same types without dual-profile loaders or variant DLL folders.
:::

::: zh-CN
启动时，KitLib 用 **`Sts2ProfileMap`**（`KitLib.Abstractions.Compat`）解析游戏的 `release_info.json` 版本。PC 端 `0.106+` 视为 **supported**；更旧或未知版本可能显示启动横幅。

其它 mod 可直接复用这些类型，无需多版本 Loader 或 `lib/<mod>_<version>.dll` 变体目录。
:::

## Runtime APIs (Abstractions){lang="en"}

## 运行时 API（Abstractions）{lang="zh-CN"}

::: en
Safe for content mods to reference:

| API | Purpose |
| --- | --- |
| `Sts2ProfileMap.Resolve(version, platform)` | Map raw game version → `Sts2GameProfile` |
| `Sts2GameProfile` | `Unknown`, `Supported` |
| `Sts2SupportedGameVersions.All` | Pinned version strings KitLib was built for |
| `KitLibHostPaths` | Resolve sibling `KitLib/` mod directory paths |
| `KitLibCompatDocument` / `KitLibCompatTomlReader` | Parse `kitlib.compat.toml` |
| `KitLibCompatEvaluator` | Evaluate constraints at load time |
| `KitLibCompatRuntime` | Facts you supply (game version, KitLib version, loaded modules) |

Use runtime checks for **behavior** differences on the same API surface. When Megacrit changes member names or signatures, bump the pinned beta ref and fix KitLib against the new `sts2.dll` — KitLib no longer ships dual compile profiles or per-mod variant bundles.
:::

::: zh-CN
内容 mod 可安全引用：

| API | 用途 |
| --- | --- |
| `Sts2ProfileMap.Resolve(version, platform)` | 原始游戏版本 → `Sts2GameProfile` |
| `Sts2GameProfile` | `Unknown`、`Supported` |
| `Sts2SupportedGameVersions.All` | KitLib 构建时固定的版本字符串 |
| `KitLibHostPaths` | 解析相邻 `KitLib/` mod 目录路径 |
| `KitLibCompatDocument` / `KitLibCompatTomlReader` | 解析 `kitlib.compat.toml` |
| `KitLibCompatEvaluator` | 加载时评估约束 |
| `KitLibCompatRuntime` | 由你提供的运行时事实（游戏版本、KitLib 版本、已加载模块） |

同一 API 表面上的**行为**差异用运行时检查。Megacrit 改名或改签名时，提升固定的 beta ref 并对新 `sts2.dll` 修 KitLib —— KitLib 已不再提供双 profile 编译或其它 mod 的多版本变体包。
:::

## `kitlib.compat.toml`{lang="en"}

## `kitlib.compat.toml`{lang="zh-CN"}

::: en
Content mods can ship a sidecar next to the manifest. KitLib’s Mod Panel reads it for warnings when semver checks fail.

Example:

```toml
[game]
version = [">=0.109.0"]

[kitlib]
version = [">=0.13.0"]
modules = ["KitLib.Panel"]

[dependencies]
"SomeOtherMod" = [">=1.2.0"]
```

Ranges use npm-style semver (`||`, `>=`, exact pins). Empty file sections are ignored.
:::

::: zh-CN
内容 mod 可在 manifest 旁放置 sidecar。KitLib Mod Panel 在 semver 不满足时会提示玩家。

示例：

```toml
[game]
version = [">=0.109.0"]

[kitlib]
version = [">=0.13.0"]
modules = ["KitLib.Panel"]

[dependencies]
"SomeOtherMod" = [">=1.2.0"]
```

范围使用 npm 风格 semver（`||`、`>=`、精确版本）。空 section 会被忽略。
:::

## What stays internal to KitLib{lang="en"}

## 仍属于 KitLib 内部的工具{lang="zh-CN"}

::: en
Not required for consumer mods:

- `eng/api_touchpoints.yaml` and `KitLib.ApiCheck` — contract tests against the pinned beta `sts2.dll` ref
- Satellite module loader — KitLib packaging only

Use this Valaxy site for extension guides and the **[rail panels](/guide/panels/)** overview while playing.
:::

::: zh-CN
内容 mod **不必**接入：

- `eng/api_touchpoints.yaml` 与 `KitLib.ApiCheck` — 对固定 beta `sts2.dll` ref 的契约测试
- 卫星模块加载器 — 仅 KitLib 打包

扩展开发与本站 **[轨道面板](/guide/panels/)** 概览即可。
:::

## Related{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Mod runtime API](/developer/extending/mod-runtime) — catalog and load timing
- [Dev panel registry](/developer/extending/panel-registry) — UI tabs
- [STS2 compile refs (maintainers)](/developer/sts2-api-profiles) — LFS refs, `make verify`, CI
:::

::: zh-CN
- [Mod 运行时 API](/developer/extending/mod-runtime) — 目录与加载时机
- [开发者面板注册](/developer/extending/panel-registry) — UI 标签页
- [STS2 编译引用（维护者）](/developer/sts2-api-profiles) — LFS ref、`make verify`、CI
:::
