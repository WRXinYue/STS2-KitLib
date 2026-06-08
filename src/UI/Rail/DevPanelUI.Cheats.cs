using System.Collections.Generic;
using KitLib;
using KitLib.Cheat;
using KitLib.Map;
using KitLib.Icons;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Helpers;
using KitLib.Panels;
using KitLib.Presets;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal static void ShowCheatsOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        var existing = ((Node)globalUi).GetNodeOrNull<Control>(CheatsRootName);
        if (existing != null) {
            ((Node)globalUi).RemoveChild(existing);
            existing.QueueFree();
        }

        var (root, _, vbox) = CreateOverlayRoot(globalUi, CheatsRootName, 920f);

        AddBrowserNavTab(vbox, I18N.T("panel.cheats", "Cheats"));
        MpCheatUi.AddSessionBanner(vbox);

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };

        // ── Column containers (ExpandFill, spacing between sections) ──────
        VBoxContainer MakeCol() {
            var c = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            c.AddThemeConstantOverride("separation", 12);
            return c;
        }
        var colA = MakeCol();
        var colB = MakeCol();
        var colC = MakeCol();

        // Wrap each column in a MarginContainer for left/right inner padding
        MarginContainer WrapCol(VBoxContainer col) {
            var m = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            m.AddThemeConstantOverride("margin_left", 12);
            m.AddThemeConstantOverride("margin_right", 12);
            m.AddThemeConstantOverride("margin_top", 6);
            m.AddThemeConstantOverride("margin_bottom", 6);
            m.AddChild(col);
            return m;
        }
        var wrapA = WrapCol(colA);
        var wrapB = WrapCol(colB);
        var wrapC = WrapCol(colC);

        ColorRect MakeDivider() => new ColorRect {
            Color = KitLibTheme.Separator,
            CustomMinimumSize = new Vector2(1, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        var divAB = MakeDivider();
        var divBC = MakeDivider();

        var columns = new HBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        columns.AddThemeConstantOverride("separation", 0);

        scroll.AddChild(columns);
        vbox.AddChild(scroll);

        // ── Build sections (each a VBoxContainer with header + items) ─────

        VBoxContainer NewSection(string key, string fallback) {
            var s = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            s.AddThemeConstantOverride("separation", 4);
            s.AddChild(CreateSectionHeader(I18N.T(key, fallback)));
            return s;
        }

        // Player
        var secPlayer = NewSection("panel.section.player", "Player");
        secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteHp", "Infinite HP"), I18N.T("cheat.infiniteHp.desc", "Player cannot lose HP"), () => KitLibState.PlayerCheats.InfiniteHp, MpCheatUi.WrapBoolSetter(v => KitLibState.PlayerCheats.InfiniteHp = v)));
        secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteBlock", "Infinite Shield"), I18N.T("cheat.infiniteBlock.desc", "Block refills to 999 after loss"), () => KitLibState.PlayerCheats.InfiniteBlock, MpCheatUi.WrapBoolSetter(v => {
            KitLibState.PlayerCheats.InfiniteBlock = v;
            if (!MpCheatSession.InMultiplayerRun && v && RunContext.TryGetRunAndPlayer(out _, out var bp)) {
                var c = bp.Creature;
                if (c.Block < 999) c.GainBlockInternal(999 - c.Block);
            }
        })));
        secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteEnergy", "Infinite Energy"), I18N.T("cheat.infiniteEnergy.desc", "Keep energy at 999+ (immediate + after spend)"), () => KitLibState.PlayerCheats.InfiniteEnergy, MpCheatUi.WrapBoolSetter(v => {
            KitLibState.PlayerCheats.InfiniteEnergy = v;
            if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
        })));
        secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteStars", "Infinite Stars"), I18N.T("cheat.infiniteStars.desc", "Keep stars at 999+ (immediate + after spend)"), () => KitLibState.PlayerCheats.InfiniteStars, MpCheatUi.WrapBoolSetter(v => {
            KitLibState.PlayerCheats.InfiniteStars = v;
            if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
        })));
        if (MpCheatUi.IsFrameCheatAllowed) {
            secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.godMode", "God Mode"), I18N.T("cheat.godMode.desc", "Auto-heal to max HP every frame"), () => CheatRunState.StatModifiers?.GodMode ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.GodMode = v; }));
            secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.negateDebuffs", "Negate Debuffs"), I18N.T("cheat.negateDebuffs.desc", "Continuously remove all debuffs"), () => CheatRunState.StatModifiers?.NegateDebuffs ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.NegateDebuffs = v; }));
            secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.alwaysPlayerTurn", "Always Player Turn"), I18N.T("cheat.alwaysPlayerTurn.desc", "Force combat to player turn"), () => CheatRunState.StatModifiers?.AlwaysPlayerTurn ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.AlwaysPlayerTurn = v; }));
            secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.drawToLimit", "Draw to Hand Limit"), I18N.T("cheat.drawToLimit.desc", "Auto-draw to 10 cards"), () => CheatRunState.StatModifiers?.DrawToHandLimit ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.DrawToHandLimit = v; }));
            secPlayer.AddChild(CreateCheatToggle(I18N.T("cheat.extraDraw", "Extra Draw Each Turn"), I18N.T("cheat.extraDraw.desc", "Draw extra cards at turn start"), () => CheatRunState.StatModifiers?.ExtraDrawEachTurn ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.ExtraDrawEachTurn = v; }));
            secPlayer.AddChild(CreateCheatNumberEdit(I18N.T("cheat.extraDrawAmount", "Extra Draw Amount"), 1, 20, () => CheatRunState.StatModifiers?.ExtraDrawEachTurnAmount ?? 1, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.ExtraDrawEachTurnAmount = (int)v; }));
        }
        secPlayer.AddChild(CreateCheatSlider(I18N.T("cheat.defenseMultiplier", "Defense Multiplier"), I18N.T("cheat.defenseMultiplier.desc", "Multiply block gained"), 0, 10, 0.5f, () => KitLibState.PlayerCheats.DefenseMultiplier, MpCheatUi.WrapFloatSetter(v => KitLibState.PlayerCheats.DefenseMultiplier = v)));

        // Enemy
        var secEnemy = NewSection("panel.section.enemy", "Enemy");
        secEnemy.AddChild(CreateCheatToggle(I18N.T("cheat.freezeEnemies", "Freeze Enemies"), I18N.T("cheat.freezeEnemies.desc", "Enemies skip their turns"), () => KitLibState.EnemyCheats.FreezeEnemies, MpCheatUi.WrapBoolSetter(v => KitLibState.EnemyCheats.FreezeEnemies = v)));
        secEnemy.AddChild(CreateCheatToggle(I18N.T("cheat.oneHitKill", "One-Hit Kill"), I18N.T("cheat.oneHitKill.desc", "Deal massive damage to enemies"), () => KitLibState.EnemyCheats.OneHitKill, MpCheatUi.WrapBoolSetter(v => KitLibState.EnemyCheats.OneHitKill = v)));
        if (MpCheatSession.InMultiplayerRun) {
            var killBtn = CreatePlainButton(I18N.T("cheat.killAllOnce", "Kill All (sync)"), MdiIcon.Skull);
            killBtn.Pressed += () => TaskHelper.RunSafely(MpCheatCombatEnemyCoordinator.TryHostKillAllAsync());
            killBtn.Disabled = !MpCheatSession.SessionArmed;
            secEnemy.AddChild(killBtn);
        }
        else {
            secEnemy.AddChild(CreateCheatToggle(I18N.T("cheat.killAll", "Kill All Enemies"), I18N.T("cheat.killAll.desc", "Continuously kill all enemies"), () => CheatRunState.StatModifiers?.KillAllEnemies ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.KillAllEnemies = v; }));
        }
        if (MpCheatUi.IsFrameCheatAllowed)
            secEnemy.AddChild(CreateCheatToggle(I18N.T("cheat.autoAlly", "Auto-Act Friendly Monsters"), I18N.T("cheat.autoAlly.desc", "Auto-execute friendly monster turns"), () => CheatRunState.StatModifiers?.AutoActFriendlyMonsters ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.AutoActFriendlyMonsters = v; }));
        secEnemy.AddChild(CreateCheatSlider(I18N.T("cheat.damageMultiplier", "Damage Multiplier"), I18N.T("cheat.damageMultiplier.desc", "Multiply damage dealt to enemies"), 0, 10, 0.5f, () => KitLibState.EnemyCheats.DamageMultiplier, MpCheatUi.WrapFloatSetter(v => KitLibState.EnemyCheats.DamageMultiplier = v)));

        // Inventory
        var secInventory = NewSection("panel.section.inventory", "Inventory");
        var goldEdit = CreateCheatNumberEdit(I18N.T("cheat.editGold", "Edit Gold"), 0, 99999,
            () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.Gold; },
            v => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return; p.Gold = (int)v; });
        MpCheatUi.ApplyMultiplayerUnsupported(goldEdit, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secInventory.AddChild(goldEdit);
        secInventory.AddChild(CreateCheatSlider(I18N.T("cheat.goldMultiplier", "Gold Multiplier"), I18N.T("cheat.goldMultiplier.desc", "Multiply gold gained"), 0, 10, 0.5f, () => KitLibState.GameplayModifiers.GoldMultiplier, MpCheatUi.WrapFloatSetter(v => KitLibState.GameplayModifiers.GoldMultiplier = v)));
        secInventory.AddChild(CreateCheatToggle(I18N.T("cheat.freeShop", "Free Shop"), I18N.T("cheat.freeShop.desc", "All shop purchases are free"), () => KitLibState.GameplayModifiers.FreeShop, MpCheatUi.WrapBoolSetter(v => KitLibState.GameplayModifiers.FreeShop = v)));

        // Status
        var secStatus = NewSection("panel.section.status", "Status");
        var energyEdit = CreateCheatNumberEdit(I18N.T("cheat.editEnergyCap", "Edit Energy Cap"), 0, 99,
            () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.MaxEnergy; },
            v => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return; p.MaxEnergy = (int)v; });
        MpCheatUi.ApplyMultiplayerUnsupported(energyEdit, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secStatus.AddChild(energyEdit);
        var potionSlotsEdit = CreateCheatNumberEdit(I18N.T("cheat.editPotionSlots", "Edit Potion Slots"), 0, 20,
            () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.MaxPotionCount; },
            v => {
                if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return;
                int current = p.MaxPotionCount;
                int diff = (int)v - current;
                if (diff > 0) { p.AddToMaxPotionCount(diff); }
                else if (diff < 0) {
                    for (int i = current - 1; i >= current + diff; i--) {
                        var potion = p.GetPotionAtSlotIndex(i);
                        if (potion != null) p.DiscardPotionInternal(potion);
                    }
                    p.SubtractFromMaxPotionCount(-diff);
                }
            });
        MpCheatUi.ApplyMultiplayerUnsupported(potionSlotsEdit, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secStatus.AddChild(potionSlotsEdit);
        var maxScoreToggle = CreateCheatToggle(I18N.T("cheat.maxScore", "Max Score"), I18N.T("cheat.maxScore.desc", "Enable max score tracking"), () => KitLibState.GameplayModifiers.MaxScore, v => KitLibState.GameplayModifiers.MaxScore = v);
        MpCheatUi.ApplyMultiplayerUnsupported(maxScoreToggle, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secStatus.AddChild(maxScoreToggle);
        var scoreMult = CreateCheatSlider(I18N.T("cheat.scoreMultiplier", "Score Multiplier"), I18N.T("cheat.scoreMultiplier.desc", "Multiply score gained"), 0, 10, 0.5f, () => KitLibState.GameplayModifiers.ScoreMultiplier, v => KitLibState.GameplayModifiers.ScoreMultiplier = v);
        MpCheatUi.ApplyMultiplayerUnsupported(scoreMult, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secStatus.AddChild(scoreMult);

        // Rewards
        var secRewards = NewSection("panel.section.rewards", "Rewards");
        secRewards.AddChild(CreateCheatToggle(I18N.T("cheat.alwaysPotion", "Always Reward Potion"), null, () => KitLibState.PlayerCheats.AlwaysRewardPotion, MpCheatUi.WrapBoolSetter(v => KitLibState.PlayerCheats.AlwaysRewardPotion = v)));
        secRewards.AddChild(CreateCheatToggle(I18N.T("cheat.alwaysUpgrade", "Always Upgrade Reward"), I18N.T("cheat.alwaysUpgrade.desc", "Card rewards are always upgraded"), () => KitLibState.PlayerCheats.AlwaysUpgradeCardReward, MpCheatUi.WrapBoolSetter(v => KitLibState.PlayerCheats.AlwaysUpgradeCardReward = v)));
        secRewards.AddChild(CreateCheatToggle(I18N.T("cheat.maxRarity", "Max Card Reward Rarity"), I18N.T("cheat.maxRarity.desc", "All card rewards are Rare"), () => KitLibState.PlayerCheats.MaxCardRewardRarity, MpCheatUi.WrapBoolSetter(v => KitLibState.PlayerCheats.MaxCardRewardRarity = v)));

        // Game
        var secGame = NewSection("panel.section.game", "Game");
        secGame.AddChild(CreateCheatToggle(I18N.T("cheat.unknownTreasure", "Unknown → Treasure"), I18N.T("cheat.unknownTreasure.desc", "Unknown map nodes always give treasure"), () => KitLibState.MapCheats.UnknownMapAlwaysTreasure, MpCheatUi.WrapBoolSetter(v => KitLibState.MapCheats.UnknownMapAlwaysTreasure = v)));
        var mapJumpToggle = CreateCheatToggle(
            I18N.T("cheat.mapDebugJump", "Map Debug Jump"),
            I18N.T("cheat.mapDebugJump.desc", "When enabled, click any node on the map to debug-jump. When disabled, vanilla path travel only."),
            () => KitLibState.MapCheats.MapDebugJumpEnabled,
            MpCheatUi.WrapBoolSetter(v => {
                KitLibState.MapCheats.MapDebugJumpEnabled = v;
                if (NMapScreen.Instance is not { IsOpen: true } openMap) return;
                if (v)
                    MapScreenUnlock.ApplyUnlock(openMap);
                else
                    MapScreenUnlock.ClearVisuals(openMap);
            }));
        secGame.AddChild(mapJumpToggle);
        var mapRewriteToggle = CreateCheatToggle(I18N.T("mapRewrite.enabled", "Enable Map Rewrite"), "", () => KitLibState.MapCheats.MapRewriteEnabled, v => KitLibState.MapCheats.MapRewriteEnabled = v);
        MpCheatUi.ApplyMultiplayerUnsupported(mapRewriteToggle, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secGame.AddChild(mapRewriteToggle);
        var mapModeBtn = CreatePlainButton(I18N.T("mapRewrite.mode", "Mode") + ": " + GetMapRewriteLabel(), MdiIcon.Map);
        mapModeBtn.Pressed += () => {
            KitLibState.MapCheats.MapRewriteMode = KitLibState.MapCheats.MapRewriteMode switch {
                MapRewriteMode.None => MapRewriteMode.AllChest,
                MapRewriteMode.AllChest => MapRewriteMode.AllElite,
                MapRewriteMode.AllElite => MapRewriteMode.AllBoss,
                MapRewriteMode.AllBoss => MapRewriteMode.None,
                _ => MapRewriteMode.None
            };
            mapModeBtn.Text = I18N.T("mapRewrite.mode", "Mode") + ": " + GetMapRewriteLabel();
        };
        MpCheatUi.ApplyMultiplayerUnsupported(mapModeBtn, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secGame.AddChild(mapModeBtn);
        var keepBossToggle = CreateCheatToggle(I18N.T("mapRewrite.keepFinalBoss", "Keep Final Boss"), "", () => KitLibState.MapCheats.MapKeepFinalBoss, v => KitLibState.MapCheats.MapKeepFinalBoss = v);
        MpCheatUi.ApplyMultiplayerUnsupported(keepBossToggle, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
        secGame.AddChild(keepBossToggle);

        // Stat Locks
        var secLocks = NewSection("statLock.title", "Stat Locks");
        void AddLockRow(Control row) {
            MpCheatUi.ApplyMultiplayerUnsupported(row, "mpcheat.unsupported.mp", "Not synced in multiplayer.");
            secLocks.AddChild(row);
        }
        AddLockRow(CreateStatLockRow(I18N.T("statLock.gold", "Lock Gold"), 0, 99999, () => CheatRunState.StatModifiers?.LockGold ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockGold = v; }, () => CheatRunState.StatModifiers?.LockedGoldValue ?? 0, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedGoldValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.Gold; }));
        AddLockRow(CreateStatLockRow(I18N.T("statLock.currentHp", "Lock Current HP"), 1, 9999, () => CheatRunState.StatModifiers?.LockCurrentHp ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockCurrentHp = v; }, () => CheatRunState.StatModifiers?.LockedCurrentHpValue ?? 1, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedCurrentHpValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 1; return p.Creature.CurrentHp; }));
        AddLockRow(CreateStatLockRow(I18N.T("statLock.maxHp", "Lock Max HP"), 1, 9999, () => CheatRunState.StatModifiers?.LockMaxHp ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockMaxHp = v; }, () => CheatRunState.StatModifiers?.LockedMaxHpValue ?? 1, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedMaxHpValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 1; return p.Creature.MaxHp; }));
        AddLockRow(CreateStatLockRow(I18N.T("statLock.currentEnergy", "Lock Current Energy"), 0, 99, () => CheatRunState.StatModifiers?.LockCurrentEnergy ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockCurrentEnergy = v; }, () => CheatRunState.StatModifiers?.LockedCurrentEnergyValue ?? 0, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedCurrentEnergyValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.PlayerCombatState?.Energy ?? 0; }));
        AddLockRow(CreateStatLockRow(I18N.T("statLock.maxEnergy", "Lock Max Energy"), 1, 99, () => CheatRunState.StatModifiers?.LockMaxEnergy ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockMaxEnergy = v; }, () => CheatRunState.StatModifiers?.LockedMaxEnergyValue ?? 1, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedMaxEnergyValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 1; return p.MaxEnergy; }));
        AddLockRow(CreateStatLockRow(I18N.T("statLock.stars", "Lock Stars"), 0, 999, () => CheatRunState.StatModifiers?.LockStars ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockStars = v; }, () => CheatRunState.StatModifiers?.LockedStarsValue ?? 0, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedStarsValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.PlayerCombatState?.Stars ?? 0; }));
        AddLockRow(CreateStatLockRow(I18N.T("statLock.orbSlots", "Lock Orb Slots"), 0, 10, () => CheatRunState.StatModifiers?.LockOrbSlots ?? false, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockOrbSlots = v; }, () => CheatRunState.StatModifiers?.LockedOrbSlotsValue ?? 0, v => { if (CheatRunState.StatModifiers != null) CheatRunState.StatModifiers.LockedOrbSlotsValue = (int)v; }));

        // ── Responsive column distribution ────────────────────────────────
        int lastCols = 0;

        void Distribute(int numCols) {
            if (numCols == lastCols) return;
            lastCols = numCols;

            // Remove all sections from their current columns
            var allSections = new[] { secPlayer, secEnemy, secInventory, secStatus, secRewards, secGame, secLocks };
            foreach (var sec in allSections)
                sec.GetParent()?.RemoveChild(sec);

            // Remove wrappers and dividers from columns container
            foreach (var child in new List<Node>(columns.GetChildren()))
                columns.RemoveChild(child);

            switch (numCols) {
                case 1:
                    foreach (var sec in allSections)
                        colA.AddChild(sec);
                    columns.AddChild(wrapA);
                    break;

                case 2:
                    colA.AddChild(secPlayer);
                    colA.AddChild(secEnemy);
                    colB.AddChild(secInventory);
                    colB.AddChild(secStatus);
                    colB.AddChild(secRewards);
                    colB.AddChild(secGame);
                    colB.AddChild(secLocks);
                    columns.AddChild(wrapA);
                    columns.AddChild(divAB);
                    columns.AddChild(wrapB);
                    break;

                default: // 3 columns
                    colA.AddChild(secPlayer);
                    colB.AddChild(secEnemy);
                    colB.AddChild(secInventory);
                    colB.AddChild(secStatus);
                    colC.AddChild(secRewards);
                    colC.AddChild(secGame);
                    colC.AddChild(secLocks);
                    columns.AddChild(wrapA);
                    columns.AddChild(divAB);
                    columns.AddChild(wrapB);
                    columns.AddChild(divBC);
                    columns.AddChild(wrapC);
                    break;
            }
        }

        columns.Resized += () => {
            float w = columns.Size.X;
            Distribute(w >= 860 ? 3 : w >= 520 ? 2 : 1);
        };

        // Initial layout after first frame
        columns.Ready += () => Callable.From(() => {
            float w = columns.Size.X;
            Distribute(w >= 860 ? 3 : w >= 520 ? 2 : 1);
        }).CallDeferred();

        ((Node)globalUi).AddChild(root);
    }

    private static string GetMapRewriteLabel() => KitLibState.MapCheats.MapRewriteMode switch {
        MapRewriteMode.None => I18N.T("mapRewrite.none", "None"),
        MapRewriteMode.AllChest => I18N.T("mapRewrite.allChest", "All Chest"),
        MapRewriteMode.AllElite => I18N.T("mapRewrite.allElite", "All Elite"),
        MapRewriteMode.AllBoss => I18N.T("mapRewrite.allBoss", "All Boss"),
        _ => "?"
    };
}
