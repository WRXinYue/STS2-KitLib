# Changelog

**KitAI** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Changed

- **AutoPlay / 同伴** — 出牌与地图/奖励点击改走 KitLib 共用的玩家操作（与 DevTools MCP 同一条路径）。KitAI 仍负责决定做什么。

## [0.0.1] - 2026-08-31

### Added

- 首次作为独立产品发布：AI 宿主 / AutoPlay 及相关多人同伴辅助。
- 依赖 KitLib。KitDevTools 为可选。

### Changed

- **开发面板** — 不再在 KitDevTools 侧栏注册 AI Host；后续由 KitAI 独立面板承载。
- **联机测试驾驶** — SyncBot 大厅/幻影战斗补丁迁到 KitDevTools。KitAI 只保留决策循环，出牌入队改调 Core NetPlay API。
