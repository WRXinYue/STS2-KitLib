KitLib serves both mod authors and players. Authors can start test runs, use the left-edge dev panel in-game to edit cards, stats, and enemy state, read logs and combat info, and debug multiplayer (pseudo co-op, dual-instance LAN)—with hooks, scripts, and automation to validate mods with fewer restarts. Players get a better Mod panel, progress protection, feedback export, and optional assist features.

KitLib is modular: [i]KitLib[/i] Core handles loading; satellite modules are optional. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[b]Features[/b]

[b]Dev Mode (title screen)[/b]

[list]
[*]New test run / with seed
[*]Load save, pseudo co-op quick start
[*]Unlock all progress
[*]Log viewer, mod feedback
[/list]

[b]In-run dev rail[/b]

[list]
[*]Card / relic / enemy / power / potion / event browsers
[*]Room teleport, command reference
[*]Cheats, presets, card test
[*]Enemy intents, combat stats
[*]AI Host, hooks, scripts
[*]Harmony analysis, framework bridge
[*]Save / load, settings, logs, mod feedback
[*]Rail expand/collapse, tab reorder/hide, hotkeys
[/list]

[b]Browsers (in-run editing)[/b]

[list]
[*]Cards: browse, add/remove/upgrade, edit stats and enchantments
[*]Relics, potions, powers, events
[*]Enemies: encounter picker and map overrides
[*]Rooms: shop, rest, treasure, test room, etc.
[/list]

[b]Cheats[/b]

[list]
[*]Player combat (invincibility, energy/block/stars, turns, draw, etc.)
[*]Enemies (freeze, damage, kill, etc.)
[*]Economy and shop (gold, multipliers, free purchases)
[*]Status and rewards (energy cap, potion slots, score, card rewards)
[*]Map (debug jump, map rewrite)
[*]Stat lock (gold, HP, energy, stars, orb slots, etc.)
[/list]

[b]Saves and presets[/b]

[list]
[*]Multi-slot and quick save/load, combat/turn checkpoints
[*]Preset save, apply, import/export
[/list]

[b]Logs[/b]

[list]
[*]In-run / main-menu log viewer with filters
[*]Optional kitlog live tail
[/list]

[b]AI and multiplayer[/b]

[list]
[*]Solo AI autoplay and HUD
[*]Pseudo co-op, dual-instance LAN, teammate hosting, SyncBot
[/list]

[b]Automation[/b]

[list]
[*]Hook rules, SpireScratch scripts
[*]MCP bridge (external agents and tools)
[/list]

[b]Debug[/b]

[list]
[*]Combat stats, enemy intents, performance overlay and trace
[*]Harmony and framework (RitsuLib) summaries
[/list]

[b]Main-menu mod settings (Mods → KitLib)[/b]

[list]
[*]Module load profiles and optional module toggles
[*]Theme, hotkeys, in-run DevMode level
[*]Progress guard, multiplayer cheat opt-in
[/list]

[b]Mod panel[/b]

[list]
[*]Mod list (source, load status), enable / disable
[*]Embedded per-mod settings pages
[/list]

[b]Mod feedback[/b]

[list]
[*]Reproduction steps and ZIP export (logs, mod list, diagnostics)
[/list]

[b]Install[/b]

[list]
[*][b]KitLib mod[/b] — Steam Workshop or Nexus.
[*][b]Auxiliary tools[/b] ([i]kitlog[/i] CLI, [i]KitLib.Mcp[/i]) — [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] or Nexus.
[/list]

[b]For mod developers[/b]

[list]
[*]Add [i]eng/KitLib.ContentMod.props[/i] to your csproj ([i]KitLib.Abstractions.dll[/i] at compile time).
[*]At runtime, depend on KitLib core and the satellite modules your mod actually uses.
[*]Extension API, logging, AI integration, etc.: [url=https://sts2-devmod.wrxinyue.org/]docs site[/url] → Developer.
[/list]

[b]Docs[/b]

[list]
[*]Docs: [url=https://sts2-devmod.wrxinyue.org/]sts2-devmod.wrxinyue.org[/url]
[*]Contributing: [[i]CONTRIBUTING.md[/i]](CONTRIBUTING.md)
[/list]

[b]Acknowledgments[/b]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[b]License[/b]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]

[line]

KitLib 同时面向 mod 开发者与普通玩家。开发者可以开测试局，在游戏里用左侧开发面板直接改卡牌、数值、敌人状态，看日志和战斗信息，也支持伪联机、双开 LAN 等联机调试；配合 Hook、脚本和自动化，少重启就能验证自己的 mod。玩家侧则有更好用的 Mod 面板、进度保护、问题反馈导出，以及可选的辅助功能。

本 mod 采用模块化结构：[i]KitLib[/i] Core 负责加载，卫星模块可按需开关；某个可选模块加载失败时，不会拖垮核心，也不会影响依赖 KitLib 的其他 mod。

[b]功能[/b]

[b]Dev Mode（标题画面）[/b]

[list]
[*]新建测试 / 指定种子
[*]读档、伪联机一键开跑
[*]解锁全部进度
[*]日志查看、Mod 反馈
[/list]

[b]局内侧栏[/b]

[list]
[*]卡牌 / 遗物 / 敌人 / 能力 / 药水 / 事件浏览器
[*]房间传送、命令手册
[*]作弊、预设、卡牌测试
[*]敌方意图、战斗统计
[*]AI 托管、钩子、脚本
[*]Harmony 分析、框架桥接
[*]存档 / 读档、设置、日志、Mod 反馈
[*]侧栏展开收起、标签排序隐藏、快捷键
[/list]

[b]浏览器（局内改内容）[/b]

[list]
[*]卡牌：浏览搜索、增删改与附魔
[*]遗物、药水、能力、事件
[*]敌人：遭遇战选择与地图覆盖
[*]房间：商店、营火、宝箱、测试房等
[/list]

[b]作弊[/b]

[list]
[*]玩家战斗（无敌、能量 / 护盾 / 星辉、回合与抽牌等）
[*]敌人（冻结、伤害、击杀等）
[*]经济与商店（金币、倍率、免费购买）
[*]状态与奖励（能量上限、药水槽、分数、卡牌奖励）
[*]地图（调试跳转、地图改写）
[*]数值锁定（金币、HP、能量、星辉、球栏位等）
[/list]

[b]存档与预设[/b]

[list]
[*]多槽位存读、快速存读、战斗 / 回合检查点
[*]预设保存、应用、导入导出
[/list]

[b]日志[/b]

[list]
[*]局内 / 主菜单日志查看与筛选
[*]kitlog 实时跟踪（可选）
[/list]

[b]AI 与联机[/b]

[list]
[*]单人 AI 托管与 HUD
[*]伪联机、双开 LAN、队友托管与 SyncBot
[/list]

[b]自动化[/b]

[list]
[*]钩子规则、SpireScratch 脚本
[*]MCP 桥接（供外部 Agent / 工具调用）
[/list]

[b]调试[/b]

[list]
[*]战斗统计、敌方意图、性能叠层与 trace
[*]Harmony 与框架（RitsuLib）摘要
[/list]

[b]主菜单 Mod 设置（Mods → KitLib）[/b]

[list]
[*]模块档位与可选模块开关
[*]主题、快捷键、局内 DevMode 级别
[*]进度保护、联机作弊 opt-in
[/list]

[b]Mod 面板[/b]

[list]
[*]模组列表（来源、加载状态）、启用 / 禁用
[*]各模组设置页嵌入展示
[/list]

[b]Mod 反馈[/b]

[list]
[*]填写复现步骤，导出 ZIP（日志、Mod 列表、诊断信息）
[/list]

[b]安装[/b]

[list]
[*][b]KitLib 本体[/b]：通过 Steam 创意工坊或 Nexus 安装。
[*][b]辅助工具[/b]（[i]kitlog[/i] 命令行、[i]KitLib.Mcp[/i] 等）：从 [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] 或 Nexus 下载对应平台的可执行文件。
[/list]

[b]给 Mod 开发者[/b]

[list]
[*]编译时在 csproj 引用 [i]eng/KitLib.ContentMod.props[/i]（[i]KitLib.Abstractions.dll[/i]）。
[*]运行时按 mod 实际用到的能力，依赖 KitLib 主体与对应卫星模块。
[*]扩展 API、日志、AI 接入等见 [url=https://sts2-devmod.wrxinyue.org/]文档站[/url] 开发者章节。
[/list]

[b]文档[/b]

[list]
[*]文档入口：[url=https://sts2-devmod.wrxinyue.org/]sts2-devmod.wrxinyue.org[/url]
[*]贡献说明：[[i]CONTRIBUTING.md[/i]](CONTRIBUTING.md)
[/list]

[b]致谢[/b]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[b]许可证[/b]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
