---
title:
  en: Content Mods & Multi-Version Support
  zh-CN: 内容 Mod 与多版本支持
top: 9500
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
This page explains how to build a **content mod** that uses KitLib APIs and ships
**one mod folder that works across multiple game versions** (`lib/<api>/` variants),
the same layout KitLib itself and `MpLib` use.

If you only need to compile against KitLib APIs on the current game version, jump to
[section 2](#project-and-csproj-setup) — the variant machinery is optional.
:::

::: zh-CN
本文说明如何构建一个**内容 mod**：既能使用 KitLib 的 API，又能以**一个 mod 目录支持多个游戏版本**（`lib/<api>/` 变体）——与 KitLib 自身及 `MpLib` 采用相同的布局。

如果只是想在当前游戏版本上编译并调用 KitLib API，直接看[第 2 节](#project-and-csproj-setup)——多版本变体机制是可选的。
:::

## Install layout{lang="en"}

## 安装布局{lang="zh-CN"}

::: en
A multi-version mod installs under the game's `mods/<ModId>/` folder:

```text
mods/MyMod/
  MyMod.dll            # self-contained picker (built from eng/ModVariantContentLoader)
  mod_manifest.json
  mod_image.png
  lib/
    0.107.1/
      MyMod.dll        # implementation compiled against the 0.107.1 sts2 API
      compat-target.txt  # contains "0.107.1"
    0.111.0/
      MyMod.dll
      compat-target.txt
```

- The **root `MyMod.dll`** is a zero-dependency picker. It is the only assembly the
  game's mod loader calls (`[ModInitializer]`).
- Each **`lib/<api>/` folder** holds the real implementation compiled against a
  specific sts2 API, plus a `compat-target.txt` marker naming that API.
- At runtime the picker reads the current game version, picks the newest variant
  whose `compat-target.txt` ≤ that version, and loads it.
:::

::: zh-CN
多版本 mod 安装到游戏 `mods/<ModId>/` 目录：

```text
mods/MyMod/
  MyMod.dll            # 自包含 picker（由 eng/ModVariantContentLoader 构建）
  mod_manifest.json
  mod_image.png
  lib/
    0.107.1/
      MyMod.dll        # 针对 0.107.1 版 sts2 API 编译的实现
      compat-target.txt  # 内容为 "0.107.1"
    0.111.0/
      MyMod.dll
      compat-target.txt
```

- **根 `MyMod.dll`** 是零依赖 picker，也是游戏 mod 加载器唯一会调用的程序集（`[ModInitializer]`）。
- 每个 **`lib/<api>/`** 目录存放针对特定 sts2 API 编译的实现，并带一个 `compat-target.txt` 标记该 API 版本。
- 运行时 picker 读取当前游戏版本，选择 `compat-target.txt` ≤ 该版本的最新变体并加载。
:::

## Registering the mod (mod_manifest.json){lang="en"}

## 注册 mod（mod_manifest.json）{lang="zh-CN"}

::: en
KitLib resolves content-mod → host dependencies by **manifest id** (`dependencies[].id`).
Ship a `mod_manifest.json` next to the picker:

```json
{
  "id": "MyMod",
  "name": "MyMod",
  "author": "you",
  "description": "What this mod does.",
  "version": "0.1.0",
  "has_pck": false,
  "has_dll": true,
  "min_game_version": "0.107.1",
  "dependencies": [
    { "id": "KitLib", "min_version": "0.40.0" }
  ],
  "affects_gameplay": true
}
```

Key fields:

| Field | Meaning |
|---|---|
| `id` | Must match the folder name and the `ModId` in your csproj. KitLib looks this up when resolving your mod. |
| `dependencies[].id` | Declare `KitLib` here so the game enables it before your mod. `min_version` should be the oldest KitLib whose API you compiled against. |
| `min_game_version` | Oldest game version your variants cover. Used by the game to warn/block on too-old installs. |
| `has_dll` | `true` — your mod ships the picker DLL. |
:::

::: zh-CN
KitLib 通过 **manifest id**（`dependencies[].id`）解析"内容 mod → 宿主"的依赖关系。请在 picker 旁放置 `mod_manifest.json`：

```json
{
  "id": "MyMod",
  "name": "MyMod",
  "author": "you",
  "description": "这个 mod 做什么。",
  "version": "0.1.0",
  "has_pck": false,
  "has_dll": true,
  "min_game_version": "0.107.1",
  "dependencies": [
    { "id": "KitLib", "min_version": "0.40.0" }
  ],
  "affects_gameplay": true
}
```

关键字段：

| 字段 | 含义 |
|---|---|
| `id` | 必须与目录名、csproj 里的 `ModId` 一致。KitLib 依赖它定位你的 mod。 |
| `dependencies[].id` | 在这里声明 `KitLib`，让游戏先启用它再启用你的 mod。`min_version` 填你编译所基于的最旧 KitLib 版本。 |
| `min_game_version` | 你的变体覆盖的最旧游戏版本，游戏用它做版本检查。 |
| `has_dll` | `true`——你的 mod 带有 picker DLL。 |
:::

## Project and csproj setup{lang="en"}

## 项目与 csproj 写法{lang="zh-CN"}

::: en
Import KitLib's content-mod scaffolding from your csproj. It wires up compile-time
references to `KitLib.Abstractions` / `KitLib.Core` and, when `EnableModVariantBundle=true`,
the variant-bundle targets (`StageModVariantContentLoader` + `ComposeModVariantBundle`).

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ModId>MyMod</ModId>
    <AssemblyName>$(ModId)</AssemblyName>
    <!-- latest sts2 API your code is compiled against -->
    <Sts2ApiCompat Condition="'$(Sts2ApiCompat)' == ''">0.111.0</Sts2ApiCompat>
    <!-- compile-time references: sts2, 0Harmony, GodotSharp -->
    <Sts2DataDir>$(Sts2Dir)/data_sts2_windows_x86_64</Sts2DataDir>
  </PropertyGroup>

  <Import Project="$(RepoRoot)eng\KitLib.ContentMod.props" />

  <ItemGroup>
    <None Include="MyMod.json" CopyToPublishDirectory="PreserveNewest"
          TargetPath="mod_manifest.json" />
    <None Include="mod_image.png" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Entry point is a `[ModInitializer]` on your implementation class:

```csharp
[ModInitializer(nameof(Initialize))]
public static class Main {
    public static void Initialize() {
        // Patch / register against sts2 + KitLib APIs.
    }
}
```

> `KitLib.ContentMod.props` requires `RepoRoot` to be the KitLib repo root, and
> resolves `KitLib.Abstractions.dll` / `KitLib.Core.dll` from `build/KitLib/` (run
> `make build` first). For signature-only builds set `Sts2ApiSignatureRoot`.
:::

::: zh-CN
在 csproj 里导入 KitLib 的内容 mod 脚手架。它会配置 `KitLib.Abstractions` / `KitLib.Core`
的编译期引用；当 `EnableModVariantBundle=true` 时，还会引入变体打包 target
（`StageModVariantContentLoader` + `ComposeModVariantBundle`）。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>true</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ModId>MyMod</ModId>
    <AssemblyName>$(ModId)</AssemblyName>
    <!-- 代码编译所基于的最新 sts2 API -->
    <Sts2ApiCompat Condition="'$(Sts2ApiCompat)' == ''">0.111.0</Sts2ApiCompat>
    <!-- 编译期引用：sts2、0Harmony、GodotSharp -->
    <Sts2DataDir>$(Sts2Dir)/data_sts2_windows_x86_64</Sts2DataDir>
  </PropertyGroup>

  <Import Project="$(RepoRoot)eng\KitLib.ContentMod.props" />

  <ItemGroup>
    <None Include="MyMod.json" CopyToPublishDirectory="PreserveNewest"
          TargetPath="mod_manifest.json" />
    <None Include="mod_image.png" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

入口是实现类上的 `[ModInitializer]`：

```csharp
[ModInitializer(nameof(Initialize))]
public static class Main {
    public static void Initialize() {
        // 对 sts2 与 KitLib API 做 Patch / 注册。
    }
}
```

> `KitLib.ContentMod.props` 要求 `RepoRoot` 指向 KitLib 仓库根目录，并从 `build/KitLib/`
> 解析 `KitLib.Abstractions.dll` / `KitLib.Core.dll`（先执行 `make build`）。
> 只做签名构建时设置 `Sts2ApiSignatureRoot`。
:::

## Building lib/<api> variants{lang="en"}

## 构建 lib/<api> 变体{lang="zh-CN"}

::: en
Each variant must be published once per target API. The pattern is:

1. **Publish** the project with `Sts2ApiCompat=<api>`. A `StageContentModImplIntoLib`
   target (after `Publish`) copies the output into
   `$(RepoBuildDir)/lib/<api>/$(ModId).dll` and writes `compat-target.txt`.
2. **Stage the picker** — `StageModVariantContentLoader` (from
   `eng/STS2.KitLib.ModVariantLoader.targets`, imported by `KitLib.ContentMod.props`
   when `EnableModVariantBundle=true`) builds the root `$(ModId).dll` from
   `eng/ModVariantContentLoader`.
3. **Compose** — `ComposeModVariantBundle` validates the `lib/<api>/` set, requires
   the stable+beta targets (`RequireAllVariants=true`), and copies the manifest/icon.

The simplest driver is a loop (Makefile or shell):

```bash
for api in 0.107.1 0.109.0 0.110.1 0.111.0; do
  dotnet publish -c Debug -p:Sts2ApiCompat=$api \
    -p:RepoRoot=$(repo) -p:Sts2Dir=$(sts2)
done
```

Each publish invokes the staging targets, so after the loop `build/MyMod/` contains
`MyMod.dll` (picker) + `lib/<api>/MyMod.dll` (+ `compat-target.txt`) for every API.

> Reference implementation: `MpLib` in the LustTravel2 repo — `mods/MpLib/MpLib.csproj`
> imports `eng/LustTravel.ContentMod.props` (which sets `ContentModCompatTargets`,
> `IntermediateOutputPath` per API, and imports the KitLib targets), and
> `eng/LustTravel.ContentMod.targets` implements `StageContentModImplIntoLib` +
> `DeployRepoBuildToMods`.
:::

::: zh-CN
每个变体都要按目标 API 各 publish 一次，模式如下：

1. **Publish**：以 `Sts2ApiCompat=<api>` 发布项目。`StageContentModImplIntoLib`
   target（挂在 `Publish` 后）把产物复制到 `$(RepoBuildDir)/lib/<api>/$(ModId).dll`
   并写入 `compat-target.txt`。
2. **生成 picker**：`StageModVariantContentLoader`（位于
   `eng/STS2.KitLib.ModVariantLoader.targets`，`EnableModVariantBundle=true` 时由
   `KitLib.ContentMod.props` 导入）从 `eng/ModVariantContentLoader` 构建根 `$(ModId).dll`。
3. **合成**：`ComposeModVariantBundle` 校验 `lib/<api>/` 完整性（`RequireAllVariants=true`
   时强制要求 stable+beta 目标齐全），并复制 manifest/图标。

最简单的驱动是一个循环（Makefile 或 shell）：

```bash
for api in 0.107.1 0.109.0 0.110.1 0.111.0; do
  dotnet publish -c Debug -p:Sts2ApiCompat=$api \
    -p:RepoRoot=$(repo) -p:Sts2Dir=$(sts2)
done
```

每次 publish 都会触发打包 target，循环结束后 `build/MyMod/` 就包含
`MyMod.dll`（picker）和各 `lib/<api>/MyMod.dll`（+ `compat-target.txt`）。

> 参考实现：LustTravel2 仓库中的 `MpLib` —— `mods/MpLib/MpLib.csproj` 导入
> `eng/LustTravel.ContentMod.props`（设置 `ContentModCompatTargets`、按 API 隔离的
> `IntermediateOutputPath`，并导入 KitLib targets），`eng/LustTravel.ContentMod.targets`
> 实现了 `StageContentModImplIntoLib` 与 `DeployRepoBuildToMods`。
:::

## Runtime selection rules{lang="en"}

## 运行时选版规则{lang="zh-CN"}

::: en
The picker (`eng/ModVariantContentLoader/MainFile.cs`) does:

1. Probe the game version via `HostVersionProbe` (`ReleaseInfoManager` release info,
   falling back to the `sts2` assembly version).
2. Enumerate `lib/*/compat-target.txt`, keep folders whose target parses as a version.
3. Pick the **newest target ≤ game version**; if none is ≤ it, pick the oldest
   (best-effort fallback).
4. Load that `lib/<api>/<ModId>.dll` in the current `AssemblyLoadContext` and invoke
   its `[ModInitializer]`.

So a mod shipping `0.107.1 … 0.111.0` variants works on any game in that range
without a second install or a KitLib dependency at runtime.
:::

::: zh-CN
picker（`eng/ModVariantContentLoader/MainFile.cs`）的逻辑：

1. 用 `HostVersionProbe` 探测游戏版本（优先 `ReleaseInfoManager` 的 release 信息，回退到 `sts2` 程序集版本）。
2. 枚举 `lib/*/compat-target.txt`，保留目标可解析为版本的目录。
3. 选择 **≤ 游戏版本的最新目标**；若都大于游戏版本，则取最旧（尽力回退）。
4. 在当前的 `AssemblyLoadContext` 中加载该 `lib/<api>/<ModId>.dll`，并调用其 `[ModInitializer]`。

因此一个带 `0.107.1 … 0.111.0` 变体的 mod，在该区间任意游戏版本上都能直接运行，
无需二次安装，运行时也不依赖 KitLib。
:::

## Packaging and deployment{lang="en"}

## 打包与部署{lang="zh-CN"}

::: en
KitLib's own repo drives `make bundle` / `make sync-bundle` / `make zip-release`
(`scripts/package_bundle.py`). For your own mod, either:

- add the same `StageContentModImplIntoLib` + `DeployRepoBuildToMods` targets
  (see `LustTravel.ContentMod.targets`) and deploy with `DeployToGame=true`; or
- zip the `build/MyMod/` folder (picker + `lib/` + `mod_manifest.json` + icon) and
  publish it to Steam Workshop.

The root `MyMod.dll` is **self-contained** — it does not reference
`KitLib.ModVariantLoader` (that legacy assembly was removed). Your Workshop package
only needs its own folder.
:::

::: zh-CN
KitLib 仓库用 `make bundle` / `make sync-bundle` / `make zip-release`
（`scripts/package_bundle.py`）驱动打包。你自己的 mod 可以：

- 加同样的 `StageContentModImplIntoLib` + `DeployRepoBuildToMods` target
  （见 `LustTravel.ContentMod.targets`），以 `DeployToGame=true` 部署；或
- 把 `build/MyMod/` 目录（picker + `lib/` + `mod_manifest.json` + 图标）打成 zip 发布到 Steam 创意工坊。

根 `MyMod.dll` 是**自包含**的——不引用 `KitLib.ModVariantLoader`（该旧程序集已移除）。
你的创意工坊包只需要自己的目录。
:::

## See also{lang="en"}

## 相关文档{lang="zh-CN"}

::: en
- [Architecture](./architecture) — repo layout, host vs. content mods.
- [Install](./install) — installing KitLib itself.
- Reference: `MpLib` (LustTravel2) — a published multi-version content mod.
:::

::: zh-CN
- [架构](./architecture) — 仓库布局、宿主与内容 mod。
- [安装](./install) — 安装 KitLib 本体。
- 参考：`MpLib`（LustTravel2）—— 已发布的多版本内容 mod。
:::
