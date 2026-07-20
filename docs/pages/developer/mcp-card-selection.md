---
title:
  en: MCP card selection
  zh-CN: MCP 卡牌选择
top: 9935
---

## Background{lang="en"}

## 背景{lang="zh-CN"}

::: en
`combat_action` `play_card` fails when a card opens an in-combat selection UI (`NCombatPileCardSelectScreen`, hand multi-select, etc.). Example: **Renew and Replace** (LustTravel2 `LUST_TRAVEL2_CARD_RENEW_AND_REPLACE`) asks the player to pick a card from the discard pile before copies are generated.

Agents could not verify copy cost reduction (e.g. **Trade Secret** 2→1, **Travel Light** 1→0) without manual clicks.
:::

::: zh-CN
`combat_action` 的 `play_card` 在卡牌弹出战斗内选择界面时会失败（`NCombatPileCardSelectScreen`、手牌多选等）。例如 LustTravel2 的 **弃旧换新**（`LUST_TRAVEL2_CARD_RENEW_AND_REPLACE`）需要先从弃牌堆选一张牌，再生成带减费复制的牌。

此前 agent 无法自动完成选牌，因而不能验证复制费用（如 **商业机密** 2→1、**轻装上阵** 1→0），只能依赖玩家手动点击。
:::

## New MCP tools{lang="en"}

## 新增 MCP 工具{lang="zh-CN"}

::: en
| Tool | Purpose |
| --- | --- |
| **`get_selection_state`** | Returns `active`, `screenType`, `options[]` (index, id, name, cost), `confirmAvailable` |
| **`selection_action`** | Pick by `card_index`, `card_indices`, or `card_id`; optional `confirm` |

**`combat_action` extensions**

- `selection_card_id` / `selection_index` on `play_card` — one-shot auto-pick when the selection UI opens during the same call
- On timeout with selection still open: `pendingSelection: true` + `selectionState` instead of a bare failure
:::

::: zh-CN
| 工具 | 作用 |
| --- | --- |
| **`get_selection_state`** | 返回 `active`、`screenType`、`options[]`（index、id、name、cost）、`confirmAvailable` |
| **`selection_action`** | 用 `card_index`、`card_indices` 或 `card_id` 点选；可选 `confirm` |

**`combat_action` 扩展**

- `play_card` 支持 `selection_card_id` / `selection_index` — 同一次调用内自动点选
- 选择界面仍打开时超时：返回 `pendingSelection: true` + `selectionState`，而非单纯失败
:::

## LustTravel2 test recipe{lang="en"}

## LustTravel2 测试步骤{lang="zh-CN"}

::: en
1. Load Elise combat save; `dev_add_card` **Trade Secret** → `discard`, **Renew and Replace** → `hand`
2. One-shot:

```json
{
  "action": "play_card",
  "card_index": 4,
  "selection_card_id": "LUST_TRAVEL2_CARD_TRADE_SECRET"
}
```

3. Or two-step: `play_card` → `selection_action` `{ "card_id": "LUST_TRAVEL2_CARD_TRADE_SECRET" }`
4. `get_game_state` — expect **two** Trade Secret copies in hand at **cost 1** with Exhaust
5. Repeat with `LUST_TRAVEL2_CARD_TRAVEL_LIGHT` in discard; expect copies at **cost 0**
:::

::: zh-CN
1. 加载 Elise 战斗存档；`dev_add_card` 把 **商业机密** 放入 `discard`，**弃旧换新** 放入 `hand`
2. 一步完成：

```json
{
  "action": "play_card",
  "card_index": 4,
  "selection_card_id": "LUST_TRAVEL2_CARD_TRADE_SECRET"
}
```

3. 或分步：`play_card` → `selection_action` `{ "card_id": "LUST_TRAVEL2_CARD_TRADE_SECRET" }`
4. `get_game_state` — 手牌应出现 **两张** 费用 **1**、带 **消耗** 的商业机密复制
5. 弃牌堆换成 `LUST_TRAVEL2_CARD_TRAVEL_LIGHT` 再测；复制应为 **0** 费
:::

## Implementation notes{lang="en"}

## 实现说明{lang="zh-CN"}

::: en
- Core helper: `KitLib.AI` → `McpCardSelectionHelper` (UI click path, shared with card test auto-resolve)
- `Sts2CombatPlayHelper` no longer treats selection overlays as immediate play failure; waits up to 30s
- Reuse pattern from `CardTestPlayHelper` / `Sts2ActionExecutor.PickCardReward`
:::

::: zh-CN
- 核心逻辑：`KitLib.AI` → `McpCardSelectionHelper`（UI 点击路径，与卡牌测试自动选牌共用思路）
- `Sts2CombatPlayHelper` 不再把选择层当作立即失败；最长等待 30 秒
- 实现参考 `CardTestPlayHelper` / `Sts2ActionExecutor.PickCardReward`
:::
