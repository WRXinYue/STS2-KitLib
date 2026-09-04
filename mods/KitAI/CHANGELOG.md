# Changelog

All notable changes to **KitAI** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.1] - 2026-09-05

### Changed

- **AI plugins** — Character strategies, move modifiers, and snapshot contributors now live in KitAI. Mods that extend AutoPlay or companions must reference KitAI. Card/map action types (`GameAction`, `GamePhase`) stay in KitLib.

## [0.1.0] - 2026-09-03

### Changed

- **AutoPlay / companions** — Card plays and map/reward clicks go through KitLib's shared player actions (the same path DevTools MCP uses). KitAI still decides what to do.

## [0.0.1] - 2026-08-31

### Added

- Initial standalone product release: AI host / AutoPlay and related multiplayer companion helpers.
- Requires KitLib. KitDevTools is optional.

### Changed

- **Dev panel** — AI Host is no longer registered on the KitDevTools side rail; a dedicated KitAI panel will replace it.
- **Multiplayer harness** — SyncBot lobby/phantom combat patches moved to KitDevTools. KitAI keeps decision loops and calls Core NetPlay APIs to enqueue combat.
