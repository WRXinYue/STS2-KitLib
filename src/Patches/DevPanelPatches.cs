using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NGlobalUi), "_Ready")]
public static class GlobalUiReadyPatch {
    // Track the instance we already attached to avoid duplicate panels on re-entry
    private static NGlobalUi? _attached;
    private static AssetWarmupService? _warmup;

    public static void Postfix(NGlobalUi __instance) {
        if (!KitLibState.IsActive) return;
        if (KitLibState.PseudoCoopLaunchPending) return;
        if (KitLibState.PseudoCoopDeferHeavyUi) {
            EnsureProcessNodeOnly(__instance);
            return;
        }

        if (_attached != null && !GodotObject.IsInstanceValid(_attached))
            _attached = null;

        // NGlobalUi can survive scene changes; TreeExiting may detach HUD while _attached stays set.
        if (_attached == __instance) {
            KitLibPanelOps.OnPanelAttach?.Invoke(__instance);
            KitLibPanelOps.OnPanelSync?.Invoke(__instance);
            return;
        }

        TryAttachDeferred(__instance);
    }

    /// <summary>Lightweight process hook without DevPanel (pseudo-coop embark).</summary>
    public static void EnsureProcessNodeOnly(NGlobalUi? globalUi) {
        if (!KitLibState.IsActive || globalUi == null) return;
        var parent = (Node)globalUi;
        if (parent.GetNodeOrNull<Node>("KitLibProcessNode") != null) return;
        parent.AddChild(new KitLibProcessNode { Name = "KitLibProcessNode" });
    }

    /// <summary>Attach DevPanel and warmup after pseudo-coop scene transition stabilizes.</summary>
    /// <param name="deferWarmupBuild">When true, warmup job list builds on next Process tick instead of synchronously.</param>
    /// <param name="skipWarmup">When true, DevPanel only; call <see cref="StartWarmupIfAttached"/> later.</param>
    public static void TryAttachDeferred(NGlobalUi? globalUi, bool deferWarmupBuild = false, bool skipWarmup = false) {
        if (!KitLibState.IsActive) return;
        if (KitLibState.PseudoCoopDeferHeavyUi) {
            EnsureProcessNodeOnly(globalUi);
            return;
        }
        if (globalUi == null) return;
        if (_attached == globalUi) return;
        _attached = globalUi;
        DevPanel.Attach(globalUi);
        KitLibPanelOps.OnPanelSync?.Invoke(globalUi);
        KitLibCheatOps.EnsureRuntimeStatModifiers?.Invoke();

        if (!skipWarmup) {
            if (_warmup == null)
                _warmup = new AssetWarmupService();

            if (deferWarmupBuild)
                _warmup.DeferBuildToProcess();
            else
                _warmup.Ready();
        }

        EnsureProcessNodeOnly(globalUi);
    }

    /// <summary>Dual-instance LAN: AI Host rail only (no context pane / warmup).</summary>
    public static void TryAttachDualInstanceMinimal(NGlobalUi? globalUi) {
        if (!KitLibState.IsActive) return;
        if (KitLibState.PseudoCoopDeferHeavyUi) {
            EnsureProcessNodeOnly(globalUi);
            return;
        }
        if (globalUi == null) return;
        if (_attached == globalUi) return;

        KitLibState.DualInstanceMinimalRail = true;
        _attached = globalUi;
        DevPanel.Attach(globalUi);
        EnsureProcessNodeOnly(globalUi);
    }

    /// <summary>Start asset warmup after pseudo-coop late init (DevPanel already attached).</summary>
    public static void StartWarmupIfAttached() {
        if (_attached == null) return;
        if (_warmup == null)
            _warmup = new AssetWarmupService();
        _warmup.DeferBuildToProcess();
    }

    internal static void Process(double delta) {
        KitLibCheatOps.ProcessFrame?.Invoke(delta);
        _warmup?.Process(delta);
    }
}

[HarmonyPatch(typeof(NCardLibrary), "ShowCardDetail")]
public static class CardLibraryShowCardDetailPatch {
    public static bool Prefix(NCardHolder holder) {
        return !DevPanel.TryHandleCardSelection(holder);
    }
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "OnRelicEntryPressed")]
public static class RelicCollectionEntryPressedPatch {
    public static bool Prefix(NRelicCollectionEntry entry) {
        return !DevPanel.TryHandleRelicSelection(entry);
    }
}

[HarmonyPatch(typeof(NCardLibrary), "OnSubmenuClosed")]
public static class CardLibraryClosedPatch {
    public static void Postfix() {
        DevPanel.NotifyCardLibraryClosed();
    }
}

[HarmonyPatch(typeof(NRelicCollection), "OnSubmenuClosed")]
public static class RelicCollectionClosedPatch {
    public static void Postfix() {
        DevPanel.NotifyRelicCollectionClosed();
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "GetCardVisibility")]
public static class CardVisibilityPatch {
    public static void Postfix(ref ModelVisibility __result) {
        if (KitLibState.InDevRun) {
            __result = ModelVisibility.Visible;
            return;
        }
        if (KitLibState.IsActive && SettingsStore.Current.ShowHiddenCards)
            __result = ModelVisibility.Visible;
    }
}

/// <summary>Append library-hidden cards when DevMode option is enabled.</summary>
[HarmonyPatch(typeof(NCardLibraryGrid), "_Ready")]
public static class CardLibraryIncludeHiddenPatch {
    private static readonly AccessTools.FieldRef<NCardLibraryGrid, List<CardModel>> AllCardsRef =
        AccessTools.FieldRefAccess<NCardLibraryGrid, List<CardModel>>("_allCards");

    public static void Postfix(NCardLibraryGrid __instance) {
        if (!KitLibState.IsActive || !SettingsStore.Current.ShowHiddenCards) return;

        var list = AllCardsRef(__instance);
        var existing = new HashSet<CardModel>(list);
        foreach (var card in ModelDb.AllCards) {
            if (card.ShouldShowInCardLibrary || existing.Contains(card)) continue;
            list.Add(card);
            existing.Add(card);
        }
    }
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "LoadRelicNodes")]
public static class RelicVisibilityPatch {
    public static void Prefix(
        IEnumerable<RelicModel> relics,
        ref HashSet<RelicModel> seenRelics,
        ref HashSet<RelicModel> unlockedRelics) {
        if (!KitLibState.InDevRun) return;
        foreach (var relic in relics) {
            seenRelics.Add(relic);
            unlockedRelics.Add(relic);
        }
    }
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "LoadRelics")]
public static class RelicCategoryVisibilityPatch {
    public static void Prefix(
        ref HashSet<RelicModel> seenRelics,
        ref HashSet<RelicModel> allUnlockedRelics) {
        if (!KitLibState.InDevRun) return;
        foreach (var relic in ModelDb.AllRelics) {
            seenRelics.Add(relic);
            allUnlockedRelics.Add(relic);
        }
    }
}

public static class AncientUnlockPatch {
    public static void Postfix(ActModel __instance, ref IEnumerable<AncientEventModel> __result) {
        if (!KitLibState.InDevRun) return;
        __result = __instance.AllAncients;
    }
}

[HarmonyPatch(typeof(Glory), nameof(Glory.GetUnlockedAncients))]
public static class GloryAncientPatch {
    public static void Postfix(ActModel __instance, ref IEnumerable<AncientEventModel> __result)
        => AncientUnlockPatch.Postfix(__instance, ref __result);
}

[HarmonyPatch(typeof(Hive), nameof(Hive.GetUnlockedAncients))]
public static class HiveAncientPatch {
    public static void Postfix(ActModel __instance, ref IEnumerable<AncientEventModel> __result)
        => AncientUnlockPatch.Postfix(__instance, ref __result);
}

[HarmonyPatch(typeof(Overgrowth), nameof(Overgrowth.GetUnlockedAncients))]
public static class OvergrowthAncientPatch {
    public static void Postfix(ActModel __instance, ref IEnumerable<AncientEventModel> __result)
        => AncientUnlockPatch.Postfix(__instance, ref __result);
}

[HarmonyPatch(typeof(Underdocks), nameof(Underdocks.GetUnlockedAncients))]
public static class UnderdocksAncientPatch {
    public static void Postfix(ActModel __instance, ref IEnumerable<AncientEventModel> __result)
        => AncientUnlockPatch.Postfix(__instance, ref __result);
}
