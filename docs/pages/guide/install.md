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

1. Go to the [GitHub releases page](https://github.com/WRXinYue/STS2-DevMode/releases) and download the latest `DevMode-*.zip`.

2. Locate your STS2 `mods` folder. On Windows the default path is:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods
   ```

3. Extract the zip so that `DevMode\` is a direct subfolder of `mods\`:

   ```text
   mods\
   └── DevMode\
       ├── DevMode.dll
       ├── DevMode.json
       ├── editor\
       └── scripts\
   ```

4. Launch the game — DevMode loads automatically with the mod loader.
:::

::: zh-CN
**前置条件：** Steam 版《杀戮尖塔 2》。

1. 前往 [GitHub Releases 页面](https://github.com/WRXinYue/STS2-DevMode/releases)，下载最新的 `DevMode-*.zip`。

2. 找到 STS2 的 `mods` 目录，Windows 默认路径为：

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods
   ```

3. 将压缩包解压，使 `DevMode\` 成为 `mods\` 的直接子目录：

   ```text
   mods\
   └── DevMode\
       ├── DevMode.dll
       ├── DevMode.json
       ├── editor\
       └── scripts\
   ```

4. 启动游戏，DevMode 会随模组加载器自动载入。
:::

## Build from source{lang="en"}

## 从源码构建{lang="zh-CN"}

::: en
**Additional prerequisites:** **.NET 9 SDK**; **Python 3** (optional, for `make init` and icon scripts).

```bash
git clone https://github.com/WRXinYue/STS2-DevMode.git
cd DevMode
make init    # detect STS2 path, write local.props
make sync    # build + deploy to game mods folder
```

`make init` only needs to run once. After that, `make sync` (or `make build` + `make deploy` separately) covers the usual iteration loop.

For the full list of Makefile targets and collaboration norms, see **[Contributing](/developer/dev/)**.
:::

::: zh-CN
**额外前置条件：** **.NET 9 SDK**；**Python 3**（可选，用于 `make init` 和图标脚本）。

```bash
git clone https://github.com/WRXinYue/STS2-DevMode.git
cd DevMode
make init    # 检测 STS2 路径，生成 local.props
make sync    # 构建 + 部署到游戏 mods 目录
```

`make init` 只需执行一次。此后日常迭代使用 `make sync`（或分步执行 `make build` + `make deploy`）即可。

完整 Makefile 目标与协作约定见 **[参与贡献](/developer/dev/)**。
:::
