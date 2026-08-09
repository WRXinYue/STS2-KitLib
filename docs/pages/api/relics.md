---
title:
  en: Relics
  zh-CN: 遗物
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Grant or remove relics in the **current run** by **string model id**.

| | |
| --- | --- |
| **Entry point** | `KitLib.RunInventory.RunInventoryBridge` |
| **Types** | `KitLib.Abstractions.Host` |
| **Result** | `KitLibRunItemResult` (`Ok`, `Error`, `ItemId`) |
:::

::: zh-CN
用 **字符串模型 id** 在**当前一局**里给予或移除遗物。

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

var add = await RunInventoryBridge.TryAddRelic("DRIFTWOOD");
if (!add.Ok)
{
    // add.Error
    return;
}

var remove = await RunInventoryBridge.TryRemoveRelic("DRIFTWOOD");
if (!remove.Ok)
{
    // remove.Error — e.g. player does not have it
}
```
:::

::: zh-CN
```csharp
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

var add = await RunInventoryBridge.TryAddRelic("DRIFTWOOD");
if (!add.Ok)
{
    // add.Error
    return;
}

var remove = await RunInventoryBridge.TryRemoveRelic("DRIFTWOOD");
if (!remove.Ok)
{
    // remove.Error — 例如玩家没有该遗物
}
```
:::

## TryAddRelic{lang="en"}

## TryAddRelic{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryAddRelic(string relicId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryAddRelic(KitLibRelicRequest request)
```

### Parameters

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `RelicId` | `string` | — | Relic model id |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = local player |

### Examples

**String overload**

```csharp
await RunInventoryBridge.TryAddRelic("DRIFTWOOD");
```

**Request object**

```csharp
await RunInventoryBridge.TryAddRelic(new KitLibRelicRequest("DRIFTWOOD"));
```

**Another player**

```csharp
await RunInventoryBridge.TryAddRelic(
    new KitLibRelicRequest("DRIFTWOOD", TargetPlayerNetId: otherNetId));

// or
await RunInventoryBridge.TryAddRelic("DRIFTWOOD", otherNetId);
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryAddRelic(string relicId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryAddRelic(KitLibRelicRequest request)
```

### 参数

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `RelicId` | `string` | — | 遗物模型 id |
| `TargetPlayerNetId` | `ulong?` | `null` | `null` = 本地玩家 |

### 示例

**字符串重载**

```csharp
await RunInventoryBridge.TryAddRelic("DRIFTWOOD");
```

**请求对象**

```csharp
await RunInventoryBridge.TryAddRelic(new KitLibRelicRequest("DRIFTWOOD"));
```

**指定其他玩家**

```csharp
await RunInventoryBridge.TryAddRelic(
    new KitLibRelicRequest("DRIFTWOOD", TargetPlayerNetId: otherNetId));

// 或
await RunInventoryBridge.TryAddRelic("DRIFTWOOD", otherNetId);
```
:::

## TryRemoveRelic{lang="en"}

## TryRemoveRelic{lang="zh-CN"}

::: en
```csharp
Task<KitLibRunItemResult> TryRemoveRelic(string relicId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryRemoveRelic(KitLibRelicRequest request)
```

Same parameters as `TryAddRelic`.

### Examples

```csharp
await RunInventoryBridge.TryRemoveRelic("DRIFTWOOD");

await RunInventoryBridge.TryRemoveRelic(
    new KitLibRelicRequest("DRIFTWOOD", TargetPlayerNetId: otherNetId));
```
:::

::: zh-CN
```csharp
Task<KitLibRunItemResult> TryRemoveRelic(string relicId, ulong? targetPlayerNetId = null)
Task<KitLibRunItemResult> TryRemoveRelic(KitLibRelicRequest request)
```

参数与 `TryAddRelic` 相同。

### 示例

```csharp
await RunInventoryBridge.TryRemoveRelic("DRIFTWOOD");

await RunInventoryBridge.TryRemoveRelic(
    new KitLibRelicRequest("DRIFTWOOD", TargetPlayerNetId: otherNetId));
```
:::

## Types{lang="en"}

## 类型{lang="zh-CN"}

::: en
### `KitLibRelicRequest`

| Member | Type | Description |
| --- | --- | --- |
| `RelicId` | `string` | Relic model id |
| `TargetPlayerNetId` | `ulong?` | Optional multiplayer target |

### `KitLibRunItemResult`

| Member | Type | Description |
| --- | --- | --- |
| `Ok` | `bool` | Whether the operation succeeded |
| `Error` | `string?` | Failure message when `Ok` is `false` |
| `ItemId` | `string?` | Related id on success when available |
:::

::: zh-CN
### `KitLibRelicRequest`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `RelicId` | `string` | 遗物模型 id |
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
- [Potions](/api/potions/)
- [Powers](/api/power/)
:::

::: zh-CN
- [卡牌](/api/cards/)
- [药水](/api/potions/)
- [Power](/api/power/)
:::
