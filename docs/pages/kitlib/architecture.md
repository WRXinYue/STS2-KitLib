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
KitLib is a **single game-mod product**: a host plus the satellite modules `KitModPanel`, `KitLib.Hud`, and `KitLib.Dev`.

Host sources live under `src/KitLib/`; satellite module sources live under `src/KitLib/Modules/<Module>/`. Everything installs as one `mods/KitLib/` folder.
:::

::: zh-CN
KitLib 是**一个游戏 mod 产品**：宿主，加上卫星模块 `KitModPanel`、`KitLib.Hud`、`KitLib.Dev`。

宿主源码在 `src/KitLib/`，卫星模块源码在 `src/KitLib/Modules/<Module>/`。整体安装为 `mods/KitLib/` 一个目录。
:::

## Repository layout{lang="en"}

## 仓库布局{lang="zh-CN"}

::: en
```text
KitLib.sln
KitLib.json
eng/
scripts/lib/mod_products.py
src/
  KitLib/
    Abstractions/
    Abstractions.Facade/
    Core/
    Loader/
    Modules/
      ModPanel/
        KitModPanel.csproj
        src/
      Hud/
        KitLib.Hud.csproj
        src/
      Dev/
        KitLib.Dev.csproj
        src/
```
:::

::: zh-CN
```text
KitLib.sln
KitLib.json
eng/
scripts/lib/mod_products.py
src/
  KitLib/
    Abstractions/
    Abstractions.Facade/
    Core/
    Loader/
    Modules/
      ModPanel/
        KitModPanel.csproj
        src/
      Hud/
        KitLib.Hud.csproj
        src/
      Dev/
        KitLib.Dev.csproj
        src/
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
    lib/<api>/
      KitLib.Core.dll, KitLib.Abstractions.dll (type-forward facade)
      modules/
        KitModPanel.dll, KitLib.Hud.dll, KitLib.Dev.dll
```

`KitLib.dll` is a zero-dependency picker. Each `lib/<api>/` folder is a complete implementation unit (fat Core + Abstractions facade) and carries its own `modules/`. Content-mod Workshop roots use the same picker shape (`eng/ModVariantContentLoader`).

Dependencies: satellite modules (`KitModPanel`, `KitLib.Hud`, `KitLib.Dev`) depend on the KitLib host.
:::

::: zh-CN
```text
mods/
  KitLib/
    mod_manifest.json
    KitLib.dll
    lib/<api>/
      KitLib.Core.dll, KitLib.Abstractions.dll（type-forward 门面）
      modules/
        KitModPanel.dll, KitLib.Hud.dll, KitLib.Dev.dll
```

`KitLib.dll` 只做版本选择。每个 `lib/<api>/` 目录是一套完整实现（合并后的 Core + Abstractions 门面），并自带 `modules/`。内容 mod 的 Workshop 根 DLL 同样是零依赖 picker（`eng/ModVariantContentLoader`）。

依赖：卫星模块（`KitModPanel`、`KitLib.Hud`、`KitLib.Dev`）→ 宿主 KitLib。
:::

## Dependency rules{lang="en"}

## 依赖规则{lang="zh-CN"}

::: en
| Assembly | References | Harmony |
|----------|------------|---------|
| `KitLib.Abstractions` | compile-time NuGet; runtime facade in `lib/<api>/`. Host contracts only (not satellite module models). | — |
| `KitLib.Core` | Abstractions sources + game | `KitLibHarmony` |
| Product / satellites | Core + Abstractions | `KitLibHarmony.Apply(assembly, id)` in `ModuleEntry` |

Cross-module wiring uses `InternalsVisibleTo` and `KitLib*Ops` / `KitLibCheatApi` delegates. Public APIs (game I/O and mutations): **[API](/api/)**.
:::

::: zh-CN
| 程序集 | 引用 | Harmony |
|--------|------|---------|
| `KitLib.Abstractions` | 编译期 NuGet；运行时 `lib/<api>/` 门面。只含宿主契约（不含卫星模块模型）。 | — |
| `KitLib.Core` | Abstractions 源码 + 游戏 | `KitLibHarmony` |
| 产品 / 卫星 | Core + Abstractions | `ModuleEntry` 中 `KitLibHarmony.Apply` |

跨模块用 `InternalsVisibleTo` 与 `KitLib*Ops` / `KitLibCheatApi`。对外 API（游戏 I/O 与突变）见 **[API](/api/)**。
:::

## Build{lang="en"}

## 构建{lang="zh-CN"}

::: en
- KitLib host → `build/KitLib/`
- Satellites (`KitModPanel`, `KitLib.Hud`, `KitLib.Dev`) → `build/KitLib/lib/<api>/modules/`

```bash
make sync
make sync PRODUCT=KitLib
make zip-full    # build/KitLib-vX.Y.Z.zip
```
:::

::: zh-CN
- KitLib 宿主 → `build/KitLib/`
- 卫星模块（`KitModPanel`、`KitLib.Hud`、`KitLib.Dev`）→ `build/KitLib/lib/<api>/modules/`

```bash
make sync
make sync PRODUCT=KitLib
make zip-full    # build/KitLib-vX.Y.Z.zip
```
:::

## Runtime load order{lang="en"}

## 运行时加载顺序{lang="zh-CN"}

::: en
1. Game loads `KitLib.dll`, which picks `lib/<api>/KitLib.Core.dll` and runs Core’s initializer.
2. Core inits **User**, and **Cheat** when `AllowHighRiskModules`.
3. `SatelliteModuleLoader` loads `KitModPanel`, `KitLib.Hud`, and `KitLib.Dev` from `lib/<api>/modules/`.
:::

::: zh-CN
1. 游戏加载 `KitLib.dll`，由其选择 `lib/<api>/KitLib.Core.dll` 并调用 Core 初始化。
2. Core 初始化 **User**；在 `AllowHighRiskModules` 时初始化 **Cheat**。
3. `SatelliteModuleLoader` 从 `lib/<api>/modules/` 加载 `KitModPanel`、`KitLib.Hud`、`KitLib.Dev`。
:::

## Content-mod authors{lang="en"}

## 内容 mod 作者{lang="zh-CN"}

::: en
Compile against NuGet **`STS2.KitLib.Abstractions`**. Runtime requires the **KitLib** product; its satellite modules (`KitModPanel`, `KitLib.Hud`, `KitLib.Dev`) ship inside it and load automatically.
:::

::: zh-CN
编译期用 NuGet **`STS2.KitLib.Abstractions`**。运行时需要 **KitLib**；其卫星模块（`KitModPanel`、`KitLib.Hud`、`KitLib.Dev`）随 KitLib 发布并自动加载。
:::
