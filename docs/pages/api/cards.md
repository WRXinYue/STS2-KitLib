---
title:
  en: Cards
  zh-CN: 卡牌
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Add or remove cards in the **current run** by **string model id**. Use this from content mods, MCP, or automation.

| | |
| --- | --- |
| **Entry point** | `KitLib.RunInventory.RunInventoryBridge` |
| **Types** | `KitLib.Abstractions.Host` |
| **Result** | `KitLibRunItemResult` (`Ok`, `Error`, `ItemId`) |

Staged edits in the in-game card browser still use internal `CardActions`. Prefer this bridge for programmatic changes.
:::

::: zh-CN
用 **字符串模型 id** 在**当前一局**里增删卡牌。适合内容 mod、MCP、自动化。

| | |
| --- | --- |
| **调用入口** | `KitLib.RunInventory.RunInventoryBridge` |
| **类型** | `KitLib.Abstractions.Host` |
| **返回值** | `KitLibRunItemResult`（`Ok`、`Error`、`ItemId`） |

局内卡牌浏览器的暂存编辑仍走内部 `CardActions`。程序化修改请用本桥接。
:::

## Basic usage{lang="en"}

## 基本用法{lang="zh-CN"}

::: en
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

// Add Strike+ to the deck
var result = await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "IRONCLAD_CARD_STRIKE",
    Pile: KitLibCardPile.Deck,
    UpgradeLevels: 1));

if (!result.Ok)
{
    // result.Error — e.g. unknown id, no active run
    return;
}

// Remove one Strike from the hand
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Hand,
    CardId: "IRONCLAD_CARD_STRIKE"));
```
:::

::: zh-CN
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

// 向牌组加入打击+
var result = await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "IRONCLAD_CARD_STRIKE",
    Pile: KitLibCardPile.Deck,
    UpgradeLevels: 1));

if (!result.Ok)
{
    // result.Error — 例如未知 id、当前无局
    return;
}

// 从手牌移除一张打击
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Hand,
    CardId: "IRONCLAD_CARD_STRIKE"));
```
:::

## TryAddCard{lang="en"}

## TryAddCard{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryAddCard(KitLibAddCardRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `CardId` | `string` | — | Card model id |
| `Pile` | `KitLibCardPile` | `Hand` | Destination pile |
| `Duration` | `KitLibCardDuration` | `Permanent` | Persist for the run, or temporary only |
| `UpgradeLevels` | `int` | `0` | Extra upgrades applied on add |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = local player |

### Examples

**Add to hand (defaults)**

```csharp
await RunInventoryBridge.TryAddCard(
    new KitLibAddCardRequest("IRONCLAD_CARD_STRIKE"));
```

**Permanent card into the deck, upgraded once**

```csharp
await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "IRONCLAD_CARD_BASH",
    Pile: KitLibCardPile.Deck,
    Duration: KitLibCardDuration.Permanent,
    UpgradeLevels: 1));
```

**Temporary card into draw pile**

```csharp
await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "COLORLESS_CARD_FLASH_OF_STEEL",
    Pile: KitLibCardPile.Draw,
    Duration: KitLibCardDuration.Temporary));
```

**Target another player (multiplayer)**

```csharp
await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "IRONCLAD_CARD_STRIKE",
    Pile: KitLibCardPile.Deck,
    TargetPlayerNetId: otherNetId));
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryAddCard(KitLibAddCardRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `CardId` | `string` | — | 卡牌模型 id |
| `Pile` | `KitLibCardPile` | `Hand` | 目标牌堆 |
| `Duration` | `KitLibCardDuration` | `Permanent` | 整局保留，或仅临时 |
| `UpgradeLevels` | `int` | `0` | 加入时额外升级层数 |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = 本地玩家 |

### 示例

**加入手牌（默认）**

```csharp
await RunInventoryBridge.TryAddCard(
    new KitLibAddCardRequest("IRONCLAD_CARD_STRIKE"));
```

**永久加入牌组，并升一级**

```csharp
await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "IRONCLAD_CARD_BASH",
    Pile: KitLibCardPile.Deck,
    Duration: KitLibCardDuration.Permanent,
    UpgradeLevels: 1));
```

**临时牌加入抽牌堆**

```csharp
await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "COLORLESS_CARD_FLASH_OF_STEEL",
    Pile: KitLibCardPile.Draw,
    Duration: KitLibCardDuration.Temporary));
```

**指定其他玩家（多人）**

```csharp
await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
    "IRONCLAD_CARD_STRIKE",
    Pile: KitLibCardPile.Deck,
    TargetPlayerNetId: otherNetId));
```
:::

## TryRemoveCard{lang="en"}

## TryRemoveCard{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryRemoveCard(KitLibRemoveCardRequest request)
```

Provide **`CardId`**, **`PileIndex`**, or both.

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `Pile` | `KitLibCardPile` | — | Pile to search |
| `CardId` | `string?` | `null` | Match by model id |
| `PileIndex` | `int?` | `null` | Match by index in that pile |
| `RemoveFromRun` | `bool` | `true` | Also drop from run state when applicable |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = local player |

### Examples

**Remove by model id**

```csharp
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Deck,
    CardId: "IRONCLAD_CARD_STRIKE"));
```

**Remove the card at index 0 in hand**

```csharp
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Hand,
    PileIndex: 0));
```

**Combat-only remove (keep in run master deck when applicable)**

```csharp
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Hand,
    CardId: "IRONCLAD_CARD_DEFEND",
    RemoveFromRun: false));
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryRemoveCard(KitLibRemoveCardRequest request)
```

提供 **`CardId`**、**`PileIndex`**，或两者都提供。

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `Pile` | `KitLibCardPile` | — | 在哪个牌堆查找 |
| `CardId` | `string?` | `null` | 按模型 id 匹配 |
| `PileIndex` | `int?` | `null` | 按该牌堆下标匹配 |
| `RemoveFromRun` | `bool` | `true` | 适用时从局内状态一并去掉 |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = 本地玩家 |

### 示例

**按模型 id 移除**

```csharp
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Deck,
    CardId: "IRONCLAD_CARD_STRIKE"));
```

**移除手牌第 0 张**

```csharp
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Hand,
    PileIndex: 0));
```

**仅战斗内移除（适用时保留局内主牌组）**

```csharp
await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
    KitLibCardPile.Hand,
    CardId: "IRONCLAD_CARD_DEFEND",
    RemoveFromRun: false));
```
:::

## Types{lang="en"}

## 类型{lang="zh-CN"}

::: en
### `KitLibCardPile`

| Value | Description |
| --- | --- |
| `Deck` | Master / run deck |
| `Hand` | Current hand |
| `Draw` | Draw pile |
| `Discard` | Discard pile |
| `Exhaust` | Exhaust pile |

### `KitLibCardDuration`

| Value | Description |
| --- | --- |
| `Permanent` | Stays for the run |
| `Temporary` | Combat / temporary only |

### `KitLibRunItemResult`

| Member | Type | Description |
| --- | --- | --- |
| `Ok` | `bool` | Whether the operation succeeded |
| `Error` | `string?` | Failure message when `Ok` is `false` |
| `ItemId` | `string?` | Related id on success when available |

Always check `Ok` before assuming the mutation applied.
:::

::: zh-CN
### `KitLibCardPile`

| 值 | 说明 |
| --- | --- |
| `Deck` | 局内主牌组 |
| `Hand` | 当前手牌 |
| `Draw` | 抽牌堆 |
| `Discard` | 弃牌堆 |
| `Exhaust` | 消耗堆 |

### `KitLibCardDuration`

| 值 | 说明 |
| --- | --- |
| `Permanent` | 整局保留 |
| `Temporary` | 仅战斗 / 临时 |

### `KitLibRunItemResult`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `Ok` | `bool` | 是否成功 |
| `Error` | `string?` | `Ok` 为 `false` 时的错误信息 |
| `ItemId` | `string?` | 成功时相关 id（若有） |

先检查 `Ok`，再假定修改已生效。
:::

## See also{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Relics](/api/relics/)
- [Potions](/api/potions/)
- [Powers](/api/power/)
- [Cheats & run stats](/api/runtime-cheat/)
:::

::: zh-CN
- [遗物](/api/relics/)
- [药水](/api/potions/)
- [Power](/api/power/)
- [作弊与局内数值](/api/runtime-cheat/)
:::
