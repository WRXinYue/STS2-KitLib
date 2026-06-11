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
Slay the Spire 2 modding is still young. The game ships without a Steam Workshop, so mods are installed manually into the local `mods` folder, and the tooling ecosystem is being built in the open by the community.

This page points to the key community resources before diving into DevMode itself.
:::

::: zh-CN
《杀戮尖塔 2》模组生态目前仍处于早期阶段。游戏尚未接入 Steam 创意工坊，mod 需手动安装到本地 `mods` 目录，相关工具链也正由社区持续完善。

本页整理了进入 DevMode 正文之前值得了解的社区资源。
:::

## Community resources{lang="en"}

## 社区资源{lang="zh-CN"}

::: en
**[Slay the Spire 2 Modding Tutorials](https://glitchedreme.github.io/SlayTheSpire2ModdingTutorials/index.html)** (by Reme, CC BY-NC-SA 4.0)

The primary community tutorial site. Covers environment setup, common issues, BaseLib usage for cards, relics, potions, monsters, and events, as well as version migration guides and art replacement.

---

**[BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)** (by Alchyr)

The standard foundation library for STS2 mods. Provides base classes, utilities, abstractions, and diagnostic hooks that reduce boilerplate across the modding community. Available as a NuGet package. Most mods that add game content depend on it.

---

**[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)** (by BAKAOLC)

A practical authoring library that complements BaseLib without conflicting. Adds a player-facing mod settings UI, debug compatibility fallbacks, and utilities spanning cards, relics, audio, saves, localization, and patching.
:::

::: zh-CN
**[杀戮尖塔 2 模组制作教程](https://glitchedreme.github.io/SlayTheSpire2ModdingTutorials/index.html)** (作者：Reme，协议：CC BY-NC-SA 4.0)

社区主要教程站。涵盖环境搭建、常见问题、BaseLib 用于卡牌 / 遗物 / 药水 / 怪物 / 事件的用法，以及版本迁移指南与美术替换教程。

---

**[BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)** (作者：Alchyr)

STS2 模组的标准基础库。提供基类、工具方法、抽象层与诊断钩子，大幅减少重复代码。以 NuGet 包形式分发，绝大多数添加游戏内容的 mod 都依赖它。

---

**[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)** (作者：BAKAOLC)

与 BaseLib 互补的实用创作库，互不冲突。提供面向玩家的 mod 设置 UI、调试兼容性回退机制，以及横跨卡牌、遗物、音频、存档、本地化与补丁的实用工具集。
:::

## What DevMode is{lang="en"}

## DevMode 是什么{lang="zh-CN"}

::: en
DevMode is an **in-game developer tool**, not a modding framework. It does not provide base classes for content creation — use BaseLib and RitsuLib for that. What it provides is a **vertical rail** of panels attached to the main menu and in-run UI, letting you inspect and modify game state without restarting: add or remove cards, relics, potions, and powers; configure test setups from the main menu; run SpireScratch scripts; view in-game logs; and more.

Other mods can also register their own tabs into the DevMode rail via `DevPanelRegistry`. See **Developer → Dev panel registry** for details.

Ready to install? Head to **[Install →](/guide/install/)**.
:::

::: zh-CN
DevMode 是一个**游戏内开发者工具**，而非模组制作框架。它不提供用于创建游戏内容的基类——那是 BaseLib 和 RitsuLib 的职责。DevMode 提供的是附着于主菜单与对局界面的**竖向轨道面板**，让你无需重启即可检视和修改游戏状态：增删卡牌、遗物、药水、能力；在主菜单配置测试对局；运行 SpireScratch 脚本；查看游戏内日志等。

其他 mod 也可通过 `DevPanelRegistry` 向 DevMode 轨道注册自己的标签页，详见 **开发者 → 开发者面板注册**。

准备好安装了吗？前往 **[安装 →](/guide/install/)**。
:::
