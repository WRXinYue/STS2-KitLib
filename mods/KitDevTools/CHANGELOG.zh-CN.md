# Changelog

**KitDevTools** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added

- 首次作为独立产品发布：局内侧栏、浏览器、作弊、存档、日志及相关开发工具。
- KitModPanel 为可选：存在时通过 KitLib 宿主 API 注册进度保护等相关设置页。
- **卡牌浏览器拖放** — 可将卡牌拖到面板外：上边 → 抽牌堆，右边 → 弃牌堆，下边 → 手牌。
- **悬停打开** — 鼠标悬停侧栏图标即可打开对应面板（无需点击）。

### Changed

- **面板切换** — 再次打开已访问过的开发面板更流畅。
- **手牌投放区** — 底部投放标签改为 **手牌**。
- **移除 AI Host 标签** — 开发侧栏不再显示 AI 机器人入口；AI 控制将随 KitAI 独立面板提供。
