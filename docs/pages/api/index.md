---
title:
  en: Extension API
  zh-CN: 扩展 API
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概览{lang="zh-CN"}

::: en
Public surfaces for content mods live in **KitLib.Abstractions** / **KitLib.Core**. Install only the products you need — missing product DLLs soft-skip.

### Modify the current run

Cheat-style mutations during a run (string ids; no STS2 model types on the public surface).

| Page | What it does |
| --- | --- |
| **[Cards](/api/cards/)** | Add/remove cards by pile — `RunInventoryBridge` |
| **[Relics](/api/relics/)** | Grant/remove relics — `RunInventoryBridge` |
| **[Potions](/api/potions/)** | Add potions / discard by slot — `RunInventoryBridge` |
| **[Powers](/api/power/)** | Apply / remove / clear combat powers — `PowerBridge` |
| **[Cheats & run stats](/api/runtime-cheat/)** | Toggle cheats and edit gold/HP/energy/… — `RuntimeCheatBridge` |

### Host utilities

| Page | What it does |
| --- | --- |
| **[Mod settings pages](/api/mod-settings/)** | Register pages + form builders (`ModSettingsUi`) — KitLib-native; Ritsu wins if both |
| **[Logging](/api/kitlib-log/)** | Write via STS2 `Logger`; tools subscribe/filter with `LogStreamHub` + `LogStreamFilters` |
:::

::: zh-CN
内容 mod 的公共接口在 **KitLib.Abstractions** / **KitLib.Core**。按需安装产品即可 — 缺少产品 DLL 时软跳过。

### 修改当前一局

局内作弊式修改（字符串 id；公共表面不暴露 STS2 模型类型）。

| 页面 | 做什么 |
| --- | --- |
| **[卡牌](/api/cards/)** | 按牌堆增删卡牌 — `RunInventoryBridge` |
| **[遗物](/api/relics/)** | 给予 / 移除遗物 — `RunInventoryBridge` |
| **[药水](/api/potions/)** | 加药水 / 按槽位丢弃 — `RunInventoryBridge` |
| **[Power](/api/power/)** | 施加 / 移除 / 清空战斗 Power — `PowerBridge` |
| **[作弊与局内数值](/api/runtime-cheat/)** | 开关作弊，改金币/生命/能量等 — `RuntimeCheatBridge` |

### 宿主工具

| 页面 | 做什么 |
| --- | --- |
| **[Mod 设置页](/api/mod-settings/)** | 注册页面 + 表单控件（`ModSettingsUi`）— KitLib 原生；与 Ritsu 并存时 Ritsu 优先 |
| **[日志](/api/kitlib-log/)** | 用 STS2 `Logger` 写；工具用 `LogStreamHub` + `LogStreamFilters` 订阅/筛选 |
:::
