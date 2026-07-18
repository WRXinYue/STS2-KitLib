---
title:
  en: Rail panels
  zh-CN: 轨道面板
top: 9985
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## What this is{lang="en"}

## 说明{lang="zh-CN"}

::: en
**KitLib.Panel** attaches a **vertical rail** to the title screen and in-run UI. Hover the left-edge **peek tab** to expand it; each icon opens a panel. Built-in tabs group into **primary** (gameplay / automation / debug) and **utility** (save/load, settings).

Other mods add tabs via **`DevPanelRegistry`** — see **[Dev panel registry](/developer/extending/panel-registry/)**.

Title-screen entry: **[Title Dev Mode](/guide/title-dev-mode/)**. Panel overview also on the **[docs site](/guide/panels/)**.
:::

::: zh-CN
**KitLib.Panel** 在标题画面与局内提供**竖向轨道**。鼠标移到左侧 **peek 标签** 展开；每个图标打开一个面板。内置标签分为 **主分区**（玩法 / 自动化 / 调试）与 **工具分区**（存读档、设置）。

其他 mod 通过 **`DevPanelRegistry`** 添加标签 — 见 **[开发者面板注册](/developer/extending/panel-registry/)**。

标题入口：**[标题开发模式](/guide/title-dev-mode/)**。面板概览见 **[文档站](/guide/panels/)**。
:::

## Gameplay & content{lang="en"}

## 玩法与内容{lang="zh-CN"}

::: en

- **Cheats** — God mode, energy/block/stars, damage multipliers, enemy freeze, map overrides; some options limited in **multiplayer**
- **Cards** — Full library; filters (type, rarity, mod source, hidden cards); edit stats; add to piles
- **Relics / Powers / Potions** — Browse, spawn, auto-apply hooks; mod-source filters
- **Enemies / Events / Rooms** — Replace encounters, trigger events, jump room types
- **Presets** — Save/load combat and run snapshots

:::

::: zh-CN

- **作弊** — 无敌、能量/格挡/星星、伤害倍率、冻结敌人、地图覆盖；**联机**下部分受限
- **卡牌** — 全库浏览；筛选（类型、稀有度、Mod 来源、隐藏卡）；改数值；加入牌堆
- **遗物 / 能力 / 药水** — 浏览、生成、一键自动施加钩子；Mod 来源筛选
- **敌人 / 事件 / 房间** — 替换遭遇、触发事件、跳转房间
- **预设** — 战斗与 run 快照存读

:::

## Automation & AI{lang="en"}

## 自动化与 AI{lang="zh-CN"}

::: en

- **Hooks** — Trigger → Condition → Action rules
- **AI Host** — Solo autoplay (**StrongStrategy** default); disabled during multiplayer hand-play
- **MCP** — See **[MCP](/guide/mcp/)**
- **Dev viewer** — Browser logs at `http://127.0.0.1:9878/#/logs` (see **[Log export](/guide/mod-feedback/)**)

:::

::: zh-CN

- **钩子** — 触发器 → 条件 → 动作
- **AI 托管** — 单人自动（默认 **StrongStrategy**）；联机手打时禁用
- **MCP** — 见 **[MCP](/guide/mcp/)**
- **开发者控制台** — 浏览器日志 `http://127.0.0.1:9878/#/logs`（见 **[日志导出](/guide/mod-feedback/)**）

:::

## Developer & debug{lang="en"}

## 开发者与调试{lang="zh-CN"}

::: en

- **Logs** — Live + file history, filters, alerts; **Log Export** button in the header exports a ZIP — see **[Log export](/guide/mod-feedback/)**
- **Combat stats / Enemy intents** — Overlays and rails (optional, under **Settings → Game**)
- **Console** — Native + KitLib command reference

:::

::: zh-CN

- **日志** — 实时 + 文件历史、筛选、提醒；标题栏 **日志导出** 按钮可导出 ZIP — 见 **[日志导出](/guide/mod-feedback/)**
- **战斗统计 / 敌人意图** — Overlay 与 rail（**设置 → 游戏** 中可选）
- **控制台** — 原版与 KitLib 命令参考
- **Harmony 分析 / 框架** — 补丁检视、mod 快照（默认在 **设置 → 侧栏** 隐藏）

:::

## Built-in tab order{lang="en"}

## 内置标签顺序{lang="zh-CN"}

::: en
Tabs sort by `order` within each group. Third-party tabs pick an `order` in the gaps.

| ID | Panel | Group | Order |
| --- | --- | --- | --- |
| `devmode.cards` | Cards | Primary | 100 |
| `devmode.relics` | Relics | Primary | 200 |
| `devmode.enemies` | Enemies | Primary | 300 |
| `devmode.powers` | Powers | Primary | 400 |
| `devmode.potions` | Potions | Primary | 500 |
| `devmode.events` | Events | Primary | 600 |
| `devmode.rooms` | Rooms | Primary | 650 |
| `devmode.console` | Console | Primary | 700 |
| `devmode.presets` | Presets | Primary | 800 |
| `devmode.hooks` | Hooks | Primary | 900 |
| `devmode.logs` | Logs | Primary | 960 |
| `devmode.save` | Save / Load | Utility | 100 |
| `devmode.settings` | Settings | Utility | 200 |

:::

::: zh-CN
标签在各分组内按 `order` 排序。第三方标签在间隙中选择 `order`。

| ID | 面板 | 分组 | Order |
| --- | --- | --- | --- |
| `devmode.cards` | 卡牌 | Primary | 100 |
| `devmode.relics` | 遗物 | Primary | 200 |
| `devmode.enemies` | 敌人 | Primary | 300 |
| `devmode.powers` | 能力 | Primary | 400 |
| `devmode.potions` | 药水 | Primary | 500 |
| `devmode.events` | 事件 | Primary | 600 |
| `devmode.rooms` | 房间 | Primary | 650 |
| `devmode.console` | 控制台 | Primary | 700 |
| `devmode.presets` | 预设 | Primary | 800 |
| `devmode.hooks` | 钩子 | Primary | 900 |
| `devmode.logs` | 日志 | Primary | 960 |
| `devmode.save` | 存档 / 读档 | Utility | 100 |
| `devmode.settings` | 设置 | Utility | 200 |

:::
