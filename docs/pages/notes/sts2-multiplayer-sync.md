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
| 0 | 对称 Harmony + `MpCheatState` | `CheatPatches`（无限血/能量、倍率、冻怪等）— 战斗内 **零网络包** |
| 1 | `INetMessage` 配置快照 | 经 `ZzzMpCheatEnvelopeNetMessage`（channel=Config，JSON + magic） |
| 2 | `INetMessage` 命令 | 同一 envelope（channel=Command：击杀、加牌 prepare/execute） |
| 2b | 加牌 ACK | 同一 envelope（channel=AddCardAck，客机 → 主机） |
| 2c | 客机加牌请求 | channel=AddCardRequest（客机 → 主机）；主机跑 prepare/ACK/execute 后 channel=AddCardRequestResult 回传 |
| 2d | 删牌 | Command：RemoveCardPrepare / RemoveCardExecute（按牌堆索引定位实例）；ACK 复用 AddCardAck |
| 2e | 客机删牌请求 | channel=RemoveCardRequest / RemoveCardRequestResult（与加牌相同主机权威流程） |
| 2f | 改牌 | Command：EditCardPrepare / EditCardExecute（牌堆索引定位 + `CardEditTemplate` JSON）；ACK 复用 AddCardAck |
| 2g | 客机改牌请求 | channel=EditCardRequest (7) / EditCardRequestResult (8) |
| — | **单槽位** | 仅注册 **1** 个 mod `INetMessage` 类型，降低与其他 mod 的 id 冲突 |
| — | **多人 ACK** | 主机等待「Run 内远端玩家 ∩ 大厅已连接」全部 ACK；超时随人数递增（8s + 1.5s×(n−1)，上限 20s）；`commandId` 多路复用，支持并发多笔加牌 |
| — | **禁用** | `RuntimeStatModifiers` 帧循环；战斗中改 gold/HP 等本地直写 |

### 启用条件

1. 全员安装 **相同版本 DevMode**（**不依赖** RitsuLib；消息 id 按类型名排序，`Zzz*` 前缀避免与 `PeerInputMessage` 等冲突）
2. 开发者模式菜单 → **联机作弊：ON**（仅本地 opt-in，**不会**写入原版 mod 握手列表）
3. 主机在跑档中打开 Cheats 面板修改；客机只读，状态与主机一致

### 代码入口

- `src/Multiplayer/Cheat/` — `MpCheatSession`, `MpCheatConfig`, `MpCheatApplier`, `MpCheatNetBus`, `MpCheatNetMessages`
- Handler 在 `NRun._Ready` 注册（与 LustTravel2 `ArousalSync` 相同模式）
- `CheatPatches.cs` — 通过 `MpCheatApplier` 查询标志

### 双人回归测试清单

- [ ] 双方 `联机作弊：ON` + DevMode，能进大厅并进跑
- [ ] 日志有 `[MpCheat] NetMessage handlers registered.`
- [ ] 主机开无限血/冻怪/伤害倍率，客机无 StateDivergence，跑 3+ 场战斗
- [ ] 主机点「击杀全部（同步）」，双方敌人同时死亡
- [ ] 主机卡牌浏览器加牌：侧栏 **Player** 选客机角色后再加；客机牌组出现相同卡牌（无 8s 超时）
- [ ] 客机卡牌浏览器加牌：点添加后日志有 `AddCard client request` / `AddCard host start`（target=客机 netId）；牌出现在客机角色
- [ ] 客机在 Hand/Deck 等分页删牌：日志 `RemoveCard client request` → `RemoveCard host start`；各方牌堆一致
- [ ] 手牌/牌组分页改费、改名：日志 `EditCard host start` / `EditCard execute`；双方卡牌属性一致
- [ ] 客机改自己手牌：日志 `EditCard client request` → 主机执行；无 StateDivergence
- [ ] 客机 Cheats 面板只读，改开关不生效
- [ ] 一方关闭联机作弊 opt-in 时，会话不 arm（面板提示原因）

---

本页为**模式说明**，具体类名、字段与补丁目标以你的 mod 与引擎版本为准。

:::
