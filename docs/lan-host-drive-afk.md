# LAN 主机代打 + 客机 AFK（MpAiTeammate）

DevMode 在 **LAN 双开** 场景下，让主机玩家手动操作本地角色（netId=1），由规则 AI 通过动作队列代打真实 ENet 客机（netId=1000）；客机开启 AFK 后不向本地队列提交战斗输入，地图投票由主机镜像。

---

## 快速上手

每次改 DevMode 并 `make compile` / `make deploy` 后，**两边游戏窗口都要完全重启**。

### 双开（推荐）

检测到同机双实例时，**RunManager.Launch 会自动应用 preset**，无需手动点按钮：

| 端 | 自动行为 | log 关键词 |
| --- | --- | --- |
| **主机** | `ApplyLanHostPreset()` | `[DualInstance] Auto-applied LAN host preset` / `LAN host preset applied` |
| **客机** | `ApplyLanClientPreset()` | `[DualInstance] Auto-applied LAN client AFK preset` / `AFK client enabled` |

仍可在 DevPanel → AI 托管 里手动一键；双开时 AFK 开关是 **per-process**（`DevModeInstance.SessionLan`），主机 preset 写入共享 `settings.json`。

### 手动 preset

| 端 | DevPanel 操作 |
| --- | --- |
| **主机** | 「一键：LAN 主机代打客机」 |
| **客机** | 「一键：LAN 客机 AFK」→ 应看到 `● AFK 已开启` |

**半配置会 desync：** 只开客机 AFK、主机未开 LAN preset 时，1000 无人代打且 coop 回合门控易乱。双开已自动避免；单窗测试时两边都要点。

### 流程

1. Host profile1 + Client profile2（避免共享存档冲突）。
2. Host → LAN Host；Client → Join `127.0.0.1`。
3. 主机点地图 → 客机自动镜像投票。
4. 进战斗：主机手打 1 号位；AI ~400ms poll 为 1000 出牌。
5. 成功：完整跑完玩家阶段 + 敌人阶段，无 `StateDivergence`。

---

## 架构概览

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
    → MarkInFlight(player.NetId)
  ActionExecutor.AfterActionFinished → ClearInFlight(_player.NetId)
  客机 AFK: 拦截本地 RequestEnqueue

结束回合（live ENet）:
  SetReadyToEndTurn(peer) — 禁止 RequestEnqueue(EndPlayerTurnAction)
  主机 EndPlayerTurnAction 后 EnsureHostDrivenPeersEndTurn 兜底

敌人阶段 ready:
  主机 ReadyToBeginEnemyTurnAction → ReadySimulatedPeersToBeginEnemyTurn
  客机 **不** 单方面 SetReadyToBeginEnemyTurn（由主机统一驱动）

地图:
  GetMapMirrorTargets() 含 live ENet peer
```

### 与 phantom / SyncBot 模式

| | Phantom + SyncBot | LAN host-drive |
| --- | --- | --- |
| 队友 | phantom 1001 | 真实 ENet 1000 |
| End turn | RequestEnqueue | live peer 仅 SetReadyToEndTurn |
| 客机 | 无真实客户端 | AFK 拦截本地 enqueue |

---

## 关键源码

| 路径 | 职责 |
| --- | --- |
| `src/Multiplayer/LanTest/DualInstanceTestBootstrap.cs` | 双开自动 preset |
| `src/Multiplayer/PseudoCoop/PseudoCoopBootstrap.cs` | LAN 主机/客机一键 preset |
| `src/Multiplayer/PseudoCoop/MpAiTeammateHost.cs` | 主机 AI poll、`HasPendingCombatActions` 门控 |
| `src/Multiplayer/PseudoCoop/MpAiTeammateCombatActions.cs` | `SignalEndTurn`；live ENet 禁止 enqueue end turn |
| `src/Multiplayer/PseudoCoop/MpAiTeammateAfkClient.cs` | 客机 AFK session |
| `src/Multiplayer/PseudoCoop/PseudoCoopActionQueue.cs` | 队列 + in-flight；`ResolvePlayerNetId` |
| `src/Multiplayer/PseudoCoop/SimulatedPeerRegistry.cs` | `IsLiveEnetPeer` / `IsHostDrivenPeer` |
| `src/Multiplayer/PseudoCoop/Patches/PseudoCoopCombatReadyPatch.cs` | `EnsureHostDrivenPeersEndTurn` |
| `src/Multiplayer/PseudoCoop/Patches/MpAiTeammateAfkClientPatch.cs` | AFK 拦截 RequestEnqueue（无客机 enemy-turn patch） |
| `src/Multiplayer/PseudoCoop/Patches/MpAiTeammateActionFlightPatch.cs` | `AfterActionFinished` 清 in-flight |
| `src/AI/Sts2/Sts2ActionExecutor.cs` | enqueue + `MarkInFlight` |
| `src/UI/Rail/DevPanelUI.Ai.cs` | 一键按钮与 AFK banner |

---

## 日志

| 用途 | 路径 |
| --- | --- |
| DevMode 分实例 | `%AppData%\SlayTheSpire2\steam\<steamid>\mod_data\DevMode\instances\<pid>\session.log` |
| Godot 合并输出 | `%AppData%\SlayTheSpire2\logs\godot.log` |

双开时 grep 同一时间段；合并 log 会 interleave，优先用各 pid 的 `session.log`。

### 成功标准

```powershell
Select-String -Path "$env:APPDATA\SlayTheSpire2\logs\godot.log" `
  -Pattern "LAN host preset|Auto-applied LAN host|AFK client enabled|GameLoop|Ready end turn \(live ENet\)|StateDivergence|Enqueued end turn"
```

- 有 `LAN host preset applied`（或 Auto-applied）
- 有 `[MpAiTeammate] GameLoop`（1000 由 AI 驱动）
- `Ready end turn (live ENet) netId=1000` 在对应 `Player 1000 playing ... finished execution` **之后**
- **无** `StateDivergence`
- **无** `Enqueued end turn netId=1000`（live ENet 路径）
- **无** `AFK client ready-to-begin-enemy-turn`（已移除客机单方面 ready）

---

## 已修复问题（摘要）

| # | 现象 | 根因 | 修复 |
| --- | --- | --- | --- |
| 1 | 地图不跟票 | mirror 不含 live ENet | LAN preset 含 live peer |
| 2 | checksum 6 | `RequestEnqueue(EndPlayerTurn)` 无目标 ID | live ENet 仅 `SetReadyToEndTurn` |
| 3 | checksum 7 | AI `finally` 过早 ready | 移除 `TrySignalEndTurnIfDone` |
| 4 | strike 中途 ready | Poll 只看队列不看 in-flight | `HasPendingCombatActions` |
| 5 | in-flight 无效 | async Finalizer 过早 Clear | `AfterActionFinished` 锚点 |
| 6 | phase 1 vs 2 | 主机未 preset + 客机单方面 ready enemy turn + `ClearInFlight(OwnerId)` 清错 | 双开自动 preset；移除客机 enemy-turn patch；`ResolvePlayerNetId` |

### #6 详情（2026-05-24 ~16:29）

- 主机 log **无** `LAN host preset applied`；第一轮 **无** `GameLoop`
- 客机 alone AFK → 1000 未 `SetReadyToEndTurn`，phase 门控分叉
- 客机 `SetReadyToBeginEnemyTurn(1000)` 可能提前进入 phase 2
- 手牌/敌人 HP 不一致；UndeadSpirit 两侧一致 → **非** LustTravel2 UndeadSoul 问题

---

## 调试检查清单

1. 两边重启并加载最新 DLL。
2. 主机 log：`LAN host preset applied` 或 `Auto-applied LAN host preset`。
3. 客机 log：`AFK client enabled`。
4. 有 live ENet 时不应出现 `Enqueued end turn netId=1000`。
5. desync 时对照 `Ready end turn` 与 `Player 1000 playing` 顺序。
6. UndeadSpirit 一致但 HP/牌堆不同 → 查 DevMode 时序，非 mod mutator。

---

## 与 LustTravel2 的边界

UndeadSoul 等 mod 字段在 LustTravel2 仓库维护。多次 desync 中 **UndeadSpirit 两侧相同、敌人 HP/牌堆不同**，根因在 DevMode 战斗/end-turn 时序。

---

## 可选改进

- [ ] 进战斗 in-game toast 提示 AFK 已生效
- [ ] 单窗（非双开）手动测试时 UI 强提示「主机必须先开 LAN preset」

---

## 构建

```bash
cd DevMode
make deploy   # build + 复制到游戏 mods/DevMode
```

修改 Harmony patch 或 MpAiTeammate 后必须 recompile，双实例均需重启。
