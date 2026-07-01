---
title:
  en: Combat add-monster mod compat
  zh-CN: 战斗中加怪 mod 兼容
top: 9600
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
When **LustTravel2** and KitLib run together, **Add monster** in the dev panel can hang at `CreatureCmd.Add starting`. Root cause, fixes, and verification are documented in Chinese below.
:::

::: zh-CN
狐妖 mod（**LustTravel2**）+ DevMode 同时启用时，Dev 面板 **加怪物** 可能卡住；日志停在 `CreatureCmd.Add starting`。单独使用 DevMode 或移除其他 mod 后正常。
:::

## 原因（摘要）{lang="zh-CN"}

::: zh-CN

DevMode 调用原版 `CreatureCmd.Add`，与地图跳转无关。

| 因素 | 说明 |
| --- | --- |
| **LustTravel2 敌人耐力条**（狐妖局） | 旧版在 `CreatureCmd.Add` Postfix 里对 `PowerCmd` 使用 `GetResult()`，阻塞 `await CreatureCmd.Add`。已在 LustTravel2 改为 `TaskHelper.RunSafely` + `EnsureOnCreatureAsync`。 |
| **未缓存怪物场景** | `creature_visuals/*.tscn` 在主线程同步加载会顿挫。DevMode 在 `CombatEnemyActions.TryPreloadMonsterVisualsAsync` 中做 threaded preload。 |
:::

## KitLib 侧{lang="zh-CN"}

::: zh-CN

- 实现：`src/Actions/CombatEnemyActions.cs`
- 诊断日志：`[KitLib.CombatAdd]`（`begin` / `CreatureCmd.Add starting` / `done` / `success`）
:::

## LustTravel2 侧（详细）{lang="zh-CN"}

::: zh-CN

见并列仓库文档：[mid-combat-creature-add-async-pitfalls.md](../../LustTravel2/docs/mid-combat-creature-add-async-pitfalls.md)（`STS2/LustTravel2/docs/`）

内容包括：`CallDeferred` vs `RunSafely`、为何 defer 不能替代 `await PowerCmd`、验证清单。
:::

## 用户建议{lang="zh-CN"}

::: zh-CN

- 狐妖局加怪卡住：更新 **LustTravel2** 至含方案 A 的版本后再试。
- 仍顿挫：多为冷加载场景，属顿挫非死锁；可多等或预先加过同种怪以命中缓存。
:::
