---
title:
  en: Mod AI integration
  zh-CN: Mod AI 集成
top: 9850
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
KitLib exposes a **soft-dependency** AI platform for content mods. KitLib owns the loop, snapshot capture, action execution, and vanilla combat scoring; your mod bridge supplies character semantics (snapshot extensions, strategy rules, score tweaks).

**Requires:** KitLib loaded at runtime. Reference `KitLib.dll` or **`STS2.KitLib.Abstractions`** at compile time only (do not bundle KitLib in your mod).
:::

::: zh-CN
KitLib 为内容 mod 提供**软依赖**的 AI 平台：KitLib 负责循环、快照采集、动作执行与原版战斗评分；你的 mod 桥接层提供角色语义（快照扩展、策略规则、分数修正）。

**需要：** 运行时加载 KitLib。编译期引用 `KitLib.dll` 或 **`STS2.KitLib.Abstractions`** 即可（不要把 KitLib 打进自己的 mod 包）。
:::

### Registration (mod init){lang="en"}

### 注册（mod 初始化）{lang="zh-CN"}

::: en
Call from your mod’s `[ModInitializer]` after KitLib is available:

```csharp
using KitLib.AI.Core;
using KitLib.Companion;

CompanionBridge.RegisterCharacterStrategy(
    "YOUR_CHARACTER_MODEL_ID",
    myStrategy,
    new CharacterAiProfile(SupportsNonCombat: true));

CompanionBridge.RegisterSnapshotContributor(mySnapshotContributor);
CompanionBridge.RegisterMoveModifier(myMoveModifier);

// Optional per-spawn override (e.g. custom companion summon):
CompanionBridge.RegisterStrategy(netId, overrideStrategy);
```

**Strategy resolution order:** per-`netId` registry → `CharacterAiRegistry` by character model id → `StrongStrategy` fallback (`Simple` if `AutoPlayStrategy=Simple`).
:::

::: zh-CN
在 mod 的 `[ModInitializer]` 里、确认 KitLib 可用后调用：

```csharp
using KitLib.AI.Core;
using KitLib.Companion;

CompanionBridge.RegisterCharacterStrategy(
    "YOUR_CHARACTER_MODEL_ID",
    myStrategy,
    new CharacterAiProfile(SupportsNonCombat: true));

CompanionBridge.RegisterSnapshotContributor(mySnapshotContributor);
CompanionBridge.RegisterMoveModifier(myMoveModifier);

// 可选：按 spawn 覆盖（如自定义同伴召唤）
CompanionBridge.RegisterStrategy(netId, overrideStrategy);
```

**策略解析顺序：** 按 `netId` 注册表 → 按角色 model id 的 `CharacterAiRegistry` → `StrongStrategy` 回退（`AutoPlayStrategy=Simple` 时用 `Simple`）。
:::

### DeckPlan and character packs{lang="en"}

### DeckPlan 与角色包{lang="zh-CN"}

::: en
`DeckPlanInferer` builds a weight vector (thin/thick, attack, block, exhaust, scaling, …) from deck + relics + ascension. Vanilla characters register `IDeckPlanContributor` packs under `src/KitLib.Modules.AI/AI/Characters/Vanilla/`.

Mods can adjust deck planning and card tags:

```csharp
CompanionBridge.RegisterDeckPlanContributor(myDeckPlanContributor);
CompanionBridge.RegisterCardTagProvider(myCardTagProvider);
```

`ICardTagProvider` merges extra tags into `CardCatalog` for macro scoring. `IDeckPlanContributor.AdjustPlan` mutates `DeckPlan.Builder` before each macro decision.

A10 regression seeds: `tools/ai-bench/` (fixed seed list + log parser). Algorithm details: **[AI algorithm](/developer/ai-algorithm)**.
:::

::: zh-CN
`DeckPlanInferer` 根据牌组 + 遗物 + 进阶生成权重向量（薄/厚、攻击、格挡、消耗、成长等）。原版角色在 `src/KitLib.Modules.AI/AI/Characters/Vanilla/` 注册 `IDeckPlanContributor`。

Mod 可调整牌组规划与卡牌标签：

```csharp
CompanionBridge.RegisterDeckPlanContributor(myDeckPlanContributor);
CompanionBridge.RegisterCardTagProvider(myCardTagProvider);
```

`ICardTagProvider` 向 `CardCatalog` 合并额外标签供宏观评分；`IDeckPlanContributor.AdjustPlan` 在每次宏观决策前修改 `DeckPlan.Builder`。

A10 回归种子：`tools/ai-bench/`（固定种子列表 + 日志解析）。算法细节见 **[AI 算法](/developer/ai-algorithm)**。
:::

### Snapshot extensions{lang="en"}

### 快照扩展{lang="zh-CN"}

::: en
`GameSnapshot` writes mod data under `snapshot["extensions"][yourKey]`. Implement `IAiSnapshotContributor`:

```csharp
public interface IAiSnapshotContributor {
    string ExtensionKey { get; }  // e.g. "lusttravel2", "winefox"
    void Enrich(JsonObject snapshot, Player player, GamePhase phase);
}
```

Strategies **must** read `extensions.*`; KitLib does not hard-code mod power types.
:::

::: zh-CN
`GameSnapshot` 把 mod 数据写在 `snapshot["extensions"][yourKey]`。实现 `IAiSnapshotContributor`：

```csharp
public interface IAiSnapshotContributor {
    string ExtensionKey { get; }  // 如 "lusttravel2", "winefox"
    void Enrich(JsonObject snapshot, Player player, GamePhase phase);
}
```

策略**必须**读 `extensions.*`；KitLib 不会硬编码 mod 能力类型。
:::

### Combat scoring{lang="en"}

### 战斗评分{lang="zh-CN"}

::: en
`CombatScorer.PickBestCombatMove(snapshot)` scores play-card and end-turn moves using vanilla heuristics (threat vs block, lethal, energy efficiency, target selection). Mods adjust scores via `IAiMoveModifier`:

```csharp
public interface IAiMoveModifier {
    bool AppliesTo(string? characterId);
    int ModifyScore(JsonObject snapshot, GameAction move, int baseScore);
}
```

Implement `IDecisionMaker` for full control, or delegate non-combat phases to `SimpleStrategy` and use `CombatScorer` inside combat.
:::

::: zh-CN
`CombatScorer.PickBestCombatMove(snapshot)` 用原版启发式给出牌/结束回合打分（威胁 vs 格挡、斩杀、能量效率、目标选择）。Mod 通过 `IAiMoveModifier` 调整分数：

```csharp
public interface IAiMoveModifier {
    bool AppliesTo(string? characterId);
    int ModifyScore(JsonObject snapshot, GameAction move, int baseScore);
}
```

要完全自控可实现 `IDecisionMaker`；或把非战斗阶段委托给 `SimpleStrategy`，战斗内用 `CombatScorer`。
:::

### Companion full pipeline{lang="en"}

### 同伴全流程{lang="zh-CN"}

::: en
By default, pseudo-coop companions only run AI during **combat**. For map/events/rewards/rest/shop, set `EnableNonCombatAi: true` on `CompanionSpawnRequest`:

```csharp
CompanionBridge.TrySummon(new CompanionSpawnRequest(
    character,
    EnableNonCombatAi: true,
    MirrorMapVotes: true));
```

`CompanionDecisionHost` runs `GameLoop` for registered companions in overlay phases when `CharacterAiProfile.SupportsNonCombat` is true. Map votes still mirror the host by default (`MirrorMapVotes`).

Build a bridge DLL against a fresh `KitLib.dll` (`build/KitLib/KitLib.dll` after `dotnet build`). Ship the bridge as a separate mod with `"dependencies": ["YourContentMod"]` (KitLib is runtime-only).
:::

::: zh-CN
默认伪联机同伴只在**战斗**跑 AI。地图/事件/奖励/休息/商店也要 AI 时，在 `CompanionSpawnRequest` 设 `EnableNonCombatAi: true`：

```csharp
CompanionBridge.TrySummon(new CompanionSpawnRequest(
    character,
    EnableNonCombatAi: true,
    MirrorMapVotes: true));
```

`CharacterAiProfile.SupportsNonCombat` 为 true 时，`CompanionDecisionHost` 在 overlay 阶段为已注册同伴跑 `GameLoop`。地图投票默认仍由主机镜像（`MirrorMapVotes`）。

用新编的 `KitLib.dll`（`dotnet build` 后 `build/KitLib/KitLib.dll`）编译桥接 DLL。桥接作为独立 mod 发布，`"dependencies": ["YourContentMod"]`（KitLib 仅运行时依赖）。
:::
