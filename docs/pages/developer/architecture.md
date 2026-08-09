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
KitLib is a **family of game mods**: `KitLib` (host library), `KitModPanel`, `KitDevTools`, and `KitAI`. Satellite feature DLLs live under each product’s `modules/` folder. KitLib Core discovers sibling product folders and loads satellites into one ALC.
:::

::: zh-CN
KitLib 是一组**游戏 mod**：`KitLib`（宿主库）、`KitModPanel`、`KitDevTools`、`KitAI`。功能卫星 DLL 放在各产品自己的 `modules/` 下；KitLib Core 发现兄弟产品目录并在同一 ALC 中加载。
:::

## Repository layout{lang="en"}

## 仓库布局{lang="zh-CN"}

::: en
```text
KitLib.sln
KitLib.json                    # KitLib product manifest
mods/
  KitLib/
    KitLib.User.csproj
    src/                       # User module sources
  KitModPanel/
    KitLib.ModPanel.csproj
    src/
  KitDevTools/
    KitLib.Panel.csproj, KitLib.Cheat.csproj, KitLib.Dev.csproj
    src/Panel/, src/Cheat/, src/Dev/
  KitAI/
    KitLib.AI.csproj
    src/
eng/                           # MSBuild props/targets
scripts/lib/mod_products.py    # product → satellite catalog
src/
  KitLib.Abstractions/         # NuGet contracts (+ KitLibCheatApi, KitLibProductIds)
  KitLib.Core/                 # host, settings, satellite loader
  KitLib.Loader/               # KitLib.dll entry
```
:::

::: zh-CN
```text
KitLib.sln
KitLib.json                    # KitLib 产品清单
mods/
  KitLib/              # User 源码 + KitLib.User.csproj
  KitModPanel/         # ModPanel 源码
  KitDevTools/         # Panel + Cheat + Dev 源码
  KitAI/               # AI 源码
eng/
scripts/lib/mod_products.py
src/
  KitLib.Abstractions/   # + KitLibCheatApi
  KitLib.Core/
  KitLib.Loader/
```
:::

## Runtime layout{lang="en"}

## 运行时布局{lang="zh-CN"}

::: en
```text
mods/
  KitLib/
    mod_manifest.json
    KitLib.dll, KitLib.Core.dll, KitLib.Abstractions.dll, …
    modules/KitLib.User.dll
  KitModPanel/
    KitModPanel.dll
    modules/KitLib.ModPanel.dll
  KitDevTools/
    KitDevTools.dll
    modules/KitLib.Panel.dll, KitLib.Cheat.dll, KitLib.Dev.dll
  KitAI/
    KitAI.dll
    modules/KitLib.AI.dll
```

Dependencies: KitModPanel → KitLib; KitDevTools → KitLib + KitModPanel; KitAI → KitLib + KitDevTools.
:::

::: zh-CN
```text
mods/
  KitLib/          # User
  KitModPanel/     # ModPanel
  KitDevTools/     # Panel + Cheat + Dev
  KitAI/           # AI
```

依赖：KitModPanel → KitLib；KitDevTools → KitLib + KitModPanel；KitAI → KitLib + KitDevTools。
:::

## Dependency rules{lang="en"}

## 依赖规则{lang="zh-CN"}

::: en
| Assembly | References | Harmony |
|----------|------------|---------|
| `KitLib.Abstractions` | (none) | — |
| `KitLib` (Core) | Abstractions, game | `MultiplayerCompatPatch` |
| Satellites | Core + Abstractions (+ peers at compile time) | `KitLibHarmony.Apply(assembly, id)` in `ModuleEntry` |

Cross-module internals use `InternalsVisibleTo` within the KitLib family and `KitLib*Ops` / `KitLibCheatApi` delegates on `KitLib.Host` / Abstractions where compile-time cycles must be avoided. `KitLib.Cheat` ships with KitDevTools but is not a user-toggleable satellite; cheat capabilities surface through `KitLibCheatApi` in Abstractions.
:::

::: zh-CN
| 程序集 | 引用 | Harmony |
|--------|------|---------|
| `KitLib.Abstractions` | （无） | — |
| `KitLib`（Core） | Abstractions、游戏 | `MultiplayerCompatPatch` |
| 卫星模块 | Core + Abstractions（编译期可引用 peer） | `ModuleEntry` 里 `KitLibHarmony.Apply(assembly, id)` |

跨模块内部通过 KitLib 家族内的 `InternalsVisibleTo`，以及 `KitLib.Host` / Abstractions 上的 `KitLib*Ops` / `KitLibCheatApi` 委托避免编译期环。`KitLib.Cheat` 随 KitDevTools 分发但不是用户可切换的卫星；作弊能力通过 Abstractions 中的 `KitLibCheatApi` 暴露。
:::

## Build{lang="en"}

## 构建{lang="zh-CN"}

::: en
- **Core / Loader**: `src/KitLib.Core`, `src/KitLib.Loader` → `build/KitLib/`
- **Satellites**: staged to `build/<ProductId>/modules/` via `KitLibProductId`
- **Thin product loaders**: `mods/KitModPanel|KitDevTools|KitAI` → `build/<ProductId>/`

```bash
make sync         # build + deploy all four products to game mods/
make sync PRODUCT=KitLib   # deploy one product only
make zip-full     # package build/KitLib-vX.X.X.zip (four mod folders)
```
:::

::: zh-CN
- **Core / Loader** → `build/KitLib/`
- **卫星** → `build/<ProductId>/modules/`（`KitLibProductId`）
- **薄产品入口** → `mods/KitModPanel|KitDevTools|KitAI`

```bash
make sync
make sync PRODUCT=KitLib
make zip-full
```
:::

## Runtime load order{lang="en"}

## 运行时加载顺序{lang="zh-CN"}

::: en
`SatelliteModuleLoader` searches `KitLib/modules` plus sibling product `modules/` folders, then inits in order:

1. User → 2. ModPanel → 3. Panel → 4. Cheat (internal, with Panel) → 5. AI (needs Panel) → 6. Dev (needs Panel)

`KitLib.User` must ship with KitLib. ModPanel loads when the KitModPanel product is installed. Optional satellites follow settings toggles; missing product DLLs soft-skip.
:::

::: zh-CN
`SatelliteModuleLoader` 搜索 `KitLib/modules` 与兄弟产品的 `modules/`，初始化顺序：

1. User → 2. ModPanel → 3. Panel → 4. Cheat（内部，随 Panel）→ 5. AI（需 Panel）→ 6. Dev（需 Panel）

`KitLib.User` 必须随 KitLib 提供；安装 KitModPanel 后加载 ModPanel；其余按设置开关，缺少产品 DLL 时软跳过。
:::

## Content-mod authors{lang="en"}

## 内容 mod 作者{lang="zh-CN"}

::: en
NuGet **`STS2.KitLib.Abstractions`** for compile-time contracts. Runtime needs the **KitLib** product installed (`dependencies: [{ "id": "KitLib", ... }]`). Install KitModPanel / KitDevTools / KitAI only when your players need those features.
:::

::: zh-CN
编译期用 NuGet **`STS2.KitLib.Abstractions`**。运行时需安装 **KitLib** 产品。仅在需要对应能力时再装 KitModPanel / KitDevTools / KitAI。
:::
