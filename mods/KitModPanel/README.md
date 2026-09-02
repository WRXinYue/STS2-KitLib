# KitModPanel

**English** | [中文](./README.zh-CN.md)

Main-menu mod list and settings for Slay the Spire 2. Complements the official mod manager; it does not replace it.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384)

> **Note:** Split out of KitLib in **0.40.0**. Subscribe separately; KitLib ≥0.40.0 no longer ships this panel.

## Requirements

- **[KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669)** ≥0.40.0 (required)
- **STS2-RitsuLib** (optional) — when loaded, each mod's Ritsu-registered settings pages appear in this panel
- **KitDevTools** (optional) — Harmony patch counts and the inspect report; Progress protection page

Disable **KitModPanel** in the game's mod list to turn the Mods UI off completely.

## Open

- Main menu corner **KitModPanel [模组面板]** shortcut (official title list unchanged)
- Settings → General → KitLib entry
- Configurable hotkey (KitLib → Hotkeys)

Controller: cycle mods in the sidebar; LB / RB switch settings pages.

## Mod list

- Official scan: enable / disable (restart to apply). Read-only during a run
- Load status, version, Workshop vs local, install size
- Click the source chip to open the install folder
- Duplicate local override called out in the list

## Settings

- **RitsuLib mods** — same pages those mods register with STS2-RitsuLib, hosted in this shell (no RitsuLib → list still works; those pages are skipped)
- **KitLib** — General (theme / normal-run / rail), Progress protection, Performance overlay, Hotkeys
- Other mods can register KitLib-native pages the same way

CJK uses the game's locale fonts (Wine / Linux included).

[MIT](../../LICENSE)
