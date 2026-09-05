# Changelog

**KitDevTools** 的重要变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

## [0.1.1] - 2026-09-05

### Changed

- **为 KitLib 0.43.1 重新发布** — 在 `KitLib.ModVariantLoader` 移除后，改用共享的自包含 picker 重建。行为不变，DevTools 不再依赖该宿主 DLL。

## [0.1.0] - 2026-09-03

### Changed

- **MCP 对局工具** — `get_game_state`、`combat_action`、`map_action` 与选牌工具在未安装 KitAI 时也可使用，改由 KitLib 驱动对局。
- **MCP `dev_dump_monster_mechanics`** — 已移除。怪物 mechanics 导出属于 KitAI，不属于 DevTools。

### Fixed

- **不安装 KitAI 也能启动** — 只装 KitDevTools、不装 KitAI 时，游戏不再在启动阶段崩溃。感谢 初葉夜澜 反馈。

## [0.0.1] - 2026-08-31

### Fixed

- **STS2 0.107.1 出发** — 按 beta 编译的伪联机 postfix 把 `StartRunLobby.Players` 当成 `List<StartRunLobbyPlayer>`。正式版该属性是 `List<LobbyPlayer>`，JIT 抛 `MissingMethodException`，Harmony 中断原版出发。postfix 改为反射读取，且不再让原方法失败。

### Added

- **AutoSlay** — 开发模式标题屏菜单启动官方冒烟机器人，可选种子；仅单人。
- 首次作为独立产品发布：局内侧栏、浏览器、作弊、存档、日志及相关开发工具。
- KitModPanel 为可选：存在时通过 KitLib 宿主 API 注册进度保护等相关设置页。
- **卡牌浏览器拖放** — 可将卡牌拖到面板外：上边 → 抽牌堆，右边 → 弃牌堆，下边 → 手牌。
- **悬停打开** — 鼠标悬停侧栏图标即可打开对应面板（无需点击）。
- **Mod 测试房间传送** — 房间浏览器提供 **Mod 测试 — 篝火** 与 **Mod 测试 — 宝箱房**，额外生成假玩家以在单人开发局中预览官方多人 UI。
- **日志导出截图** — 导出 ZIP 可含截图、描述、分类芯片、战斗快照、`latest.mcr` 与档案存档。
- **加载反馈 ZIP** — 开发模式可打开日志导出 ZIP，预览后进入对局，不覆盖正式存档。
- **官方回放** — 开发模式可播放官方 `.mcr`；底部坞含时间线、播放控制、倍速与结束回放。
- **DevTools 回放** — 可播放录制的整局（`.replay`），含房间时间线、实况速度与输入锁定；每局一个文件，目录 `KitLib/run-replays`。重启游戏不会清空。读取该局存档会续写同一份日志；读更早的存档会丢掉该存档之后的操作。
- **选遗物回放** — 选遗物记为 `ChooseRelic {index}` 并在回放时点选。
- **可点击房间时间线** — 点击已过房间可重开并快进；第一段为开局起始奖励。
- **实况回放** — 默认按真实玩家速度播放，可切到游戏速度。
- **ReplayCore 版本** — 整局回放文件带独立于模组版本的引擎格式号；更新或不受支持的 core 无法播放。
- **DevTools 回放保留** — 默认只留最新 5 个 `.replay`，更旧的会删。数量在 KitDevTools 模组设置页手动填写。

### Changed

- **标题屏开发模式** — 改为打开 KitDevTools 自建面板，不再替换官方主菜单文字按钮。遮罩与原版标题屏一致，角标仍可见。打开时角标飞到更新日志槽位；再点一次角标、点返回或按 Esc 即可关闭。
- **作弊侧栏页签** — Cards、Cheats、Card Test、存档等作弊侧栏入口改由本产品注册，不再由 KitLib Core 登记。
- **伪联机测试驾驶** — SyncBot 幻影、开房与模拟对端战斗/地图补丁改由本产品承载，并调用 Core NetPlay API。KitAI 不再拥有这套测试驾驶。
- **面板切换** — 再次打开已访问过的开发面板更流畅。
- **手牌投放区** — 底部投放标签改为 **手牌**。
- **移除 AI Host 标签** — 开发侧栏不再显示 AI 机器人入口；AI 控制将随 KitAI 独立面板提供。

### Fixed

- **Mod 测试 — 篝火** — 在稳定版游戏构建上离开预览房间后，会正确恢复单人布局。
- **DevTools 回放** — 开局奥涅与事件画面在回放中正常显示，内容排在回放底栏上方。回放中不可手动出牌、改作弊或点地图移动（仍可查看信息）。
