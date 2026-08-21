# Changelog

**KitDevTools** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added

- **AutoSlay 面板** — 开发模式侧栏可启动/停止官方 AutoSlay 冒烟机器人，可选种子。仅单人；Steam 正式包无法使用 `--autoslay`，因此本面板直接调用 `AutoSlayer.Start`，并在跑完后阻止机器人退出游戏进程。
- 首次作为独立产品发布：局内侧栏、浏览器、作弊、存档、日志及相关开发工具。
- KitModPanel 为可选：存在时通过 KitLib 宿主 API 注册进度保护等相关设置页。
- **卡牌浏览器拖放** — 可将卡牌拖到面板外：上边 → 抽牌堆，右边 → 弃牌堆，下边 → 手牌。
- **悬停打开** — 鼠标悬停侧栏图标即可打开对应面板（无需点击）。
- **Mod 测试房间传送** — 房间浏览器提供 **Mod 测试 — 篝火** 与 **Mod 测试 — 宝箱房**，额外生成假玩家以在单人开发局中预览官方多人 UI。

### Changed

- **作弊侧栏页签** — Cards、Cheats、Card Test、存档等作弊侧栏入口改由本产品注册，不再由 KitLib Core 登记。
- **面板切换** — 再次打开已访问过的开发面板更流畅。
- **手牌投放区** — 底部投放标签改为 **手牌**。
- **移除 AI Host 标签** — 开发侧栏不再显示 AI 机器人入口；AI 控制将随 KitAI 独立面板提供。

### Fixed

- **Mod 测试 — 篝火** — 在稳定版游戏构建上离开预览房间后，会正确恢复单人布局。
