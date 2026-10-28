Foundation library and host for Slay the Spire 2 mods.

KitLib is for both mod authors and players. Authors can start a test run, edit cards, numbers, and enemy state from the left-hand rail, watch logs and combat info, and debug multiplayer with pseudo co-op or dual-instance LAN. Hooks and automation let you verify a mod without constantly restarting the game. Players get a clearer mod panel, progress protection, feedback ZIP export, and optional helpers.

The layout is modular: KitLib Core loads first; satellite modules can be toggled. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[url=https://sts2-devmod.wrxinyue.org/]Docs[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]Steam Workshop[/url] / Nexus

[quote]
[b]Note:[/b] From KitLib [b]0.44.0[/b], Mod Panel and Dev Tools are bundled back into KitLib. Subscribe to KitLib only; it now includes them.
[/quote]

[h3]Products[/h3]

One package; the satellite modules load automatically:

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b] — host. Loads first, then exposes APIs other mods call from their initializer. Also runs settings, progress protection, theme, and hotkeys.
[*][b]Mod Panel[/b] — main-menu mod list and per-mod settings, including STS2-RitsuLib pages when RitsuLib is present.
[*][b]Dev Tools[/b] — title-screen Dev Mode, in-run rail, replay, cheats, and multiplayer debug.
[/list]

[h3]Mod Panel[/h3]

Main-menu mod list and per-mod settings. Complements the official mod manager; it does not replace it.

[list]
[*]Enable / disable mods (restart to apply; read-only during a run); load status, version, Workshop vs local, install size
[*]Swipe a row to enable or disable it; rows mark mods that need a restart, duplicate local overrides, and Workshop updates
[*]Settings pages from KitLib and from any mod that registers them, plus STS2-RitsuLib pages when RitsuLib is loaded
[*]Open from the main-menu corner, Settings → General → KitLib, or a configurable hotkey. Controller: LB / RB switch pages
[*]Disable Mod Panel in the game's mod list to turn the Mods UI off completely; CJK uses the game's locale fonts (Wine / Linux included)
[/list]

[h3]Dev Tools[/h3]

[list]
[*][b]Title screen (Dev Mode)[/b] — new test / seed / AutoSlay, load a save or a feedback ZIP (without overwriting the live slot), pseudo co-op host, unlock all progress, logs
[*][b]Replay[/b] — official .mcr and recorded full runs (.replay: room timeline, speed / step, keeps the newest 5)
[*][b]In-run rail[/b] — card, relic, enemy, power, potion, event and room browsers, plus commands, cheats, presets, enemy intents, hooks, save/load, logs, settings
[*][b]Other[/b] — combat stats and rings, skip animations, game speed, Harmony analysis, RitsuLib snapshot, performance overlay, MCP bridge for external agents, feedback ZIP, dual-instance LAN helpers
[/list]

[h3]Host[/h3]

[list]
[*]Loader (KitLib.dll + KitLib.Core.dll); picks the lib/<api> variant for the running game
[*]Progress protection (keeps test edits out of live progress)
[*]Theme, hotkeys, KitLib settings (Mods → KitLib)
[*]Main-menu corner buttons (shared icon stack)
[*]Optional modules load independently; a load failure does not take down the host or other KitLib-dependent mods
[/list]

[h3]APIs (KitLib.Abstractions)[/h3]

Other mods call these from their initializer; they do not require the dev rail:

[list]
[*]Main-menu corner buttons
[*]Run mutations: cards, relics, potions, powers
[*]Cheat / run-stat toggles (gold, HP, energy, …)
[*]Logging
[/list]

At compile time, import eng/KitLib.ContentMod.props (KitLib.Abstractions.dll). At runtime, depend on KitLib core and whichever satellite modules you actually use. Details: [url=https://sts2-devmod.wrxinyue.org/]docs site[/url].

[h3]Install[/h3]

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b] (required — includes Mod Panel and Dev Tools) from Steam Workshop or the release zip on [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] / Nexus.
[*][b]Auxiliary tools[/b] (KitLib.Mcp, etc.) — same Releases / Nexus page, per-platform binaries.
[/list]

[h3]Acknowledgments[/h3]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[*][url=https://github.com/boardengineer/RunReplays]RunReplays[/url]
[/list]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
