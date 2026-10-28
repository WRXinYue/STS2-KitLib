---
title:
  en: Install
  zh-CN: 安装
top: 10000
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Install from release{lang="en"}

## 下载安装{lang="zh-CN"}

::: en
**Prerequisites:** Slay the Spire 2 on Steam.

1. Download the latest release zip from [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases): **`KitLib-vX.X.X.zip`**. It ships the host plus the mod panel, HUD, and dev tools as satellite modules. You can also subscribe on Steam Workshop.

2. Locate your STS2 `mods` folder. On Windows the default path is:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods
   ```

3. Extract the zip so the `KitLib` folder is a **direct** child of `mods\`:

   ```text
   mods\
   └── KitLib\
       ├── mod_manifest.json
       ├── KitLib.dll
       └── lib\<api>\
           ├── KitLib.Core.dll
           ├── KitLib.Abstractions.dll
           └── modules\
               ├── KitModPanel.dll
               ├── KitLib.Hud.dll
               └── KitLib.Dev.dll
   ```

4. Launch the game. Optional: **Main menu corner KitModPanel [模组面板] → KitLib** for hotkeys and progress protection. With **STS2-RitsuLib** installed, KitModPanel also shows those mods' Ritsu settings pages.

**Minimal install:** KitLib only — the satellites are inside it. **STS2-RitsuLib** is a separate mod; KitModPanel hosts its settings pages when both are present.
:::

::: zh-CN
**前置条件：** Steam 版《杀戮尖塔 2》。

1. 从 [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) 下载最新发布包：**`KitLib-vX.X.X.zip`**。其中包含宿主，以及作为卫星模块的模组面板、HUD 与开发工具。也可在 Steam 创意工坊订阅。

2. 找到 STS2 的 `mods` 目录（Windows 默认见英文节）。

3. 解压后 `KitLib` 目录应为 `mods\` 的**直接**子目录，结构如下：

   ```text
   mods\
   └── KitLib\
       ├── mod_manifest.json
       ├── KitLib.dll
       └── lib\<api>\
           ├── KitLib.Core.dll
           ├── KitLib.Abstractions.dll
           └── modules\
               ├── KitModPanel.dll
               ├── KitLib.Hud.dll
               └── KitLib.Dev.dll
   ```

4. 启动游戏。可选：在 **主菜单侧边 KitModPanel [模组面板] → KitLib** 配置快捷键与进度保护。若同时安装了 **STS2-RitsuLib**，KitModPanel 也会显示各模组的 Ritsu 设置页。

**最小安装：** 仅 KitLib — 卫星模块都在其中。**STS2-RitsuLib** 是独立模组；与 KitModPanel 同时存在时由其承载设置页。
:::

## Build from source{lang="en"}

## 从源码构建{lang="zh-CN"}

::: en
**Additional prerequisites:** **.NET 9 SDK**; **Python 3** (for `make init` and release scripts).

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init    # detect STS2 path, write local.props
make sync    # build + deploy KitLib (host + satellite modules) to game mods/
```

`make init` only needs to run once. KitLib targets the **Steam public-beta** line; a startup banner may appear when your game version is outside the supported range.

See **[Contributing](/kitlib/contributing/)** for Makefile targets and collaboration norms.
:::

::: zh-CN
**额外前置：** **.NET 9 SDK**；**Python 3**（`make init` 与发布脚本）。

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init
make sync    # 构建并部署 KitLib（宿主 + 卫星模块）到游戏 mods/
```

详见 **[参与贡献](/kitlib/contributing/)**。
:::
