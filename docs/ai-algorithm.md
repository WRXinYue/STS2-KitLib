# Solo AI 算法（StrongStrategy）

DevMode **AI Host → AutoPlay** 默认使用 `StrongStrategy`（设置项 `AutoPlayStrategy: Strong`）。这是一套纯规则、无 LLM 的单机自动化决策管线，目标是在 A10 左右具备可玩的宏观路线与战斗表现。

相关代码：`src/AI/` · 回归脚本：`tools/ai-bench/` · Mod 扩展说明：[README.md § Mod AI integration](../README.md#mod-ai-integration)

---

## 总览

```mermaid
flowchart TD
    subgraph loop [决策循环]
        A[AiPlayModule 轮询 / 决策点] --> B[Sts2StateProvider 识别 GamePhase]
        B --> C[GameSnapshot 捕获 JSON 快照]
        C --> D[StrategyResolver 解析 IDecisionMaker]
        D --> E[StrongStrategy.DecideAsync]
        E --> F[各阶段 Scorer / CombatSearch]
        F --> G[Sts2ActionExecutor 执行 GameAction]
    end

    subgraph knowledge [知识层]
        K1[CardCatalog / CardTagRules]
        K2[RelicCatalog]
        K3[DeckPlanInferer + CharacterPack]
    end

    C --> knowledge
    F --> knowledge
```

| 层级 | 职责 | 主要类型 |
| --- | --- | --- |
| 循环 | 轮询当前阶段、触发决策、写 `AiDecisionLog` | `AiPlayModule`, `GameLoop` |
| 快照 | 把 Run / 战斗 / UI 状态序列化为 `JsonObject` | `GameSnapshot`, `GameSnapshotPhaseCapture` |
| 规划 | 根据牌组 / 遗物 / 层数推断「想要什么牌」 | `DeckPlanInferer`, `DeckPlan` |
| 评分 | 各阶段选最优 `GameAction` | `*Scorer`, `CombatScorer` |
| 战斗搜索 | 不可变状态 + beam 搜索 + 斩杀检测 | `CombatPlanner`, `CombatSearch`, `LethalChecker` |
| 执行 | 点击 UI / 出牌 / 购货 | `Sts2ActionExecutor` |

**策略解析顺序**（Companion / 多人场景同样适用）：`netId` 注册表 → 角色 `CharacterAiRegistry` → 默认 `StrongStrategy`（`AutoPlayStrategy=Simple` 时回退 `SimpleStrategy`）。

---

## 决策循环

1. 单机 Run 开始且 `AutoPlayEnabled=true` 时，`AiPlayModule` 启动 `GameLoop`。
2. 每 `PollIntervalMs` 轮询一次当前 `GamePhase`；Harmony patch 也会在决策点调用 `OnDecisionPoint`。
3. `Sts2StateProvider` 调用 `GameSnapshot.Capture`，再经 `GameSnapshotPhaseCapture.Enrich` 补充 overlay 数据。
4. `IDecisionMaker.DecideAsync` 返回 `GameAction`（含 `Type`、`TargetIndex`、`Reason`）。
5. `Sts2ActionExecutor` 执行动作；`Reason` 写入日志，便于复盘。

多人 hand-play 时 AutoPlay **自动关闭**；LAN / Pseudo Co-op 走独立队列，见 [lan-host-drive-afk.md](./lan-host-drive-afk.md)。

---

## 快照字段（StrongStrategy 依赖）

### 全局

| 字段 | 含义 |
| --- | --- |
| `totalFloor`, `actIndex`, `actFloor` | 层数 / Act |
| `gold`, `currentHp`, `maxHp` | 经济与健康 |
| `characterId`, `ascensionLevel` | 角色与进阶 |
| `deck[]` | 牌组（id、name、rarity、cost、keywords、upgradeLevel） |
| `relics[]` | 已拥有遗物 |
| `potions[]` | 药水：id、slot、category、usage、rarity、retainScore |
| `hasOpenPotionSlots`, `potionSlotCount` | 腰带空位 |

### 战斗 `combat`

| 字段 | 含义 |
| --- | --- |
| `currentEnergy` | 当前能量 |
| `playerBlock` | 玩家格挡 |
| `hand[]` | 手牌（canPlay、cost、damage、block、targetType） |
| `enemies[]` | 敌人 HP、block、intentDamage、intentBlock、isAlive、`intentSteps[]`（下 1–2 回合预测） |

### 阶段扩展（`GameSnapshotPhaseCapture`）

| 字段 | 阶段 | 含义 |
| --- | --- | --- |
| `offeredCards[]` | CardReward | 奖励 / 选牌界面上的卡 |
| `deckSelectContext` | CardReward | `"reward"` / `"upgrade"` / `"remove"` / `"deckPick"` |
| `offeredRelics[]` | RelicSelection | 可选遗物 |
| `shopOffers[]` | Shop | card / relic / potion / removeCard |
| `restOptions[]` | RestSite | 休息选项按钮 |
| `restProceedReady` | RestSite | Proceed 是否可点（选完 heal/smith 后离开） |
| `mapNodes[]` | MapSelection | 可选地图节点 |
| `eventOptions[]`, `eventId` | EventChoice | 事件选项 |

Mod 扩展写入 `snapshot["extensions"][yourKey]`，StrongStrategy **不会**读取 mod 专有字段，需注册自定义 `IDecisionMaker` 或 `IAiMoveModifier`。

---

## DeckPlan（牌组规划向量）

`DeckPlanInferer.Infer(snapshot)` 在每次宏观决策前构建规划，供所有 Scorer 共用。

### 输出

| 属性 | 默认 | 说明 |
| --- | --- | --- |
| `TargetDeckSize` | 18 | 理想牌组大小 |
| `ThinPreference` | 动态 ∈ [-0.3, 1] | 删牌 / 跳过奖励的倾向 |
| `Weights[AiTag]` | 见下 | 各语义 tag 权重 |

### 基础权重（推断起点）

- `Attack` +1.0，`Block` +0.8，`Draw` +0.6
- 牌组含 ≥3 张 Exhaust 卡或 Exhaust 遗物 → `Exhaust` +1.2
- A7+ → `Block` +0.4；A10+ → `Attack` +0.3

### ThinPreference 调整

| 条件 | 调整 |
| --- | --- |
| 牌组 >18 且抽牌 tag 卡 <2 | +0.35 |
| 牌组 > TargetDeckSize+4 | +0.25 |
| 拥有 Thin 类遗物 | +0.2 |
| Act1 且 floor<15 | -0.15（早期略厚） |
| Act3+ 且牌组 >22 | +0.30 |

最后调用 `DeckPlanContributorHub`：原版五角色在 `src/AI/Characters/Vanilla/*Pack.cs` 注册额外权重与 `TargetDeckSize`（Silent/Defect 15、Ironclad 17、Necrobinder/Regent 16）。

### CodexPriorCatalog（社区 prior，可选）

离线从 [Spire Codex](https://spire-codex.com/runs) A10 宏观样本训练（`tools/codex-train/` → 嵌入 `src/AI/Data/codex-priors.json`）。启动时由 `AiKnowledgeBootstrap` 加载；**不替代**规则层，只在各 `*Scorer` 上叠加有界 bonus（±15，× `CodexPriorWeight`，默认 1，settings.json 可调 0 关闭）。

| 表 | 用途 | 接入点 |
| --- | --- | --- |
| `cards` | 选牌 pick_rate / bonus | `MacroScorerHelper.ScoreCardOffer` |
| `skip` | skip 阈值偏移 | `DeckEvaluator.SkipOpportunityCost` |
| `rest` | HEAL/SMITH/LIFT 偏好 | `RestScorer`（HP 45–85% 区间） |
| `remove` | 常删牌 bonus | `ShopScorer`、`DeckSelectScorer` |
| `relics` | 遗物 pick bonus（`event` / `combat_reward` / `shop` context） | `MacroScorerHelper.ScoreRelicOffer` |
| `events` | Neow / 事件选项 pick bonus | `EventChoiceScorer` via `GetEventOptionBonus` |

`NormalizeContext` 支持 `shop`、`event`、`combat_reward`（此前 `event` 被误映射为 `combat_reward`，已修复）。

重训流程：`tools/codex-crawl export-parquet` → `uv run codex-train --parquet ../codex-crawl/data/macro_samples.parquet train` → `dotnet build`。

Holdout（v1，4944 runs）：card_choice top-1 **56.7%**（随机 33%）；rest **60.4%**。

### 辅助公式

```
ScoreTags(tags, plan) = Σ plan.GetWeight(tag)
DilutionPenalty(deckSize, plan) = max(0, deckSize - TargetDeckSize) × ThinPreference × 0.8
```

---

## DeckEvaluator（牌组质量与删牌边际收益）

`DeckEvaluator.Evaluate(snapshot, plan)` 是删牌 / skip / 选牌删谁 的**单一真相源**（`src/AI/Planning/DeckEvaluator.cs`）。

### DeckMetrics

| 字段 | 含义 |
| --- | --- |
| `MeanValue` | 牌组内单卡均分 |
| `WorstValue` / `WorstCardName` | 最低分卡（删牌首选） |
| `RemovalUplift` | 删最差卡的边际收益（含 burn 压力项） |
| `StarterBloat` | Strike/Defend/Curse 冗余度（相对 plan 目标） |
| `StrikeSurplus` | 超出 `TargetStrikeCount` 的 Strike 张数 |
| `DefendSurplus` | 超出 `TargetDefendCount` 的 Defend 张数 |
| `ThinGap` | 超出 `TargetDeckSize` 的牌数 |
| `ExhaustCount` | 带 Exhaust tag 的卡数 |
| `CardsNeedingBurn` | 宏观「还需烧/删」债务：thinGap + starter 冗余；exhaust 构筑额外计入非 exhaust 填充卡 |
| `BlockSourceCount` | 非 starter、cost≤2 的过渡防张数（`DeckSurvivability`） |
| `DrawSourceCount` | 非 starter 抽牌源张数 |
| `BlockDeficit` | max(0, `TargetBlockSources` − BlockSourceCount) |
| `DrawDeficit` | max(0, `TargetDrawSources` − DrawSourceCount) |
| `SurvivalGap` | BlockDeficit×2 + DrawDeficit |
| `ConsistencyScore` | 一致性 0..1 |

`DeckPlan` 续航目标（`*Pack.cs`）：Silent block/draw `1/3`；Ironclad/Defect `2/2`；默认 block/draw `2/2`。Strike/Defend 目标：Silent `0/1`；Ironclad `2/2`。

**宏观选牌续航 bonus**（`MacroScorerHelper`）：过渡防 +6+BlockDeficit×4；抽牌源 +4+DrawDeficit×3。**Skip**：BlockDeficit≥2 → −6；DrawDeficit≥2 且 deck 厚 → −4；续航满且 MeanValue≥12 → +4。

### 单卡价值 ScoreInDeck（`DeckCardScoring`）

已在牌组内的卡，**不含** dilution 项，但含冗余惩罚：

| 因素 | 逻辑 |
| --- | --- |
| 基础 | ScoreTags + RarityScore + 0费/高费/已升级 |
| 诅咒 | −40 |
| Strike 冗余 | 每张 −15×max(0, strikeCount−1)，**与 deckSize 无关** |
| Defend 冗余 | 每张 −12×max(0, defendCount−1) |
| Starter 且 tag 分低 | −10 |
| 0 费且 tag 分低 | −5 |

### RemovalUplift

```
RemovalUplift = (MeanValue - WorstValue)
              + StarterBloat × 4
              + round(DilutionPenalty(deckSize))
              + FutureThinBonus(actIndex, floor)
              + round(CardsNeedingBurn × 1.5)
```

- `StarterBloat`：`StrikeSurplus` + `DefendSurplus×0.8` + 每张诅咒 +3（目标来自 `TargetStrikeCount` / `TargetDefendCount`）
- `CardsNeedingBurn`：`ThinGap + StrikeSurplus + DefendSurplus`；若 `IsExhaustFocused`，再加 `max(0, nonExhaustFiller − ExhaustCount×2)`
- `FutureThinBonus`：Act1 +5/+3，Act2 +2，Act3 +0（越早删，长期收益越高）
- **门槛**：`RemovalUplift < MinRemovalUplift(11)` 时不买商店删牌（**无 deckSize 硬门槛**）

**示例**：12 张牌、4 Strike、均分 14、最差 Strike 值 2 → uplift ≈ (14−2)+8+future ≈ 24，小卡组仍会删 Strike。

---

## 知识层：Tag 与 Catalog

### AiTag 枚举

`Attack`, `Block`, `Draw`, `Exhaust`, `Scaling`, `Thin`, `Energy`, `Aoe`, `Setup`, `Utility`

### CardCatalog

- 启动时在 `ModelDb.Init` 之后索引全卡（`AiKnowledgeBootstrap` + `ModelDbInitPatch`）。
- `CardTagRules` 从 **卡 id 前缀**、**CardType**、**CardKeyword**（Exhaust/Retain 等）推断 tag，避免逐卡硬编码。
- `CardMechanicIndex` + `OfficialMechanicProbe`：CardKeyword / DynamicVars / 类型图 / LocString key / 字段引用 → `CardMechanicFlags`（含 `AddsCardsToDeck` 如劫掠）；`DeckSynergyEvaluator` 按机制类型算 deck 协同；**仅无结构信号时**才用英文描述 fallback。
- Mod 可通过 `ICardTagProvider` 合并额外 tag。

### RelicCatalog / RelicMechanicIndex

- 从遗物 id 推断 tag（如 Exhaust、Thin），用于 `DeckPlanInferer` 与遗物评分。
- `RelicMechanicIndex` 通过 `OfficialMechanicProbe` 解析 `RelicModel` 结构与 loc key（如 `HeftyTablet` → 稀有三选一 + Injury）；描述文本为最后兜底。

---

## StrongStrategy 阶段路由

| GamePhase | 决策器 | 动作类型 |
| --- | --- | --- |
| Combat | `PotionScorer` → `CombatSearch` → `CombatScorer` | UsePotion / PlayCard / EndTurn |
| MapSelection | `MapScorer` → `MapPathPlanner` | SelectMapNode |
| CardReward | `DeckSelectScorer` | PickCardReward / SkipCardReward |
| RelicSelection | `RelicScorer` | PickRelic |
| Shop | `ShopScorer` | PurchaseShopItem / RemoveCardAtShop / LeaveShop |
| RestSite | `RestScorer` | Rest / UpgradeCard / Proceed |
| EventChoice | `EventChoiceScorer` | SelectEventChoice |
| RewardScreen | 固定 | CollectReward |
| PostCombatTransition | 固定 | Proceed |
| TreasureRoom | 固定 | HandleTreasureRoom |

---

## 宏观评分器

### CardRewardScorer（战斗后三选一）

用于 `deckSelectContext == "reward"`。

**单卡得分**（`MacroScorerHelper.ScoreCardOffer`）— 边际牌组模型：

```
marginal = DeckQualityScore(deck ∪ {card}) − DeckQualityScore(deck)
score    = marginal
         + DeckSynergyEvaluator（机制协同，如变形×攻击池）
         + ScoreDeckDilutionOffer（AddsCardsToDeck 如劫掠）
         + EarlyCardRewardAdjustments
         + CodexBonus × deckFitFactor
```

`DeckQualityScore` 综合 `TotalValue`、`MeanValue`、`DilutionPenalty`、`SurvivalGap`、`StarterBloat`、`ConsistencyScore`（与删牌评估同一套 `DeckEvaluator`）。

`deckFitFactor`：缺口已填（`BlockDeficit=0` 且 `DrawDeficit=0`）、`ThinGap>0`、`MeanValue≥12`、已有变形核心且候选非变形 → 衰减 Codex 先验。

**RarityScore**（在 `ScoreInDeck` 内）：Ancient 30 · Rare 25 · Uncommon 15 · Common 8 · Starter 3 · Event 12

**跳过逻辑**（`DeckEvaluator.SkipOpportunityCost`，与边际 pick 同一质量模型）：

```
skipScore = ThinPreference×14
          + DilutionPenalty×8
          + ThinGap×3
          + max(0, DeckSize − TargetDeckSize)×2
          + max(0, DeckQualityScore/DeckSize − 12)×2
          + (SurvivalGap=0 ? 5 : 0)
          + 后期 act/floor 加厚项
          + Codex skip offset
```

- `StarterBloat≥3` / `StrikeSurplus≥3` → 降低 skip（更倾向拿牌或去删敲）
- `SurvivalGap≥2` 且 `BlockDeficit≥2` → 降低 skip（前期仍缺过渡）
- exhaust 构筑且 `CardsNeedingBurn≥5` → +8 skip
- 若 `bestScore < max(MinPickScore, skipScore)` → `SkipCardReward`（日志含 `quality` / `thin` / `survival`）

### DeckSelectScorer（休息 smith / 商店删牌）

由 `deckSelectContext` 分流：

| context | 行为 |
| --- | --- |
| `upgrade` | 选 `ScoreUpgradeCandidate` 最高、未升满的卡（与 `AiCombatCardSelector` 篝火 smith 同源） |
| `remove` | 选 `ScoreInDeck` **最低**的卡（与 DeckEvaluator 同一套评分） |
| 其他 | 委托 `CardRewardScorer` |

篝火 smith 实际由 `CardSelectCmd` → `AiCombatCardSelector` 选牌（非 UI 点击路径时也走同一评分）。`ScoreUpgradeCandidate = ScoreInDeck + 机制协同 + 变形/脆弱加分 − strike/defend/基础牌惩罚`。

### ShopScorer

**约束**：购物后至少保留 `MinGoldAfterShopping = 25` 金币。

**删牌分**（边际对比，非牌数门槛）：

```
removeScore = RemovalUplift - cost/4 - OpportunityCost + 金币充裕加成
```

- `RemovalUplift < 11` → 不删
- `OpportunityCost`：Act3 或 gold 紧张时额外扣分（留金给后续商店）
- 日志示例：`Remove [Strike] uplift=28 strikes+2 burnDebt=5 score=24 vs buy=relic(22)`

**购买分**：

- card → `ScoreCardOffer − cost/8`
- relic → `ScoreRelicOffer − cost/12`
- potion → `PotionInventoryScorer.ValueOffer − cost/25`（瓶满且不值得腾槽 → 0）

**决策**：若 `removeScore > 0` 且 `removeScore ≥ bestPurchaseScore` → 删牌；否则买最高分商品；都不值得 → `LeaveShop`。

购买 `TargetIndex` 与快照一致：**仅计 card/relic/potion 顺序**，不含 removal slot。

### RestScorer

1. **`restProceedReady`** 或 **无 rest 选项** → 立即 `Proceed` 离开。
2. **heal 已消耗**（`healIdx < 0`）→ 仅 HP≥75% 且有升级目标时 smith，否则 `Proceed`（避免 heal 后 poll 误触 SMITH）。
3. 否则按 HP、路径与选项 id：
   - HP <55%，或 HP <70% 且 planner 下一步为 Elite → heal
   - Codex rest prior（HP 45–85%）；若 prior 为 SMITH 且 HP<75% 且下一步 Elite → 跳过 prior
   - HP≥75% 且无 Elite  ahead → smith（有升级目标）
   - HP <75% → heal
   - 否则 smith 或 Proceed

`TargetIndex` 为休息站按钮在 UI 中的**绝对索引**（与快照 `restOptions[].index` 对齐）。

### RelicScorer

```
score = RarityScore + Σ(plan.GetWeight(tag) × 3) + CodexRelicBonus(context)
```

已拥有同名/id → −100。`context` 来自快照 `relicChoiceContext`（EventRoom → `event`，Boss/精英 → `combat_reward`），同一遗物在 event/combat 下 Codex bonus 可不同。

### MapPathPlanner（Act 全程路径 + 画线）

替代原单步贪心 `MapScorer`：对 `RunState.Map` 做 **Boss 后向 DP**（DAG 拓扑序 O(V+E)），选「当前 → Boss 总分最高」路径上的下一步。

**文献依据**：STS 地图为 DAG，MIT 15.053 最长路径 DP；Bazzaz & Cooper (FDG 2025) 路径枚举；Miles Oram macro 三要素 flexibility/safety/resource。

**算法**：

```
bestToBoss(p) = nodeScore(p, ctx) + max_{c ∈ children} ( edgeBonus(p,c) + bestToBoss(c) )
```

- `ctx` = `MapRouteContext`（HP、gold、DeckPlan、DeckMetrics、WantsShopRemoval）
- 决策：从当前位置选 `argmax bestToBoss(child)`；日志 `path=Rest→Shop→M→…`

**MapNodeWeightScorer 节点分（动态）**：

| 类型 | 逻辑 |
| --- | --- |
| RestSite | 低 HP 高；成型牌组低 |
| Shop | `RemovalUplift≥11` + gold → 高；`StarterBloat` 高 → 高；`StrikeSurplus≥2` +10；`CardsNeedingBurn≥4` +14 |
| Elite | 高 HP + 高 MeanValue → 高；低 HP / A7+ → 负；Act1 floor 6–9 且 HP<75% → −15 |
| Monster | baseline；缺金略升 |
| Treasure | +20 |
| Unknown | Monster×0.7 + Event×0.3 期望 |
| Ancient | 同 Unknown |

**邻接边加成**：Rest→Elite（低 HP **−10**）；Shop→*（需删牌 +6）；Elite→Elite（低 HP −12）；Rest→Rest −5；Treasure→Elite +4；pathRisk 高时 Rest→Elite、Elite→Elite 额外扣分。

**连战续航（`MapSurvivalIndex` + `PathSurvivalRisk`）**：

- 自 Boss 行反向 DP：每格 `CombatsToRest`（到下 Rest 的预期战斗数）、`ElitesToRest`
- 战斗权重：M/Elite=1，Unknown=0.7，Ancient=0.5
- `PathRisk = combats×10×blockFactor×drawFactor×hpFactor + elites×12×hpFactor`（block/draw deficit 来自 DeckMetrics）
- DP 选边时对 **child** 叠加 `PathRiskNodeAdjust`：Rest +risk；Elite −risk；Monster/Shop/Unknown 高压时略降
- 日志：`risk=N fightsToRest=M blockDef=D`；Rest 在 `CombatsToRest≥3 && BlockDeficit≥1` 时 heal 阈值 55%→65%

**MapScorer**：委托 `MapPathPlanner`；无 run 数据时 greedy fallback。

**地图画线**（仅 `AutoPlayEnabled` + loop 运行中）：

- 打开地图：`MapPathPlanner.Plan` + `MapPathOverlay` 将规划边染为金色
- 关闭地图：恢复 path dot 原色；`MapPathPlanner.ClearCache`

源码：`MapPathPlanner.cs`、`MapNodeWeightScorer.cs`、`MapPathOverlay.cs`

### EventChoiceScorer

- 识别 Neow（`eventId` 或 option `textKey` 含 NEOW）。
- 快照 `eventOptions[]` 含 `optionKey`（`EventOptionInfer`：textKey/modelId/中文标题）。
- 评分：`keyword baseline` + `GetEventOptionBonus`（无 event prior 时 fallback `GetRelicBonus`）+ **`DeckSynergyEvaluator` 机制分**（遗物/卡选项）。
- 当 event prior `n≥20` 时 Codex 权重 ×1.5（`codex_primary` 模式，keyword 降为 baseline 10）。
- Reason 日志：`Neow pick [title] score=N key=HEFTY_TABLET codex=+4 synergy=+12 codex_primary`。
- 普通事件同样接 `GetEventOptionBonus`；无 prior 时保留关键词兜底。

---

## 战斗决策

### 顺序

```
DecideCombat:
  1. PotionScorer.TryUsePotion  → 有则 UsePotion
  2. CombatSearch.PickBestMove  → 浅层搜索
  3. CombatScorer.PickBestCombatMove → 单步启发式
  4. EndTurn
```

### PotionScorer（战斗：全瓶打分，取最高 ≥25）

启动时 `PotionMechanicIndex` 索引官方 `PotionModel`（category / usage / rarity）；`PotionTierCatalog` 提供 retain 分（`potion-tiers.json`）。

对 `potions[]` 每格按 **category + IntentCalculator + DeckPlan** 打分，日志 `potion candidates [ID:+score] …`；`TargetIndex` 为 **slot**（非枚举序）。

| Category | 典型加分局面 |
| --- | --- |
| Heal / Block | 低 HP、fatal、NeedsBlock |
| DamageSingle / AoE | 斩杀差伤害、多敌 |
| Energy | 能量不够打出关键牌且接近 lethal |
| Buff / Debuff / Random | DeckPlan 权重 + 局面 |
| 浪费惩罚 | HP 高、非精英、高 retain 药（如 SHAPED_ROCK） |

### PotionInventoryScorer（宏观腰带）

| 场景 | 行为 |
| --- | --- |
| 奖励屏瓶满 | `ShouldMakeRoom` → 先 `DiscardPotion` 最弱 slot 再收；不值替换则 `DismissRewards`；UI 弹选瓶时 `PotionRewardHelper` 点 holder |
| 篝火 | 每访仅一次：Heal/Smith 禁用后只 `Proceed`；`Proceed()` 优先点篝火 Proceed，不走战后等待 |
| 商店瓶满 | `ShopScorer` 先 `DiscardPotion` 再买；购买分 = `ValueOffer − cost/25` |
| 腾槽条件 | `incomingValue > lowestHeld + 8` |

`DiscardPotion` / `UsePotion` 均使用 **slot index**（`player.GetPotionAtSlotIndex`）。

### BlockThreatEvaluator

集中「本回合受伤风险」判定，供 `IntentCalculator`、`CombatScorer`、`CombatSearch` 共用：

| 方法 | 语义 |
| --- | --- |
| `ShouldScoreBlock` | `NeedsBlock` 或 `netIncoming ≥` 阈值（floor≤15 用 6，否则 8） |
| `ShouldSuppressTransform` | `IsFatalIfUnblocked` **或** `NeedsBlock`（非安全斩杀豁免）；轻度 `ShouldScoreBlock`  alone 不压制 |
| `ThreatDiscountScale` | 威胁下变形 follow-up 折扣（0.4–1.0，随 `BlockUrgency`） |
| `HasAffordableHandTransform` | 手牌有能量可出的 `TransformsHandAttacks` 且存在可变形攻击 |
| `IsStarterDefend` | `DEFEND_*` 或 STARTER 稀有度挡牌 |

### CombatSetupEvaluator

从 snapshot **实时比较** setup vs 立刻攻击（无固定阶段门）：

| 方法 | 语义 |
| --- | --- |
| `ComputeVulnerableDeferValue` | 挂易伤+过回合价值：后续攻击×1.5 估算、incoming/urgency、残血可斩则减分 |
| `ComputeVulnerableDeferOpportunityCost` | 攻击牌机会成本：`max(0, deferValue − attackDamage) / 2` |

### IntentCalculator

```
TotalIncomingDamage = Σ 存活敌人 intentDamage
NetDamageAfterBlock = max(0, incoming − playerBlock)
EstimateStatusDamage = Σ playerPowers（BURN/POISON/INFEST/DOOM 的 amount）
NeedsBlock：fatal 始终 true；floor≤15 且 net≥6 为 true（早期小伤害也挡）
CanEliminateIncomingThreats：仅单攻击敌人、本回合可斩杀、且 net≤8 时才因斩杀跳过防守
CanLethal 豁免：net ≤ max(6, effectiveHp/5) 或（HP>65% 且 net < effectiveHp/3）时不挡
BlockUrgency 0–100 驱动攻击惩罚与 EndTurn 惩罚
```

### CombatScorer（单步）

对每张可打出、能量足够的牌生成 `(PlayCard, score)`；对需选目标的 Attack 枚举敌人索引。

**出牌分（概要）**：

| 情况 | 加分 |
| --- | --- |
| NeedsBlock 且 Skill/挡牌 | 25 + min(block, net)×2；incoming≥15 再 +12；fatal 再 +25 |
| ShouldScoreBlock（非 NeedsBlock）且挡牌 | 15 + min(block, net)×2（incoming 威胁保底） |
| 一级防 + incoming>0 | +8（`starter-block`） |
| NeedsBlock 时 Attack | −BlockUrgency/2 − max(0, net−damage)/2；可斩杀但会死 → lethal-risky +8 而非 +25 |
| !ShouldScoreBlock 时的挡牌 | −40 |
| 先打可变形攻击（手牌有廉价变形技） | `attack-before-transform` −min(伤害增益, 50) |
| 受威胁时 transform（原始力量等） | `suppressTransform`：跳过固定 +20/+40；follow-up 按 `ThreatDiscountScale` 折扣；`transform-threat-discount` 最多 −20 |
| 易伤 setup（痛击等） | `defer-vuln-setup` 动态加分；攻击侧 `defer-vuln` 机会成本 |
| 低 HP 且 Skill 且 NeedsBlock | +15 |
| 自损牌（HEMOKINESIS 等）且 HP<65% | −30 |
| Attack | 20 + cost×5 + damage + 目标加成（残血敌 +30；有存活爪牙时召唤者 +35、爪牙 −30）；CanLethal +25 |
| Skill | 15 + cost×2 +（NeedsBlock 时 block/2） |
| AOE / 多敌 Attack | +12 / +15；多敌无谓 Defend −20 |
| 高费 | −(cost−1)×2 |

**EndTurn** 基础分 −10；NeedsBlock 且 incoming>0 再 −15；敌人已挂易伤时略加分（最多 +15）。

**机制驱动加分**（`MechanicCombatBonus`，权重在 `CombatScoreWeights`，非按 card id 写死）：

| 机制 | 来源 | 效果 |
| --- | --- | --- |
| `TransformsHandAttacks` | 原始力量等 | 伤害增益 + 变形后 follow-up/2（威胁时 follow-up 折扣，不清零）；无威胁时 +20/+40 等固定套路分 |
| `AppliesVulnerable` | DynamicVar 探测（痛击等） | 无易伤时：18 + 层数×8 + followup/3 + `CombatSetupEvaluator` defer 动态分；已有易伤 −12 |
| `AppliesWeak` | DynamicVar | 类似，权重略低 |
| Setup Skill | 上述机制牌 | **不再**吃「非挡牌 Skill −40」惩罚 |

伤害读取：`CombatCardStats.ResolveDamage` — 快照 `damage` 缺失时回退 `CardMechanicIndex.Damage`（修复巨石 0 伤评分）。

**战斗日志**（`AiCombatVerboseLog=true`，默认开）：每次出牌记录 top pick + 最多 4 个备选，含分项 `[attack:+31, mechanic:+48, …]`。见 `CombatDecisionLog`。

Mod 可通过 `IAiMoveModifier.ModifyScore` 调整任意 move 分数（日志中显示 `mod:+N`）。

### Enemy Intelligence Layer

启动时镜像卡牌 `CardMechanicIndex`：`MonsterMechanicIndex` 从 `ModelDb.AllEncounters` 枚举怪物，经 `OfficialMonsterProbe` + `MonsterMoveScanner` 探测：

| 探测源 | 输出 |
| --- | --- |
| 类型图（`IllusionPower` / `MinionPower` / `AdaptablePower`） | `EnemyMechanicFlags` |
| 状态机 Intent 扫描（`SetUpForCombat` 后遍历 `MoveState`） | 每怪 `moveId → IntentType[]` |
| Encounter 共现 + `monster-probe-overrides.json` | `spawnedMonsterIds[]` |

运行时快照 enrich（`GameSnapshot.CaptureEnemies`）：

- `mechanicFlags`、`intentTags[]`、`nonDamageThreat`
- `intentSteps[]` 扩展为 `{ moveId, intentDamage, intentTypes[], nonDamageThreat, isUncertain }`
- `CombatEnemyGraph` 观测召唤并写入 `summonerIndex`

**验证**：MCP `dev_dump_monster_mechanics`；`tools/monster-probe-dump/dump-monster-mechanics.ps1` 导出 JSON。

**调优边界**：当前数据支持权重网格（`EnemyThreatWeights`）+ `tools/ai-bench` 胜率；不支持端到端 ML（缺目标选择标签）。场景回归见 `tools/ai-bench/scenarios.json`。

### EnemyTargetPriority / MinionEngagementPolicy

`isMinion`（`IsSecondaryEnemy` + power）标记爪牙。`MinionEngagementPolicy` 按 `mechanicFlags` 动态偏置（替代固定 ±30）：

| 场景 | 偏置 |
| --- | --- |
| 标准爪牙 + 主人存活 | 主人 +35，爪牙 −30 |
| `HasIllusionRevive`（Parafright 等） | 爪牙 −60，模拟层不 wipe |
| `PeerSummon`（TwoTailedRat） | 正常 HP/伤害排序 |
| 高 debuff 威胁爪牙 | −10 ~ +15（按 `nonDamageThreat`） |

`ThreatModel.EffectiveIncoming` = `intentDamage + nonDamageThreat`；`NextTurnIncoming` 在 `isUncertain` 时 ×1.15。`OrderByPriority` 与 `LethalChecker` 使用上述策略。

### LethalChecker

对每个存活敌人（经 `OrderByPriority` 排序）：若 `EstimateMaxDamage(手牌, 能量)` ≥ `hp + block`，判定可斩杀并返回 targetIndex。`EstimateMaxDamage` 按伤害降序贪心消耗能量（含 id 猜测伤害）。

### Combat Simulation Layer

战斗层采用 **Immutable State → Legal Actions → Apply → Evaluate → Bounded Search** 前向模拟。动机是修正常见 STS 战斗 bot 的静态威胁求和、AOE 误枚举、召唤目标错误等问题；模块位于 `src/AI/Combat/Simulation/`，`CombatSearch.PickBestMove` 仅委托 `CombatPlanner`。

```mermaid
flowchart LR
    snap[GameSnapshot] --> state[CombatState.FromSnapshot]
    state --> legal[LegalActionGenerator]
    legal --> sim[CombatSimulator.Apply]
    sim --> next[Next CombatState]
    next --> eval[CombatEvaluator]
    next --> beam[CombatPlanner beam search]
    beam --> action[GameAction]
```

#### 设计原则与常见局限

常见 STS 战斗 bot 往往在以下环节失真；DevMode 模拟层针对这些点做了显式建模：

| 常见局限 | 典型表现 | DevMode 做法 |
| --- | --- | --- |
| 威胁静态求和 | `incoming` 不因击杀重算 | `ThreatModel`：只计存活且 `intentDamage>0`；模拟击杀后自动下降 |
| AOE 当单体枚举 | `AllEnemy` 按每个 target 重复评分 | `LegalActionGenerator` 单动作 + `CombatSimulator` 群伤一次转移 |
| 召唤语义缺失 | 打爪牙不打主人 | `MonsterMechanicIndex` + `MinionEngagementPolicy` + 主怪死后 `ThreatModel.OnPrimaryEnemyKilled`（跳过幻象爪牙） |
| 评分与局面混用 | 一套启发式既评牌又评回合末 | `CombatScorer`（单步/fallback）与 `CombatEvaluator`（叶节点局面）分离 |
| 浅层搜索 | depth 1–2 或全枚举不剪枝 | `CombatPlanner` beam（160ms / depth 4 / width 10） |
| 仅本回合 intent | 不看下回合高伤 | 快照 `intentSteps[]` + `CombatEvaluator` 下回合权重 |
| 非伤害 intent 忽略 | debuff/summon 不计威胁 | `nonDamageThreat` + `EnemyThreatWeights` |
| 硬编码 / 不透明 | card id 列表、缺什么靠调权重掩盖 | 快照 + `CardMechanicProfile` + `MonsterMechanicProfile`；文档列出「故意不模拟」边界 |

**已知局限**（不假装完美）：敌人行动顺序仍用 sum（未做按位 `SequentialIncoming`）；`CombatSimulator` 不模拟抽牌、复杂 power、完整 buff 结算；`SimLethalChecker.EstimateMaxDamage` 为贪心上界；beam 宽度与效用权重需 `tools/ai-bench` 实战调参。

| 类型 | 职责 |
| --- | --- |
| `CombatState` | 不可变战斗状态（玩家 HP/block/能量、手牌、敌人 intent） |
| `LegalActionGenerator` | 枚举合法动作；`AllEnemy` / AOE **只生成一个动作**（`SecondaryIndex=-1`） |
| `CombatSimulator` | `Apply(action)`：扣能量、单体/群伤、变形、易伤/虚弱、挡牌；主怪死后爪牙 `MarkDead` |
| `ThreatModel` | `Incoming` = 存活且 `intentDamage>0` 的敌人求和；击杀后自动重算；`IntentCalculator` 桥接 |
| `AoeDamageEstimator` | 群伤斩杀判定、`FindBestAoeLethalAction` |
| `CombatEvaluator` | 叶节点多因子效用（HP、netDamage×3/4、下回合 intent、敌 HP、易伤、浪费能量） |
| `CombatPlanner` | beam search + 时间预算；输出路径首步 |

**CombatPlanner 参数**：

| 参数 | 值 |
| --- | --- |
| 时间预算 | 160 ms |
| 最大深度 | 4（玩家出牌，不含敌人回合） |
| Beam 宽度 | 10 |

**快捷路径**（在 beam 之前）：`SimLethalChecker.CanLethalAfterTransform` → `AoeDamageEstimator` 群杀 → `CanLethal`；**`ShouldSuppressTransform` 为 true 时跳过**（有 incoming 且非安全斩杀时不抢先变形/攻击）。

**故意不模拟**（渐进补全）：抽牌堆、RNG、完整敌人回合出牌与 buff 结算、复杂 power 互动。`intentSteps[]` 仅用于评估下回合威胁权重，不推进敌人状态机。

**NeedsBlock 与多攻击者**：`CanEliminateIncomingThreats` 不再要求单一威胁；可 AOE/逐个斩杀全部 `intentDamage>0` 敌人，或模拟击杀最高 intent 后 `Incoming` 归零且 net ≤ `SafeLethalNetMax`。

快照 enrich：`GameSnapshot.CaptureEnemies` 调用 `MonsterIntentReader.CaptureIntentSteps`，每敌最多 3 步 `{ moveId, intentDamage, intentTypes[], nonDamageThreat, isUncertain }`。

**战斗日志**（`AiCombatVerboseLog`）：`CombatDecisionLog` 额外输出 `IN=` / `ND=` / `NXT=` 与 `tgt= bias= flags=` 便于 bench 调参。

`CombatScorer` 保留为 **fallback**（beam 无结果时）及 mod `IAiMoveModifier` 单步估价基线。

---

## AI HUD（游戏内托管叠加层）

单机开启 **AI Host** 且 `AiHudEnabled=true` 时，[`AiHudOverlayUI`](../src/UI/AiHudOverlayUI.cs) 在 `NGlobalUi` **左上角**以纯文字堆叠显示：

| 行 | 内容 |
| --- | --- |
| 标题 | `AI hosting` |
| 阶段 | 当前 `GamePhase` 简写 |
| Plan | 阶段策略摘要（`AiHudModel.BuildStrategyLine`） |
| Next | 最近一次 `GameAction`（`AiHudState` ← `GameLoop` 发布） |
| 可选参数 | 战斗：`HP/BLK/IN/E`；非战斗：`F/G/HP`（`AiHudShowParams`） |
| 可选分项 | 战斗 `Reason` 中的 `[block:+N, mechanic:+N, …]`（`AiHudShowScoreTerms`） |

**显示条件**：`AutoPlayEnabled && AiPlayModule.IsRunning && !多人联机`。

与侧边栏 **AI Terminal** 分工：HUD 只展示当前一步与少量参数；完整决策历史仍在 `AiDecisionLog` / session log。设置项在 AI Host 面板 Controls 区。

---

## GameLoop poll 去重

`AiPlayModule` 每 500ms 轮询当前 phase；`GameLoop` 在决策前：

- **Combat** 且 `isPlayPhaseActive=false` → 跳过（等敌方回合/动画）；`Sts2StateProvider` 在 `CombatManager.IsInProgress` 时仍返回 `Combat`（避免敌方回合误判为 `Unknown` → `AdvanceOverlay` 刷屏）
- **EndTurn 已提交**（`_endTurnPending`）→ 跳过，直到 phase 变化或 play phase 结束
- **相同 fingerprint**（phase+action+target）2s 内 → 跳过（避免 EndTurn 刷屏、Rest 双动作）

出牌同步由 `Sts2ActionExecutor` 内 `TryManualPlay` + `WaitForManualPlayAsync` 阻塞完成，不在 loop 层做 fingerprint / 能量账本等待。

快照手牌 `cost` 使用 `EnergyCost.GetWithModifiers(All)`（实战费用），不再只用 `Canonical`。

Run 结束时 `ResetDedupeState()` 清空状态。

---

## 执行层要点（Sts2ActionExecutor）

| 动作 | 行为 |
| --- | --- |
| `PlayCard` | 单机：`TryManualPlay` → action queue → `SpendResources`（扣能量 + UI）；`WaitForManualPlayAsync` 等到牌离开手牌。勿用 `CardCmd.AutoPlay`（遗物/效果自动打出路径，不扣玩家能量）。Pseudo Co-op：`PlayCardAction` 入队 |
| `PickCardReward` | 奖励屏点卡；`NDeckCardSelectScreen` 点选后点 Proceed |
| `SkipCardReward` | Skip / Back |
| `SelectRestSiteOption` | 按**绝对**按钮 index 点击；disabled 则失败 |
| `Proceed` | overlay 或 room 内 ProceedButton |
| `RemoveCardAtShop` | 购买 removal slot → 进入 DeckSelect（context=remove） |
| `PurchaseShopItem` | 按非 removal 顺序的 affordable slot 购买 |

决策日志中的 `Reason=` 字段与上述 Scorer 字符串一一对应，调参时优先看日志。

---

## SimpleStrategy 对比

设置 `AutoPlayStrategy: Simple` 启用旧版启发式：

| 方面 | Simple | Strong |
| --- | --- | --- |
| 地图 | 第一个节点 | MapScorer 多因素 |
| 卡牌 | 早期拿第一张 / 后期 skip | DeckPlan + 阈值 skip |
| 商店 | 买第一张 / 不删牌 | ShopScorer 删牌 vs 购买 |
| 休息 | HP<60% rest 否则 upgrade | RestScorer + Proceed + smith 目标 |
| 战斗 | 低血 block → 高费 attack | Potion + CombatSearch + 意图/block |

---

## Mod 扩展点（摘要）

| 接口 | 用途 |
| --- | --- |
| `IDecisionMaker` | 完全接管决策 |
| `IDeckPlanContributor` | 调整 DeckPlan.Builder |
| `ICardTagProvider` | 扩展 CardCatalog tag |
| `IAiMoveModifier` | 战斗 move 加分 |
| `IAiSnapshotContributor` | 写入 `extensions.*` |

注册入口：`CompanionBridge.Register*`（见 README）。

---

## 调参与回归

- 固定 seed 列表：`tools/ai-bench/seeds.json`
- 跑完一局后：`powershell -File tools/ai-bench/run-bench.ps1`
- 目标：各角色 A10 胜率约 **40–50%**（见 bench README）

常用调参旋钮：

| 文件 | 旋钮 |
| --- | --- |
| `CardRewardScorer` | `MinPickScore`；skip 委托 `SkipOpportunityCost` |
| `ShopScorer` | `MinGoldAfterShopping`、`DeckEvaluator.MinRemovalUplift` |
| `DeckEvaluator` / `DeckCardScoring` | 冗余惩罚系数、FutureThinBonus |
| `RestScorer` | HP 比例阈值 |
| `MapNodeWeightScorer` / `MapPathPlanner` | 节点分、边加成 |
| `CombatSearch` | `TimeBudgetMs`、`MaxDepth` |
| `DeckPlanInferer` / `*Pack.cs` | tag 权重、ThinPreference |

---

## 源码索引

| 路径 | 内容 |
| --- | --- |
| `src/AI/AutoPlay/Strategies/StrongStrategy.cs` | 阶段分发 |
| `src/AI/Planning/DeckPlanInferer.cs` | 规划推断 |
| `src/AI/Planning/DeckEvaluator.cs` | 牌组质量、RemovalUplift |
| `src/AI/Planning/DeckCardScoring.cs` | 牌组内单卡评分 |
| `src/AI/Planning/MapPathPlanner.cs` | Act 路径 DP |
| `src/AI/Planning/MapNodeWeightScorer.cs` | 地图节点/边权重 |
| `src/Map/MapPathOverlay.cs` | AutoPlay 路径高亮 |
| `src/AI/AutoPlay/Scoring/*.cs` | 宏观评分 |
| `src/AI/AutoPlay/Scoring/CombatScorer.cs` | 战斗单步评分 |
| `src/AI/Combat/CombatSearch.cs` | 浅层搜索 |
| `src/AI/Combat/LethalChecker.cs` | 斩杀 |
| `src/AI/Combat/IntentCalculator.cs` | 意图伤害 |
| `src/AI/Sts2/Snapshots/` | 快照捕获 |
| `src/AI/Sts2/Helpers/Sts2CombatPlayHelper.cs` | 等待手动出牌 action queue 完成 |
| `src/AI/Sts2/Sts2ActionExecutor.cs` | UI 执行 |
