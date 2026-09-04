---
title:
  en: Architecture
  zh-CN: 架构
top: 9700
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
KitLib is a **family of game mods**: `KitLib` (host), `KitModPanel`, `KitDevTools`, and `KitAI`.

Host sources live under `src/`. Sibling products live under `mods/<Product>/`. Each product installs as its own folder under the game’s `mods/`.
:::

::: zh-CN
KitLib 是一组**游戏 mod**：`KitLib`（宿主）、`KitModPanel`、`KitDevTools`、`KitAI`。

宿主源码在 `src/`，兄弟产品在 `mods/<Product>/`。每个产品对应游戏 `mods/` 下的一个目录。
:::

## Repository layout{lang="en"}

## 仓库布局{lang="zh-CN"}

::: en
```text
KitLib.sln
KitLib.json
mods/
  KitModPanel/
    KitModPanel.csproj
    src/
  KitDevTools/
    KitLib.Panel.csproj, KitLib.Dev.csproj
    src/Panel/, src/Dev/
  KitAI/
    KitLib.AI.csproj
    src/
eng/
scripts/lib/mod_products.py
src/
  KitLib/
    Abstractions/
    Abstractions.Facade/
    Core/
    Loader/
    ModVariantLoader/
```
:::

::: zh-CN
```text
KitLib.sln
KitLib.json
mods/
  KitModPanel/
  KitDevTools/
  KitAI/
eng/
scripts/lib/mod_products.py
src/
  KitLib/
    Abstractions/
    Abstractions.Facade/
    Core/
    Loader/
    ModVariantLoader/
```
:::

## Runtime layout{lang="en"}

## 运行时布局{lang="zh-CN"}

::: en
```text
mods/
  KitLib/
    mod_manifest.json
    KitLib.dll
    lib/<api>/KitLib.Core.dll, KitLib.Abstractions.dll (type-forward facade)
  KitModPanel/
    KitModPanel.dll
  KitDevTools/
    KitDevTools.dll
    modules/KitLib.Panel.dll, KitLib.Dev.dll
  KitAI/
    KitAI.dll
    modules/KitLib.AI.dll
```

`KitLib.dll` is a zero-dependency picker. Each `lib/<api>/` folder is a complete implementation unit (fat Core + Abstractions facade). Content-mod Workshop roots use the same picker shape (`eng/ModVariantContentLoader`) and do not reference `KitLib.ModVariantLoader`. KitLib may still ship `KitLib.ModVariantLoader.dll` for already-published content packs.
:::

::: zh-CN
```text
mods/
  KitLib/
    mod_manifest.json
    KitLib.dll
    lib/<api>/KitLib.Core.dll, KitLib.Abstractions.dll（type-forward 门面）
  KitModPanel/
    KitModPanel.dll
  KitDevTools/
    KitDevTools.dll
    modules/KitLib.Panel.dll, KitLib.Dev.dll
  KitAI/
    KitAI.dll
    modules/KitLib.AI.dll
```

`KitLib.dll` 只做版本选择。每个 `lib/<api>/` 目录是一套完整实现（合并后的 Core + Abstractions 门面）。内容 mod 的 Workshop 根 DLL 同样是零依赖 picker（`eng/ModVariantContentLoader`），不引用 `KitLib.ModVariantLoader`。KitLib 仍可能带上该 DLL，供已经发布的旧内容包解析。

依赖：KitModPanel → KitLib；KitDevTools → KitLib；KitAI → KitLib（KitDevTools 可选，用于 AI Host 面板）。
:::

## Dependency rules{lang="en"}

## 依赖规则{lang="zh-CN"}

::: en
| Assembly | References | Harmony |
|----------|------------|---------|
| `KitLib.Abstractions` | compile-time NuGet; runtime facade in `lib/<api>/`. Host contracts only (not KitModPanel / KitAI / KitDevTools models). | — |
| `KitLib.Core` | Abstractions sources + game | `KitLibHarmony` |
| Product / satellites | Core + Abstractions | `KitLibHarmony.Apply(assembly, id)` in `ModuleEntry` |

Cross-module wiring uses `InternalsVisibleTo` and `KitLib*Ops` / `KitLibCheatApi` delegates. Public APIs (game I/O and mutations): **[API](/api/)**.
:::

::: zh-CN
| 程序集 | 引用 | Harmony |
|--------|------|---------|
| `KitLib.Abstractions` | 编译期 NuGet；运行时 `lib/<api>/` 门面。只含宿主契约（不含 KitModPanel / KitAI / KitDevTools 产品模型）。 | — |
| `KitLib.Core` | Abstractions 源码 + 游戏 | `KitLibHarmony` |
| 产品 / 卫星 | Core + Abstractions | `ModuleEntry` 中 `KitLibHarmony.Apply` |

跨模块用 `InternalsVisibleTo` 与 `KitLib*Ops` / `KitLibCheatApi`。对外 API（游戏 I/O 与突变）见 **[API](/api/)**。
:::

## Build{lang="en"}

## 构建{lang="zh-CN"}

::: en
- KitLib host → `build/KitLib/`
- KitModPanel → `build/KitModPanel/KitModPanel.dll`
- KitDevTools / KitAI → product entry + `build/<ProductId>/modules/`

```bash
make sync
make sync PRODUCT=KitLib
make zip-full    # build/KitLib-vX.Y.Z.zip, KitModPanel-vX.Y.Z.zip, ...
```
:::

::: zh-CN
- KitLib 宿主 → `build/KitLib/`
- KitModPanel → `build/KitModPanel/KitModPanel.dll`
- KitDevTools / KitAI → 产品入口 + `build/<ProductId>/modules/`

```bash
make sync
make sync PRODUCT=KitLib
make zip-full    # build/KitLib-vX.Y.Z.zip, KitModPanel-vX.Y.Z.zip, ...
```
:::

## Runtime load order{lang="en"}

## 运行时加载顺序{lang="zh-CN"}

::: en
1. Game loads `KitLib.dll`, which picks `lib/<api>/KitLib.Core.dll` and runs Core’s initializer.
2. Core inits **User**, and **Cheat** when `AllowHighRiskModules`.
3. Game loads **KitModPanel** when that product is enabled.
4. `SatelliteModuleLoader` loads Panel → AI → Dev from sibling `modules/` folders.
:::

::: zh-CN
1. 游戏加载 `KitLib.dll`，由其选择 `lib/<api>/KitLib.Core.dll` 并调用 Core 初始化。
2. Core 初始化 **User**；在 `AllowHighRiskModules` 时初始化 **Cheat**。
3. 游戏启用 **KitModPanel** 时加载该产品。
4. `SatelliteModuleLoader` 从兄弟产品 `modules/` 加载 Panel → AI → Dev。
:::

## Content-mod authors{lang="en"}

## 内容 mod 作者{lang="zh-CN"}

::: en
Compile against NuGet **`STS2.KitLib.Abstractions`**. Runtime requires the **KitLib** product. Add KitModPanel / KitDevTools / KitAI only when needed.
:::

::: zh-CN
编译期用 NuGet **`STS2.KitLib.Abstractions`**。运行时需要 **KitLib**。按需加装 KitModPanel / KitDevTools / KitAI。
:::
