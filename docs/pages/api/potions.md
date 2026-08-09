---
title:
  en: Potions
  zh-CN: 药水
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Add a potion to a free belt slot, or discard by **slot index**, in the **current run**.

| | |
| --- | --- |
| **Entry point** | `KitLib.RunInventory.RunInventoryBridge` |
| **Types** | `KitLib.Abstractions.Host` |
| **Result** | `KitLibRunItemResult` (`Ok`, `Error`, `ItemId`) |
:::

::: zh-CN
在**当前一局**向空槽加入药水，或按**槽位下标**丢弃。

| | |
| --- | --- |
| **调用入口** | `KitLib.RunInventory.RunInventoryBridge` |
| **类型** | `KitLib.Abstractions.Host` |
| **返回值** | `KitLibRunItemResult`（`Ok`、`Error`、`ItemId`） |
:::

## Basic usage{lang="en"}

## 基本用法{lang="zh-CN"}

::: en
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var add = await RunInventoryBridge.TryAddPotion("POTION_SHAPED_ROCK");
if (!add.Ok)
{
    // add.Error — e.g. belt full, unknown id
    return;
}

// Discard the potion in the first slot (index 0)
await RunInventoryBridge.TryDiscardPotionAtSlot(0);
```
:::

::: zh-CN
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var add = await RunInventoryBridge.TryAddPotion("POTION_SHAPED_ROCK");
if (!add.Ok)
{
    // add.Error — 例如栏位已满、未知 id
    return;
}

// 丢弃第 0 槽的药水
await RunInventoryBridge.TryDiscardPotionAtSlot(0);
```
:::

## TryAddPotion{lang="en"}

## TryAddPotion{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryAddPotion(string potionId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryAddPotion(KitLibPotionAddRequest request)
```

Fills the next free potion-belt slot.

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `PotionId` | `string` | — | Potion model id |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = local player |

### Examples

**String overload**

```csharp
await RunInventoryBridge.TryAddPotion("POTION_SHAPED_ROCK");
```

**Request object**

```csharp
await RunInventoryBridge.TryAddPotion(
    new KitLibPotionAddRequest("POTION_SHAPED_ROCK"));
```

**Another player**

```csharp
await RunInventoryBridge.TryAddPotion(
    new KitLibPotionAddRequest("POTION_SHAPED_ROCK", TargetPlayerNetId: otherNetId));

// or
await RunInventoryBridge.TryAddPotion("POTION_SHAPED_ROCK", otherNetId);
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryAddPotion(string potionId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryAddPotion(KitLibPotionAddRequest request)
```

占用下一个空的药水栏槽位。

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `PotionId` | `string` | — | 药水模型 id |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = 本地玩家 |

### 示例

**字符串重载**

```csharp
await RunInventoryBridge.TryAddPotion("POTION_SHAPED_ROCK");
```

**请求对象**

```csharp
await RunInventoryBridge.TryAddPotion(
    new KitLibPotionAddRequest("POTION_SHAPED_ROCK"));
```

**指定其他玩家**

```csharp
await RunInventoryBridge.TryAddPotion(
    new KitLibPotionAddRequest("POTION_SHAPED_ROCK", TargetPlayerNetId: otherNetId));

// 或
await RunInventoryBridge.TryAddPotion("POTION_SHAPED_ROCK", otherNetId);
```
:::

## TryDiscardPotionAtSlot{lang="en"}

## TryDiscardPotionAtSlot{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryDiscardPotionAtSlot(int slotIndex, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryDiscardPotionAtSlot(KitLibPotionDiscardRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `SlotIndex` | `int` | — | Zero-based index on the potion belt |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = local player |

### Examples

**Discard slot 0**

```csharp
await RunInventoryBridge.TryDiscardPotionAtSlot(0);
```

**Discard slot 2 for another player**

```csharp
await RunInventoryBridge.TryDiscardPotionAtSlot(
    new KitLibPotionDiscardRequest(2, TargetPlayerNetId: otherNetId));

// or
await RunInventoryBridge.TryDiscardPotionAtSlot(2, otherNetId);
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryDiscardPotionAtSlot(int slotIndex, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryDiscardPotionAtSlot(KitLibPotionDiscardRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `SlotIndex` | `int` | — | 药水栏从 0 开始的下标 |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = 本地玩家 |

### 示例

**丢弃第 0 槽**

```csharp
await RunInventoryBridge.TryDiscardPotionAtSlot(0);
```

**为其他玩家丢弃第 2 槽**

```csharp
await RunInventoryBridge.TryDiscardPotionAtSlot(
    new KitLibPotionDiscardRequest(2, TargetPlayerNetId: otherNetId));

// 或
await RunInventoryBridge.TryDiscardPotionAtSlot(2, otherNetId);
```
:::

## Types{lang="en"}

## 类型{lang="zh-CN"}

::: en
### `KitLibPotionAddRequest`

| Member | Type | Description |
| --- | --- | --- |
| `PotionId` | `string` | Potion model id |
| `TargetPlayerNetId` | `ulong?` | Optional multiplayer target |

### `KitLibPotionDiscardRequest`

| Member | Type | Description |
| --- | --- | --- |
| `SlotIndex` | `int` | Zero-based belt slot |
| `TargetPlayerNetId` | `ulong?` | Optional multiplayer target |

### `KitLibRunItemResult`

| Member | Type | Description |
| --- | --- | --- |
| `Ok` | `bool` | Whether the operation succeeded |
| `Error` | `string?` | Failure message when `Ok` is `false` |
| `ItemId` | `string?` | Related id on success when available |
:::

::: zh-CN
### `KitLibPotionAddRequest`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `PotionId` | `string` | 药水模型 id |
| `TargetPlayerNetId` | `ulong?` | 可选多人目标 |

### `KitLibPotionDiscardRequest`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `SlotIndex` | `int` | 药水栏从 0 开始的下标 |
| `TargetPlayerNetId` | `ulong?` | 可选多人目标 |

### `KitLibRunItemResult`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Ok` | `bool` | 是否成功 |
| `Error` | `string?` | `Ok` 为 `false` 时的错误信息 |
| `ItemId` | `string?` | 成功时相关 id（若有） |
:::

## See also{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Cards](/api/cards/)
- [Relics](/api/relics/)
- [Cheats & run stats](/api/runtime-cheat/) — includes `PotionSlots` via `TrySetStat`
:::

::: zh-CN
- [卡牌](/api/cards/)
- [遗物](/api/relics/)
- [作弊与局内数值](/api/runtime-cheat/) — 可用 `TrySetStat` 调整 `PotionSlots`
:::
