# KitLib 待办

## Android / 移动端

- [ ] **DevMode 在 v0.103.3 APK 上初始化失败** — Harmony 打 `Creature.LoseHpInternal` 时报 `MissingMethodException: DamageResult.set_UnblockedDamage(int)`。PC Steam 同版本 v0.103.3 的 `sts2.dll` 反射仍为 `int`；Android 运行时找不到 int setter（疑似同版本号、不同平台二进制）。需从 APK 抽出 `sts2.dll` 对照后再改补丁/统计代码；若双端都要支持，可能要条件编译或分构建。**先搁置。**

## 战斗统计（Phase 2 剩余）

MVP 与 Phase 2 主体已随 0.10.0+ 发布，见 CHANGELOG。

### 数据与归因

- [x] 宠物/召唤物伤害归主人（`ResolveDamageOwner`：宠物 dealer → `PetOwner`）
- [x] 无 dealer 能力伤害推断（毒、绞杀、Haunt → `CombatHistoryTailer`）
- [ ] Misery 等多玩家分摊、同目标助攻按比例拆分

- [x] 能量消耗统计（`EnergySpent` / `EnergySpentByCard`）
- [ ] 能量浪费（回合末未使用能量）

### 联机

- [x] 合作模式玩家 chip 切换、显示名称（统计面板 `playerRow`）
- [x] 联机右上角分数 overlay（可拖、位置持久化 `CombatStatsMpOverlayPos*`）
- [ ] 按玩家固定分色（当前仅 leader 高亮，条形图按贡献类别上色）

- [ ] 助攻 / 同目标伤害分摊（现有 Synergy 加分 ≠ 伤害归属拆分）

### UI

- [x] 战斗内贡献展示 — 单人右侧 Context Pane 竖条 + 联机 top-right overlay（见 0.10.0 CHANGELOG）
- [x] 联机 overlay 开关（统计面板内 `Show top-right score panel`；总开关：**Settings → In-game right sidebar**）
- [x] 联机 overlay 位置持久化（拖拽保存）
- [ ] 开战自动打开完整统计面板
- [ ] 单人 rail 独立开关/位置（可选；现与 enemy intent 等共用 sidebar 设置）

### 集成

- [x] 内部事件 `CombatStatsTracker.Changed` / `NotifyStatsUpdated()`
- [ ] 脚本 Hook：`OnCombatStatsUpdated`（`ScriptBridge` 对外，可选）

## 无槽位遭遇 mid-combat 召唤 — 版本跟进备忘

实现：`src/Patches/CombatSummonSlotCompatPatch.cs`（Normalize + Reposition）；`CombatEnemyActions` 加怪路径 `""` → `null`。

Megacrit **beta 合入 stable** 或 bump 游戏版本后过一遍：

### 1. 查 vanilla 是否已修 Ovicopter 下蛋

| 版本 | `Ovicopter.LayEggsMove` 行为 |
|------|------------------------------|
| **stable（旧）** | `LastOrDefault(..., string.Empty)`，**无条件** `CreatureCmd.Add<ToughEgg>(..., slotName)` → 无槽位时传 `""`，必崩 |
| **beta 0.106.1+ / 0.107.0** | `LastOrDefault` 无默认、`if (text != null)` 才 Add → 无槽位时跳过下蛋，**不再因 Ovicopter 自身崩** |

**动作**：打开新 stable 的 `Core/Models/Monsters/Ovicopter.cs`。若已是 beta 写法，Ovicopter 单点崩溃可降级；若仍是 `string.Empty` 默认 + 无条件 Add，**必须保留** `CombatSummonSlotCompatPatch`。

### 2. 查 `NCombatRoom.AddCreature` / `EncounterModel.GetNextSlot`

- `AddCreature` 仍用 `SlotName != null`（不把 `""` 当无槽）→ **patch 仍有必要**（Fabricator、`GetNextSlot` 等路径仍会传 `""`）。
- 若 vanilla 改为 `!string.IsNullOrEmpty(SlotName)` 且 `GetNextSlot` 无槽时返回 `null` → 可评估 **移除 Normalize patch**，但 Postfix 重排仍建议保留（DevMode 无 scene 遭遇加怪排版）。

### 3. KitLib 统一构建与回归

- **双版本编译**：`make build-profiles`（`eng/sts2-refs/`，见 [docs/sts2-api-profiles.md](docs/sts2-api-profiles.md)）
- **反射触点**：`make extract-touchpoints` → `make check-api`；发版前 `make verify-profiles`
- **日常开发**：`make init` → `make sync` / `make sync-full`（`local.props` 的 `Sts2Dir` + `Sts2Profile`；切 Steam 分支后重新 `make init`）
- **运行时**：`Sts2RuntimeProfile` 按游戏版本 + 平台选择 API profile（stable `0.103.x` vs beta `0.107.x`）
- 两套游戏安装都要测：**BYRDONIS_ELITE（或任意无槽位战）→ mid-combat 加 Ovicopter → 等下蛋**，确认不抛 `EncounterSlots is null`。

## 参考

- 战斗统计：`src/CombatStats/`、`src/UI/CombatStatsUI*.cs`
- 无槽位召唤：`src/Patches/CombatSummonSlotCompatPatch.cs`
- STS2：`Core/Models/Monsters/Ovicopter.cs`、`Core/Nodes/Rooms/NCombatRoom.cs`
