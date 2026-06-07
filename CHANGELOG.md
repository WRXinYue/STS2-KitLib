# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased]

### Added

- **AI Host & StrongStrategy** — Mod AI platform with an **AI Host** panel in the dev rail, external companion terminal APIs, and **StrongStrategy** solo autoplay informed by Codex priors.
- **Combat beam planner** — In-fight AI uses deck simulation and multi-turn line scoring (block-first lines, relic/power hooks, potion sim in search, minion engagement, mechanic discovery).
- **AI HUD overlay** — Top-left run overlay with big/small deck forecast, win estimate, and live sim telemetry (replaces the older heuristic HUD).
- **Reward & map AI** — Card rewards scored by marginal deck/sim value; map routing weighs path risk; rest sites prefer campfires when the deck needs upgrades; scored hand picks for exhaust and upgrade prompts.
- **Ancient event debug (dev tools)** — From **Events** or **Room Teleport → Ancient Ones**, pick any ancient in the extension panel and enter randomly or pin a listed option (same picker for Darv, Orobas, and mod ancients); `dmevent force <eventId> [choice]`.
- **MCP Nexus upload** — Pipeline to publish mod packages to Nexus from the MCP tooling (see README **MCP**).

### Changed

- **Combat AI** — Focus fire, vulnerable setup, and block timing follow sim-backed tradeoffs instead of fixed heuristics; potion use is unified under beam scoring with a narrower emergency path.

## [0.13.0] - 2026-06-02

### Added

- **Dev sidebar keyboard shortcuts** — Toggle the sidebar, close the active panel, switch tabs, or lock the rail from the keyboard. Rebind under **Settings → Keyboard shortcuts** (conflicts with in-game shortcuts are rejected).
- **Quick save / load** — **F5** quick-saves and **F9** quick-loads the current run (slot 0) during a run; rebind in **Settings → Keyboard shortcuts**. The **Save / Load** panel shows a dedicated **Quick Save** row at the top.
- **Combat checkpoint nodes** — During combat, DevMode auto-saves checkpoints at combat start and each player turn start (with a `.combat.bin` sidecar for in-fight restore of HP, piles, etc., without a full `LoadRun`). **F8** replays from combat start, **F6** from the current turn start (rebind in **Settings → Keyboard shortcuts**). The **Save / Load** panel lists **This combat** nodes separately from quick save.
- **MCP bridge** — Connect MCP clients (Cursor, Claude Desktop, etc.) to a running session on port **9877** for automated testing and agent workflows: game state, combat/map actions, save slots, cards, monsters, and cheats. See README **MCP**; bootstrap with `make dev-session`.
- **MCP combat observability** — During combat, MCP `get_game_state` includes your active powers (stack counts and mod power ids), combat phase, and indexed enemies. Playing a card via MCP returns an after-play snapshot for assertions without guessing from HP alone.

### Fixed

- **Crash recovery prompt** — Exporting a feedback ZIP or dismissing the dialog no longer leaves a stale pending report, so the main-menu prompt does not reappear every launch. Normal game quit now clears the session marker more reliably.
- **STS2 Steam beta** — Mod dependency metadata loads correctly on the beta game branch again.

## [0.12.0] - 2026-06-02

### Added

- **Crash recovery prompts** — When an unhandled error occurs in-game, DevMode shows a dialog to view logs or export a feedback ZIP (prefilled with crash summary). If the game exits abnormally, the main menu offers the same on next launch. Toggle under **Settings → Crash recovery**. Session markers live under `mod_data/DevMode/instances/{pid}/`.
- **Progress protection** — On mod set fingerprint change, DevMode backs up the active profile's `progress.save` (and optional related saves) under `mod_data/DevMode/profile_backups/` before vanilla save filtering can run. Optional warn-only log when progress still references unloaded mods. Toggle under **Settings → Progress protection** or the title-screen **DEVMODE → Progress protection** panel (status + recent backups + one-click restore). Each backup row has a **Details** button to inspect account progress, per-character stats, epochs, compendium counts, and backup metadata.
- **Progress protection startup prompt** — On main menu load, if mod character stats were filtered but a recoverable backup exists, DevMode offers a **Restore** / **Not now** dialog (toggle under **Progress protection** settings).
- **Content browsers (mod source)** — Card, relic, potion, power, and event pickers include a **Mod source** filter to include or exclude game content and loaded mods. Detail views show **Source:** for the selected entry. The card browser remembers your mod-source filter across sessions.

### Changed

- **DEVMODE main menu** — **Logs** and **Mod Feedback** are grouped under **Diagnostics**; **Progress protection** opens a dedicated panel from the title screen.
- **AI Host (dev rail)** — Uses a robot icon instead of sharing the Scripts puzzle icon.

### Fixed

- **DEVMODE main menu** — Re-entering the title-screen DEVMODE submenu after Mod Feedback and save/load no longer stacks stock menu buttons with dev entries; overlays are torn down on hide. (Thanks @Crimson707707 for the report.)
- **Add enemies to combat picker** — The encounter and monster lists scroll correctly within the panel; switching to **Monsters** no longer leaves a large blank scroll area.
- **Enemy intent overlay** — Saving and exiting or leaving combat mid-fight with the intent overlay enabled no longer crashes or leaves a black screen; the overlay hides cleanly when combat ends. (Thanks @Crimson707707 for the report.)

## [0.11.2] - 2026-06-01

### Fixed

- **Install packages** — Official release zips and NuGet packages again include the required PCK file, so the mod loader no longer reports a missing PCK and the in-game mod list shows the preview image.

## [0.11.1] - 2026-05-27

### Fixed

- **Mid-combat add monster** — Adding a monster type for the first time no longer freezes the game for a long time.
- **Mid-combat add encounter (multiplayer)** — When the host syncs an entire encounter, all clients receive every monster correctly.
- **Battle rewards** — Opening the dev sidebar no longer dismisses the post-combat reward screen.
- **Map debug jump** — Map clicks work again; jumping to an Ancient room enters the Ancient encounter (not a random event); free map travel while the map screen is open only, normal path rules return after you close the map.

## [0.11.0] - 2026-05-25

### Added

- **LAN host-drive + AFK testing** — Run two game instances on one machine: the host plays while an AFK client receives AI-driven actions. Includes end-turn and enemy-turn ready sync, ally-target card support for AI teammates, and cleanup when host AI is toggled off mid-combat.
- **DEVMODE main menu** — The title screen uses a single **DEVMODE** button; **Logs**, **Mod Feedback**, and **Multiplayer** (pseudo co-op, LAN test scene) are inside that menu.

### Changed

- **In-game right sidebar** defaults to **off** — turn on **In-game right sidebar** under **Settings → Game** to show the combat right rail (stats, enemy intent preview, combat tools).
- **Enemy intent overlay** defaults to **off** — enable it from the **Enemy intents** panel when you want the draggable prediction float during fights.
- Removed non-working **Card Library** and **Relic Collection** entries from the DEVMODE main menu.
- **Settings** — Game options (right sidebar, speed, skip animations) appear above the sidebar layout section; **Show hidden cards** was removed from Settings (still available in the card browser).

### Fixed

- **Log viewer** — Closing and reopening from the header works again when the rail tab state was bypassed.
- **STS2 Steam beta** — Additional API compatibility fixes on the beta game branch.

## [0.10.0] - 2026-05-24

### Added

- **Main menu DevMod shortcuts** — On the title screen: **Logs (DevMod)** and **Mod Feedback (DevMod)** open the log viewer and feedback ZIP export without entering a run; in **Dev Mode (DevMod)**, **Unlock all progress** (with confirmation) reveals timeline epochs, ascension A10, and compendium entries on your save.
- **In-game manual** — **Manual** rail panel with embedded help for each dev tool (replaces the separate Valaxy documentation site).
- **Card browser** — Optional **show hidden cards** and safer preview for event-only cards; mod character pools in a dropdown; right-click a filter chip to **exclude** that tag; pool filters split into clearer rows.
- **Card enchantment picker** — Expandable card grid when choosing enchantments during card edit.
- **Logs** — Viewer loads earlier lines from the on-disk game log; clearer mod vs game source coloring; rail tab alerts when new Warn/Error entries arrive; peek tab blinks until you hover it once.
- **Enemy intent tools** — Draggable in-combat intent overlay; **Enemy intents** DevPanel tab with next-turn preview rail; edit per-turn enemy intents in the enemy browser.
- **Intent badges** on the combat context rail stack vertically when an enemy has multiple intents.
- **Rooms panel** — Teleport to ancient shop locations from a slide-out submenu.
- **AI Host** panel — AI-driven combat from the dev rail; **SyncBot** simulates multiplayer ACKs for solo dev testing.
- **Pseudo co-op (Host)** — From Developer Mode main menu: one-click host with optional phantom player, map vote mirroring, and AI teammate in combat.
- **Multiplayer cheat sync** — When hosting with multiplayer cheat enabled: synced cheats, card/relic/potion edits, combat enemy tools, powers, and per-player cheat flags across clients.
- **Combat enemy context rail** — During fights, the game right Context Pane shows compact icons to add encounters/monsters, kill individual enemies, or kill all. Overlays handle search and selection.
- **Combat Stats** panel — live per-combat damage statistics (dealt, taken, block, cards played, breakdowns by card/source/turn) via the game's `CombatHistory` API. Extended stats (overkill, blocked damage, energy, potions, debuffs, power damage, event timeline), run totals, current-vs-last comparison, JSON export, and `dmstats` console command. See `TODO.md` for remaining backlog (HUD, co-op polish).
- **Combat Stats game right rail** — During fights, a slim pane on the right edge of the game shows live player contribution bars or a compact score breakdown; it updates in combat without opening the full stats panel. When the stats browser is nearly full width, the panel can merge flush with that rail.
- **Combat Stats pie breakdown** — The stats panel includes a right sidebar with a category pie chart (overview, cards, offense, support, damage taken) and color legend; the default category follows the active view tab.
- **STS2 Steam beta support** — DevMode can be used on the Steam beta branch of Slay the Spire 2, including cheats and dev tools for powers, mid-combat card adds, and enemy/combat edits. Use the beta build of the mod on the beta game install (not interchangeable with the stable/public build).
- **Customizable dev sidebar** — In **Settings → Sidebar**, drag to reorder rail panels and uncheck any you want hidden. Your layout is saved between sessions. **Harmony analysis**, **Scripts**, and **Frameworks** start hidden; turn them on in Settings when you need them.
- **Multiplayer combat stats overlay** — During co-op fights, a **draggable top-right panel** shows each player's live score bars (bar length scales to the highest score) with breakdown tooltips. Single-player still uses the slim vertical bar on the game right rail.

### Changed

- **Enemy panel map-centric UI** — Global, By Type, and Map tabs are merged into a single map editor: left side shows the run map with hover previews; right side has **Run rules** (all combats / Normal / Elite / Boss, active for this run only), **Selected node** editing, and an embedded encounter picker. Combat kill/spawn actions live on the game **Context Pane** rail during fights, not in the enemy browser header.
- **RitsuLib is now optional.** DevMode no longer lists RitsuLib as a required dependency mod — it loads and runs without RitsuLib installed. The Framework Bridge panel shows "—" when RitsuLib is absent instead of preventing DevMode from loading.
- **Normal run Dev Mode preference** — In the Developer Mode main menu, the **Normal run** cycle (disabled / Dev Mode / Cheat Mode) **defaults to Dev Mode** and is **saved** for the next launch.
- Opening a dev browser panel no longer hides the **multiplayer combat stats overlay**; it stays visible above the panel.

### Fixed

- **Enemy map hover tooltip** — Node hover previews render inside the map panel again (no longer invisible due to incorrect top-level layering).
- **Settings, save slots, presets, and scripts** are now stored in the game's user-data directory (`SlayTheSpire2/steam/<id>/mod_data/DevMode/`) instead of inside the mod folder — they survive Steam Workshop updates without data loss.
- Settings and save-slot files are now written atomically, preventing file corruption if the game crashes mid-save.
- Applying powers, adding cards in combat, and loading presets work on the **stable/public** STS2 build again, not only the Steam beta branch.
- Combat Stats **pie chart sidebar** layout when the stats panel is nearly full width and flush with the game right rail.
- Combat Stats **Timeline** tab no longer shows a harsh white focus ring on the event text area.

## [0.6.0] - 2026-05-17

### Added

- **Mod Feedback** panel — describe an issue and export a **ZIP report** for any mod author: filtered in-game logs, loaded mod list, Harmony patch dump, and framework bridge snapshot. Attach a **game log** from the `logs` folder (defaults to the current session’s `godot.log`, last 512 KB). **Privacy mode** (on by default) replaces your user-data path with `<user-data>` in the package.
- **Harmony analysis** — exclude patch owners by id (wildcards supported; DevMode and RitsuLib excluded by default), clearer smart-report layout, and smoother handling of large dumps (type list + detail panes, less stutter when refreshing).
- In-game **log viewer** — new default **noise rules** hide routine background/foreground FPS limit messages.
- The in-game **mod list** now shows a **preview image** for DevMode.
- Card browser (full library): set **base energy cost** before you add a card — the value applies to the new copy you receive, not the catalog entry.
- Card browser **remembers** type/rarity/cost/pool filters, sort, search text, and **View upgrades** between sessions.
- Clearer **upgrade preview** when browsing library cards with View upgrades enabled.
- Dev **map** entry from the dev room: on that map screen, **more nodes stay selectable** for movement (normally greyed-out paths unlock) so you can hop around faster while testing.
- Dedicated **Cheats** rail tab — all gameplay modifiers (player, enemy, rewards, map, stat locks, and related toggles) in one panel when cheat mode is active.

### Changed

- **Settings** now only covers appearance and workflow (game speed, skip animations); gameplay cheats live on the **Cheats** tab instead of under Settings.
- **Cheats** panel is wider and arranges sections in **one, two, or three columns** based on panel width, with more space between columns so options are easier to scan.
- Dev panel **rail tooltips** show the tab name only (no extra Dev/Cheat suffix).
- Card browser **pool filter** defaults to the **current character** instead of all pools.
- Card **number and text** fields in the browser apply as you edit (on change or when leaving the field), without a separate apply step.
- Dev **save / load snapshot** overlay (main menu and dev panel) now follows your **DevMode appearance theme** — backdrop, panels, separators, buttons, name field, and stat chips stay in sync with Dark / OLED / Light / Warm.
- In-run **Save / Load** opens the snapshot picker in a **slide-out column** beside the menu (same browser rail style as other dev panels) instead of a separate full-screen dialog.
- Main-menu **Load Save** fullscreen picker **grows taller on large displays** (height scales with your window).

### Removed

- **Third-party panel registration**: `DevPanelModApi`, `RegisterPanelWhenReady`, and the related deferred-registration queue have been removed. DevMode no longer supports external mods registering custom rail panels.

### Fixed

- **Cheats** panel strings (including god mode, draw limits, and map cheats) now localize correctly in Chinese instead of showing English fallbacks.
- **Infinite energy** cheat label and behavior are unified: one toggle keeps energy topped up every frame (no duplicate “runtime” entry).
- DevMode now appears under the correct **display name** in the game’s mod list manifest.
- Dev save/load snapshot list: clicking the **same slot again** no longer makes the right-hand detail area **flash**.
- In-run snapshot picker no longer appears as a **thin strip** at the top of the extension column — it now fills the panel height.
- Cards whose description text uses **dynamic placeholders** no longer trigger formatter errors in the dev card UI or preset flows.
- **Energy cost** edits on cards you already own (deck or combat piles) apply more reliably; the library list itself stays read-only for cost, so cost is applied when you add the card.

## [0.5.0] - 2026-04-26

### Added

- Dev rail browser panels now support drag-resize width and remember each panel's width between sessions.

### Changed

- Browser panels now use smoother side-rail transitions: open with a rail-origin slide animation and close with a matching slide-out animation.

### Fixed

- Switching panels no longer causes the rail edge to flicker back to rounded corners during transition.
- Dragging a resized browser panel after animation no longer detaches it from the rail edge.

## [0.4.2] - 2026-04-11

### Fixed

- Mod UIs that register with DevMode after startup now show up reliably once every mod has finished loading (deferred registration was running too early).
- Crash when entering combat after replacing an encounter from the map (right-click on a node, including per-floor overrides).

## [0.4.1] - 2026-04-10

### Added

- **SpireScratch** — visual block scripting with a bundled Blockly editor: rules save as JSON in the mod `scripts` folder, reload on file changes, and run on the same hook triggers/conditions/actions as the Hooks system. The **Scripts** panel lists loaded scripts with per-script enable/disable, shortcuts to open the scripts folder or editor, and optional migration of existing Hook rules into script files.
- SpireScratch scripts now support **live reload via WebSocket bridge**: the editor pushes saves directly to the running game, and the Scripts panel auto-refreshes to reflect changes without restarting.
- **Exhaust pile** target — the card browser's Add target picker and nav tabs now include the Exhaust pile alongside Hand, Draw, Discard, and Deck. The `dmcard add` console command also accepts `exhaust` as a target argument.
- Card browser Add target defaults to **Hand** instead of Deck.
- **Character / pool filter chips** in the card browser (All Cards tab): Character group (Ironclad, Silent, Defect, Regent, Necrobinder, Colorless) and Special group (Ancients, Status, Curse, Event, Quest, Token) — combine freely with Type, Rarity, and Cost filters. Mod-added characters are detected automatically and appended to the Character group.

### Fixed

- Draw/Discard (and now Exhaust) pile count labels no longer stay stale after DevMode adds a card: `CardAddFinished` is now fired manually for the silent add path that bypasses normal VFX animations. Also fixes the same stale count when a preset with a combat snapshot restores cards to those piles.
- **Map encounter preview** now shows the correct monster for every node: the current combat floor reads the actual running encounter from the combat room (instead of re-deriving it from the queue counter), and future floors use a DAG-BFS path walk to compute the exact queue offset along the player's reachable route. Unreachable nodes on bypassed branches show the encounter that would be entered next if teleported there.
- Right-clicking a map node to replace its encounter is now blocked for the **current combat position** — the encounter is already running and cannot be swapped mid-fight.

## [0.4.0] - 2026-04-08

### Added

- **Hook System** — a new "Hooks" panel for defining automated Trigger → Condition → Action rules that fire during gameplay (e.g. apply a power every combat start, add a card every turn start).
  - Triggers: Combat Start/End, Turn Start/End, On Draw, On Damage Dealt/Taken, On Potion Used.
  - Conditions: HP Below/Above %, Floor Above/Below, Has Power, Not Has Power.
  - Actions: Apply Power, Add Card, Use Potion, Save Slot.
  - Rules are persisted across sessions and can be enabled/disabled individually.
- **Auto-Apply shortcuts** — one-click "Add to Auto-Apply" button in the Power, Potion, and Card browsers to instantly create a Combat Start hook for the selected item.
- **Copyable IDs** — all browser detail panels (Power, Potion, Card, Relic, Enemy) now show a **Copy** button next to the item ID for easy clipboard access.
- **ID picker popup** — the Hook rule editor's ID field now has a `…` browse button that opens a searchable popup listing all available IDs for the chosen action type.
- Hook rule editor uses **dropdown menus** (OptionButton) for Trigger, Action Type, Condition Type, and Target — no more cycling through values by clicking.

## [0.3.0] - 2026-04-08

### Added

- **Restart with Seed** in the in-run Save/Load overlay: optionally carry cards, relics, and/or gold into a new run with a chosen seed.
- **Main menu Developer Mode**: **New Test (Seed)** to start a run with a custom seed.
- **Save Manager** redesign: dynamic slots, add/delete slots, richer slot list and detail view (including scrollable cards, relics, and mods).
- Save metadata includes **run seed** and **loaded mods** (name and version) in the detail view.
- **`dmsave` console**: `list` and `delete` subcommands.

### Changed

- Save/load UI uses a unified slot list (no separate quick-save row); carry-over toggles for Restart with Seed default to off.

## [0.2.0] - 2026-04-08

### Added

- Dynamic theme system with dark, light, OLED, and warm color modes.
- Room Teleport panel in the dev sidebar.
- Card browser Edit mode with preset support.

### Changed

- Redesigned Powers panel as a two-pane browser layout.
- Rebuilt Potion browser with a visual grid.
- Preset Manager enhanced with scope-based save/load and combat snapshot support.
- Replaced vanilla relic collection with self-drawn RelicBrowserUI.
- Replaced card browser top bar with custom CardBrowserUI and rail sliding indicator.
- Unified all DevMode panels with rail-spliced browser-panel layout.

### Fixed

- Power apply not working correctly in the Powers panel.
- Add Potion API not functioning properly.
- MDI icon tree-shaking regression causing missing icons after build.

## [0.1.0] - 2026-04-07

### Added

- Sidebar panel with categorized sections: Player, Inventory, Status, Enemy, and Game.
  - Player: invincible, infinite block/energy/stars, defense multiplier.
  - Inventory: edit gold, gold multiplier, free shop, edit energy cap, edit potion slots.
  - Status: always reward potion, always upgrade card rewards, max card reward rarity, max score, score multiplier.
  - Enemy: freeze enemies, one-hit kill, damage multiplier.
  - Game: unknown nodes → treasure, game speed.
- Runtime stat modifiers: god mode, kill all enemies, infinite energy, always player turn, draw to hand limit, extra draw each turn, auto-act friendly monsters, negate debuffs.
- Stat locks: lock gold, current/max HP, current/max energy, stars, orb slots.
- Map rewrite: force all rooms to chest, elite, or boss; keep final boss option.
- Power select panel with 4 target modes (self, all enemies, specific, allies).
- Potion select and event select panels.
- Card editor: edit base cost, replay, damage, block, exhaust, ethereal, unplayable, enchantments.
- Preset manager: save/load/export/import loadout presets.
- Console command reference UI with search, native and DevMode command sections.
- 10 new console command modules (card, cheat, enemy, event, game, potion, power, relic, runtime, save).
- Redesigned DevPanel as Apple-style icon rail with unified overlay system and slide-down animations.
- Iconify MDI adapter with build-time tree-shaking; replaced all text icons with real MDI icons.
- Click-to-lock toggle for sidebar panel.
- Always-enable DevMode toggle for normal (non-dev) runs.
- New test button in save/load panel.
- Multiplayer compatibility patch (filter mod signatures, normalize ModelDb hash).
- Asset warmup service with frame-budgeted texture/scene preloader.
- Cross-version API compatibility layer (Sts2ApiCompat).

### Changed

- Removed StartingGold override; uses game default gold instead.

### Fixed

- Infinite block not refilling correctly after loss.
- Potion slot removal and stat lock values not updating live.
- Overlay panels stacking on tab switch instead of closing.
- UTF-8 encoding for changelog read/write to prevent Chinese garbling.

## [0.0.1] - 2026-04-06

### Added

- Developer Mode panel accessible from the main menu.
- Customizable relics, cards, gold, and encounter selection for testing.
- Enemy encounter system with unified select UI, combat monster spawning, and idle animation preview.
- i18n support with English and Simplified Chinese localization.
- STS2AI integration panel with AI control, speed, and animation controls.
