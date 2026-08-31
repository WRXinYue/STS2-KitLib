Foundation library and host for Slay the Spire 2 mods.

KitLib is for both mod authors and players. Authors can start a test run, edit cards, numbers, and enemy state from the left-hand rail, watch logs and combat info, and debug multiplayer with pseudo co-op or dual-instance LAN. Hooks and automation let you verify a mod without constantly restarting the game. Players get a clearer mod panel, progress protection, feedback ZIP export, and optional helpers.

The layout is modular: KitLib Core loads first; satellite modules can be toggled. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[url=https://sts2-devmod.wrxinyue.org/]Docs[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · Steam Workshop / Nexus

[h3]Products[/h3]

Install companions as needed:

[list]
[*][b]KitLib[/b] (this host) — loads first, then exposes APIs other mods call from their initializer. Also runs settings, progress protection, theme, and hotkeys.
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitModPanel/README.md]KitModPanel[/url][/b] — main-menu mod list and per-mod settings.
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitDevTools/README.md]KitDevTools[/url][/b] — title-screen Dev Mode, in-run rail, replay, cheats, and multiplayer debug.
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitAI/README.md]KitAI[/url][/b] — optional AI host / autoplay.
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
[*][b]KitLib[/b] (required) plus [b]KitModPanel[/b] / [b]KitDevTools[/b] / [b]KitAI[/b] from Steam Workshop or the release zip on [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] / Nexus.
[*][b]Auxiliary tools[/b] (KitLib.Mcp, etc.) — same Releases / Nexus page, per-platform binaries.
[/list]

[h3]Acknowledgments[/h3]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[*][url=https://github.com/boardengineer/RunReplays]RunReplays[/url]
[/list]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
