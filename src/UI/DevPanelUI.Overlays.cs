using System;
using DevMode;
using DevMode.Icons;
using DevMode.Presets;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    private const string SettingsRootName = "DevModeSettings";
    private const string SaveLoadRootName = "DevModeSaveLoad";
    private const string SaveLoadMenuHostName = "SaveLoadMenuHost";
    private const string SaveLoadExtensionWidthKey = "DevModeSaveLoad_ext";
    private const string RestartSeedRootName = "DevModeRestartSeed";

    // ── Helper: build the standard browser-panel root ──────────────────────

    private static (Control root, PanelContainer panel, VBoxContainer vbox) CreateOverlayRoot(
        NGlobalUi globalUi, string rootName, float panelWidth = 0f, int contentSeparation = 10) {
        var (root, panel, vbox) = CreateBrowserOverlayShell(
            globalUi,
            rootName,
            panelWidth,
            () => ((Node)globalUi).GetNodeOrNull<Control>(rootName)?.QueueFree(),
            contentSeparation);
        return (root, panel, vbox);
    }

    // ── Settings (Cheats) ──────────────────────────────────────────────────

    internal static void ShowCheatsOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        var existing = ((Node)globalUi).GetNodeOrNull<Control>(SettingsRootName);
        if (existing != null) {
            ((Node)globalUi).RemoveChild(existing);
            existing.QueueFree();
        }

        var (root, _, vbox) = CreateOverlayRoot(globalUi, SettingsRootName, 640f);

        // Nav tab header
        AddBrowserNavTab(vbox, I18N.T("panel.settings", "Settings"));

        // Scrollable content
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 4);

        // ── Section: Appearance ──
        inner.AddChild(CreateSectionHeader(I18N.T("appearance.title", "Appearance")));
        inner.AddChild(CreateAppearanceSection(() => ShowCheatsOverlay(globalUi, actions)));

        // ── Section: Player ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.player", "Player")));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteHp", "Infinite HP"), I18N.T("cheat.infiniteHp.desc", "Player cannot lose HP"), () => DevModeState.PlayerCheats.InfiniteHp, v => DevModeState.PlayerCheats.InfiniteHp = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteBlock", "Infinite Shield"), I18N.T("cheat.infiniteBlock.desc", "Block refills to 999 after loss"), () => DevModeState.PlayerCheats.InfiniteBlock, v => {
            DevModeState.PlayerCheats.InfiniteBlock = v;
            if (v && RunContext.TryGetRunAndPlayer(out _, out var bp)) {
                var c = bp.Creature;
                if (c.Block < 999) c.GainBlockInternal(999 - c.Block);
            }
        }));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteEnergy", "Infinite Energy"), I18N.T("cheat.infiniteEnergy.desc", "Energy refills after spending"), () => DevModeState.PlayerCheats.InfiniteEnergy, v => DevModeState.PlayerCheats.InfiniteEnergy = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.infiniteStars", "Infinite Stars"), I18N.T("cheat.infiniteStars.desc", "Stars refill after spending"), () => DevModeState.PlayerCheats.InfiniteStars, v => DevModeState.PlayerCheats.InfiniteStars = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.alwaysPotion", "Always Reward Potion"), null, () => DevModeState.PlayerCheats.AlwaysRewardPotion, v => DevModeState.PlayerCheats.AlwaysRewardPotion = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.alwaysUpgrade", "Always Upgrade Reward"), I18N.T("cheat.alwaysUpgrade.desc", "Card rewards are always upgraded"), () => DevModeState.PlayerCheats.AlwaysUpgradeCardReward, v => DevModeState.PlayerCheats.AlwaysUpgradeCardReward = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.maxRarity", "Max Card Reward Rarity"), I18N.T("cheat.maxRarity.desc", "All card rewards are Rare"), () => DevModeState.PlayerCheats.MaxCardRewardRarity, v => DevModeState.PlayerCheats.MaxCardRewardRarity = v));
        inner.AddChild(CreateCheatSlider(I18N.T("cheat.defenseMultiplier", "Defense Multiplier"), I18N.T("cheat.defenseMultiplier.desc", "Multiply block gained"), 0, 10, 0.5f, () => DevModeState.PlayerCheats.DefenseMultiplier, v => DevModeState.PlayerCheats.DefenseMultiplier = v));

        // ── Section: Inventory ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.inventory", "Inventory")));
        inner.AddChild(CreateCheatNumberEdit(I18N.T("cheat.editGold", "Edit Gold"), 0, 99999,
            () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.Gold; },
            v => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return; p.Gold = (int)v; }));
        inner.AddChild(CreateCheatSlider(I18N.T("cheat.goldMultiplier", "Gold Multiplier"), I18N.T("cheat.goldMultiplier.desc", "Multiply gold gained"), 0, 10, 0.5f, () => DevModeState.GameplayModifiers.GoldMultiplier, v => DevModeState.GameplayModifiers.GoldMultiplier = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.freeShop", "Free Shop"), I18N.T("cheat.freeShop.desc", "All shop purchases are free"), () => DevModeState.GameplayModifiers.FreeShop, v => DevModeState.GameplayModifiers.FreeShop = v));

        // ── Section: Status ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.status", "Status")));
        inner.AddChild(CreateCheatNumberEdit(I18N.T("cheat.editEnergyCap", "Edit Energy Cap"), 0, 99,
            () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.MaxEnergy; },
            v => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return; p.MaxEnergy = (int)v; }));
        inner.AddChild(CreateCheatNumberEdit(I18N.T("cheat.editPotionSlots", "Edit Potion Slots"), 0, 20,
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
            }));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.maxScore", "Max Score"), I18N.T("cheat.maxScore.desc", "Enable max score tracking"), () => DevModeState.GameplayModifiers.MaxScore, v => DevModeState.GameplayModifiers.MaxScore = v));
        inner.AddChild(CreateCheatSlider(I18N.T("cheat.scoreMultiplier", "Score Multiplier"), I18N.T("cheat.scoreMultiplier.desc", "Multiply score gained"), 0, 10, 0.5f, () => DevModeState.GameplayModifiers.ScoreMultiplier, v => DevModeState.GameplayModifiers.ScoreMultiplier = v));

        // ── Section: Enemy ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.enemy", "Enemy")));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.freezeEnemies", "Freeze Enemies"), I18N.T("cheat.freezeEnemies.desc", "Enemies skip their turns"), () => DevModeState.EnemyCheats.FreezeEnemies, v => DevModeState.EnemyCheats.FreezeEnemies = v));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.oneHitKill", "One-Hit Kill"), I18N.T("cheat.oneHitKill.desc", "Deal massive damage to enemies"), () => DevModeState.EnemyCheats.OneHitKill, v => DevModeState.EnemyCheats.OneHitKill = v));
        inner.AddChild(CreateCheatSlider(I18N.T("cheat.damageMultiplier", "Damage Multiplier"), I18N.T("cheat.damageMultiplier.desc", "Multiply damage dealt to enemies"), 0, 10, 0.5f, () => DevModeState.EnemyCheats.DamageMultiplier, v => DevModeState.EnemyCheats.DamageMultiplier = v));

        // ── Section: Game ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.game", "Game")));
        inner.AddChild(CreateCheatToggle(I18N.T("cheat.unknownTreasure", "Unknown → Treasure"), I18N.T("cheat.unknownTreasure.desc", "Unknown map nodes always give treasure"), () => DevModeState.MapCheats.UnknownMapAlwaysTreasure, v => DevModeState.MapCheats.UnknownMapAlwaysTreasure = v));
        inner.AddChild(CreateCheatToggle(I18N.T("mapRewrite.enabled", "Enable Map Rewrite"), "", () => DevModeState.MapCheats.MapRewriteEnabled, v => DevModeState.MapCheats.MapRewriteEnabled = v));

        var mapModeBtn = CreatePlainButton(I18N.T("mapRewrite.mode", "Mode") + ": " + GetMapRewriteLabel(), MdiIcon.Map);
        mapModeBtn.Pressed += () => {
            DevModeState.MapCheats.MapRewriteMode = DevModeState.MapCheats.MapRewriteMode switch {
                MapRewriteMode.None => MapRewriteMode.AllChest,
                MapRewriteMode.AllChest => MapRewriteMode.AllElite,
                MapRewriteMode.AllElite => MapRewriteMode.AllBoss,
                MapRewriteMode.AllBoss => MapRewriteMode.None,
                _ => MapRewriteMode.None
            };
            mapModeBtn.Text = I18N.T("mapRewrite.mode", "Mode") + ": " + GetMapRewriteLabel();
        };
        inner.AddChild(mapModeBtn);
        inner.AddChild(CreateCheatToggle(I18N.T("mapRewrite.keepFinalBoss", "Keep Final Boss"), "", () => DevModeState.MapCheats.MapKeepFinalBoss, v => DevModeState.MapCheats.MapKeepFinalBoss = v));

        var gameSpeedBtn = CreatePlainButton(I18N.T("panel.speed", "Speed: {0}", actions.GetGameSpeedLabel()), MdiIcon.SpeedometerMedium);
        gameSpeedBtn.Pressed += () => {
            actions.OnCycleGameSpeed();
            gameSpeedBtn.Text = I18N.T("panel.speed", "Speed: {0}", actions.GetGameSpeedLabel());
        };
        inner.AddChild(gameSpeedBtn);

        var skipAnimBtn = CreatePlainButton(I18N.T("panel.skipAnim", "Skip Anim: {0}", actions.GetSkipAnimLabel()), MdiIcon.AnimationPlay);
        skipAnimBtn.Pressed += () => {
            actions.OnToggleSkipAnim();
            skipAnimBtn.Text = I18N.T("panel.skipAnim", "Skip Anim: {0}", actions.GetSkipAnimLabel());
        };
        inner.AddChild(skipAnimBtn);

        // ── Section: Runtime Stats ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.runtime", "Runtime Stats")));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.godMode", "God Mode"), I18N.T("runtime.godMode.desc", "Auto-heal to max HP every frame"), () => DevModeState.StatModifiers?.GodMode ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.GodMode = v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.killAll", "Kill All Enemies"), I18N.T("runtime.killAll.desc", "Continuously kill all enemies"), () => DevModeState.StatModifiers?.KillAllEnemies ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.KillAllEnemies = v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.infiniteEnergy", "Infinite Energy (Runtime)"), I18N.T("runtime.infiniteEnergy.desc", "Keep energy at 99+"), () => DevModeState.StatModifiers?.InfiniteEnergy ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.InfiniteEnergy = v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.alwaysPlayerTurn", "Always Player Turn"), I18N.T("runtime.alwaysPlayerTurn.desc", "Force combat to player turn"), () => DevModeState.StatModifiers?.AlwaysPlayerTurn ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.AlwaysPlayerTurn = v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.drawToLimit", "Draw to Hand Limit"), I18N.T("runtime.drawToLimit.desc", "Auto-draw to 10 cards"), () => DevModeState.StatModifiers?.DrawToHandLimit ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.DrawToHandLimit = v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.extraDraw", "Extra Draw Each Turn"), I18N.T("runtime.extraDraw.desc", "Draw extra cards at turn start"), () => DevModeState.StatModifiers?.ExtraDrawEachTurn ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.ExtraDrawEachTurn = v; }));
        inner.AddChild(CreateCheatNumberEdit(I18N.T("runtime.extraDrawAmount", "Extra Draw Amount"), 1, 20, () => DevModeState.StatModifiers?.ExtraDrawEachTurnAmount ?? 1, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.ExtraDrawEachTurnAmount = (int)v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.autoAlly", "Auto-Act Friendly Monsters"), I18N.T("runtime.autoAlly.desc", "Auto-execute friendly monster turns"), () => DevModeState.StatModifiers?.AutoActFriendlyMonsters ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.AutoActFriendlyMonsters = v; }));
        inner.AddChild(CreateRuntimeToggle(I18N.T("runtime.negateDebuffs", "Negate Debuffs"), I18N.T("runtime.negateDebuffs.desc", "Continuously remove all debuffs"), () => DevModeState.StatModifiers?.NegateDebuffs ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.NegateDebuffs = v; }));

        // ── Stat Locks ──
        inner.AddChild(CreateSectionHeader(I18N.T("statLock.title", "Stat Locks")));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.gold", "Lock Gold"), 0, 99999, () => DevModeState.StatModifiers?.LockGold ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockGold = v; }, () => DevModeState.StatModifiers?.LockedGoldValue ?? 0, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedGoldValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.Gold; }));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.currentHp", "Lock Current HP"), 1, 9999, () => DevModeState.StatModifiers?.LockCurrentHp ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockCurrentHp = v; }, () => DevModeState.StatModifiers?.LockedCurrentHpValue ?? 1, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedCurrentHpValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 1; return p.Creature.CurrentHp; }));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.maxHp", "Lock Max HP"), 1, 9999, () => DevModeState.StatModifiers?.LockMaxHp ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockMaxHp = v; }, () => DevModeState.StatModifiers?.LockedMaxHpValue ?? 1, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedMaxHpValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 1; return p.Creature.MaxHp; }));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.currentEnergy", "Lock Current Energy"), 0, 99, () => DevModeState.StatModifiers?.LockCurrentEnergy ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockCurrentEnergy = v; }, () => DevModeState.StatModifiers?.LockedCurrentEnergyValue ?? 0, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedCurrentEnergyValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.PlayerCombatState?.Energy ?? 0; }));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.maxEnergy", "Lock Max Energy"), 1, 99, () => DevModeState.StatModifiers?.LockMaxEnergy ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockMaxEnergy = v; }, () => DevModeState.StatModifiers?.LockedMaxEnergyValue ?? 1, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedMaxEnergyValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 1; return p.MaxEnergy; }));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.stars", "Lock Stars"), 0, 999, () => DevModeState.StatModifiers?.LockStars ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockStars = v; }, () => DevModeState.StatModifiers?.LockedStarsValue ?? 0, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedStarsValue = (int)v; }, () => { if (!RunContext.TryGetRunAndPlayer(out _, out var p)) return 0; return p.PlayerCombatState?.Stars ?? 0; }));
        inner.AddChild(CreateStatLockRow(I18N.T("statLock.orbSlots", "Lock Orb Slots"), 0, 10, () => DevModeState.StatModifiers?.LockOrbSlots ?? false, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockOrbSlots = v; }, () => DevModeState.StatModifiers?.LockedOrbSlotsValue ?? 0, v => { if (DevModeState.StatModifiers != null) DevModeState.StatModifiers.LockedOrbSlotsValue = (int)v; }));

        scroll.AddChild(inner);
        vbox.AddChild(scroll);

        ((Node)globalUi).AddChild(root);
    }

    // ── Appearance (theme) controls ───────────────────────────────────────

    private static Control CreateAppearanceSection(Action rebuild) {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);

        // ── Mode toggle row: label + single icon button ──
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 8);

        var modeLbl = new Label {
            Text = ThemeManager.IsDarkMode
                ? I18N.T("appearance.mode.dark", "Dark Mode")
                : I18N.T("appearance.mode.light", "Light Mode"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        modeLbl.AddThemeFontSizeOverride("font_size", 12);
        modeLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        modeRow.AddChild(modeLbl);

        // Icon button: shows sun (→ switch to dark) when in light mode,
        //              shows moon (→ switch to light) when in dark mode
        var modeIcon = ThemeManager.IsDarkMode ? MdiIcon.WeatherNight : MdiIcon.WeatherSunny;
        var modeBtn = new Button {
            CustomMinimumSize = new Vector2(36, 36),
            FocusMode = Control.FocusModeEnum.None,
            Icon = modeIcon.Texture(20, DevModeTheme.Accent),
            TooltipText = ThemeManager.IsDarkMode
                ? I18N.T("appearance.mode.light", "Light Mode")
                : I18N.T("appearance.mode.dark", "Dark Mode")
        };
        var modeBtnStyle = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        var modeBtnHover = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgHover,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        modeBtn.AddThemeStyleboxOverride("normal", modeBtnStyle);
        modeBtn.AddThemeStyleboxOverride("hover", modeBtnHover);
        modeBtn.AddThemeStyleboxOverride("pressed", modeBtnHover);
        modeBtn.AddThemeStyleboxOverride("focus", modeBtnStyle);
        modeBtn.Pressed += () => {
            ThemeManager.SetDarkMode(!ThemeManager.IsDarkMode);
            Callable.From(rebuild).CallDeferred();
        };
        modeRow.AddChild(modeBtn);
        col.AddChild(modeRow);

        // ── Dark theme selector ──
        var darkThemeBtn = CreatePlainButton(
            I18N.T("appearance.darkTheme", "Dark Theme: {0}",
                I18N.T("theme." + SettingsStore.Current.DarkThemeName.ToLowerInvariant(),
                    SettingsStore.Current.DarkThemeName)),
            MdiIcon.WeatherNight);
        darkThemeBtn.Pressed += () => {
            ThemeManager.CycleDarkTheme();
            Callable.From(rebuild).CallDeferred();
        };
        col.AddChild(darkThemeBtn);

        // ── Light theme selector ──
        var lightThemeBtn = CreatePlainButton(
            I18N.T("appearance.lightTheme", "Light Theme: {0}",
                I18N.T("theme." + SettingsStore.Current.LightThemeName.ToLowerInvariant(),
                    SettingsStore.Current.LightThemeName)),
            MdiIcon.WeatherSunny);
        lightThemeBtn.Pressed += () => {
            ThemeManager.CycleLightTheme();
            Callable.From(rebuild).CallDeferred();
        };
        col.AddChild(lightThemeBtn);

        var resetWidthBtn = CreatePlainButton(
            I18N.T("appearance.resetPanelWidths", "Reset saved panel widths"));
        resetWidthBtn.Pressed += () => {
            SettingsStore.Current.BrowserPanelWidths.Clear();
            SettingsStore.Save();
        };
        col.AddChild(resetWidthBtn);

        return col;
    }

    // ── Save / Load (rail | main browser panel | extension browser panel) ───

    internal static void ShowSaveLoadOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        const float defaultExtWidth = 600f;
        const float extSlideOutSec = 0.28f;

        var root = CreateAndSetupRoot(globalUi, SaveLoadRootName, 1250);
        root.SetMeta("dm_dual_save_load", true);

        void FallbackClose() => ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();
        root.AddChild(CreateBrowserBackdrop(() => RequestCloseBrowserOverlay(globalUi, SaveLoadRootName, FallbackClose)));

        float mainW = ResolveBrowserPanelWidth(SaveLoadRootName, 520f, (Node)globalUi);
        float extW = ResolveBrowserPanelWidth(SaveLoadExtensionWidthKey, defaultExtWidth, (Node)globalUi);

        var clipHost = CreateBrowserPanelClipHost();

        var mover = new Control {
            Name = "SaveLoadDualCarrier",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        mover.AnchorLeft = 0;
        mover.AnchorRight = 0;
        // Same vertical band as `Rail` and `CreateBrowserPanel` (0.15–0.85 of clip host).
        // Full-height mover + FullRect inner panels made columns taller than the rail.
        mover.AnchorTop = 0.15f;
        mover.AnchorBottom = 0.85f;
        mover.OffsetTop = 0;
        mover.OffsetBottom = 0;

        var row = new HBoxContainer {
            Name = "SaveLoadDualRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 0);
        // Parent `mover` is a plain Control; without full-rect anchors the row only uses minimum
        // height and sits at the top — middle column looks detached / "floating" upward.
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var mainSlot = new Control {
            CustomMinimumSize = new Vector2(mainW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var mainPanel = CreateBrowserPanelInner(mainW, joinFlushOnRight: true);
        mainPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainSlot.AddChild(mainPanel);

        var mainVbox = mainPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException("Save/Load main panel missing Content");

        AddBrowserNavTab(mainVbox, I18N.T("panel.section.save", "Save / Load"));

        var menuHost = new VBoxContainer {
            Name = SaveLoadMenuHostName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        menuHost.AddThemeConstantOverride("separation", 6);

        var newTestBtn = CreateListItemButton(I18N.T("panel.newTest", "New Test"));
        newTestBtn.Icon = MdiIcon.Plus.Texture(16);
        newTestBtn.Alignment = HorizontalAlignment.Left;
        newTestBtn.Pressed += () => { ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree(); actions.OnNewTest(); };
        menuHost.AddChild(newTestBtn);

        var restartSeedBtn = CreateListItemButton(I18N.T("panel.restartWithSeed", "Restart with Seed"));
        restartSeedBtn.Icon = MdiIcon.Refresh.Texture(16);
        restartSeedBtn.Alignment = HorizontalAlignment.Left;
        restartSeedBtn.Pressed += () => ShowRestartSeedOverlay(globalUi, actions);
        menuHost.AddChild(restartSeedBtn);

        var saveBtn = CreateListItemButton(I18N.T("panel.save", "Save"));
        saveBtn.Icon = MdiIcon.ContentSave.Texture(16);
        saveBtn.Alignment = HorizontalAlignment.Left;
        menuHost.AddChild(saveBtn);

        var loadBtn = CreateListItemButton(I18N.T("panel.load", "Load"));
        loadBtn.Icon = MdiIcon.FolderOpen.Texture(16);
        loadBtn.Alignment = HorizontalAlignment.Left;
        menuHost.AddChild(loadBtn);

        mainVbox.AddChild(menuHost);

        var extSlot = new Control {
            CustomMinimumSize = new Vector2(extW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var extPanel = CreateBrowserPanelInner(extW);
        extPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        extSlot.AddChild(extPanel);

        var extVbox = extPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException("Save/Load extension panel missing Content");

        var slotHost = new Control {
            Name = "SaveLoadSlotHost",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        slotHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        extVbox.AddChild(slotHost);

        row.AddChild(mainSlot);
        row.AddChild(extSlot);
        mover.AddChild(row);
        clipHost.AddChild(mover);
        root.AddChild(clipHost);

        Tween? extCloseTween = null;

        void KillExtCloseTween() {
            extCloseTween?.Kill();
            extCloseTween = null;
        }

        void SyncMoverWidth() {
            float totalW = mainSlot.CustomMinimumSize.X + (extSlot.Visible ? extSlot.CustomMinimumSize.X : 0f);
            mover.OffsetLeft = 0;
            mover.OffsetRight = Mathf.Max(1f, totalW);
        }

        void CloseExtensionPanel() {
            if (!extSlot.Visible) return;
            KillExtCloseTween();
            float w = Mathf.Max(1f, extPanel.GetRect().Size.X);
            extCloseTween = extPanel.CreateTween();
            extCloseTween.SetTrans(Tween.TransitionType.Cubic);
            extCloseTween.SetEase(Tween.EaseType.In);
            extCloseTween.TweenProperty(extPanel, "position:x", w, extSlideOutSec);
            extCloseTween.TweenCallback(Callable.From(() => {
                extCloseTween = null;
                SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                extPanel.Position = Vector2.Zero;
                extSlot.Visible = false;
                SyncMoverWidth();
            }));
        }

        void OpenPicker(bool saveMode) {
            Callable.From(() => {
                KillExtCloseTween();
                if (extSlot.Visible)
                    SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                extPanel.Position = Vector2.Zero;
                extSlot.Visible = true;
                SyncMoverWidth();
                SaveSlotUI.Show(
                    slotHost,
                    saveMode: saveMode,
                    onConfirm: slot => {
                        if (saveMode)
                            SaveSlotManager.SaveToSlot(slot);
                        else
                            SaveSlotManager.LoadFromSlot(slot);
                    },
                    host: SaveSlotUiHost.EmbeddedInDevPanel,
                    onEmbeddedCancel: CloseExtensionPanel,
                    onEmbeddedAfterLoadClose: saveMode
                        ? null
                        : () => {
                            KillExtCloseTween();
                            SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                            extPanel.Position = Vector2.Zero;
                            extSlot.Visible = false;
                            SyncMoverWidth();
                            RequestCloseBrowserOverlay(globalUi, SaveLoadRootName, FallbackClose);
                        });
                Callable.From(() => PlayBrowserPanelOpenFromLeft(extPanel)).CallDeferred();
            }).CallDeferred();
        }

        saveBtn.Pressed += () => OpenPicker(saveMode: true);
        loadBtn.Pressed += () => OpenPicker(saveMode: false);

        clipHost.Resized += () => SyncMoverWidth();

        bool opened = false;
        clipHost.TreeEntered += () => {
            if (opened) return;
            opened = true;
            Callable.From(() => {
                SyncMoverWidth();
                PlaySubPanelSlideOpenFromLeft(mover);
            }).CallDeferred();
        };

        ((Node)globalUi).AddChild(root);
    }

    // ── Restart with Seed ──────────────────────────────────────────────────

    internal static void ShowRestartSeedOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
        ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        var (root, _, vbox) = CreateOverlayRoot(globalUi, RestartSeedRootName, 520f);

        AddBrowserNavTab(vbox, I18N.T("restart.title", "Restart with Seed"));

        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 12);

        // ── Seed input ──
        var seedSection = new VBoxContainer();
        seedSection.AddThemeConstantOverride("separation", 4);

        var seedLbl = new Label { Text = I18N.T("restart.seed.label", "Seed (leave empty for random):") };
        seedLbl.AddThemeFontSizeOverride("font_size", 12);
        seedLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        seedSection.AddChild(seedLbl);

        var seedInput = new LineEdit {
            PlaceholderText = I18N.T("restart.seed.placeholder", "e.g. DEADBEEF"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        seedSection.AddChild(seedInput);
        inner.AddChild(seedSection);

        // ── Divider ──
        inner.AddChild(new ColorRect {
            Color = DevModeTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        // ── Carry-over scope ──
        var carryLbl = new Label { Text = I18N.T("restart.carry.label", "Carry over from current run:") };
        carryLbl.AddThemeFontSizeOverride("font_size", 12);
        carryLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        inner.AddChild(carryLbl);

        bool hasRun = RunContext.TryGetRunAndPlayer(out _, out _);

        var cardsToggle = new CheckButton {
            Text = I18N.T("preset.scope.cards", "Cards"),
            ButtonPressed = false,
            Disabled = !hasRun,
            FocusMode = Control.FocusModeEnum.None,
        };
        cardsToggle.AddThemeFontSizeOverride("font_size", 13);
        cardsToggle.AddThemeColorOverride("font_color", new Color(0.35f, 0.58f, 0.95f));
        inner.AddChild(cardsToggle);

        var relicsToggle = new CheckButton {
            Text = I18N.T("preset.scope.relics", "Relics"),
            ButtonPressed = false,
            Disabled = !hasRun,
            FocusMode = Control.FocusModeEnum.None,
        };
        relicsToggle.AddThemeFontSizeOverride("font_size", 13);
        relicsToggle.AddThemeColorOverride("font_color", new Color(0.88f, 0.72f, 0.22f));
        inner.AddChild(relicsToggle);

        var goldToggle = new CheckButton {
            Text = I18N.T("restart.carry.gold", "Gold"),
            ButtonPressed = false,
            Disabled = !hasRun,
            FocusMode = Control.FocusModeEnum.None,
        };
        goldToggle.AddThemeFontSizeOverride("font_size", 13);
        goldToggle.AddThemeColorOverride("font_color", new Color(0.32f, 0.76f, 0.50f));
        inner.AddChild(goldToggle);

        if (!hasRun) {
            var noRunLbl = new Label { Text = I18N.T("restart.noRun", "(No active run — carry-over unavailable)") };
            noRunLbl.AddThemeFontSizeOverride("font_size", 11);
            noRunLbl.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
            inner.AddChild(noRunLbl);
        }

        inner.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // ── Status label ──
        var statusLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        statusLbl.AddThemeFontSizeOverride("font_size", 11);
        statusLbl.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        inner.AddChild(statusLbl);

        // ── Action buttons ──
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);

        var cancelBtn = CreateListItemButton(I18N.T("restart.cancel", "Cancel"));
        cancelBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cancelBtn.Pressed += () => ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
        btnRow.AddChild(cancelBtn);

        var restartBtn = CreateListItemButton(I18N.T("restart.go", "Restart"));
        restartBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        restartBtn.Icon = MdiIcon.Refresh.Texture(16);
        restartBtn.Alignment = HorizontalAlignment.Center;
        restartBtn.Pressed += () => {
            var seed = seedInput.Text?.Trim();

            // Capture carry-over state from current run
            var scope = PresetContents.None;
            if (cardsToggle.ButtonPressed) scope |= PresetContents.Cards;
            if (relicsToggle.ButtonPressed) scope |= PresetContents.Relics;

            if (scope != PresetContents.None && hasRun) {
                var preset = PresetManager.CaptureFromRun(scope);
                if (preset != null) {
                    DevModeState.PendingRestartPreset = preset;
                    DevModeState.PendingRestartScope = scope;
                    MainFile.Logger.Info($"[DevMode] RestartWithSeed: captured preset scope={scope}.");
                }
            }

            if (goldToggle.ButtonPressed && hasRun && RunContext.TryGetRunAndPlayer(out _, out var player)) {
                DevModeState.PendingRestartGold = player.Gold;
                MainFile.Logger.Info($"[DevMode] RestartWithSeed: captured gold={player.Gold}.");
            }

            // Store seed for SeedInjectPatch to inject into NGame.StartNewSingleplayerRun.
            // (NGame.DebugSeedOverride is unreliable — NCharacterSelectScreen clears it before the run.)
            if (!string.IsNullOrEmpty(seed)) {
                DevModeState.PendingRestartSeed = seed;
                MainFile.Logger.Info($"[DevMode] RestartWithSeed: seed override set to '{seed}'.");
            }

            // Signal MainMenuPatch to skip the Dev menu and go straight to character select
            DevModeState.AutoProceedToCharSelect = true;

            ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
            actions.OnNewTest();
        };
        btnRow.AddChild(restartBtn);

        inner.AddChild(btnRow);

        vbox.AddChild(inner);
        ((Node)globalUi).AddChild(root);
        seedInput.GrabFocus();
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    private static void AddBrowserNavTab(VBoxContainer vbox, string title) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var tab = new Button { Text = title, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 4,
            ContentMarginBottom = 6
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            tab.AddThemeStyleboxOverride(s, flat);
        tab.AddThemeColorOverride("font_color", DevModeTheme.Accent);
        tab.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(tab);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = DevModeTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
    }

    private static string GetMapRewriteLabel() => DevModeState.MapCheats.MapRewriteMode switch {
        MapRewriteMode.None => I18N.T("mapRewrite.none", "None"),
        MapRewriteMode.AllChest => I18N.T("mapRewrite.allChest", "All Chest"),
        MapRewriteMode.AllElite => I18N.T("mapRewrite.allElite", "All Elite"),
        MapRewriteMode.AllBoss => I18N.T("mapRewrite.allBoss", "All Boss"),
        _ => "?"
    };
}
