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
KitLib is a **family of game mods**: `KitLib` (required host), `KitModPanel`, `KitDevTools`, and `KitAI`.

**Repo sources:** KitLib host code lives under `src/` (Core, Loader, Abstractions, User, Cheat). Sibling products live under `mods/<Product>/`.

**Game install:** each product is a folder under the game’s `mods/` (KitLib Core discovers sibling folders and loads satellite DLLs into one ALC).
:::

::: zh-CN
KitLib 是一组**游戏 mod**：`KitLib`（必装宿主）、`KitModPanel`、`KitDevTools`、`KitAI`。

**仓库源码：** KitLib 宿主在 `src/`（Core、Loader、Abstractions、User、Cheat）；兄弟产品在 `mods/<Product>/`。

**游戏安装：** 每个产品对应游戏 `mods/` 下的一个目录；KitLib Core 发现兄弟目录并在同一 ALC 中加载卫星 DLL。
:::

## Repository layout{lang="en"}

## 仓库布局{lang="zh-CN"}

::: en
```text
KitLib.sln
KitLib.json                    # KitLib product manifest
mods/
  KitModPanel/
    KitLib.ModPanel.csproj
    src/
  KitDevTools/
    KitLib.Panel.csproj, KitLib.Dev.csproj
    src/Panel/, src/Dev/
  KitAI/
    KitLib.AI.csproj
    src/
eng/                           # MSBuild props/targets
scripts/lib/mod_products.py    # product → satellite catalog
src/
  KitLib.Abstractions/         # NuGet contracts (+ KitLibCheatApi, KitLibProductIds)
  KitLib.Core/                 # host, settings, satellite loader
  KitLib.Loader/               # KitLib.dll entry
  KitLib.Modules.User/         # User satellite (logs, progress)
  KitLib.Modules.Cheat/        # Cheat satellite (mutation APIs)
```
:::

::: zh-CN
```text
KitLib.sln
KitLib.json                    # KitLib 产品清单
mods/
  KitModPanel/         # ModPanel 源码
  KitDevTools/         # Panel + Dev 源码
  KitAI/               # AI 源码
eng/
scripts/lib/mod_products.py
src/
  KitLib.Abstractions/   # + KitLibCheatApi
  KitLib.Core/
  KitLib.Loader/
  KitLib.Modules.User/
  KitLib.Modules.Cheat/
```
:::

## Runtime layout{lang="en"}

## 运行时布局{lang="zh-CN"}

::: en
```text
mods/                          # game install layout (not repo sources)
  KitLib/                      # host + User + Cheat
    mod_manifest.json
    KitLib.dll, KitLib.Core.dll, KitLib.Abstractions.dll, …
    modules/KitLib.User.dll, KitLib.Cheat.dll
  KitModPanel/
    KitModPanel.dll
    modules/KitLib.ModPanel.dll
  KitDevTools/
    KitDevTools.dll
    modules/KitLib.Panel.dll, KitLib.Dev.dll
  KitAI/
    KitAI.dll
    modules/KitLib.AI.dll
```

Dependencies: KitModPanel → KitLib; KitDevTools → KitLib + KitModPanel; KitAI → KitLib + KitDevTools.
:::

::: zh-CN
```text
mods/                          # 游戏安装布局（不是仓库源码）
  KitLib/                      # 宿主 + User + Cheat
  KitModPanel/                 # ModPanel
  KitDevTools/                 # Panel + Dev
  KitAI/                       # AI
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

Cross-module internals use `InternalsVisibleTo` within the KitLib family and `KitLib*Ops` / `KitLibCheatApi` delegates on `KitLib.Host` / Abstractions where compile-time cycles must be avoided. Mutation APIs surface through the Core bridges under **[API](/api/)** (`RunInventoryBridge`, `PowerBridge`, `RuntimeCheatBridge`).
:::

::: zh-CN
| 程序集 | 引用 | Harmony |
|--------|------|---------|
| `KitLib.Abstractions` | （无） | — |
| `KitLib`（Core） | Abstractions、游戏 | `MultiplayerCompatPatch` |
| 卫星模块 | Core + Abstractions（编译期可引用 peer） | `ModuleEntry` 里 `KitLibHarmony.Apply(assembly, id)` |

跨模块内部通过 KitLib 家族内的 `InternalsVisibleTo`，以及 `KitLib.Host` / Abstractions 上的 `KitLib*Ops` / `KitLibCheatApi` 委托避免编译期环。突变 API 见 **[API](/api/)**（`RunInventoryBridge`、`PowerBridge`、`RuntimeCheatBridge`）。
:::

## Build{lang="en"}

## 构建{lang="zh-CN"}

::: en
- **KitLib host** (`src/`): `KitLib.Core`, `KitLib.Loader`, `KitLib.Modules.User`, `KitLib.Modules.Cheat` → `build/KitLib/` (+ `modules/`)
- **Sibling satellites**: `mods/<Product>/` projects stage to `build/<ProductId>/modules/` via `KitLibProductId`
- **Thin product loaders**: `mods/KitModPanel|KitDevTools|KitAI` → `build/<ProductId>/`

```bash
make sync         # build + deploy all four products to game mods/
make sync PRODUCT=KitLib   # deploy one product only
make zip-full     # package build/KitLib-vX.X.X.zip (four mod folders)
```
:::

::: zh-CN
- **KitLib 宿主**（`src/`）：Core、Loader、Modules.User、Modules.Cheat → `build/KitLib/`（含 `modules/`）
- **兄弟产品卫星**：`mods/<Product>/` → `build/<ProductId>/modules/`（`KitLibProductId`）
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

1. User → 2. ModPanel → 3. Panel → 4. Cheat (always when present) → 5. AI (needs Panel) → 6. Dev (needs Panel)

`KitLib.User` and `KitLib.Cheat` ship with KitLib. Sibling satellites load when their product folder is installed (DLL present); missing products soft-skip. No per-satellite settings toggles.
:::

::: zh-CN
`SatelliteModuleLoader` 搜索 `KitLib/modules` 与兄弟产品的 `modules/`，初始化顺序：

1. User → 2. ModPanel → 3. Panel → 4. Cheat（有 DLL 即加载）→ 5. AI（需 Panel）→ 6. Dev（需 Panel）

`KitLib.User` 与 `KitLib.Cheat` 随 KitLib 提供；兄弟产品有安装目录（DLL 存在）才加载，缺失则软跳过。没有按卫星的设置开关。
:::

## Content-mod authors{lang="en"}

## 内容 mod 作者{lang="zh-CN"}

::: en
NuGet **`STS2.KitLib.Abstractions`** for compile-time contracts. Runtime needs the **KitLib** product installed (`dependencies: [{ "id": "KitLib", ... }]`). Install KitModPanel / KitDevTools / KitAI only when your players need those features.
:::

::: zh-CN
编译期用 NuGet **`STS2.KitLib.Abstractions`**。运行时需安装 **KitLib** 产品。仅在需要对应能力时再装 KitModPanel / KitDevTools / KitAI。
:::
