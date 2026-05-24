# 战斗统计 — 待办

DevMode 战斗伤害统计。MVP 基于 `CombatHistory`（公开 API，不对伤害管线打 Harmony patch）。

## MVP ✅（已完成）

- [x] **数据层** — `CombatStatsTracker` + `CombatHistoryTailer`
  - DevMode 激活时订阅 `CombatManager.CombatSetUp` / `CombatEnded`
  - 增量消费 `CombatHistory.Changed`
  - 处理：`DamageReceivedEntry`、`BlockGainedEntry`、`CardPlayFinishedEntry`
- [x] **单场战斗、按玩家聚合**
  - 总造成伤害（玩家/宠物 → 敌人）
  - 总承伤（未格挡部分）
  - 获得格挡
  - 出牌数
  - 分组：按卡牌、按伤害来源、按回合（DPT）
- [x] **UI** — DevPanel 侧栏 **战斗统计**
  - 概览 + 分类标签 + 排行列表
  - 面板打开时自动刷新
  - 战斗结束后保留上一场摘要
- [x] **i18n** — 英文 + 简体中文

## 完整版（Phase 2 — 大部分已完成）

### 数据与归因

- [x] 无 dealer 的能力伤害（毒、绞杀、Haunt；Doom 等待补全）
- [ ] 宠物/召唤物归因边界（Misery、助攻按比例分摊等）
- [x] 溢出伤害 / 被目标格挡 等列
- [x] 敌人对玩家造成的伤害（怪物行动 → 时间线事件）
- [x] 每回合能量消耗
- [ ] 能量浪费（未使用能量）
- [x] 药水使用统计
- [x] 施加 debuff 统计
- [x] 战斗事件时间线

### 范围与持久化

- [x] 整局 Run 累计（当前 run 内所有战斗）
- [x] 导出快照 JSON（反馈 / 调试报告用）
- [x] Mod Feedback ZIP 附带战斗统计 `combat-stats.json`

### 联机

- [x] 合作模式多玩家切换（按玩家 chip 选择）
- [ ] 合作模式按玩家分色、显示名称优化
- [ ] 助攻 / 同目标伤害分摊

### UI

- [x] 回合条形图（复用简单 bar 组件）
- [x] 扩展 / 时间线 / 整局累计 标签页
- [x] 当前场 vs 上一场对比
- [x] 面板内 Export JSON 按钮
- [ ] 战斗内迷你 HUD（设置里可选开关）
- [ ] 设置：战斗开始自动打开、HUD 位置

### 集成

- [ ] Hook 触发：`OnCombatStatsUpdated`（供脚本用，可选）
- [x] 控制台命令：`dmstats` 输出 dump / export

## 参考

- 游戏 API：`CombatManager.Instance.History`，条目在 `MegaCrit.Sts2.Core.Combat.History.Entries`
- 伤害写入：`CreatureCmd.Damage` → `History.DamageReceived(...)`
- 参考 mod（仅思路）：DamageMeter 的 `HistoryTailer` + `CombatDataCollector`

---

# 无槽位遭遇 mid-combat 召唤 — 版本跟进备忘

DevMode 在 **无槽位遭遇**（如 `BYRDONIS_ELITE`：`HasScene=false`，`Slots` 为空）里 mid-combat 加会召唤的怪（如 Ovicopter），或让原版召唤逻辑在无槽位战里跑起来时，可能触发：

`NCombatRoom.AddCreature` → `creature.SlotName != null`（**空字符串 `""` 也算**）→ `EncounterSlots == null` → 抛异常。

**责任**：DevMode 使用场景触发；LustTravel2 无关（只有 Postfix，不加敌人）。正常地图进 `OVICOPTER_NORMAL` 不受影响。

## 已实现（DevMode 侧）

- [x] **合入** — `src/Patches/CombatSummonSlotCompatPatch.cs`
  - `CombatSummonSlotNormalizePatch`：`NCombatRoom.AddCreature` **Prefix**，`string.IsNullOrEmpty(SlotName)` → `null`
  - `CombatSummonSlotRepositionPatch`：`CreatureCmd.Add(Creature)` **Postfix**，无槽位敌人走 `CombatEnemyActions.RepositionEnemies` 自动排版
- [x] **DevMode 自己加怪** — `CombatEnemyActions.AddMonsterInternal` 已把 `GetNextSlot` 返回的 `""` 归一成 `null`，并打 `LogSlotlessSummonWarning`

Harmony 经 `MainFile` 的 `PatchAll()` 自动注册，无需额外挂接。

## 正式版（stable）更新时怎么改

Megacrit 把 **beta 合入 stable** 或 bump 游戏版本后，按下面 checklist 过一遍（`Slay the Spire 2` 源码 vs `Slay the Spire 2 v0.106.1(beta)` 对照）：

### 1. 查 vanilla 是否已修 Ovicopter 下蛋

| 版本 | `Ovicopter.LayEggsMove` 行为 |
|------|------------------------------|
| **stable（旧）** | `LastOrDefault(..., string.Empty)`，**无条件** `CreatureCmd.Add<ToughEgg>(..., slotName)` → 无槽位时传 `""`，必崩 |
| **beta 0.106.1+** | `LastOrDefault` 无默认、`if (text != null)` 才 Add → 无槽位时跳过下蛋，**不再因 Ovicopter 自身崩** |

**动作**：打开新 stable 的 `Core/Models/Monsters/Ovicopter.cs`。若已是 beta 写法，Ovicopter 单点崩溃可降级；若仍是 `string.Empty` 默认 + 无条件 Add，**必须保留** `CombatSummonSlotCompatPatch`。

### 2. 查 `NCombatRoom.AddCreature` / `EncounterModel.GetNextSlot`

- `AddCreature` 仍用 `SlotName != null`（不把 `""` 当无槽）→ **patch 仍有必要**（Fabricator、`GetNextSlot` 等路径仍会传 `""`）。
- 若 vanilla 改为 `!string.IsNullOrEmpty(SlotName)` 且 `GetNextSlot` 无槽时返回 `null` → 可评估 **移除 Normalize patch**，但 Postfix 重排仍建议保留（DevMode 无 scene 遭遇加怪排版）。

### 3. DevMode 构建目标

- **stable 包**：`local.props` 里 `Sts2Beta=false`（或不设），对照 `Slay the Spire 2` 源码。
- **beta 包**：`Sts2Beta=true` + `make sync-beta`，对照 beta 安装目录源码。
- 两套都要测：**BYRDONIS_ELITE（或任意无槽位战）→ mid-combat 加 Ovicopter → 等下蛋**，确认不抛 `EncounterSlots is null`。

### 4. 对外说明（issue / 群友）

- 用了 DevMode 在无槽位战里塞 Ovicopter → **DevMode 兼容问题**，不是 LustTravel2。
- 正常地图 `OVICOPTER_NORMAL` → 有 `EncounterSlots`，不是同一条 crash 链。
- 群友「产卵 + 打死小苍蝇 + 卡死」且**未**用 DevMode 换怪 → 另查 FoxHime 耐力/async，需 log。

## 相关源码路径（STS2）

- `Core/Models/Encounters/OvicopterNormal.cs` — `HasScene`, `Slots`
- `Core/Models/Encounters/ByrdonisElite.cs` — 无槽位范例
- `Core/Models/EncounterModel.cs` — `GetNextSlot` 默认 `string.Empty`
- `Core/Nodes/Rooms/NCombatRoom.cs` — `AddCreature` / `EncounterSlots`
- `Core/Models/Monsters/Ovicopter.cs` — `LayEggsMove`
