---
title:
  en: Cheats & run stats
  zh-CN: 作弊与局内数值
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Toggle **cheats / multipliers**, and edit **run stats** (gold, HP, energy, …). Cheat ids match MCP `dev_set_cheat` / `dev_set_stat`.

| | |
| --- | --- |
| **Entry point** | `KitLib.RunInventory.RuntimeCheatBridge` |
| **Types** | `KitLib.Abstractions.Host` |
:::

::: zh-CN
开关**作弊项 / 倍率**，并修改**局内数值**（金币、生命、能量等）。作弊 id 与 MCP `dev_set_cheat` / `dev_set_stat` 一致。

| | |
| --- | --- |
| **调用入口** | `KitLib.RunInventory.RuntimeCheatBridge` |
| **类型** | `KitLib.Abstractions.Host` |
:::

## Basic usage{lang="en"}

## 基本用法{lang="zh-CN"}

::: en
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var cheat = RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("god_mode", Enabled: true));
if (!cheat.Ok)
{
    // cheat.Error
    return;
}

RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("damage_multiplier", Value: 2f));

var gold = RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.Gold, 999, LockEnabled: true));
if (!gold.Ok)
{
    // gold.Error
}
```
:::

::: zh-CN
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var cheat = RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("god_mode", Enabled: true));
if (!cheat.Ok)
{
    // cheat.Error
    return;
}

RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("damage_multiplier", Value: 2f));

var gold = RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.Gold, 999, LockEnabled: true));
if (!gold.Ok)
{
    // gold.Error
}
```
:::

## TrySetCheat{lang="en"}

## TrySetCheat{lang="zh-CN"}

::: en
```csharp
KitLibCheatOpResult TrySetCheat(KitLibSetCheatRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `Cheat` | `string` | — | Cheat id (same as MCP) |
| `Enabled` | `bool?` | `null` | Omit to toggle; set `true` / `false` to force |
| `Value` | `float?` | `null` | Used by multipliers / `extra_draw` |

### Cheat ids

| Kind | Examples |
| --- | --- |
| Boolean patch toggles | `infinite_hp`, `infinite_block`, `infinite_energy`, `infinite_stars`, `freeze_enemies`, `free_shop`, `always_potion`, `always_upgrade`, `max_rarity`, `unknown_treasure`, `max_score` |
| Multipliers (`Value`) | `damage_multiplier`, `defense_multiplier`, `gold_multiplier`, `score_multiplier`, `game_speed` |
| Runtime toggles | `god_mode`, `kill_all`, `runtime_infinite_energy`, `always_player_turn`, `draw_to_hand_limit`, `extra_draw`, `auto_ally`, `negate_debuffs` |

Frame / runtime toggles are **not** applied via this API in multiplayer (returns an error).

### Examples

**Enable god mode**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("god_mode", Enabled: true));
```

**Toggle freeze enemies**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("freeze_enemies"));
```

**Set damage multiplier**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("damage_multiplier", Value: 2.5f));
```

**Extra draw amount**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("extra_draw", Enabled: true, Value: 2f));
```
:::

::: zh-CN
```csharp
KitLibCheatOpResult TrySetCheat(KitLibSetCheatRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `Cheat` | `string` | — | 作弊 id（与 MCP 相同） |
| `Enabled` | `bool?` | `null` | 省略为切换；传入 `true` / `false` 为强制设定 |
| `Value` | `float?` | `null` | 倍率 / `extra_draw` 使用 |

### 作弊 id

| 类型 | 示例 |
| --- | --- |
| 布尔补丁开关 | `infinite_hp`、`infinite_block`、`infinite_energy`、`infinite_stars`、`freeze_enemies`、`free_shop`、`always_potion`、`always_upgrade`、`max_rarity`、`unknown_treasure`、`max_score` |
| 倍率（`Value`） | `damage_multiplier`、`defense_multiplier`、`gold_multiplier`、`score_multiplier`、`game_speed` |
| 运行时开关 | `god_mode`、`kill_all`、`runtime_infinite_energy`、`always_player_turn`、`draw_to_hand_limit`、`extra_draw`、`auto_ally`、`negate_debuffs` |

帧级 / 运行时开关在**多人**下不通过本 API 生效（返回错误）。

### 示例

**开启无敌**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("god_mode", Enabled: true));
```

**切换冻结敌人**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("freeze_enemies"));
```

**设置伤害倍率**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("damage_multiplier", Value: 2.5f));
```

**额外抽牌数量**

```csharp
RuntimeCheatBridge.TrySetCheat(
    new KitLibSetCheatRequest("extra_draw", Enabled: true, Value: 2f));
```
:::

## TrySetStat{lang="en"}

## TrySetStat{lang="zh-CN"}

::: en
```csharp
KitLibStatOpResult TrySetStat(KitLibSetStatRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `Stat` | `KitLibRunStat` | — | Which value to edit |
| `Value` | `int` | — | New value |
| `LockEnabled` | `bool?` | `null` | Hold the value while locked (when supported) |

### `KitLibRunStat`

| Value | Notes |
| --- | --- |
| `Gold` | Can lock |
| `CurrentHp` / `MaxHp` | Can lock |
| `CurrentEnergy` / `MaxEnergy` | Can lock |
| `Stars` | Can lock |
| `OrbSlots` | Can lock base orb slots |
| `PotionSlots` | Resize belt; not lockable via this API |

Stat edits are **not** supported via this API in multiplayer (returns an error).

### Examples

**Set gold and lock it**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.Gold, 999, LockEnabled: true));
```

**Full heal**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.CurrentHp, 999));
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.MaxHp, 999));
```

**Combat energy**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.CurrentEnergy, 99));
```

**More potion slots**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.PotionSlots, 5));
```
:::

::: zh-CN
```csharp
KitLibStatOpResult TrySetStat(KitLibSetStatRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `Stat` | `KitLibRunStat` | — | 要修改的数值 |
| `Value` | `int` | — | 新值 |
| `LockEnabled` | `bool?` | `null` | 支持时锁定该值 |

### `KitLibRunStat`

| 值 | 说明 |
| --- | --- |
| `Gold` | 可锁定 |
| `CurrentHp` / `MaxHp` | 可锁定 |
| `CurrentEnergy` / `MaxEnergy` | 可锁定 |
| `Stars` | 可锁定 |
| `OrbSlots` | 可锁定基础充能球槽 |
| `PotionSlots` | 调整药水栏容量；本 API 不支持锁定 |

局内数值修改在**多人**下不通过本 API 生效（返回错误）。

### 示例

**设置金币并锁定**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.Gold, 999, LockEnabled: true));
```

**回满生命**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.CurrentHp, 999));
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.MaxHp, 999));
```

**战斗能量**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.CurrentEnergy, 99));
```

**增加药水栏**

```csharp
RuntimeCheatBridge.TrySetStat(
    new KitLibSetStatRequest(KitLibRunStat.PotionSlots, 5));
```
:::

## Types{lang="en"}

## 类型{lang="zh-CN"}

::: en
### `KitLibCheatOpResult`

| Member | Type | Description |
| --- | --- | --- |
| `Ok` | `bool` | Whether the operation succeeded |
| `Error` | `string?` | Failure message |
| `Cheat` | `string?` | Cheat id |
| `Enabled` | `bool?` | Resulting enabled state when applicable |
| `Value` | `float?` | Resulting value when applicable |

### `KitLibStatOpResult`

| Member | Type | Description |
| --- | --- | --- |
| `Ok` | `bool` | Whether the operation succeeded |
| `Error` | `string?` | Failure message |
| `Stat` | `string?` | Stat name |
| `Value` | `int` | Applied value |
| `Locked` | `bool` | Whether the lock is on |
:::

::: zh-CN
### `KitLibCheatOpResult`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Ok` | `bool` | 是否成功 |
| `Error` | `string?` | 错误信息 |
| `Cheat` | `string?` | 作弊 id |
| `Enabled` | `bool?` | 适用时的开关状态 |
| `Value` | `float?` | 适用时的数值 |

### `KitLibStatOpResult`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Ok` | `bool` | 是否成功 |
| `Error` | `string?` | 错误信息 |
| `Stat` | `string?` | 数值名 |
| `Value` | `int` | 已应用的值 |
| `Locked` | `bool` | 是否锁定 |
:::

## See also{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Cards](/api/cards/)
- [Potions](/api/potions/)
- [Powers](/api/power/)
:::

::: zh-CN
- [卡牌](/api/cards/)
- [药水](/api/potions/)
- [Power](/api/power/)
:::
