---
title:
  en: Multiplayer enemy-turn desync handoff
  zh-CN: 联机敌人回合 desync 交接
top: 9590
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
> 主文档：[LAN host-drive & AFK co-op](/developer/lan-host-drive-afk) issue **#9** / **#10**

## 现象（210300 / godot.log ~17:50）

- 第一场战斗第一回合：双方 AI 结束玩家回合后卡住
- 日志停在 `ActionQueueSet] No action is ready`，之后只有 ping
- **无** `After enemy turn start`、**无** `StateDivergence`、**无** C# 异常 → 回合过渡软锁

## Vanilla 规则

coop 需要 **每位玩家** 各执行一次 `ReadyToBeginEnemyTurnAction` 后敌人才动。

## 根因

1. `#6` 后 `ReadySimulatedPeersToBeginEnemyTurn` **跳过 live ENet P1000**（避免 host-only `SetReadyToBeginEnemyTurn` 单方面 phase 2）
2. AFK client 拦截本地 `RequestEnqueue`，vanilla 自 enqueue 的 ready 也被挡
3. **双端 AI EndTurn** 路径：`EndTurnPhaseOne` 先于/替代 P1 ready 时的 `NotPlayPhase`，旧 AFK hook（197996 的 `AFK client ready-to-begin-enemy-turn`）不触发

## 与 checksum / UndeadSoul 无关

210300 进房 `desire=4` 已 Applied remote（defer 收窄生效），不是上一场 desire 分叉。

## 修复（DevMode #9 → #10）

| 位置 | 改动 |
| --- | --- |
| `PseudoCoopCombatReady.ReadySimulatedPeersToBeginEnemyTurn` | live ENet host-drive → `RequestEnqueue(ReadyToBeginEnemyTurnAction(peer))` |
| `PseudoCoopHostEnqueuePatch` | `ReadyToBeginEnemyTurnAction` 在 `NotPlayPhase` 仍走 owner-routed |
| ~~`MpAiTeammateAfkClient.TrySignalReadyToBeginEnemyTurn`~~ | **#10 移除** — 与 host 路径重复 enqueue |
| `MpAiTeammateAfkRequestEnqueuePatch` | AFK 拦截全部本地 RequestEnqueue（含 Ready）；仅接受 host 广播 |

### #10 回归（212268/210636）

#9 client 兜底 + host enqueue → 每 round 两条 P1000 Ready → phase 1/2 分叉、checksum 17。

## 复测标准

```powershell
Select-String -Path "$env:APPDATA\SlayTheSpire2\logs\godot.log" `
  -Pattern "Enqueued ready-to-begin-enemy-turn|RequestEnqueueActionMessage.*ReadyToBegin|After enemy turn start|EndPlayerTurnPhaseTwo|No action is ready"
```

- 每 round **仅 1 条** host `Enqueued ready-to-begin-enemy-turn netId=1000`
- **无** client `Requesting to enqueue.*ReadyToBegin.*1000`
- **不应** 30s+ 仅 ping

## 临时绕过

Client 窗口手动点「准备进入敌人回合」（若有 UI 按钮）。

## 无关项

| 日志 | 说明 |
| --- | --- |
| `ERROR: Cannot get class ''` | FoxHime EnergyVfx PCK 缺失，进战斗 UI 警告 |
| `[BurstClothingSync]` | 回合边界外 off-queue 伤害；**勿在 end turn 时点爆衣**（LustTravel2 侧可加 PlayPhase guard） |
| `InvalidOperationException Index`（215260） | 旧局 Neow/NewLeaf，非本局 |
:::

::: zh-CN
（维护者向长文；用户向说明见 README.zh-CN.md 与 [文档站](/guide/panels/)。）
:::
