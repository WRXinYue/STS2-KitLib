# KitLib

**English** | [中文](./README.zh-CN.md)

KitLib is a foundation host library for Slay the Spire 2 mods. Install companion products as needed: **KitModPanel** (mod list/settings), **KitDevTools** (side rail, cheats, saves, logs), and **KitAI** (optional AI host). KitLib Core loads satellite modules from those sibling mods; if an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

> The plan is to keep polishing what already exists. Unnecessary features get cut to limit maintenance load and cognitive overhead—code quality over feature count.

## Features

### Dev Mode (title screen)

- New test run / with seed
- Load save, pseudo co-op quick start
- Unlock all progress
- Log viewer, mod feedback

### In-run dev rail

- Card / relic / enemy / power / potion / event browsers
- Room teleport, command reference
- Cheats, presets, card test
- Enemy intents, combat stats
- AI Host, hooks
- Harmony analysis, framework bridge
- Save / load, settings, logs, mod feedback
- Rail expand/collapse, tab reorder/hide, hotkeys

### Browsers (in-run editing)

- Cards: browse, add/remove/upgrade, edit stats and enchantments
- Relics, potions, powers, events
- Enemies: encounter picker and map overrides
- Rooms: shop, rest, treasure, test room, etc.

### Cheats

- Player combat (invincibility, energy/block/stars, turns, draw, etc.)
- Enemies (freeze, damage, kill, etc.)
- Economy and shop (gold, multipliers, free purchases)
- Status and rewards (energy cap, potion slots, score, card rewards)
- Map (debug jump, map rewrite)
- Stat lock (gold, HP, energy, stars, orb slots, etc.)

### Saves and presets

- Multi-slot and quick save/load, combat/turn checkpoints
- Preset save, apply, import/export

### Logs

- In-run / main-menu log viewer with filters
- Browser dev viewer (`http://127.0.0.1:9878/#/logs`)

### AI and multiplayer

- Solo AI autoplay and HUD
- Pseudo co-op, dual-instance LAN, teammate hosting, SyncBot

### Automation

- Hook rules
- MCP bridge (external agents and tools)

### Debug

- Combat stats, enemy intents, performance overlay and trace
- Harmony and framework (RitsuLib) summaries

### Main-menu mod settings (Mods → KitLib)

- Optional module toggles
- Theme, hotkeys, in-run DevMode level
- Progress guard, multiplayer cheat opt-in

### Mod panel

- Mod list (source, load status), enable / disable
- Embedded per-mod settings pages

### Mod feedback

- Reproduction steps and ZIP export (logs, mod list, diagnostics)

## Install

- **KitLib** (required host) plus **KitModPanel** / **KitDevTools** / **KitAI** from Steam Workshop or the release zip on [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) / Nexus.
- **Auxiliary tools** (`KitLib.Mcp`) — [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) or Nexus.

## For mod developers

- Add `eng/KitLib.ContentMod.props` to your csproj (`KitLib.Abstractions.dll` at compile time).
- At runtime, depend on KitLib core and the satellite modules your mod actually uses.
- Extension API, logging, AI integration, etc.: [docs site](https://sts2-devmod.wrxinyue.org/) → **API** / **KitAI**.

## Acknowledgments

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## License

[MIT](https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE)
