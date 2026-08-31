# KitLib

**English** | [中文](./README.zh-CN.md)

Foundation library and host for Slay the Spire 2 mods.

KitLib loads first, then exposes APIs other mods call from their initializer. It also runs settings, progress protection, theme, and hotkeys.

[Docs](https://sts2-devmod.wrxinyue.org/) · [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) · Steam Workshop / Nexus

## APIs (`KitLib.Abstractions`)

- Main-menu corner buttons (shared icon stack)
- Run mutations: cards, relics, potions, powers
- Cheat / run-stat toggles (gold, HP, energy, …)
- Logging

## Host

- Loader (`KitLib.dll` + `KitLib.Core.dll`)
- Progress protection, theme, hotkeys, KitLib settings
- Optional modules load independently; load failures do not affect the host

[MIT](./LICENSE)
