# Changelog

All notable changes to **KitDevTools** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **AutoSlay** — DevMode title-screen menu starts the official AutoSlay smoke bot with an optional seed. It is no longer a Dev side-rail tab. Single-player only; Steam release builds cannot use `--autoslay`, so this calls `AutoSlayer.Start` directly and skips the bot's process quit when the run ends.
- Initial standalone product release: in-run side rail, browsers, cheats, saves, logs, and related developer tools.
- KitModPanel is optional: Progress Guard and related settings register through KitLib host APIs when ModPanel is present.
- **Card browser drag-and-drop** — Drag a card outside the panel: top → draw pile, right → discard, bottom → hand.
- **Hover to open** — Hovering a side-rail icon opens that panel (no click required).
- **Mod test room teleports** — Room browser **Mod test — Rest site** and **Mod test — Treasure** spawn extra fake players to preview official multiplayer UI in solo dev runs.
- **Log export screenshots** — The log ZIP can include a game screenshot (KitLib UI hidden), extra images, a short description, official-style category chips (`Bug - this is a bug`), reaction tags, and the last combat snapshot.

### Changed

- **Cheat sidebar tabs** — Cards, Cheats, Card Test, Save/Load, and the other cheat rail entries are registered by this product, not by KitLib Core.
- **Pseudo-coop harness** — SyncBot phantom spawn, lobby host, and simulated-peer combat/map patches live here and call Core NetPlay APIs. KitAI no longer owns that test driving.
- **Panel switching** — Revisiting already-opened Dev panels is much snappier.
- **Hand drop zone** — Bottom drop label is now **Hand**.
- **AI Host tab removed** — AI controls no longer appear on the Dev side rail; use KitAI when a dedicated AI panel is available.

### Fixed

- **Mod test — Rest site** — Leaving the preview room restores your solo layout on stable game builds.
