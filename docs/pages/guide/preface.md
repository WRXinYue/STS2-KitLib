---
title:
  en: Introduction
  zh-CN: 前言
top: 10010
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Slay the Spire 2 modding is still young. Mods install into the local `mods` folder (and increasingly via Steam Workshop). The tooling ecosystem is being built in the open by the community.

This page points to community resources before diving into KitLib itself.
:::

::: zh-CN
《杀戮尖塔 2》模组生态仍处于早期阶段。Mod 安装于本地 `mods` 目录（以及 Steam 创意工坊）。相关工具链由社区持续完善。

本页整理进入 KitLib 正文之前值得了解的社区资源。
:::

## Community resources{lang="en"}

## 社区资源{lang="zh-CN"}

::: en
**[Slay the Spire 2 Modding Tutorials](https://glitchedreme.github.io/SlayTheSpire2ModdingTutorials/index.html)** (Reme, CC BY-NC-SA 4.0) — environment setup, BaseLib content modding, migration guides.

**[BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)** — standard foundation library for STS2 content mods (NuGet).

**[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)** — player-facing mod settings UI and utilities; KitLib integrates via `KitLib.ModPanel`.
:::

::: zh-CN
**[杀戮尖塔 2 模组制作教程](https://glitchedreme.github.io/SlayTheSpire2ModdingTutorials/index.html)**（Reme，CC BY-NC-SA 4.0）— 环境搭建、BaseLib 内容 mod、迁移指南。

**[BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)** — STS2 内容 mod 常用基础库（NuGet）。

**[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)** — 玩家向 Mod 设置 UI 与工具集；KitLib 通过 `KitLib.ModPanel` 桥接。
:::

## What KitLib is{lang="en"}

## KitLib 是什么{lang="zh-CN"}

::: en
**KitLib** is a **modular in-game toolkit** for STS2 — not a content-mod framework (use BaseLib / RitsuLib for cards, relics, etc.).

- **Core host** (`KitLib.dll`) hot-loads **satellite modules** under `modules/` — dev rail, cheats, AI, logging, mod settings, MCP, and more.
- **Content-mod authors** reference NuGet **`STS2.KitLib.Abstractions`**, ship `kitlib.compat.toml`, and optionally register dev-rail tabs via **`DevPanelRegistry`**.

Ready to install? **[Install →](/guide/install/)** · Panel overview: **[Rail panels →](/guide/panels/)**
:::

::: zh-CN
**KitLib** 是 STS2 的**模块化游戏内工具库** — 不是内容 mod 框架（卡牌/遗物等请用 BaseLib / RitsuLib）。

- **Core 宿主**（`KitLib.dll`）热加载 `modules/` 下**卫星模块** — 开发侧栏、作弊、AI、日志、Mod 设置、MCP 等。
- **内容 mod 作者** 引用 NuGet **`STS2.KitLib.Abstractions`**，可发布 `kitlib.compat.toml`，并通过 **`DevPanelRegistry`** 注册 dev 轨道标签。

准备安装？**[安装 →](/guide/install/)** · 面板概览：**[轨道面板 →](/guide/panels/)**
:::
