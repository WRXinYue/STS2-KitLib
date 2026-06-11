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
DevMode attaches a **vertical rail** to the main menu and in-run UI. Each icon opens a **panel** (browser-style content area). Built-in tabs are registered in fixed order: **primary** (cards, relics, combat content, console, presets, hooks, scripts, logs) then **utility** (save/load, settings).

Use the **sidebar on this site** for one page per panel, in the same order as the in-game rail. Other mods can add tabs via `DevPanelRegistry`; see **Developer → Dev panel**.
:::

::: zh-CN
DevMode 会在**主菜单**与**对局内界面**旁挂上一条**竖向轨道**，每个图标对应一个**面板** (浏览器式内容区)。内置标签按固定顺序注册：先 **主分区** (卡牌、遗物、战斗向内容、控制台、预设、钩子、脚本、日志)，再 **工具分区** (存档 / 读档、设置)。

本站**左侧边栏**下列出与游戏内轨道**一致顺序**的各面板文档。其他 mod 可通过 `DevPanelRegistry` 增加标签；见 **开发者 → 开发者面板**。
:::

## Order{lang="en"}

## 顺序{lang="zh-CN"}

::: en
Tabs are sorted by `order` within each group. Built-in values:

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
| `devmode.scripts` | Scripts | Primary | 950 |
| `devmode.logs` | Logs | Primary | 960 |
| `devmode.save` | Save / Load | Utility | 100 |
| `devmode.settings` | Settings | Utility | 200 |

Third-party tabs insert between built-in ones by choosing an `order` value in the appropriate gap.
:::

::: zh-CN
标签在各分组内按 `order` 值升序排列。内置面板默认值如下：

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
| `devmode.scripts` | 脚本 | Primary | 950 |
| `devmode.logs` | 日志 | Primary | 960 |
| `devmode.save` | 存档 / 读档 | Utility | 100 |
| `devmode.settings` | 设置 | Utility | 200 |

第三方标签可通过选择合适的 `order` 值插入到内置面板之间。
:::
