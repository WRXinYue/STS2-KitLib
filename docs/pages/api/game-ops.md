---
title:
  en: Game ops
  zh-CN: 游戏操作
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Read a **lean** run snapshot and drive **player UI** (play a card, pick a map node, collect rewards, …). Use this from content mods, MCP, or automation. Ships in **KitLib Core**.

| | |
| --- | --- |
| **Entry point** | `KitLib.Host.KitLibGameOps` |
| **Types** | `KitLib.Game` (`GameAction`, `ActionType`, `SelectionHint`, `ActionResult`, `GamePhase`) |
| **Result** | `ActionResult` (`Success`, `Message`) |

This is **not** a cheat API. It clicks the same screens a player would. For adding cards or toggling cheats, use [Cards](/api/cards/) / [Cheats](/api/runtime-cheat/).

Lean snapshots omit AI knowledge (`mechanicFlags`, `MonsterMechanicIndex`, scoring).
:::

::: zh-CN
读取 **lean** 局面，并驱动**玩家 UI**（出牌、选图、领奖励等）。适合内容 mod、MCP、自动化。在 **KitLib Core** 中提供。

| | |
| --- | --- |
| **调用入口** | `KitLib.Host.KitLibGameOps` |
| **类型** | `KitLib.Game`（`GameAction`、`ActionType`、`SelectionHint`、`ActionResult`、`GamePhase`） |
| **返回值** | `ActionResult`（`Success`、`Message`） |

这**不是**作弊 API，走的是玩家会点的同一套界面。加牌或开关作弊请用 [卡牌](/api/cards/) / [作弊](/api/runtime-cheat/)。

Lean 快照不含 AI 知识库（`mechanicFlags`、`MonsterMechanicIndex`、打分）。
:::

## Basic usage{lang="en"}

## 基本用法{lang="zh-CN"}

::: en
Call from the **Godot main thread** (or use `Execute` / `PickSelection`, which marshal onto it). `Snapshot()` and `SelectionState()` are synchronous and do **not** marshal.

```csharp
using KitLib.Game;
using KitLib.Host;

var snap = KitLibGameOps.Snapshot();
if (snap == null)
    return; // no active run

var result = await KitLibGameOps.Execute(new GameAction {
    Type = ActionType.PlayCard,
    TargetIndex = 0,       // hand index
    SecondaryIndex = 1,    // enemy combat index, or -1
});

if (!result.Success) {
    // result.Message — e.g. not in play phase, pending_selection
    return;
}
```
:::

::: zh-CN
在 **Godot 主线程**调用（或使用会切到主线程的 `Execute` / `PickSelection`）。`Snapshot()` 与 `SelectionState()` 是同步的，**不会**切线程。

```csharp
using KitLib.Game;
using KitLib.Host;

var snap = KitLibGameOps.Snapshot();
if (snap == null)
    return; // 当前无局

var result = await KitLibGameOps.Execute(new GameAction {
    Type = ActionType.PlayCard,
    TargetIndex = 0,       // 手牌下标
    SecondaryIndex = 1,    // 敌人战斗下标，或 -1
});

if (!result.Success) {
    // result.Message — 例如不在出牌阶段、pending_selection
    return;
}
```
:::

## Snapshot{lang="en"}

## Snapshot{lang="zh-CN"}

::: en
```csharp
JsonObject? Snapshot()
```

Returns `null` when there is no run/player. Otherwise a JSON object. Always includes:

| Field | Type | Notes |
| --- | --- | --- |
| `phase` | `string` | `GamePhase` name (`Combat`, `MapSelection`, …) |
| `totalFloor`, `actIndex`, `actFloor` | number | Run progress |
| `gold`, `currentHp`, `maxHp` | number | Local player |
| `characterId` | string | Character model id |
| `ascensionLevel` | number | |
| `hasOpenPotionSlots`, `potionSlotCount` | bool / number | Potion belt |
| `deck[]` | cards | `id`, `name`, `cost`, `targetType` |
| `relics[]` | `{ id, name }` | |
| `potions[]` | `{ id, slot }` | Occupied slots only |
| `roomType` | string | When a room is active |

In combat, `combat` includes energy, `hand[]` (`index`, `canPlay`, …), `enemies[]` (`index`, `monsterId`, HP, `intents`, `intentDamage`, `powers`), `playerPowers[]`. Enemy entries do **not** include `mechanicFlags`.

Phase extras (when that screen is up): `mapNodes[]`, `offeredCards[]`, `eventOptions[]`, `shopOffers[]`, `restOptions[]`, `offeredRelics[]`, `rewardsHaveCollectable`.
:::

::: zh-CN
```csharp
JsonObject? Snapshot()
```

无局/无玩家时返回 `null`，否则为 JSON。始终包含：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `phase` | `string` | `GamePhase` 名（`Combat`、`MapSelection` 等） |
| `totalFloor`、`actIndex`、`actFloor` | number | 进度 |
| `gold`、`currentHp`、`maxHp` | number | 本地玩家 |
| `characterId` | string | 角色模型 id |
| `ascensionLevel` | number | |
| `hasOpenPotionSlots`、`potionSlotCount` | bool / number | 药水栏 |
| `deck[]` | 卡牌 | `id`、`name`、`cost`、`targetType` |
| `relics[]` | `{ id, name }` | |
| `potions[]` | `{ id, slot }` | 仅已占用槽 |
| `roomType` | string | 当前房间有值时 |

战斗中 `combat` 含能量、`hand[]`（`index`、`canPlay` 等）、`enemies[]`（`index`、`monsterId`、HP、`intents`、`intentDamage`、`powers`）、`playerPowers[]`。敌人**不含** `mechanicFlags`。

对应界面打开时还有：`mapNodes[]`、`offeredCards[]`、`eventOptions[]`、`shopOffers[]`、`restOptions[]`、`offeredRelics[]`、`rewardsHaveCollectable`。
:::

## Execute{lang="en"}

## Execute{lang="zh-CN"}

::: en
```csharp
Task<ActionResult> Execute(GameAction action, SelectionHint? hint = null)
```

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `action.Type` | `ActionType` | — | What to do |
| `action.TargetIndex` | `int` | `-1` | Primary index (hand, map node, reward, …) |
| `action.SecondaryIndex` | `int` | `-1` | Extra index (enemy for a targeted card/potion) |
| `action.Reason` | `string?` | `null` | Optional log/debug note |
| `hint` | `SelectionHint?` | `null` | Auto-pick on a nested card picker during `PlayCard` |

`Execute` always runs on the Godot main thread.

### ActionType → indices

| Type | `TargetIndex` | `SecondaryIndex` |
| --- | --- | --- |
| `PlayCard` | Hand index | Enemy combat index (`-1` if untargeted) |
| `UsePotion` | Potion slot | Enemy combat index if single-target |
| `DiscardPotion` | Potion slot | — |
| `SelectMapNode` | Available map node index | — |
| `PickCardReward` / `PickRelic` / `SelectEventChoice` / `PurchaseShopItem` / `Rest` / `UpgradeCard` / `CollectReward` | Option index | — |
| `EndTurn`, `SkipCardReward`, `LeaveShop`, `Proceed`, `DismissRewards`, `HandleTreasureRoom`, `AdvanceOverlay`, `PressConfirm`, `Wait` | ignored | — |

Potion **rewards**: if the belt is full, `CollectReward` **skips** that button (no AI scoring / discard-to-make-room).

If `PlayCard` opens a pile/hand picker and `hint` does not resolve it, `Success` is false and `Message` is `pending_selection`. Then call `SelectionState` / `PickSelection`, or pass `hint` on the next `PlayCard`.

```csharp
await KitLibGameOps.Execute(
    new GameAction { Type = ActionType.PlayCard, TargetIndex = 2, SecondaryIndex = 0 },
    new SelectionHint { CardId = "SOME_CARD_ID" });
```
:::

::: zh-CN
```csharp
Task<ActionResult> Execute(GameAction action, SelectionHint? hint = null)
```

| 名称 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `action.Type` | `ActionType` | — | 要做的事 |
| `action.TargetIndex` | `int` | `-1` | 主下标（手牌、地图节点、奖励等） |
| `action.SecondaryIndex` | `int` | `-1` | 附加下标（指向敌人的牌/药水） |
| `action.Reason` | `string?` | `null` | 可选日志说明 |
| `hint` | `SelectionHint?` | `null` | `PlayCard` 过程中弹出选牌时自动点选 |

`Execute` 始终在 Godot 主线程执行。

### ActionType → 下标

| 类型 | `TargetIndex` | `SecondaryIndex` |
| --- | --- | --- |
| `PlayCard` | 手牌下标 | 敌人战斗下标（无目标为 `-1`） |
| `UsePotion` | 药水槽 | 单体目标时的敌人下标 |
| `DiscardPotion` | 药水槽 | — |
| `SelectMapNode` | 当前可点地图节点下标 | — |
| `PickCardReward` / `PickRelic` / `SelectEventChoice` / `PurchaseShopItem` / `Rest` / `UpgradeCard` / `CollectReward` | 选项下标 | — |
| `EndTurn`、`SkipCardReward`、`LeaveShop`、`Proceed`、`DismissRewards`、`HandleTreasureRoom`、`AdvanceOverlay`、`PressConfirm`、`Wait` | 忽略 | — |

药水**奖励**：栏满时 `CollectReward` **跳过**该按钮（不做 AI 打分 / 丢瓶腾位）。

`PlayCard` 若弹出牌堆/手牌选择且 `hint` 未能点选，则 `Success` 为 false，`Message` 为 `pending_selection`。随后调用 `SelectionState` / `PickSelection`，或在下次 `PlayCard` 传入 `hint`。

```csharp
await KitLibGameOps.Execute(
    new GameAction { Type = ActionType.PlayCard, TargetIndex = 2, SecondaryIndex = 0 },
    new SelectionHint { CardId = "SOME_CARD_ID" });
```
:::

## Selection UI{lang="en"}

## 选牌界面{lang="zh-CN"}

::: en
```csharp
JsonObject SelectionState()
Task<JsonObject> PickSelection(JsonObject args)
```

`SelectionState()` when idle:

```json
{ "active": false }
```

When a picker is open: `active`, `screenType` (`hand`, `combat_pile`, `choose_a_card`, `deck`, `simple`, `grid`, …), `options[]` (lean card JSON + `index`), `confirmAvailable`.

`PickSelection` arguments (same keys as MCP `selection_action`):

| Key | Type | Description |
| --- | --- | --- |
| `card_index` | int | Single option index |
| `card_indices` | int[] | Multi-select |
| `card_id` | string | First visible option with this model id |
| `confirm` | bool | Click confirm/proceed when enabled (default `true`) |

Result: `{ "ok": true, "pickedCount": n, "selectionActive": bool }` or `{ "ok": false, "error": "..." }`.
:::

::: zh-CN
```csharp
JsonObject SelectionState()
Task<JsonObject> PickSelection(JsonObject args)
```

无选择界面时 `SelectionState()`：

```json
{ "active": false }
```

有选择界面时：`active`、`screenType`（`hand`、`combat_pile`、`choose_a_card`、`deck`、`simple`、`grid` 等）、`options[]`（lean 卡牌 JSON + `index`）、`confirmAvailable`。

`PickSelection` 参数（与 MCP `selection_action` 相同）：

| 键 | 类型 | 说明 |
| --- | --- | --- |
| `card_index` | int | 单个选项下标 |
| `card_indices` | int[] | 多选 |
| `card_id` | string | 第一个匹配该模型 id 的可见选项 |
| `confirm` | bool | 可点时点确认/继续（默认 `true`） |

结果：`{ "ok": true, "pickedCount": n, "selectionActive": bool }` 或 `{ "ok": false, "error": "..." }`。
:::

## After a play{lang="en"}

## 出牌之后{lang="zh-CN"}

::: en
```csharp
JsonObject? CaptureCombatAfterState()
```

Compact combat follow-up: `playerPowers[]` and `enemies[]` (same lean enemy fields as `Snapshot`). `null` if there is no run. Used by MCP `combat_action` after a successful play.
:::

::: zh-CN
```csharp
JsonObject? CaptureCombatAfterState()
```

精简战斗后续：`playerPowers[]` 与 `enemies[]`（字段与 `Snapshot` 的 lean 敌人一致）。无局时 `null`。MCP `combat_action` 在出牌成功后使用。
:::

## See also{lang="en"}

## 另见{lang="zh-CN"}

::: en
- [Cards](/api/cards/) — mutate the deck (not UI play)
- [Cheats & run stats](/api/runtime-cheat/)
- KitDevTools MCP: `get_game_state`, `combat_action`, `map_action`, `get_selection_state`, `selection_action` wrap this API
:::

::: zh-CN
- [卡牌](/api/cards/) — 改牌组（不是点 UI 出牌）
- [作弊与局内数值](/api/runtime-cheat/)
- KitDevTools MCP：`get_game_state`、`combat_action`、`map_action`、`get_selection_state`、`selection_action` 封装本 API
:::
