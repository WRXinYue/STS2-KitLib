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

1. Download the latest **`KitLib-vX.X.X.zip`** from [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) (Steam Workshop multi-item split may follow later).

2. Locate your STS2 `mods` folder. On Windows the default path is:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods
   ```

3. Extract the zip so these folders are **direct** children of `mods\`:

   ```text
   mods\
   ├── KitLib\            # required host + mutation APIs (Cheat satellite)
   ├── KitModPanel\       # main-menu mod list / settings (RitsuLib pages if present)
   ├── KitDevTools\       # side rail UI, saves, logs, MCP
   └── KitAI\             # optional AI host
   ```

4. Launch the game. Optional: **Main menu → Mods → KitLib** for hotkeys and progress protection. With **STS2-RitsuLib** installed, KitModPanel also shows those mods' Ritsu settings pages.

**Minimal install:** KitLib only. **Typical player/dev:** KitLib + KitModPanel + KitDevTools. Add **KitAI** for AI host features. **STS2-RitsuLib** is a separate mod; KitModPanel hosts its settings pages when both are present. Install only the products you need — missing folders simply do not load.
:::

::: zh-CN
**前置条件：** Steam 版《杀戮尖塔 2》。

1. 从 [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) 下载最新 **`KitLib-vX.X.X.zip`**（创意工坊多条目拆分后续再做）。

2. 找到 STS2 的 `mods` 目录（Windows 默认见英文节）。

3. 解压后下列目录应为 `mods\` 的**直接**子目录：`KitLib`、`KitModPanel`、`KitDevTools`、`KitAI`。

4. 启动游戏。可选：在 **主菜单 → Mods → KitLib** 配置快捷键与进度保护。若同时安装了 **STS2-RitsuLib**，KitModPanel 也会显示各模组的 Ritsu 设置页。

**最小安装：** 仅 KitLib。**常用：** KitLib + KitModPanel + KitDevTools。需要 AI 托管时再装 **KitAI**。**STS2-RitsuLib** 是独立模组；与 KitModPanel 同时存在时由其承载设置页。按需安装产品即可 — 缺少的目录不会加载。
:::

## Build from source{lang="en"}

## 从源码构建{lang="zh-CN"}

::: en
**Additional prerequisites:** **.NET 9 SDK**; **Python 3** (for `make init` and release scripts).

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init    # detect STS2 path, write local.props
make sync    # build + deploy all four products to game mods/
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
make sync    # 构建并部署四个产品到游戏 mods/
```

详见 **[参与贡献](/kitlib/contributing/)**。
:::
