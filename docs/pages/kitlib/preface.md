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

**[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)** — player-facing mod settings UI; KitLib bridges via **KitModPanel**.
:::

::: zh-CN
**[杀戮尖塔 2 模组制作教程](https://glitchedreme.github.io/SlayTheSpire2ModdingTutorials/index.html)**（Reme，CC BY-NC-SA 4.0）— 环境搭建、BaseLib 内容 mod、迁移指南。

**[BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)** — STS2 内容 mod 常用基础库（NuGet）。

**[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)** — 玩家向 Mod 设置 UI；KitLib 经 **KitModPanel** 桥接。
:::

## What KitLib is{lang="en"}

## KitLib 是什么{lang="zh-CN"}

::: en
**KitLib** is a **foundation library and host** for STS2. Other mods declare it in the manifest, reference **`STS2.KitLib.Abstractions`**, and call the **[Extension API](/api/)** — the same pattern as RitsuLib / BaseLib.

- **KitLib**: host, logging, progress helpers, mutation APIs, corner-button registry.
- **Sibling mods** (own READMEs under `mods/`): `KitModPanel`, `KitDevTools`.

Ready to install? **[Install →](/kitlib/install/)** · **[Extension API →](/api/)** · **[Architecture →](/kitlib/architecture/)**
:::

::: zh-CN
**KitLib** 是 STS2 的**基础库和宿主**。其他 mod 在清单里声明它、引用 **`STS2.KitLib.Abstractions`**、调用 **[扩展 API](/api/)** — 与 RitsuLib / BaseLib 相同。

- **KitLib**：宿主、日志、进度辅助、突变 API、角标注册。
- **兄弟产品**（各自 README 在 `mods/`）：`KitModPanel`、`KitDevTools`。

准备安装？**[安装 →](/kitlib/install/)** · **[扩展 API →](/api/)** · **[架构 →](/kitlib/architecture/)**
:::
