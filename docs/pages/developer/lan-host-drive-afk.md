---
title:
  en: LAN host-drive & AFK co-op
  zh-CN: LAN host-drive 与 AFK 联机
top: 9800
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
LAN dual-instance testing: host hand-plays locally while AI drives the ENet client; AFK client blocks local combat input and mirrors map votes. Setup, logs, and regression checklist are in Chinese below.
:::

::: zh-CN
DevMode 在 **LAN 双开** 场景下，让主机玩家手动操作本地角色（netId=1），由规则 AI 通过动作队列代打真实 ENet 客机（netId=1000）；客机开启 AFK 后不向本地队列提交战斗输入，地图投票由主机镜像。

---
:::

## 快速上手{lang="zh-CN"}

::: zh-CN

每次改 DevMode 并 `make compile` / `make deploy` 后，**两边游戏窗口都要完全重启**。
:::

### 双开（推荐）{lang="zh-CN"}

::: zh-CN

检测到同机双实例时，**RunManager.Launch 会自动应用 preset**，无需手动点按钮：

| 端 | 自动行为 | log 关键词 |
| --- | --- | --- |
| **主机** | `ApplyLanHostPreset()` | `[DualInstance] Auto-applied LAN host preset` / `LAN host preset applied` |
| **客机** | `ApplyLanClientPreset()` | `[DualInstance] Auto-applied LAN client AFK preset` / `AFK client enabled` |

仍可在 DevPanel → AI 托管 里手动一键；双开时 AFK 开关是 **per-process**（`KitLibInstance.SessionLan`），主机 preset 写入共享 `settings.json`。
:::

### 手动 preset{lang="zh-CN"}

::: zh-CN

| 端 | DevPanel 操作 |
| --- | --- |
| **主机** | 「一键：LAN 主机代打客机」 |
| **客机** | 「一键：LAN 客机 AFK」→ 应看到 `● AFK 已开启` |

**半配置会 desync：** 只开客机 AFK、主机未开 LAN preset 时，1000 无人代打且 coop 回合门控易乱。双开已自动避免；单窗测试时两边都要点。
:::

### 流程{lang="zh-CN"}

::: zh-CN

1. Host profile1 + Client profile2（避免共享存档冲突）。
2. Host → LAN Host；Client → Join `127.0.0.1`。
3. 主机点地图 → 客机自动镜像投票。
4. 进战斗：主机手打 1 号位；AI ~400ms poll 为 1000 出牌。
5. 成功：完整跑完玩家阶段 + 敌人阶段，无 `StateDivergence`。

---
:::

## Vanilla 官方 coop 模型{lang="zh-CN"}

::: zh-CN

官方源码（`Slay the Spire 2/src/Core/`）中 coop 战斗同步分 **两层**：

| 层 | 机制 | 是否走网络 |
| --- | --- | --- |
| 动作队列 | `ActionQueueSynchronizer` → `ActionEnqueuedMessage` | 是 |
| 回合阶段门控 | `CombatManager._playersReadyToEndTurn` / `_playersReadyToBeginEnemyTurn` | **否（各进程本地）** |

Vanilla 要求：两边在同一动作流上执行 `EndPlayerTurnAction` → 各自 `SetReadyToEndTurn` → **两边本地**都满足 `AllPlayersReadyToEndTurn()` 后才进入 phase 1（`EndPlayerTurnPhaseOneInternal`），并生成 checksum `"After player turn phase one end"`。
:::

### 关键 vanilla 约束{lang="zh-CN"}

::: zh-CN

1. **`NetEndPlayerTurnAction` 无玩家 ID**，只有 `combatRound`；玩家来自 `ActionEnqueuedMessage.playerId`（`NetActionToGameAction`）。
2. **Host 侧 `RequestEnqueue` 默认** `EnqueueAction(action, _netService.NetId)` — 代 1000 出牌/结束回合时，client 会重建出 **host 玩家** 的 action。
3. **`SetReadyToEndTurn` 不联网** — 只在调用它的进程更新 ready 集合；若仅 host 调用，client 永远到不了 `AllPlayersReadyToEndTurn()`，或 host 单方面先进 phase 1 → `StateDivergence`。
:::

### Vanilla 回合流程（2 人 coop）{lang="zh-CN"}

::: zh-CN

```text
Play phase
  → 各玩家 RequestEnqueue(EndPlayerTurnAction) [playerId = sender]
  → 两边执行 action → SetReadyToEndTurn(各自玩家)
  → AllPlayersReadyToEndTurn → AfterAllPlayersReadyToEndTurn
  → Phase 1: EndPlayerTurnPhaseOneInternal → checksum "After player turn phase one end"
  → 各端 RequestEnqueue(ReadyToBeginEnemyTurnAction for GetMe)
  → SetReadyToBeginEnemyTurn × N → Phase 2 → 敌人回合 → RoundNumber++
```

DevMode host-drive 必须在 **动作队列层** 用正确的 `playerId` 广播，才能让 client 同步更新 ready 标志；不能依赖 host-only `SetReadyToEndTurn`。

---
:::

## 架构概览{lang="zh-CN"}

::: zh-CN

```text
双开 RunManager.Launch
  → DualInstanceTestBootstrap.TryAutoLanPresetsOnLaunch()

主机 ApplyLanHostPreset()
  MpAiTeammateEnabled = true
  MpAiTeammateDriveLiveEnet = true
  SyncBot 关闭

客机 ApplyLanClientPreset()
  MpAiTeammateAfkClient.SetSessionEnabled(true)  // 双开：仅本进程内存

战斗（主机）:
  MpAiTeammateHost.Poll → GameLoop → Sts2ActionExecutor.PlayCard
    → RequestEnqueue(PlayCardAction for 1000)
    → PseudoCoopHostEnqueuePatch: EnqueueAction(action, action.OwnerId)
    → MarkInFlight(player.NetId)
  ActionExecutor.AfterActionFinished → ClearInFlight(_player.NetId)
  客机 AFK: 拦截本地 RequestEnqueue

结束回合（live ENet）:
  RequestEnqueue(EndPlayerTurnAction for 1000) + owner-routed patch
  → 两边执行 → SetReadyToEndTurn(1000) on host AND client
  主机 EndPlayerTurnAction(1) 后 EnsureHostDrivenPeersEndTurn 兜底
  HasPendingCombatActions 门控：strike 执行中不 end turn

敌人阶段 ready:
  end-turn 同步后，各端 vanilla AfterAllPlayersReadyToEndTurn 自行 enqueue ReadyToBeginEnemyTurn(GetMe)
  phantom → 主机 SetReadyToBeginEnemyTurn(peer)
  live ENet host-drive → vanilla client RequestEnqueue(Ready for self) after phase-1 checkpoint；AFK 仅放行 Ready、不 hook 时机；host 不 enqueue P1000 Ready

地图:
  GetMapMirrorTargets() 含 live ENet peer
```
:::

### 与 phantom / SyncBot 模式{lang="zh-CN"}

::: zh-CN

| | Phantom + SyncBot | LAN host-drive |
| --- | --- | --- |
| 队友 | phantom 1001 | 真实 ENet 1000 |
| Enqueue owner | RequestEnqueue + owner patch | 同左 |
| End turn ready | 同步 action → 两边 SetReady | 同左 |
| 客机 | 无真实客户端 | AFK 拦截本地 enqueue |
| Enemy-turn ready | host SetReadyToBeginEnemyTurn(peer) | host RequestEnqueue(ReadyToBeginEnemyTurn for peer) |

---
:::

## 关键源码{lang="zh-CN"}

::: zh-CN

| 路径 | 职责 |
| --- | --- |
| `src/Multiplayer/LanTest/DualInstanceTestBootstrap.cs` | 双开自动 preset |
| `src/Multiplayer/PseudoCoop/PseudoCoopBootstrap.cs` | LAN 主机/客机一键 preset |
| `src/Multiplayer/PseudoCoop/MpAiTeammateHost.cs` | 主机 AI poll、`HasPendingCombatActions` 门控 |
| `src/Multiplayer/PseudoCoop/MpAiTeammateCombatActions.cs` | `SignalEndTurn` → `EnqueueEndTurn` |
| `src/Multiplayer/PseudoCoop/MpAiTeammateAfkClient.cs` | 客机 AFK session |
| `src/Multiplayer/PseudoCoop/PseudoCoopActionQueue.cs` | 队列 + in-flight；`ResolvePlayerNetId` |
| `src/Multiplayer/PseudoCoop/SimulatedPeerRegistry.cs` | `IsLiveEnetPeer` / `IsHostDrivenPeer` |
| `src/Multiplayer/PseudoCoop/Patches/PseudoCoopHostEnqueuePatch.cs` | host RequestEnqueue → `action.OwnerId` 路由 |
| `src/Multiplayer/PseudoCoop/Patches/PseudoCoopCombatReadyPatch.cs` | `EnsureHostDrivenPeersEndTurn` |
| `src/Multiplayer/PseudoCoop/Patches/MpAiTeammateAfkClientPatch.cs` | AFK 拦截 RequestEnqueue |
| `src/Multiplayer/PseudoCoop/Patches/MpAiTeammateActionFlightPatch.cs` | `AfterActionFinished` 清 in-flight |
| `src/AI/Sts2/Sts2ActionExecutor.cs` | enqueue + `MarkInFlight` |
| `src/UI/Rail/DevPanelUI.Ai.cs` | 一键按钮与 AFK banner |

---
:::

## 日志{lang="zh-CN"}

::: zh-CN

| 用途 | 路径 |
| --- | --- |
| 官方游戏日志 | `%AppData%\SlayTheSpire2\logs\godot.log` |
| 双开按进程（推荐） | 浏览器开发者控制台 `http://127.0.0.1:9878/#/logs`（每实例独立 Dev 服务） |
| 脚本双开分文件（可选） | `<Sts2Dir>\logs\godot-host.log` / `godot-join.log`（`make launch-mp-dual` 传 `--log-file`） |

双开时磁盘上的 `godot.log` 会交错；用 Dev 控制台或分文件日志按窗口查看。
:::

### 成功标准{lang="zh-CN"}

::: zh-CN

```powershell
Select-String -Path "$env:APPDATA\SlayTheSpire2\logs\godot.log" `
  -Pattern "LAN host preset|Auto-applied LAN host|AFK client enabled|GameLoop|Enqueued end turn|StateDivergence|Ready end turn \(live ENet\)"
```

- 有 `LAN host preset applied`（或 Auto-applied）
- 有 `[MpAiTeammate] GameLoop`（1000 由 AI 驱动）
- 有 `Enqueued end turn netId=1000`（live ENet 同步路径）
- **每个 round 至多 1 条** `Enqueued end turn netId=1000`（无 duplicate）
- `Enqueued end turn` 在对应 `Player 1000 playing ... finished execution` **之后**
- **无** `StateDivergence`
- **无** `Ready end turn (live ENet)`（已废弃路径）
- **每个 round 先** host/client 均出现 `After player turn phase one end`（id N）
- **再** client `Sending message to host requesting to enqueue action ReadyToBeginEnemyTurnAction 1000`（vanilla，非 DevMode log）
- **再** `after player turn phase two end`（id N+1）
- **无** `[MpAiTeammate] AFK client ready-to-begin-enemy-turn` 或 `[PseudoCoop] Enqueued ready-to-begin-enemy-turn`
- **无** `EndPlayerTurnPhaseTwo called while the current side is Enemy`
- 随后应有 `After enemy turn start`，**不应**长时间停在 `No action is ready` + 仅 ping

---
:::

## 已修复问题（摘要）{lang="zh-CN"}

::: zh-CN

| # | 现象 | 根因 | 修复 |
| --- | --- | --- | --- |
| 1 | 地图不跟票 | mirror 不含 live ENet | LAN preset 含 live peer |
| 2 | checksum 6（早期） | `RequestEnqueue` 用 host NetId 作 playerId | ~~live ENet 仅 SetReadyToEndTurn~~ → **owner-routed enqueue** |
| 3 | checksum 7 | AI `finally` 过早 ready | 移除 `TrySignalEndTurnIfDone` |
| 4 | strike 中途 ready | Poll 只看队列不看 in-flight | `HasPendingCombatActions` |
| 5 | in-flight 无效 | async Finalizer 过早 Clear | `AfterActionFinished` 锚点 |
| 6 | phase 1 vs 2 | 主机未 preset + 客机单方面 ready enemy turn + `ClearInFlight(OwnerId)` 清错 | 双开自动 preset；移除客机 enemy-turn patch；`ResolvePlayerNetId` |
| 7 | checksum 6 / StateDivergence（16:35） | host-only `SetReadyToEndTurn(1000)` → host 先进 phase 1，client 仍 play phase | `PseudoCoopHostEnqueuePatch` + 同步 `EndPlayerTurnAction` |
| 8 | 同 round 两次 `Enqueued end turn` / strike 中途 enqueue | postfix 双路径重叠 + `HasPendingCombatActions` 未含 executing action | `ReadyPhantomPeersToEndTurn` 分离；`HasQueuedEndTurn` + `CanSignalEndTurn` 统一门控 |
| 9 | 210300 第一回合后软锁（无 `After enemy turn start`） | live ENet 跳过 P1000 `ReadyToBeginEnemyTurn`；双端 AI EndTurn → `EndTurnPhaseOne` 无 `NotPlayPhase`；AFK 拦截 vanilla ready enqueue | host `RequestEnqueue(ReadyToBeginEnemyTurn for peer)`；owner-routed patch |
| 10 | 212268/210636 checksum 17；`EndPlayerTurnPhaseTwo.*Enemy` | #9 host + client 双路径各 enqueue P1000 Ready → 重复 action、phase 1/2 分叉 | 移除 client `EndTurnPhaseOne` 兜底；AFK 拦截含 Ready 的全部本地 RequestEnqueue |
| 11 | 214292/214288 checksum 6；host phase1 vs client phase2 | #10 后 host 同步 broadcast P1000 Ready → client 在 phase-1 checkpoint 前执行 P1000 Ready | live ENet 改回 client 在 P1 Ready 后 RequestEnqueue；host 不再 enqueue P1000 Ready |
| 12 | 217812/216604 checksum 6（#11 后） | #11 DevMode hook 在 P1 Ready postfix 过早 enqueue → 仍跳过 phase-1 checkpoint | 移除 hook；AFK 仅放行 Ready，由 vanilla 在 phase-1 后 RequestEnqueue |
:::

### #9 详情（2026-05-24 ~17:50，pid 210300）{lang="zh-CN"}

::: zh-CN

- 双方 AI `EndPlayerTurn`（P1000 + P1）→ `EndTurnPhaseOne`；P1 `ReadyToBeginEnemyTurn` 已执行
- **无** P1000 ready → 卡在 `No action is ready`，仅 ping
- 成功路径 197996：P1 ready 时 `NotPlayPhase` → 旧 AFK hook 触发 `AFK client ready-to-begin-enemy-turn`
- 210300：`EndTurnPhaseOne` 路径不经过 `NotPlayPhase`，且 `#6` 后 live ENet 被 skip
- 修复：与 end-turn 对称，host owner-routed enqueue P1000 ready
:::

### #10 详情（2026-05-24 ~18:xx，pid 212268/210636）{lang="zh-CN"}

::: zh-CN

- #9 后无软锁，host 有 `Enqueued ready-to-begin-enemy-turn netId=1000`
- 客机 `EndTurnPhaseOne` 兜底 `TrySignalReadyToBeginEnemyTurn` → 第二条 Ready（action id 11）
- Round 1：client `EndPlayerTurnPhaseTwo called while the current side is Enemy`
- Round 2：checksum 17 — host `After player turn phase one end`，client `after player turn phase two end`；敌人 HP 56 vs 62
- 修复：仅 host `ReadySimulatedPeersToBeginEnemyTurn` 单路径；AFK 不再本地 enqueue Ready
:::

### #11 详情（2026-05-24 ~18:03，pid 214292/214288）{lang="zh-CN"}

::: zh-CN

- #10 后无 duplicate Ready，但第一回合 checksum 6 即 StateDivergence
- Host：`After player turn phase one end` id 6 → host broadcast P1000 Ready → phase two id 7
- Client：P1+P1000 Ready 连续执行 → `after player turn phase two end` 作为 id 6（跳过 phase1 checkpoint）
- 成功路径 203024：P1 Ready → phase1 checksum → client `RequestEnqueue` P1000 → phase2
- 修复：host 不再 enqueue live ENet P1000 Ready；AFK client 在 P1 Ready 执行后 RequestEnqueue self
:::

### #12 详情（2026-05-24 ~18:07，pid 217812/216604）{lang="zh-CN"}

::: zh-CN

- #11 client hook 在 P1 Ready **执行中** RequestEnqueue → 仍跳过 phase-1 checkpoint
- 成功 203024：P1 Ready → phase1 checksum → **vanilla** `Sending message to host requesting... Ready 1000`
- 修复：移除 `MpAiTeammateAfkPeerReadyPatch`；AFK 只放行 `ReadyToBeginEnemyTurnAction`，不干预 vanilla 时机
:::

### Post-fix 验证（owner-routed enqueue，pid 205708/203024）{lang="zh-CN"}

::: zh-CN

- Round 1–2：**通过**（checksum 连续，无 phase 1 desync）
- Round 3：checksum 27 `After enemy turn end` — `UndeadSpirit` 14 vs 17，属 **LustTravel2 边界**（非本次 DevMode 范围）
:::

### #6 详情（2026-05-24 ~16:29）{lang="zh-CN"}

::: zh-CN

- 主机 log **无** `LAN host preset applied`；第一轮 **无** `GameLoop`
- 客机 alone AFK → 1000 未 ready，phase 门控分叉
- 客机 `SetReadyToBeginEnemyTurn(1000)` 可能提前进入 phase 2
- 手牌/敌人 HP 不一致；UndeadSpirit 两侧一致 → **非** LustTravel2 UndeadSoul 问题
:::

### #7 详情（2026-05-24 ~16:35）{lang="zh-CN"}

::: zh-CN

- 主机 log：`Ready end turn (live ENet) netId=1000` 出现在 strike `began` 与 `finished` **之间**
- 根因：host-only `SetReadyToEndTurn` 不更新 client 本地 ready 集合；host 满足 `AllPlayersReadyToEndTurn()` 后进入 phase 1 + checksum 6，client 仍在 round 1 play phase
- 修复：`PseudoCoopHostEnqueuePatch` 使 `PlayCard` / `EndPlayerTurn` 以 `action.OwnerId` 广播；live ENet 改回 `RequestEnqueue(EndPlayerTurnAction)`；live ENet 跳过 host-only `SetReadyToBeginEnemyTurn`
:::

### #8 详情（2026-05-24 ~16:44）{lang="zh-CN"}

::: zh-CN

- 主机 log：round 3 连续两条 `Enqueued end turn netId=1000 round=3`（`EnsureHostDrivenPeersEndTurn` + `ReadySimulatedPeersToEndTurn` 重叠）
- round 2：`Enqueued end turn` 出现在 strike `began` 与 `finished` 之间（in-flight / executing action 门控不足）
- 修复：postfix 仅 phantom 走 `SetReadyToEndTurn`；host-driven 统一 `CanSignalEndTurn`（pending + queued end turn + running player-driven action）

---
:::

## 调试检查清单{lang="zh-CN"}

::: zh-CN

1. 两边重启并加载最新 DLL。
2. 主机 log：`LAN host preset applied` 或 `Auto-applied LAN host preset`。
3. 客机 log：`AFK client enabled`。
4. live ENet 路径应有 `Enqueued end turn netId=1000`；**不应**有 `Ready end turn (live ENet)`；**同一 round 仅一条** end turn log。
5. desync 时对照 `Enqueued end turn` / phase log 与 `Player 1000 playing` 顺序（须在 `finished execution` 之后）。
6. UndeadSpirit 一致但 HP/牌堆不同 → 查 DevMode 时序；checksum 27 敌人回合末 UndeadSpirit 分叉 → LustTravel2 边界。

---
:::

## 与 LustTravel2 的边界{lang="zh-CN"}

::: zh-CN

UndeadSoul 等 mod 字段在 LustTravel2 仓库维护。DevMode #7 修复后 round 1–2 已通过；round 3 checksum 27（`After enemy turn end`）出现 `UndeadSpirit` 14 vs 17，需在 LustTravel2 侧调查 HpOrb/UndeadSoul 同步。

---
:::

## 可选改进{lang="zh-CN"}

::: zh-CN

- [ ] 进战斗 in-game toast 提示 AFK 已生效
- [ ] 单窗（非双开）手动测试时 UI 强提示「主机必须先开 LAN preset」

---
:::

## 构建{lang="zh-CN"}

::: zh-CN

```bash
cd DevMode
make deploy   # build + 复制到游戏 mods/KitLib
```

修改 Harmony patch 或 MpAiTeammate 后必须 recompile，双实例均需重启。
:::
