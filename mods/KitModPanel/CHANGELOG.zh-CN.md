# Changelog

**KitModPanel** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Fixed

- 再次兼容 RitsuLib **0.5.x** 构建 Ritsu 设置页（`ModSettingsUiContext` 现为 `pageScopeId` / 可选 `pageEnableGate`）。

### Added

- 首次作为独立产品发布：主菜单 **模组** 按钮、模组列表，以及 KitLib 原生 / Ritsu 设置界面。
- 产品仅为单个 `KitModPanel.dll` 入口（不再使用 `modules/` 卫星）。在游戏模组设置中禁用该产品即可完全关闭 Mods UI。
