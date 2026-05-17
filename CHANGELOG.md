# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Card browser (full library): set **base energy cost** before you add a card — the value applies to the new copy you receive, not the catalog entry.
- Card browser **remembers** type/rarity/cost/pool filters, sort, search text, and **View upgrades** between sessions.
- Clearer **upgrade preview** when browsing library cards with View upgrades enabled.
- Dev **map** entry from the dev room: on that map screen, **more nodes stay selectable** for movement (normally greyed-out paths unlock) so you can hop around faster while testing.

### Changed

- Card browser **pool filter** defaults to the **current character** instead of all pools.
- Card **number and text** fields in the browser apply as you edit (on change or when leaving the field), without a separate apply step.
- Dev **save / load snapshot** overlay (main menu and dev panel) now follows your **DevMode appearance theme** — backdrop, panels, separators, buttons, name field, and stat chips stay in sync with Dark / OLED / Light / Warm.
- In-run **Save / Load** opens the snapshot picker in a **slide-out column** beside the menu (same browser rail style as other dev panels) instead of a separate full-screen dialog.
- Main-menu **Load Save** fullscreen picker **grows taller on large displays** (height scales with your window).

### Fixed

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
