---
title:
  en: STS2 version compatibility
  zh-CN: STS2 版本兼容
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Two layers{lang="en"}

## 两层机制{lang="zh-CN"}

::: en
KitLib and other STS2 mods often need to support more than one game build (for example **stable 0.103.3** and **beta 0.107.0**). Treat compatibility as **two separate layers**:

| Layer | When | Typical tools | One DLL or two? |
| --- | --- | --- | --- |
| **Compile-time profile** | Build | `Sts2Profile`, `STS2_BETA106PLUS`, `#if` | **One build = one profile branch** |
| **Runtime profile** | Player launch | `Sts2ProfileMap`, `kitlib.compat.toml` | Same DLL checks facts at runtime |

Opening a runtime API does **not** replace `#if`. Conditional compilation exists because **stable and beta reference different `sts2.dll` surfaces** — a member renamed or removed on one line cannot be compiled against the other reference in a single pass.
:::

::: zh-CN
KitLib 与其它 STS2 mod 常需同时支持多个游戏版本（例如 **stable 0.103.3** 与 **beta 0.107.0**）。兼容应分为 **两层**：

| 层级 | 时机 | 典型手段 | 一个 DLL 还是两个？ |
| --- | --- | --- | --- |
| **编译期 profile** | 构建 | `Sts2Profile`、`STS2_BETA106PLUS`、`#if` | **一次编译只保留一个 profile 分支** |
| **运行时 profile** | 玩家启动 | `Sts2ProfileMap`、`kitlib.compat.toml` | 同一 DLL 在运行时读事实 |

开放运行时 API **不能替代** `#if`。条件编译存在的原因是 **stable 与 beta 引用的 `sts2.dll` 接口不同** —— 某成员在一侧改名或删除时，无法在同一次编译、同一引用下同时通过编译。
:::

## Pinned profiles{lang="en"}

## 固定 profile{lang="zh-CN"}

::: en
KitLib pins two PC profiles (see `KitLib.Abstractions.Compat.Sts2ProfileMap`):

| Profile | Pinned game version | MSBuild |
| --- | --- | --- |
| `StablePre106` | 0.103.3 | `-p:Sts2Profile=stable` |
| `Beta106Plus` | 0.107.0 | `-p:Sts2Profile=beta` |

When `Sts2Profile=beta`, MSBuild defines **`STS2_BETA106PLUS`**. KitLib imports this from the repo root `Directory.Build.props`.
:::

::: zh-CN
KitLib 固定两个 PC profile（见 `KitLib.Abstractions.Compat.Sts2ProfileMap`）：

| Profile | 固定游戏版本 | MSBuild |
| --- | --- | --- |
| `StablePre106` | 0.103.3 | `-p:Sts2Profile=stable` |
| `Beta106Plus` | 0.107.0 | `-p:Sts2Profile=beta` |

当 `Sts2Profile=beta` 时，MSBuild 会定义 **`STS2_BETA106PLUS`**。KitLib 在仓库根目录的 `Directory.Build.props` 中注入该常量。
:::

## Conditional compilation example{lang="en"}

## 条件编译示例{lang="zh-CN"}

::: en
When Megacrit renames or replaces a member between profiles, use `#if` and **build once per profile**:

```csharp
#if STS2_BETA106PLUS
        var inventory = merchantRoom.GetLocalInventory();
#else
        var inventory = merchantRoom.Inventory;
#endif
```

- Build with **beta** refs → only `GetLocalInventory()` remains in IL.
- Build with **stable** refs → only `Inventory` remains in IL.

You cannot ship **one** compiled DLL that contains both branches unless you switch to reflection or a shared shim library that KitLib (or you) maintains for both lines.
:::

::: zh-CN
当 Megacrit 在不同 profile 间改名或替换成员时，用 `#if` 且 **每个 profile 各编一次**：

```csharp
#if STS2_BETA106PLUS
        var inventory = merchantRoom.GetLocalInventory();
#else
        var inventory = merchantRoom.Inventory;
#endif
```

- 用 **beta** 引用编译 → IL 里只保留 `GetLocalInventory()`。
- 用 **stable** 引用编译 → IL 里只保留 `Inventory`。

除非改用反射或由 KitLib（或你）维护的双版本 shim，否则 **无法** 用 **一次** 编译产出同时包含两个分支的 DLL。
:::

## Adopting dual-profile builds in your mod{lang="en"}

## 在你的 mod 中接入双 profile 构建{lang="zh-CN"}

::: en
**Minimum build contract** (copy or import from KitLib):

1. Pin **`sts2.dll`** per profile — KitLib stores refs under `eng/sts2-refs/` (Git LFS). You may vendor the same layout or point `HintPath` at your game install when building locally.
2. Set **`Sts2Profile=stable|beta`** on `dotnet build` / `msbuild`.
3. Define **`STS2_BETA106PLUS`** when profile is beta (same symbol name keeps examples portable).
4. **Publish** the artifact that matches the player’s game line — either two release zips, or one package with profile-specific DLL names and a loader/manifest note.

KitLib daily flow: `make init` → `make sync-full` auto-detects profile from `release_info.json` or ref hash. See [STS2 API profiles](/developer/sts2-api-profiles) for LFS refs and CI.

**Optional:** add `Import` of KitLib’s `Directory.Build.props` if your mod repo lives alongside KitLib; otherwise duplicate the small `PropertyGroup` that maps `Sts2Profile` → `DefineConstants`.
:::

::: zh-CN
**最小构建约定**（从 KitLib 复制或 Import）：

1. 每个 profile 固定 **`sts2.dll`** — KitLib 存在 `eng/sts2-refs/`（Git LFS）。你可复用同一目录结构，或在本地构建时让 `HintPath` 指向当前游戏安装。
2. 在 `dotnet build` / `msbuild` 上传 **`Sts2Profile=stable|beta`**。
3. profile 为 beta 时定义 **`STS2_BETA106PLUS`**（统一符号名便于照搬示例）。
4. **发布**与玩家游戏线一致的产物 —— 两个 zip，或一个包内按 profile 放不同 DLL 并在 manifest 中说明。

KitLib 日常流程：`make init` → `make sync-full` 会根据 `release_info.json` 或 ref 哈希自动检测 profile。LFS ref 与 CI 见 [STS2 API profiles](/developer/sts2-api-profiles)。

**可选：** 若 mod 仓库与 KitLib 同 monorepo，可 `Import` KitLib 的 `Directory.Build.props`；否则复制将 `Sts2Profile` 映射到 `DefineConstants` 的那几行即可。
:::

## Runtime APIs (Abstractions){lang="en"}

## 运行时 API（Abstractions）{lang="zh-CN"}

::: en
These types live in **`KitLib.Abstractions`** and are safe for other mods to reference:

| API | Purpose |
| --- | --- |
| `Sts2ProfileMap.Resolve(version, platform)` | Map raw game version → `Sts2GameProfile` |
| `Sts2GameProfile` | `Unknown`, `StablePre106`, `Beta106Plus` |
| `Sts2SupportedGameVersions.All` | Pinned version strings |
| `KitLibCompatDocument` / `KitLibCompatTomlReader` | Parse `kitlib.compat.toml` |
| `KitLibCompatEvaluator` | Evaluate constraints at load time |
| `KitLibCompatRuntime` | Facts you supply (game version, KitLib version, loaded modules) |

Use **runtime profile** when behavior differs but **both APIs exist in the DLL you compiled against**, or for feature flags. Use **`#if`** when the **member graph** differs between stable and beta.

A thin **`Sts2Compat` runtime facade** (single entry for “current profile at launch”) is planned; until then call `Sts2ProfileMap` with the game’s release version string.
:::

::: zh-CN
以下类型在 **`KitLib.Abstractions`**，可供其它 mod 引用：

| API | 用途 |
| --- | --- |
| `Sts2ProfileMap.Resolve(version, platform)` | 原始游戏版本 → `Sts2GameProfile` |
| `Sts2GameProfile` | `Unknown`、`StablePre106`、`Beta106Plus` |
| `Sts2SupportedGameVersions.All` | 固定版本字符串列表 |
| `KitLibCompatDocument` / `KitLibCompatTomlReader` | 解析 `kitlib.compat.toml` |
| `KitLibCompatEvaluator` | 加载时评估约束 |
| `KitLibCompatRuntime` | 由你提供的运行时事实（游戏版本、KitLib 版本、已加载模块） |

在 **所编译的 DLL 两侧 API 都存在**、仅行为不同时，用 **运行时 profile** 或功能开关；当 stable/beta **成员签名/名称不同** 时用 **`#if`**。

统一的 **`Sts2Compat` 运行时门面**（启动时当前 profile 单入口）在规划中；在此之前可用游戏 release 版本字符串调用 `Sts2ProfileMap`。
:::

## `kitlib.compat.toml`{lang="en"}

## `kitlib.compat.toml`{lang="zh-CN"}

::: en
Content mods can ship a sidecar next to the manifest. KitLib’s Mod Panel reads it for warnings when semver checks fail.

Example:

```toml
[game]
version = ["0.103.3", "0.107.0"]

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
version = ["0.103.3", "0.107.0"]

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

- `eng/api_touchpoints.yaml` and `KitLib.ApiCheck` — KitLib’s private contract tests against pinned `sts2.dll` refs
- ILRepack / satellite module loader — KitLib packaging only

Use this Valaxy site for extension guides and the **[rail panels](/guide/panels/)** overview while playing.
:::

::: zh-CN
内容 mod **不必**接入：

- `eng/api_touchpoints.yaml` 与 `KitLib.ApiCheck` — KitLib 对固定 `sts2.dll` ref 的私有契约测试
- ILRepack / 卫星模块加载器 — 仅 KitLib 打包

扩展开发与本站 **[轨道面板](/guide/panels/)** 概览即可；不再内嵌游戏内手册。
:::

## Related{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Mod runtime API](/developer/extending/mod-runtime) — catalog and load timing
- [Dev panel registry](/developer/extending/panel-registry) — UI tabs
- [STS2 API profiles (maintainers)](/developer/sts2-api-profiles) — LFS refs, `make build-profiles`, CI
:::

::: zh-CN
- [Mod 运行时 API](/developer/extending/mod-runtime) — 目录与加载时机
- [开发者面板注册](/developer/extending/panel-registry) — UI 标签页
- [STS2 API profiles（维护者）](/developer/sts2-api-profiles) — LFS ref、`make build-profiles`、CI
:::
