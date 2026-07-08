# KitLib

**English** | [中文](./README.zh-CN.md)

Modular in-game toolkit for Slay the Spire 2. KitLib ships as a thin Core host with optional satellite modules for the dev rail, cheats, AI, logging, and main-menu mod settings. Use it for test runs, cheats, scripting, and mod debugging without leaving the game. Content mods can reference `eng/KitLib.ContentMod.props` and ship `kitlib.compat.toml` for version checks.

## Getting started

- **Main menu → Mods → KitLib** — Satellite load profiles (Minimal / Standard / Full / Custom), hotkeys, accent theme, compat warnings, progress protection, optional live log terminal on startup.
- **During a run** — Hover the left-edge **peek tab** to expand the dev rail, then click a panel icon.
- **Title screen** — **Dev Mode** for test runs, snapshots, diagnostics, and multiplayer dev tools.
- **Settings → Sidebar / Game** — Reorder rail tabs, hide panels, combat overlays, game speed, skip animations.
- **Normal runs** — Title **Dev Mode → Normal run** cycles Disabled / Toolkit / Cheat Mode.

Install from [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) or build from source (python scripts/init.py, then make sync-full). One package supports pinned stable and beta STS2 builds; a startup banner appears when the mod build mismatches your game.

## Features at a glance

- **Gameplay** — Cheats, cards, relics, powers, potions, enemies, events, rooms, presets
- **Automation** — Hooks, SpireScratch scripts, AI Host (solo), MCP, KitLog CLI
- **Debug** — Logs, combat stats, enemy intents, console, Harmony analysis, mod feedback
- **Utility** — Save/load slots, themes & overlays

Panel-by-panel help: **[docs site](docs/pages/index.md)** (make docs) — [Rail panels](docs/pages/guide/panels/index.md).

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)** or open an issue / PR on [GitHub](https://github.com/WRXinYue/STS2-KitLib).

## Changelog

See [CHANGELOG.md](https://github.com/WRXinYue/STS2-KitLib/blob/main/CHANGELOG.md).

## Acknowledgments

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## License

[MIT](https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE)
