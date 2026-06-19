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

### Registration (mod init)

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

### DeckPlan and character packs

`DeckPlanInferer` builds a weight vector (thin/thick, attack, block, exhaust, scaling, …) from deck + relics + ascension. Vanilla characters register `IDeckPlanContributor` packs under `src/KitLib.Modules.AI/AI/Characters/Vanilla/`.

Mods can adjust deck planning and card tags:

```csharp
CompanionBridge.RegisterDeckPlanContributor(myDeckPlanContributor);
CompanionBridge.RegisterCardTagProvider(myCardTagProvider);
```

`ICardTagProvider` merges extra tags into `CardCatalog` for macro scoring. `IDeckPlanContributor.AdjustPlan` mutates `DeckPlan.Builder` before each macro decision.

A10 regression seeds: `tools/ai-bench/` (fixed seed list + log parser). Algorithm details: **[AI algorithm](/developer/ai-algorithm)**.

### Snapshot extensions

`GameSnapshot` writes mod data under `snapshot["extensions"][yourKey]`. Implement `IAiSnapshotContributor`:

```csharp
public interface IAiSnapshotContributor {
    string ExtensionKey { get; }  // e.g. "lusttravel2", "winefox"
    void Enrich(JsonObject snapshot, Player player, GamePhase phase);
}
```

Strategies **must** read `extensions.*`; KitLib does not hard-code mod power types.

### Combat scoring

`CombatScorer.PickBestCombatMove(snapshot)` scores play-card and end-turn moves using vanilla heuristics (threat vs block, lethal, energy efficiency, target selection). Mods adjust scores via `IAiMoveModifier`:

```csharp
public interface IAiMoveModifier {
    bool AppliesTo(string? characterId);
    int ModifyScore(JsonObject snapshot, GameAction move, int baseScore);
}
```

Implement `IDecisionMaker` for full control, or delegate non-combat phases to `SimpleStrategy` and use `CombatScorer` inside combat.

### Companion full pipeline

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
（维护者向长文；用户向说明见 README.zh-CN.md 与 [文档站](/guide/panels/)。）
:::
