# Changelog

All notable changes to **KitDevTools** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.1] - 2026-08-31

### Fixed

- **Embark on STS2 0.107.1** — A beta-built pseudo-coop postfix on `StartRunLobby.IsAboutToBeginGame` called `Players` as `List<StartRunLobbyPlayer>`. On stable that getter is `List<LobbyPlayer>`, so JIT threw `MissingMethodException` and Harmony aborted vanilla embark. The postfix now uses reflection and never fails the original method.

### Added

- **AutoSlay** — DevMode title-screen menu; optional seed; single-player only.
- Initial standalone product release: in-run side rail, browsers, cheats, saves, logs, and related developer tools.
- KitModPanel is optional: Progress Guard and related settings register through KitLib host APIs when ModPanel is present.
- **Card browser drag-and-drop** — Drag a card outside the panel: top → draw pile, right → discard, bottom → hand.
- **Hover to open** — Hovering a side-rail icon opens that panel (no click required).
- **Mod test room teleports** — Room browser **Mod test — Rest site** and **Mod test — Treasure** spawn extra fake players to preview official multiplayer UI in solo dev runs.
- **Log export screenshots** — Log ZIP can include screenshots, description, category chips, combat snapshot, `latest.mcr`, and profile/run saves.
- **Load Feedback ZIP** — Open a log-export ZIP from DevMode; preview contents and enter the run without overwriting your live save slot.
- **Official replay** — Play official `.mcr` files from DevMode with bottom dock (timeline, play/pause, step, speed, restart, exit).
- **DevTools replay** — Play recorded full runs (`.replay`) with room timeline, live pace, and input lock; one file per run under `KitLib/run-replays`. Files persist across launches. Loading a save of that run continues the same log; loading an earlier save of the same run rewinds past later actions.
- **Relic picker replay** — Record and replay overlay relic picks as `ChooseRelic {index}`.
- **Clickable room timeline** — Click a past room to restart and fast-forward; first segment is starting bonus, not first combat.
- **Live replay** — Defaults to real-player pace. Can switch to game speed.
- **ReplayCore version** — Run replay files declare an engine format version, independent of the mod version. A file from a newer or unsupported core cannot be played.
- **DevTools replay retention** — Keeps the newest 5 DevTools `.replay` files by default; older files are deleted. Count is typed on the KitDevTools Mod settings page.

### Changed

- **Title-screen Dev Mode** — Opens a KitDevTools panel instead of replacing official main-menu text buttons. Uses the same title-screen backstop as vanilla overlays so corner icons stay visible. The corner icon flies to the patch-notes slot while it is open; click it again, Back, or Esc to close.
- **Cheat sidebar tabs** — Cards, Cheats, Card Test, Save/Load, and the other cheat rail entries are registered by this product, not by KitLib Core.
- **Pseudo-coop harness** — SyncBot phantom spawn, lobby host, and simulated-peer combat/map patches live here and call Core NetPlay APIs. KitAI no longer owns that test driving.
- **Panel switching** — Revisiting already-opened Dev panels is much snappier.
- **Hand drop zone** — Bottom drop label is now **Hand**.
- **AI Host tab removed** — AI controls no longer appear on the Dev side rail; use KitAI when a dedicated AI panel is available.

### Fixed

- **Mod test — Rest site** — Leaving the preview room restores your solo layout on stable game builds.
- **DevTools replay** — Starting bonus (Neow) and event screens stay visible during playback; content lays out above the replay dock. Player card plays, map travel, and cheat edits are blocked while a replay is running.
