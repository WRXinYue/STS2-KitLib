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
KitLib ships as **one game mod** (`mods/KitLib/`) with a Core DLL and optional satellite DLLs under `modules/`. Each satellite is a **separate compile target**; deleting a module DLL disables that feature at runtime.
:::

::: zh-CN
KitLib 以**一个游戏 mod**（`mods/KitLib/`）发布：Core DLL + `modules/` 下可选卫星 DLL。每个卫星是**独立编译目标**；删掉对应 DLL 即运行时禁用该功能。
:::

## Repository layout (enterprise){lang="en"}

## 仓库布局{lang="zh-CN"}

::: en
```text
KitLib.sln
KitLib.json                    # Core mod manifest (runtime: mod_manifest.json)
eng/                           # MSBuild props/targets (build infra)
scripts/
docs/pages/                    # Valaxy documentation site
src/
  KitLib.Abstractions/         # NuGet contracts
  KitLib.Core/                 # KitLib.dll — Host, Settings, Harmony entry
  KitLib.Modules.User/         # KitLib.User.dll
  KitLib.Modules.ModPanel/     # KitLib.ModPanel.dll (main-menu mod settings)
  KitLib.Modules.AI/           # KitLib.AI.dll
  KitLib.Modules.Panel/        # KitLib.Panel.dll (+ Icons, Godot assets)
  KitLib.Modules.Cheat/        # KitLib.Cheat.dll
  KitLib.Modules.Dev/          # KitLib.Dev.dll
```

Each project folder contains **its own source tree**. Opening `src/KitLib.Modules.AI/` in Solution Explorer matches the files compiled into `KitLib.AI.dll`.
:::

::: zh-CN
```text
KitLib.sln
KitLib.json                    # Core mod 清单（运行时：mod_manifest.json）
eng/                           # MSBuild props/targets（构建基础设施）
scripts/
docs/pages/                    # Valaxy 文档站
src/
  KitLib.Abstractions/         # NuGet 契约
  KitLib.Core/                 # KitLib.dll — Host、Settings、Harmony 入口
  KitLib.Modules.User/         # KitLib.User.dll
  KitLib.Modules.ModPanel/     # KitLib.ModPanel.dll（主菜单 mod 设置）
  KitLib.Modules.AI/           # KitLib.AI.dll
  KitLib.Modules.Panel/        # KitLib.Panel.dll（+ Icons、Godot 资源）
  KitLib.Modules.Cheat/        # KitLib.Cheat.dll
  KitLib.Modules.Dev/          # KitLib.Dev.dll
```

每个项目文件夹即**自己的源码树**。在 Solution Explorer 打开 `src/KitLib.Modules.AI/` 看到的文件就是编进 `KitLib.AI.dll` 的那些。
:::

## Runtime layout{lang="en"}

## 运行时布局{lang="zh-CN"}

::: en
```text
mods/KitLib/
  mod_manifest.json
  KitLib.dll
  KitLib.Abstractions.dll
  modules/
    KitLib.User.dll
    KitLib.ModPanel.dll
    KitLib.AI.dll
    KitLib.Panel.dll
    KitLib.Cheat.dll
    KitLib.Dev.dll
```
:::

::: zh-CN
```text
mods/KitLib/
  mod_manifest.json
  KitLib.dll
  KitLib.Abstractions.dll
  modules/
    KitLib.User.dll
    KitLib.ModPanel.dll
    KitLib.AI.dll
    KitLib.Panel.dll
    KitLib.Cheat.dll
    KitLib.Dev.dll
```
:::

## Dependency rules{lang="en"}

## 依赖规则{lang="zh-CN"}

::: en
| Assembly | References | Harmony |
|----------|------------|---------|
| `KitLib.Abstractions` | (none) | — |
| `KitLib` (Core) | Abstractions, game | `MultiplayerCompatPatch` |
| Satellites | Core + Abstractions (+ peers at compile time) | `KitLibHarmony.Apply(assembly, id)` in `ModuleEntry` |

Cross-module internals use `InternalsVisibleTo` within the KitLib family and `KitLib*Ops` delegates on `KitLib.Host` where compile-time cycles must be avoided.
:::

::: zh-CN
| 程序集 | 引用 | Harmony |
|--------|------|---------|
| `KitLib.Abstractions` | （无） | — |
| `KitLib`（Core） | Abstractions、游戏 | `MultiplayerCompatPatch` |
| 卫星模块 | Core + Abstractions（编译期可引用 peer） | `ModuleEntry` 里 `KitLibHarmony.Apply(assembly, id)` |

跨模块内部通过 KitLib 家族内的 `InternalsVisibleTo`，以及 `KitLib.Host` 上的 `KitLib*Ops` 委托避免编译期环。
:::

## Build{lang="en"}

## 构建{lang="zh-CN"}

::: en
- **Core**: `src/KitLib.Core/KitLib.Core.csproj` → `build/KitLib/KitLib.dll`
- **Satellites**: `src/KitLib.Modules.*/` → `build/KitLib.*.dll`
- Compile globs: `eng/KitLib.Core.Compile.props`, `eng/KitLib.Satellite.Compile.props` (per-project `**/*.cs`)

```bash
make sync-full    # build-all + deploy bundle to game mods/KitLib/
make zip-full     # package build/KitLib-vX.X.X.zip
```
:::

::: zh-CN
- **Core**：`src/KitLib.Core/KitLib.Core.csproj` → `build/KitLib/KitLib.dll`
- **卫星**：`src/KitLib.Modules.*/` → `build/KitLib.*.dll`
- 编译 glob：`eng/KitLib.Core.Compile.props`、`eng/KitLib.Satellite.Compile.props`（每项目 `**/*.cs`）

```bash
make sync-full    # build-all + 部署到游戏 mods/KitLib/
make zip-full     # 打包 build/KitLib-vX.X.X.zip
```
:::

## Runtime load order{lang="en"}

## 运行时加载顺序{lang="zh-CN"}

::: en
`SatelliteModuleLoader` loads from `mods/KitLib/modules/` according to user settings in `settings.json` (Mod settings → **Modules**; restart required):

1. User → 2. AI → 3. ModPanel → 4. Panel → 5. Cheat (needs Panel) → 6. Dev (needs Panel)

`KitLib.User` and `KitLib.ModPanel` are always loaded. New installs default to the **Standard** profile (Panel on; AI/Cheat/Dev off). Existing settings migrate to **Full** (all modules enabled).
:::

::: zh-CN
`SatelliteModuleLoader` 按 `settings.json` 用户设置从 `mods/KitLib/modules/` 加载（Mod 设置 → **模块**；需重启）：

1. User → 2. AI → 3. ModPanel → 4. Panel → 5. Cheat（依赖 Panel）→ 6. Dev（依赖 Panel）

`KitLib.User` 与 `KitLib.ModPanel` 始终加载。新安装默认 **Standard** 配置（Panel 开；AI/Cheat/Dev 关）。已有设置会迁移到 **Full**（全部模块开启）。
:::

## Content-mod authors{lang="en"}

## 内容 mod 作者{lang="zh-CN"}

::: en
NuGet **`STS2.KitLib.Abstractions`** for compile-time contracts. Runtime needs `KitLib.dll` and any satellite DLLs you depend on under `modules/`.
:::

::: zh-CN
编译期用 NuGet **`STS2.KitLib.Abstractions`** 作契约。运行时需 `KitLib.dll` 及所依赖的卫星 DLL 位于 `modules/` 下。
:::
