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
This page is for contributors working on the DevMode repository: building the mod, writing docs, and sending changes upstream. If you only want to install the released mod, see **[Install](/guide/install/)**.
:::

::: zh-CN
本文面向参与 DevMode 仓库开发的贡献者：构建 mod、编写文档、提交改动等。若只想安装已发布的 mod，请参阅 **[安装](/guide/install/)**。
:::

## Dev setup{lang="en"}

## 开发环境{lang="zh-CN"}

::: en
**Prerequisites:** **.NET 9 SDK**; **Python 3** (optional, for `make init` and icon scripts).

**First-time setup:**

```bash
git clone https://github.com/WRXinYue/STS2-DevMode.git
cd STS2-DevMode
make init   # detect STS2 + Godot paths, write local.props
```

**Common Makefile targets:**

| Target | Description |
| --- | --- |
| `make init` | Detect STS2 + Godot, write `local.props` |
| `make build` | Publish to `build/DevMode/` |
| `make deploy` | Copy build into the game `mods\DevMode` folder |
| `make sync` | `build` then `deploy` |
| `make format` | `dotnet format DevMode.sln` |

:::

::: zh-CN
**前置条件：** **.NET 9 SDK**；**Python 3**（可选，用于 `make init` 和图标脚本）。

**首次配置：**

```bash
git clone https://github.com/WRXinYue/STS2-DevMode.git
cd STS2-DevMode
make init   # 检测 STS2 与 Godot 路径，写入 local.props
```

**常用 Makefile 目标：**

| 目标 | 说明 |
| --- | --- |
| `make init` | 检测 STS2、Godot，写入 `local.props` |
| `make build` | 发布到 `build/DevMode/` |
| `make deploy` | 将构建产物复制到游戏 `mods\DevMode` 目录 |
| `make sync` | 先 `build` 再 `deploy` |
| `make format` | `dotnet format DevMode.sln` |

:::

## Code style (C#){lang="en"}

## 代码风格 (C#){lang="zh-CN"}

::: en

- **Braces — K&R (1TBS):** opening `{` on the same line as the declaration or keyword; closing `}` on its own line. Match surrounding files rather than introducing Allman-style.
- **Indentation:** 4 spaces per `[*.cs]` in [`.editorconfig`](/.editorconfig). Line endings **LF**.
- **Language level:** C# 12, nullable enabled, file-scoped namespaces — follow existing patterns.
- **Analyzers:** `dotnet build` runs Roslyn / CA rules. Fix or narrowly suppress warnings rather than adding broad `#pragma` disables.

:::

::: zh-CN

- **花括号 — K&R (1TBS)：** 开括号 `{` 与声明或关键字同行；闭括号 `}` 单独占行。遵循周边文件风格，不引入 Allman 式换行。
- **缩进：** 按 [`.editorconfig`](/.editorconfig) 中 `[*.cs]` 配置使用 4 个空格，行尾 **LF**。
- **语言版本：** C# 12，启用 nullable，文件级命名空间，遵循现有模式。
- **分析器：** `dotnet build` 会运行 Roslyn / CA 规则，请修复或针对性地抑制警告，避免使用宽泛的 `#pragma` 禁用。

:::

## Python scripts{lang="en"}

## Python 脚本{lang="zh-CN"}

::: en
Scripts under `scripts/` use **Black** for formatting ([`pyproject.toml`](/pyproject.toml)) and **flake8** for linting ([`setup.cfg`](/setup.cfg)). Prefer the standard library and keep scripts runnable with `python` / `python3` on `PATH`.
:::

::: zh-CN
`scripts/` 目录下的脚本使用 **Black** 格式化（[`pyproject.toml`](/pyproject.toml)）、**flake8** 静态检查（[`setup.cfg`](/setup.cfg)）。优先使用标准库，并确保脚本在 `PATH` 上的 `python` / `python3` 可直接运行。
:::

## Localization{lang="en"}

## 本地化{lang="zh-CN"}

::: en
All user-visible strings are keyed in:

- [`src/Localization/eng.json`](/src/Localization/eng.json) — English
- [`src/Localization/zhs.json`](/src/Localization/zhs.json) — Simplified Chinese

Add new keys to both files. Keep keys in `dot.separated.lowercase` format matching the surrounding entries.
:::

::: zh-CN
所有面向用户的字符串均通过以下文件管理：

- [`src/Localization/eng.json`](/src/Localization/eng.json) — 英文
- [`src/Localization/zhs.json`](/src/Localization/zhs.json) — 简体中文

新增字符串时两个文件都要添加。键名格式使用 `dot.separated.lowercase`，与周边条目保持一致。
:::

## Docs site{lang="en"}

## 文档站{lang="zh-CN"}

::: en
The docs site lives under **`docs/`** and is built with **[Valaxy](https://valaxy.site/)**. From the repo root:

```bash
make docs        # dev server
make docs-build  # static output → docs/dist/
```

Or manually with **pnpm** (pinned via Corepack):

```bash
cd docs
corepack enable
corepack prepare pnpm@10.24.0 --activate
pnpm install
pnpm dev
```

**Static build:** `pnpm run build:ssg` (or `make docs-build`).

Extension authors: see **[STS2 version compatibility](/developer/extending/sts2-compat)** for dual-profile builds and `kitlib.compat.toml`.

For Markdown syntax and Valaxy-specific features (containers, frontmatter, i18n blocks, etc.), see the **[Markdown writing guide](https://oceanus.wrxinyue.org/guide/writing/markdown)**.
:::

::: zh-CN
文档站源码在 **`docs/`** 目录，使用 **[Valaxy](https://valaxy.site/)** 构建。在仓库根目录：

```bash
make docs        # 开发服务器
make docs-build  # 静态输出 → docs/dist/
```

或手动使用 **pnpm**（通过 Corepack 固定版本）：

```bash
cd docs
corepack enable
corepack prepare pnpm@10.24.0 --activate
pnpm install
pnpm dev
```

**静态构建：** `pnpm run build:ssg`（或 `make docs-build`）。

扩展开发请参阅 **[STS2 版本兼容](/developer/extending/sts2-compat)**（双 profile 构建与 `kitlib.compat.toml`）。

Markdown 语法与 Valaxy 特有功能（容器、frontmatter、i18n 块等）参见 **[Markdown 编写指南](https://oceanus.wrxinyue.org/guide/writing/markdown)**。
:::

## Collaboration{lang="en"}

## 协作规范{lang="zh-CN"}

::: en

- Use **Conventional Commits** for PR titles and commit messages (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, …).
- Keep changes **scoped** to the feature or fix — avoid drive-by reformatting or unrelated file edits.
- Before opening a PR: run `dotnet build` on `DevMode.sln` and **`make format`** to ensure C# matches `.editorconfig`. If you touch `scripts/`, run **`flake8 scripts`** when flake8 is installed.
- Do **not** commit `local.props`, `.env`, or generated assets under `icons/`.

:::

::: zh-CN

- PR 标题与提交信息使用 **Conventional Commits** 格式（`feat:`、`fix:`、`docs:`、`chore:`、`refactor:` 等）。
- 改动范围应限于所涉及的功能或修复，避免顺手重新格式化或修改无关文件。
- 提 PR 前：在 `DevMode.sln` 上跑通 `dotnet build`，并执行 **`make format`** 确保 C# 格式符合 `.editorconfig`。若修改了 `scripts/`，在安装 flake8 的情况下执行 **`flake8 scripts`**。
- 不要提交 `local.props`、`.env` 或 `icons/` 下的生成产物。

:::
