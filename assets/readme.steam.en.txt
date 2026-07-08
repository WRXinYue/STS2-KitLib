KitLib serves both mod authors and players. Authors can start test runs, use the left-edge dev panel in-game to edit cards, stats, and enemy state, read logs and combat info, and debug multiplayer (pseudo co-op, dual-instance LAN)—with hooks, scripts, and automation to validate mods with fewer restarts. Players get a better Mod panel, progress protection, feedback export, and optional assist features.

KitLib is modular: KitLib Core handles loading; satellite modules are optional. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[h3]Features[/h3]

[h3]Dev Mode (title screen)[/h3]

[list]
[*]New test run / with seed
[*]Load save, pseudo co-op quick start
[*]Unlock all progress
[*]Log viewer, mod feedback
[/list]

[h3]In-run dev rail[/h3]

[list]
[*]Card / relic / enemy / power / potion / event browsers
[*]Room teleport, command reference
[*]Cheats, presets, card test
[*]Enemy intents, combat stats
[*]AI Host, hooks, scripts
[*]Harmony analysis, framework bridge
[*]Save / load, settings, logs, mod feedback
[*]Rail expand/collapse, tab reorder/hide, hotkeys
[/list]

[h3]Browsers (in-run editing)[/h3]

[list]
[*]Cards: browse, add/remove/upgrade, edit stats and enchantments
[*]Relics, potions, powers, events
[*]Enemies: encounter picker and map overrides
[*]Rooms: shop, rest, treasure, test room, etc.
[/list]

[h3]Cheats[/h3]

[list]
[*]Player combat (invincibility, energy/block/stars, turns, draw, etc.)
[*]Enemies (freeze, damage, kill, etc.)
[*]Economy and shop (gold, multipliers, free purchases)
[*]Status and rewards (energy cap, potion slots, score, card rewards)
[*]Map (debug jump, map rewrite)
[*]Stat lock (gold, HP, energy, stars, orb slots, etc.)
[/list]

[h3]Saves and presets[/h3]

[list]
[*]Multi-slot and quick save/load, combat/turn checkpoints
[*]Preset save, apply, import/export
[/list]

[h3]Logs[/h3]

[list]
[*]In-run / main-menu log viewer with filters
[*]Optional kitlog live tail
[/list]

[h3]AI and multiplayer[/h3]

[list]
[*]Solo AI autoplay and HUD
[*]Pseudo co-op, dual-instance LAN, teammate hosting, SyncBot
[/list]

[h3]Automation[/h3]

[list]
[*]Hook rules, SpireScratch scripts
[*]MCP bridge (external agents and tools)
[/list]

[h3]Debug[/h3]

[list]
[*]Combat stats, enemy intents, performance overlay and trace
[*]Harmony and framework (RitsuLib) summaries
[/list]

[h3]Main-menu mod settings (Mods → KitLib)[/h3]

[list]
[*]Module load profiles and optional module toggles
[*]Theme, hotkeys, in-run DevMode level
[*]Progress guard, multiplayer cheat opt-in
[/list]

[h3]Mod panel[/h3]

[list]
[*]Mod list (source, load status), enable / disable
[*]Embedded per-mod settings pages
[/list]

[h3]Mod feedback[/h3]

[list]
[*]Reproduction steps and ZIP export (logs, mod list, diagnostics)
[/list]

[h3]Install[/h3]

[list]
[*][b]KitLib mod[/b] — Steam Workshop or Nexus.
[*][b]Auxiliary tools[/b] (kitlog CLI, KitLib.Mcp) — [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] or Nexus.
[/list]

[h3]For mod developers[/h3]

[list]
[*]Add eng/KitLib.ContentMod.props to your csproj (KitLib.Abstractions.dll at compile time).
[*]At runtime, depend on KitLib core and the satellite modules your mod actually uses.
[*]Extension API, logging, AI integration, etc.: [url=https://sts2-devmod.wrxinyue.org/]docs site[/url] → Developer.
[/list]

[h3]Docs[/h3]

[list]
[*]Docs: [url=https://sts2-devmod.wrxinyue.org/]sts2-devmod.wrxinyue.org[/url]
[*]Contributing: [url=CONTRIBUTING.md]CONTRIBUTING.md[/url]
[/list]

[h3]Acknowledgments[/h3]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[h3]License[/h3]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
