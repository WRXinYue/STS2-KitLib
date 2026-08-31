# Changelog

**KitModPanel** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Changed

- **README / Steam 工坊说明** — 补上可选 STS2-RitsuLib 设置页承载、可选 KitDevTools 的 Harmony 与进度保护页，以及面向玩家的列表与设置功能说明。

## [0.0.1] - 2026-08-31

### Fixed

- 再次兼容 RitsuLib **0.5.x** 构建 Ritsu 设置页（`ModSettingsUiContext` 现为 `pageScopeId` / 可选 `pageEnableGate`）。
- **Wine/Linux 中文显示** — ModPanel UI 使用 STS2 区域字体，非 Windows 环境下中文可正常渲染。感谢 [Somiona](https://github.com/Somiona) 反馈。

### Added

- 首次作为独立产品发布：主菜单 **模组** 按钮、模组列表，以及 KitLib 原生 / Ritsu 设置界面。
- 产品仅为单个 `KitModPanel.dll` 入口（不再使用 `modules/` 卫星）。在游戏模组设置中禁用该产品即可完全关闭 Mods UI。
