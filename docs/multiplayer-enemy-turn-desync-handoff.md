# LAN 敌人回合 ready 软锁 — 交接摘要

> 主文档：[lan-host-drive-afk.md](./lan-host-drive-afk.md) issue **#9**

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

## 修复（DevMode #9）

| 位置 | 改动 |
| --- | --- |
| `PseudoCoopCombatReady.ReadySimulatedPeersToBeginEnemyTurn` | live ENet host-drive → `RequestEnqueue(ReadyToBeginEnemyTurnAction(peer))` |
| `PseudoCoopHostEnqueuePatch` | `ReadyToBeginEnemyTurnAction` 在 `NotPlayPhase` 仍走 owner-routed |
| `MpAiTeammateAfkClient.TrySignalReadyToBeginEnemyTurn` | client 在 `EndTurnPhaseOne` 兜底 enqueue |
| `MpAiTeammateAfkRequestEnqueuePatch` | 放行 `ReadyToBeginEnemyTurnAction` |

## 复测标准

```powershell
Select-String -Path "$env:APPDATA\SlayTheSpire2\logs\godot.log" `
  -Pattern "Enqueued ready-to-begin-enemy-turn|AFK client ready-to-begin-enemy-turn|After enemy turn start|No action is ready"
```

- 第一回合 end 后 **≤几秒内** 出现 ready log + `After enemy turn start`
- **不应** 30s+ 仅 ping

## 临时绕过

Client 窗口手动点「准备进入敌人回合」（若有 UI 按钮）。

## 无关项

| 日志 | 说明 |
| --- | --- |
| `ERROR: Cannot get class ''` | FoxHime EnergyVfx PCK 缺失，进战斗 UI 警告 |
| `[BurstClothingSync]` | 回合边界外 off-queue 伤害；**勿在 end turn 时点爆衣**（LustTravel2 侧可加 PlayPhase guard） |
| `InvalidOperationException Index`（215260） | 旧局 Neow/NewLeaf，非本局 |
