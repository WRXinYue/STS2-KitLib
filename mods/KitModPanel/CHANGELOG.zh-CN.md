# Changelog

**KitModPanel** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

## [0.1.3] - 2026-09-05

### Changed

- **为 KitLib 0.43.1 重新发布** — 在 `KitLib.ModVariantLoader` 移除后，改用共享的自包含 picker 重建。行为不变，面板不再依赖该宿主 DLL。

## [0.1.2] - 2026-09-05

### Fixed

- **配合当前 KitLib 无法启动** — 工坊入口改为 KitLib 共用的版本选择器，从 `lib/<api>/KitModPanel.dll` 加载实现，避免游戏不探测旁路 DLL 时整面板起不来。

## [0.1.1] - 2026-09-03

### Fixed

- **变体加载** — 工坊入口不再与 `lib/<api>/KitModPanel.dll` 同名，避免绑定 `KitLib.Core` 失败。

## [0.1.0] - 2026-09-03

### Changed

- **从主菜单侧边打开** — 通过 KitLib 注册 **KitModPanel [模组面板]** 角标，不再往官方 Continue / Settings 标题列表里插入 **模组** 按钮。

## [0.0.1] - 2026-08-31

### Fixed

- 再次兼容 RitsuLib **0.5.x** 构建 Ritsu 设置页（`ModSettingsUiContext` 现为 `pageScopeId` / 可选 `pageEnableGate`）。
- **Wine/Linux 中文显示** — ModPanel UI 使用 STS2 区域字体，非 Windows 环境下中文可正常渲染。感谢 [Somiona](https://github.com/Somiona) 反馈。

### Added

- 首次作为独立产品发布：主菜单 **模组** 按钮、模组列表，以及 KitLib 原生 / Ritsu 设置界面。
- 产品仅为单个 `KitModPanel.dll` 入口（不再使用 `modules/` 卫星）。在游戏模组设置中禁用该产品即可完全关闭 Mods UI。
