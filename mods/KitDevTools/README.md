# KitDevTools

**English** | [中文](./README.zh-CN.md)

In-game developer tools for Slay the Spire 2. Requires [KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840)

> **Note:** Split out of KitLib in **0.40.0**. Subscribe separately; KitLib ≥0.40.0 no longer ships these tools.

## Title screen (Dev Mode)

- New test / seed / AutoSlay
- Load save, load feedback ZIP (enter the run without overwriting your live slot)
- Pseudo co-op host (phantom / SyncBot / AI teammate)
- Unlock all progress
- Logs

## Replay

- Official `.mcr` combat replay
- Homemade full-run `.replay` (plays, map, card/relic picks, shop / chest / rest; auto-recorded under `KitLib/run-replays`)
- Room-timeline jump, speed / step, live pace or game speed
- Load-save continues the same log; ReplayCore version check; keeps the newest 5 by default

## In-run rail

Browsers: cards, relics, enemies, powers, potions, events, rooms (including map overrides and mod-test rest/treasure rooms).

Also: commands, cheats, presets, card test, enemy intents, hooks, save/load, logs, settings.

## Other

- Combat stats, skip animations, game speed
- Harmony analysis, RitsuLib framework snapshot (for logs / feedback)
- Performance overlay / trace
- Hook rules; MCP bridge for external agents
- Mod feedback ZIP (logs, mod list, diagnostics, optional screenshots)
- Dual-instance LAN test helpers

[MIT](../../LICENSE)
