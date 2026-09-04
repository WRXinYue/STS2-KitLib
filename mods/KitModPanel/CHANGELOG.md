# Changelog

All notable changes to **KitModPanel** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.2] - 2026-09-05

### Fixed

- **Won't start with current KitLib** — The Workshop file is KitLib's shared version picker, so the panel loads `lib/<api>/KitModPanel.dll` instead of dying when the game does not probe extra DLLs.

## [0.1.1] - 2026-09-03

### Fixed

- **Variant loading** — Workshop entry no longer shares the `KitModPanel` assembly name with `lib/<api>/KitModPanel.dll`, which previously failed to bind `KitLib.Core`.

## [0.1.0] - 2026-09-03

### Changed

- **Open from the main-menu corner** — A **KitModPanel [模组面板]** side shortcut is registered with KitLib instead of inserting a **Mods** title into the official Continue / Settings list.

## [0.0.1] - 2026-08-31

### Fixed

- Ritsu settings pages build again against RitsuLib **0.5.x** (`ModSettingsUiContext` now takes `pageScopeId` / optional `pageEnableGate`).
- **CJK on Wine/Linux** — ModPanel UI uses STS2 locale fonts so Chinese text renders correctly outside Windows. Thanks to [Somiona](https://github.com/Somiona) for the report.

### Added

- Initial standalone product release: main-menu **Mods** button, mod list, and KitLib-native / Ritsu settings surfaces.
- Product ships as a single `KitModPanel.dll` entry (no satellite under `modules/`). Disabling the product in game mod settings fully disables the Mods UI.
