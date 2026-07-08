---
title:
  en: Contributing
  zh-CN: 参与贡献
top: 10050
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
This page is for contributors working on the **KitLib** repository. To install the released mod only, see **[Install](/guide/install/)**.
:::

::: zh-CN
本文面向 **KitLib** 仓库贡献者。若只需安装已发布 mod，请参阅 **[安装](/guide/install/)**。
:::

## Dev setup{lang="en"}

## 开发环境{lang="zh-CN"}

::: en
**Prerequisites:** **.NET 9 SDK**; **Python 3** (for `make init`, release scripts, icon tooling).

**First-time setup:**

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init   # detect STS2 + Godot paths, write local.props
```

**Common Makefile targets:**

| Target | Description |
| --- | --- |
| `make init` | Detect STS2 + Godot, write `local.props` |
| `make build` | Artifacts under `build/KitLib/` |
| `make sync-full` | Build + deploy to game `mods/KitLib/` |
| `make format` | `dotnet format KitLib.sln` |
| `make docs` | Valaxy dev server (`docs/`) |
| `make upload-all` | Release zip + GitHub / Nexus / Steam |

:::

::: zh-CN
**前置条件：** **.NET 9 SDK**；**Python 3**（`make init`、发布脚本、图标工具）。

**首次配置：**

```bash
git clone https://github.com/WRXinYue/STS2-KitLib.git
cd STS2-KitLib
make init
```

**常用 Makefile 目标：**

| 目标 | 说明 |
| --- | --- |
| `make init` | 检测 STS2、Godot，写入 `local.props` |
| `make build` | 产物在 `build/KitLib/` |
| `make sync-full` | 构建并部署到游戏 `mods/KitLib/` |
| `make format` | `dotnet format KitLib.sln` |
| `make docs` | Valaxy 开发服务器 |
| `make upload-all` | 发布 zip + GitHub / Nexus / NuGet / Steam |

:::

## Code style (C#){lang="en"}

## 代码风格 (C#){lang="zh-CN"}

::: en

- **Braces — K&R (1TBS):** opening `{` on the same line; closing `}` on its own line.
- **Indentation:** 4 spaces per `[*.cs]` in [`.editorconfig`](/.editorconfig). Line endings **LF**.
- **Language level:** C# 12, nullable enabled, file-scoped namespaces.
- Fix or narrowly suppress analyzer warnings; avoid broad `#pragma` disables.

:::

::: zh-CN

- **花括号 — K&R (1TBS)**：开括号 `{` 与声明同行；闭括号 `}` 单独一行。
- **缩进：** [`.editorconfig`](/.editorconfig) 中 `[*.cs]` 为 4 空格，行尾 **LF**。
- **语言版本：** C# 12，nullable，文件级命名空间。
- 修复或针对性抑制分析器警告，避免宽泛 `#pragma`。

:::

## Python scripts{lang="en"}

## Python 脚本{lang="zh-CN"}

::: en
Scripts under `scripts/` use **Black** ([`pyproject.toml`](/pyproject.toml)) and **flake8** ([`setup.cfg`](/setup.cfg)).
:::

::: zh-CN
`scripts/` 使用 **Black**（[`pyproject.toml`](/pyproject.toml)）与 **flake8**（[`setup.cfg`](/setup.cfg)）。
:::

## Localization{lang="en"}

## 本地化{lang="zh-CN"}

::: en
User-visible strings live in [`src/KitLib.Core/Localization/eng.json`](/src/KitLib.Core/Localization/eng.json) and [`zhs.json`](/src/KitLib.Core/Localization/zhs.json). Add keys to both files using `dot.separated.lowercase`.
:::

::: zh-CN
用户可见字符串位于 [`eng.json`](/src/KitLib.Core/Localization/eng.json) 与 [`zhs.json`](/src/KitLib.Core/Localization/zhs.json)。新键需双语，格式 `dot.separated.lowercase`。
:::

## Docs site{lang="en"}

## 文档站{lang="zh-CN"}

::: en
Documentation lives under **`docs/pages/`** (Valaxy). From the repo root:

```bash
make docs        # dev server
make docs-build  # static output → docs/dist/
```

Markdown / Valaxy i18n: **[writing guide](https://oceanus.wrxinyue.org/guide/writing/markdown)**.

Extension authors: **[STS2 version compatibility](/developer/extending/sts2-compat)**.
:::

::: zh-CN
文档位于 **`docs/pages/`**（Valaxy）。仓库根目录：

```bash
make docs
make docs-build
```

Markdown / Valaxy 国际化：**[编写指南](https://oceanus.wrxinyue.org/guide/writing/markdown)**。

扩展开发：**[STS2 版本兼容](/developer/extending/sts2-compat)**。
:::

## Collaboration{lang="en"}

## 协作规范{lang="zh-CN"}

::: en

- **Conventional Commits** for PR titles (`feat:`, `fix:`, `docs:`, …).
- Keep changes scoped; avoid drive-by reformatting.
- Before a PR: `dotnet build` on `KitLib.sln`, **`make format`**, and **`flake8 scripts`** when touching Python.
- Do **not** commit `local.props`, `.env`, or generated assets under `icons/`.

:::

::: zh-CN

- PR 标题使用 **Conventional Commits**（`feat:`、`fix:`、`docs:` 等）。
- 改动范围限于相关功能；避免顺手全库格式化。
- 提 PR 前：`dotnet build`（`KitLib.sln`）、**`make format`**；改 Python 时运行 **`flake8 scripts`**。
- 勿提交 `local.props`、`.env`、`icons/` 生成物。

:::
