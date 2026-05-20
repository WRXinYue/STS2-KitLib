---
title:
  en: Multiplayer deterministic sync (case study)
  zh-CN: 多人确定性数据同步（案例）
top: 10009
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

::: en

When local-only state affects combat math, lockstep multiplayer can diverge. Pattern: central cache + `INetMessage` + Harmony on sync start. **Body in Chinese**; example uses generic names.

:::

::: zh-CN

## 问题背景

STS2 多人采用**确定性锁步**：各端执行相同输入序列。若某数值仅从**本地文件**或**仅本机可见的状态**读取，而战斗结算又用到了它，则两端结果可能不一致 → **StateDivergence**。

典型场景：持久化进度存在 `%LocalAppData%/...` 的 JSON，战斗公式里读取该文件计算加成，但联机时两台机器文件内容不同。

## 解决思路（模式）

1. **战斗阶段**使用**内存中的、双方已对齐的快照**，而不是直接读本地文件。
2. 在**战斗同步开始**（如 `CombatStateSynchronizer.StartSync` 一类时机）通过 **`INetMessage`** 交换快照。
3. 单人模式下仍可从文件初始化缓存；联机时在收到远端数据后写入缓存。

### 架构示意

```
CombatStateSynchronizer.StartSync()
  → 原版：同步玩家数据等
  → Postfix：InitializeLocal(本地玩家) + 发送 SyncCustomStateMessage
远端 OnReceived → 写入按 NetId 索引的缓存
战斗中 → 公式只读缓存（必要时 fallback 到本地文件）
```

### 数据流（抽象）

1. **同步阶段**：从权威数据源（本地存档）读出结构化字段 → 写入 `Cache[netId]` → 广播消息。
2. **战斗中**：所有参与结算的代码从 `Cache` 读取；若需同时更新持久化与确定性，**两边**以相同顺序调用 `Increment` 类 API。
3. **单人**：同步入口可能仍执行，发送可为空操作；缓存与文件保持一致即可。

## 消息与 API（示例命名）

```csharp
public struct SyncCustomStateMessage : INetMessage
{
    public ulong NetId;
    public int FieldA, FieldB;  // 按你的设计替换
    // ShouldBroadcast / Mode 等按引擎约定设置
}
```

静态类 `CustomStateSync` 可提供：

- `InitializeLocal(Player)` — 从文件读入本地玩家快照。
- `SetFromRemote(ulong netId, ...)` — 远端消息处理。
- `Get(Player)` / `ApplyBonus(...)` — 战斗中确定性读取。

## 时序

自定义消息应与现有同步流程**顺序相容**；若存在 `WaitForSync()`，需确认你的消息在战斗开始前已到达或具有合理 fallback（与无修复时行为一致或更优）。

## 与「把数据塞进遗物」对比

| | 遗物字段同步 | 专用消息 + 缓存 |
|---|---|---|
| 耦合 | 与具体遗物绑定 | 可与遗物解耦 |
| 扩展 | 换设计可能迁字段 | 消息与缓存可独立演进 |

---

## DevMode 联机作弊（MpCheat）

DevMode 在合作模式使用 **分层同步**，不每帧发包：

| Tier | 机制 | 内容 |
|------|------|------|
| 0 | 对称 Harmony + `MpCheatState` | `CheatPatches`（无限血/能量、倍率等）— 战斗内 **零网络包**；玩家向开关在 **`PerPlayer[netId]`**，只影响对应角色 |
| 1 | `INetMessage` 配置快照 | 经 `ZzzMpCheatEnvelopeNetMessage`（channel=Config，JSON + magic）；客机 ConfigRequest **仅提交本机 `PerPlayer`**，主机合并广播 |
| 2 | `INetMessage` 命令 | 同一 envelope（channel=Command：击杀、加牌 prepare/execute） |
| 2b | 加牌 ACK | 同一 envelope（channel=AddCardAck，客机 → 主机） |
| 2c | 客机加牌请求 | channel=AddCardRequest（客机 → 主机）；主机跑 prepare/ACK/execute 后 channel=AddCardRequestResult 回传；payload 含 `TemplateJson`（`CardEditTemplate`：费/格挡/伤害/关键词等，库页暂存后加牌时全员应用）；**Permanent 允许**（未改数值时）；**已改数值则仅 Temporary**（避免永久牌组实例与后续战斗 desync） |
| 2d | 删牌 | Command：RemoveCardPrepare / RemoveCardExecute（按牌堆索引定位实例）；ACK 复用 AddCardAck |
| 2e | 客机删牌请求 | channel=RemoveCardRequest / RemoveCardRequestResult（与加牌相同主机权威流程） |
| 2f | 改牌 | Command：EditCardPrepare / EditCardExecute（牌堆索引定位 + `CardEditTemplate` JSON）；ACK 复用 AddCardAck |
| 2g | 客机改牌请求 | channel=EditCardRequest (7) / EditCardRequestResult (8) |
| 2i | 遗物 | Command：AddRelicPrepare/Execute、RemoveRelicPrepare/Execute；payload=`MpCheatItemPayload`（目标玩家 netId + relicId） |
| 2j | 药水 | Command：AddPotionPrepare/Execute、RemovePotionPrepare/Execute；丢弃用 **slot index** 定位 |
| 2k | 战斗加怪 | Command：AddMonsterPrepare/Execute、AddEncounterPrepare/Execute；须在战斗内且 `CombatManager.IsInProgress` |
| 2l | 客机物品请求 | channel=ItemRequest (9) / ItemRequestResult (10)；遗物/药水仅可改**自己角色**；加怪/遭遇客机可请求主机代执行 |
| 2m | 客机配置请求 | channel=ConfigRequest (11) / ConfigRequestResult (12)；客机改 `MpCheatConfig` 内开关 → 主机 `HostPublishConfig` 广播 |
| 2n | 战斗击杀 | Command：KillEnemyPrepare/Execute（`ItemId` = `monsterId:slot`）；KillAll 用 kind=1 或 ItemRequest `KillAllEnemies` |
| 2o | Power | Command：AddPower/RemovePower/ClearPowers prepare/execute；payload 含 `Amount`、`PowerTarget`；客机 Apply/移除/清空仅**自己角色**；Auto-Apply 联机禁用 |
| — | **单槽位** | 仅注册 **1** 个 mod `INetMessage` 类型，降低与其他 mod 的 id 冲突 |
| — | **多人 ACK** | 主机等待「Run 内远端玩家 ∩ 大厅已连接」全部 ACK；超时随人数递增（8s + 1.5s×(n−1)，上限 20s）；`commandId` 多路复用，支持并发多笔加牌 |
| — | **禁用** | `RuntimeStatModifiers` 帧循环；战斗中改 gold/HP 等本地直写；联机下遗物/药水/战斗加怪的**本地直写**（须走 coordinator）；**Hooks** 规则（战斗开始加牌等未同步，侧栏图标灰显） |

### 启用条件

1. 全员安装 **相同版本 DevMode**（**不依赖** RitsuLib；消息 id 按类型名排序，`Zzz*` 前缀避免与 `PeerInputMessage` 等冲突）
2. 开发者模式菜单 → **联机作弊：ON**（仅本地 opt-in，**不会**写入原版 mod 握手列表）
3. 跑档中主机/客机均可改 **玩家向**开关（各自 `PerPlayer`，互不影响无敌/护盾等）；**敌人向**（秒杀、冻怪、伤害倍率）仍由主机写入 `GlobalEnemy` 全队共享；客机 ConfigRequest 只同步本机玩家项；金币/能量上限等未同步项变灰

### 代码入口

- `src/Multiplayer/Cheat/` — `MpCheatSession`, `MpCheatConfig`, `MpCheatApplier`, `MpCheatNetBus`, `MpCheatNetMessages`
- Handler 在 `NRun._Ready` 注册（与 LustTravel2 `ArousalSync` 相同模式）
- `CheatPatches.cs` — 通过 `MpCheatApplier` 查询标志

---

## DevMode AI托管

从 STS2-AI 迁入 **规则策略**（`SimpleStrategy`，无 LLM/MCP），源码在 `src/AI/`：

- `AiPlayModule` — 轮询 `GameLoop` → `Sts2ActionExecutor`（`CardCmd.AutoPlay`、地图 UI 等）
- 仅控制 **`LocalContext.GetMe()`** 本机角色
- 侧栏 **AI托管** 面板（`devmode.ai`，非作弊面板）：开关 + 操作间隔（ms）；设置写入 `settings.json`
- **SyncBot** 在同面板：仅**主机**联机跑中显示三项开关；客机见 `syncbot.hostOnly` 提示
- 作弊面板（`devmode.cheats`）仅保留 MpCheat 会话 banner 与传统作弊项
- **联机**：默认不自动启动；开启后仅托管本机，远端选牌可能卡住（需 SyncBot 或真人）

---

## DevMode SyncBot（无第二进程）

主机开发用：`src/Multiplayer/SyncBot/`

| 能力 | 说明 |
|------|------|
| 模拟 MpCheat ACK | `MpCheatNetBus.BroadcastCommand` 后对 run 内非 host 的 NetId 调用 `TryHandleAck` |
| ACK 等待集合 | SyncBot ON 时 `MpCheatParticipants` 用 **run 内远端玩家**，不要求 `ConnectedPlayerIds` |
| 远端选牌 | `PlayerChoiceSynchronizer.WaitForRemoteChoice` 前缀 → `ReceiveReplayChoice`（index 0） |
| 合作结束回合 | `NRun._Process` 每 0.5s 对模拟玩家 `SetReadyToEndTurn` |
| 幻影玩家（实验） | `SyncBotSpawnPhantomPlayer`：主机 Launch 且仅 1 人时 `AddPlayerDebug` NetId **1001** |

**不能替代**：ENet 双实例、StateDivergence、客机 `ConfigRequest` / `AddCardRequest` 回包路径。

**推荐流程（测 ACK）**：`multiplayer test` 建房 → 第二实例 Join+Ready 进跑 → 关客机进程 → 主机开 SyncBot + 联机作弊 → 主机加牌/改牌。

### 双人回归测试清单

- [ ] 双方 `联机作弊：ON` + DevMode，能进大厅并进跑
- [ ] 日志有 `[MpCheat] NetMessage handlers registered.`
- [ ] 主机开无限血仅主机角色生效；客机开无限护盾仅客机角色生效；互不影响
- [ ] 主机开冻怪/伤害倍率，全队敌人行为一致，无 StateDivergence，跑 3+ 场战斗
- [ ] 主机点「击杀全部（同步）」，双方敌人同时死亡
- [ ] 主机卡牌浏览器加牌：侧栏 **Player** 选客机角色后再加；客机牌组出现相同卡牌（无 8s 超时）
- [ ] 全部卡牌库页：改格挡/伤害/费后加牌；双方实例数值一致，打牌无 StateDivergence
- [ ] 客机卡牌浏览器加牌：点添加后日志有 `AddCard client request` / `AddCard host start`（target=客机 netId）；牌出现在客机角色
- [ ] 客机在 Hand/Deck 等分页删牌：日志 `RemoveCard client request` → `RemoveCard host start`；各方牌堆一致
- [ ] 手牌/牌组分页改费、改名：日志 `EditCard host start` / `EditCard execute`；双方卡牌属性一致
- [ ] 客机改自己手牌：日志 `EditCard client request` → 主机执行；无 StateDivergence
- [ ] 客机开/关无限血等 Config 项：日志 `ConfigRequest` → 双方 `Applied config rev=…`；灰显项（金币/能量/药栏）不可点
- [ ] 战斗侧栏：主机/客机击杀单怪、击杀全部，双方敌人列表一致
- [ ] Power：主机给客机加 Buff；客机对自己 Apply；Clear All 同步；Auto-Apply 按钮灰显
- [ ] 一方关闭联机作弊 opt-in 时，会话不 arm（面板提示原因）
- [ ] 主机遗物/药水浏览器：侧栏 **Player** 选客机后添加；客机栏位与主机一致
- [ ] 客机请求给自己加药水：日志 `Item request` → prepare/execute；药栏一致
- [ ] 战斗内主机加单怪、加遭遇：双方敌人列表一致，无 StateDivergence
- [ ] 药栏满时加药：prepare ACK 失败，有明确错误文案
- [ ] 非战斗时点加遭遇：失败且不 execute
- [ ] 单人模式：遗物/药水/战斗加怪行为与改前一致（不走 coordinator）

### AI托管 / SyncBot（开发辅助）

- [ ] 侧栏有 **AI托管**（`devmode.ai`），作弊面板无 AI/SyncBot 区块
- [ ] 单机开 AI托管：能自动打牌、选图、领奖
- [ ] 联机开 AI托管：仅本机角色动；日志有 `[AiHost]`，无 StateDivergence（本机路径）
- [ ] 双人进跑后关客机、主机开 SyncBot：主机加牌日志 `[SyncBot] Injected … ACK` 且 `AddCard execute` 无 8s 超时
- [ ] SyncBot + 合作战斗 2 人：不卡在「等待全员结束回合」
- [ ] * 标星项仍须 ENet 双开真客户端（SyncBot 不验 checksum 双端一致）

---

本页为**模式说明**，具体类名、字段与补丁目标以你的 mod 与引擎版本为准。

:::
