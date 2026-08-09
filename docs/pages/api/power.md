---
title:
  en: Powers
  zh-CN: Power
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Apply, remove, or clear **combat powers** by **string model id** (for example `STRENGTH`). Requires an active combat.

| | |
| --- | --- |
| **Entry point** | `KitLib.RunInventory.PowerBridge` |
| **Types** | `KitLib.Abstractions.Host` |
| **Result** | `KitLibRunItemResult` (`Ok`, `Error`, `ItemId`) |
:::

::: zh-CN
用 **字符串模型 id**（例如 `STRENGTH`）**施加 / 移除 / 清空战斗 Power**。需要处于战斗中。

| | |
| --- | --- |
| **调用入口** | `KitLib.RunInventory.PowerBridge` |
| **类型** | `KitLib.Abstractions.Host` |
| **返回值** | `KitLibRunItemResult`（`Ok`、`Error`、`ItemId`） |
:::

## Basic usage{lang="en"}

## 基本用法{lang="zh-CN"}

::: en
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var add = await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "STRENGTH",
    Amount: 3,
    Target: KitLibPowerTarget.Self));

if (!add.Ok)
{
    // add.Error — e.g. not in combat, unknown id
    return;
}

await PowerBridge.TryRemovePower("STRENGTH");
await PowerBridge.TryClearPowers();
```
:::

::: zh-CN
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var add = await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "STRENGTH",
    Amount: 3,
    Target: KitLibPowerTarget.Self));

if (!add.Ok)
{
    // add.Error — 例如不在战斗、未知 id
    return;
}

await PowerBridge.TryRemovePower("STRENGTH");
await PowerBridge.TryClearPowers();
```
:::

## TryAddPower{lang="en"}

## TryAddPower{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryAddPower(KitLibAddPowerRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `PowerId` | `string` | — | Power model id |
| `Amount` | `int` | `1` | Must be `>= 1` |
| `Target` | `KitLibPowerTarget` | `Self` | Who receives the power |
| `TargetPlayerNetId` | `ulong?` | `null` | Which player's creature for `Self`; `null` = local |

### Examples

**Buff yourself**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "STRENGTH",
    Amount: 3,
    Target: KitLibPowerTarget.Self));
```

**Apply Vulnerable to all enemies**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "VULNERABLE",
    Amount: 2,
    Target: KitLibPowerTarget.AllEnemies));
```

**Buff allies**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "DEXTERITY",
    Amount: 1,
    Target: KitLibPowerTarget.Allies));
```

**Another player's Self target**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "STRENGTH",
    Amount: 2,
    Target: KitLibPowerTarget.Self,
    TargetPlayerNetId: otherNetId));
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryAddPower(KitLibAddPowerRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `PowerId` | `string` | — | Power 模型 id |
| `Amount` | `int` | `1` | 须 `>= 1` |
| `Target` | `KitLibPowerTarget` | `Self` | 施加目标 |
| `TargetPlayerNetId` | `ulong?` | `null` | `Self` 作用于哪名玩家；`null` = 本地 |

### 示例

**给自己加力量**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "STRENGTH",
    Amount: 3,
    Target: KitLibPowerTarget.Self));
```

**给全体敌人脆弱**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "VULNERABLE",
    Amount: 2,
    Target: KitLibPowerTarget.AllEnemies));
```

**给盟友加敏捷**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "DEXTERITY",
    Amount: 1,
    Target: KitLibPowerTarget.Allies));
```

**作用于其他玩家的 Self**

```csharp
await PowerBridge.TryAddPower(new KitLibAddPowerRequest(
    "STRENGTH",
    Amount: 2,
    Target: KitLibPowerTarget.Self,
    TargetPlayerNetId: otherNetId));
```
:::

## TryRemovePower{lang="en"}

## TryRemovePower{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryRemovePower(string powerId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryRemovePower(KitLibRemovePowerRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `PowerId` | `string` | — | Power model id to remove |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = local player |

### Examples

```csharp
await PowerBridge.TryRemovePower("STRENGTH");

await PowerBridge.TryRemovePower(
    new KitLibRemovePowerRequest("STRENGTH", TargetPlayerNetId: otherNetId));
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryRemovePower(string powerId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryRemovePower(KitLibRemovePowerRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `PowerId` | `string` | — | 要移除的 Power 模型 id |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = 本地玩家 |

### 示例

```csharp
await PowerBridge.TryRemovePower("STRENGTH");

await PowerBridge.TryRemovePower(
    new KitLibRemovePowerRequest("STRENGTH", TargetPlayerNetId: otherNetId));
```
:::

## TryClearPowers{lang="en"}

## TryClearPowers{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryClearPowers(ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryClearPowers(KitLibClearPowersRequest request)
```

Clears **all** powers on the target creature.

### Examples

```csharp
await PowerBridge.TryClearPowers();
await PowerBridge.TryClearPowers(otherNetId);
await PowerBridge.TryClearPowers(new KitLibClearPowersRequest(otherNetId));
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryClearPowers(ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryClearPowers(KitLibClearPowersRequest request)
```

清除目标生物身上的**全部** Power。

### 示例

```csharp
await PowerBridge.TryClearPowers();
await PowerBridge.TryClearPowers(otherNetId);
await PowerBridge.TryClearPowers(new KitLibClearPowersRequest(otherNetId));
```
:::

## Types{lang="en"}

## 类型{lang="zh-CN"}

::: en
### `KitLibPowerTarget`

| Value | Description |
| --- | --- |
| `Self` | The targeted player's creature |
| `AllEnemies` | All enemy creatures |
| `Allies` | Allied creatures |

### `KitLibRunItemResult`

| Member | Type | Description |
| --- | --- | --- |
| `Ok` | `bool` | Whether the operation succeeded |
| `Error` | `string?` | Failure message when `Ok` is `false` |
| `ItemId` | `string?` | Power id on success when available |

Multiplayer uses the same host/client cheat sync as [Cards](/api/cards/), [Relics](/api/relics/), and [Potions](/api/potions/).
:::

::: zh-CN
### `KitLibPowerTarget`

| 值 | 说明 |
| --- | --- |
| `Self` | 目标玩家的生物 |
| `AllEnemies` | 全体敌人 |
| `Allies` | 盟友 |

### `KitLibRunItemResult`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Ok` | `bool` | 是否成功 |
| `Error` | `string?` | `Ok` 为 `false` 时的错误信息 |
| `ItemId` | `string?` | 成功时为 power id（若有） |

多人同步方式与 [卡牌](/api/cards/)、[遗物](/api/relics/)、[药水](/api/potions/) 相同。
:::

## See also{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Cards](/api/cards/)
- [Cheats & run stats](/api/runtime-cheat/)
:::

::: zh-CN
- [卡牌](/api/cards/)
- [作弊与局内数值](/api/runtime-cheat/)
:::
