# KitLib

**English** | [中文](./README.zh-CN.md)

Foundation library and host for Slay the Spire 2 mods.

KitLib is for both mod authors and players. Authors can start a test run, edit cards, numbers, and enemy state from the left-hand rail, watch logs and combat info, and debug multiplayer with pseudo co-op or dual-instance LAN. Hooks and automation let you verify a mod without constantly restarting the game. Players get a clearer mod panel, progress protection, feedback ZIP export, and optional helpers.

The layout is modular: KitLib Core loads first; satellite modules can be toggled. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[Docs](https://sts2-devmod.wrxinyue.org/) · [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669) / Nexus

> **Note:** From KitLib **0.40.0**, Mod Panel, Dev Tools, and AI are separate mods. Subscribe to each companion you need; KitLib alone no longer includes them.

## Products

Install companions as needed:

- **[KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669)** (this host) — loads first, then exposes APIs other mods call from their initializer. Also runs settings, progress protection, theme, and hotkeys.
- **[KitModPanel](./mods/KitModPanel/README.md)** ([Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384)) — main-menu mod list and per-mod settings, including STS2-RitsuLib pages when RitsuLib is present.
- **[KitDevTools](./mods/KitDevTools/README.md)** ([Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840)) — title-screen Dev Mode, in-run rail, replay, cheats, and multiplayer debug.
- **[KitAI](./mods/KitAI/README.md)** — optional AI host / autoplay.

## Host

- Loader (`KitLib.dll` + `KitLib.Core.dll`); picks the `lib/<api>` variant for the running game
- Progress protection (keeps test edits out of live progress)
- Theme, hotkeys, KitLib settings (Mods → KitLib)
- Main-menu corner buttons (shared icon stack)
- Optional modules load independently; a load failure does not take down the host or other KitLib-dependent mods

## APIs (`KitLib.Abstractions`)

Other mods call these from their initializer; they do not require the dev rail:

- Main-menu corner buttons
- Run mutations: cards, relics, potions, powers
- Cheat / run-stat toggles (gold, HP, energy, …)
- Logging

At compile time, import `eng/KitLib.ContentMod.props` (`KitLib.Abstractions.dll`). At runtime, depend on KitLib core and whichever satellite modules you actually use. Details: [docs site](https://sts2-devmod.wrxinyue.org/).

## Install

- **[KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669)** (required) plus **[KitModPanel](https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384)** / **[KitDevTools](https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840)** / **KitAI** from Steam Workshop or the release zip on [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) / Nexus.
- **Auxiliary tools** (`KitLib.Mcp`, etc.) — same Releases / Nexus page, per-platform binaries.

## Acknowledgments

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)
- [RunReplays](https://github.com/boardengineer/RunReplays)

[MIT](./LICENSE)
