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

1. Download the latest **`KitLib-vX.X.X.zip`** from [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) or subscribe on Steam Workshop.

2. Locate your STS2 `mods` folder. On Windows the default path is:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods
   ```

3. Extract the zip so that **`KitLib\`** is a direct subfolder of `mods\`:

   ```text
   mods\
   └── KitLib\
       ├── mod_manifest.json
       ├── KitLib.dll
       ├── KitLib.Abstractions.dll
       └── modules\
           ├── KitLib.User.dll
           ├── KitLib.ModPanel.dll
           ├── KitLib.Panel.dll
           ├── KitLib.Cheat.dll
           ├── KitLib.Dev.dll
           └── KitLib.AI.dll
   ```

4. Launch the game. Configure satellites under **Main menu → Mods → KitLib** (module toggles, hotkeys, progress protection).

**Optional modules** — delete DLLs under `modules/` to disable features, or use **Mods → KitLib → Modules** toggles (**restart required**). Keep **`KitLib.User.dll`** and **`KitLib.ModPanel.dll`** for logs and mod settings.
:::

::: zh-CN
**前置条件：** Steam 版《杀戮尖塔 2》。

1. 从 [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) 下载最新 **`KitLib-vX.X.X.zip`**，或在 Steam 创意工坊订阅。

2. 找到 STS2 的 `mods` 目录，Windows 默认路径：

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods
   ```

3. 解压后 **`KitLib\`** 应为 `mods\` 的直接子目录（结构见英文节）。

4. 启动游戏，在 **主菜单 → Mods → KitLib** 配置卫星模块、快捷键与进度保护。

**可选模块** — 删除 `modules/` 下 DLL 或在 **Mods → KitLib → 模块** 选择档位（**需重启**）。请保留 **`KitLib.User.dll`** 与 **`KitLib.ModPanel.dll`**。
:::

## Build from source{lang="en"}

## 从源码构建{lang="zh-CN"}

::: en
**Additional prerequisites:** **.NET 9 SDK**; **Python 3** (for `make init` and release scripts).

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init    # detect STS2 path, write local.props
make sync-full    # build + deploy to game mods/KitLib/
```

`make init` only needs to run once. KitLib targets the **Steam public-beta** line; a startup banner may appear when your game version is outside the supported range.

See **[Contributing](/developer/dev/)** for Makefile targets and collaboration norms.
:::

::: zh-CN
**额外前置：** **.NET 9 SDK**；**Python 3**（`make init` 与发布脚本）。

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init
make sync-full
```

`make init` 只需运行一次。KitLib 面向 **Steam public-beta**；游戏版本不在支持范围内时可能显示启动横幅。

Makefile 与协作规范见 **[参与贡献](/developer/dev/)**。
:::
