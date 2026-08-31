Foundation library and host for Slay the Spire 2 mods.

KitLib is for both mod authors and players. Authors can start a test run, edit cards, numbers, and enemy state from the left-hand rail, watch logs and combat info, and debug multiplayer with pseudo co-op or dual-instance LAN. Hooks and automation let you verify a mod without constantly restarting the game. Players get a clearer mod panel, progress protection, feedback ZIP export, and optional helpers.

The layout is modular: KitLib Core loads first; satellite modules can be toggled. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[url=https://sts2-devmod.wrxinyue.org/]Docs[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]Steam Workshop[/url] / Nexus

[quote]
[b]Note:[/b] From KitLib [b]0.40.0[/b], Mod Panel, Dev Tools, and AI are separate mods. Subscribe to each companion you need; KitLib alone no longer includes them.
[/quote]

[b]Products[/b]

Install companions as needed:

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b] (this host) — loads first, then exposes APIs other mods call from their initializer. Also runs settings, progress protection, theme, and hotkeys.
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitModPanel/README.md]KitModPanel[/url][/b] ([url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384]Workshop[/url]) — main-menu mod list and per-mod settings, including STS2-RitsuLib pages when RitsuLib is present.
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitDevTools/README.md]KitDevTools[/url][/b] ([url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840]Workshop[/url]) — title-screen Dev Mode, in-run rail, replay, cheats, and multiplayer debug.
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitAI/README.md]KitAI[/url][/b] — optional AI host / autoplay.
[/list]

[b]Host[/b]

[list]
[*]Loader ([i]KitLib.dll[/i] + [i]KitLib.Core.dll[/i]); picks the [i]lib/&lt;api&gt;[/i] variant for the running game
[*]Progress protection (keeps test edits out of live progress)
[*]Theme, hotkeys, KitLib settings (Mods → KitLib)
[*]Main-menu corner buttons (shared icon stack)
[*]Optional modules load independently; a load failure does not take down the host or other KitLib-dependent mods
[/list]

[b]APIs ([i]KitLib.Abstractions[/i])[/b]

Other mods call these from their initializer; they do not require the dev rail:

[list]
[*]Main-menu corner buttons
[*]Run mutations: cards, relics, potions, powers
[*]Cheat / run-stat toggles (gold, HP, energy, …)
[*]Logging
[/list]

At compile time, import [i]eng/KitLib.ContentMod.props[/i] ([i]KitLib.Abstractions.dll[/i]). At runtime, depend on KitLib core and whichever satellite modules you actually use. Details: [url=https://sts2-devmod.wrxinyue.org/]docs site[/url].

[b]Install[/b]

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b] (required) plus [b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384]KitModPanel[/url][/b] / [b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840]KitDevTools[/url][/b] / [b]KitAI[/b] from Steam Workshop or the release zip on [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] / Nexus.
[*][b]Auxiliary tools[/b] ([i]KitLib.Mcp[/i], etc.) — same Releases / Nexus page, per-platform binaries.
[/list]

[b]Acknowledgments[/b]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[*][url=https://github.com/boardengineer/RunReplays]RunReplays[/url]
[/list]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]

[line]

《杀戮尖塔 2》模组基础库和宿主。

KitLib 同时面向 mod 开发者与普通玩家。开发者可以开测试局，在游戏里用左侧开发面板直接改卡牌、数值、敌人状态，看日志和战斗信息，也支持伪联机、双开 LAN 等联机调试；配合 Hook 与自动化，少重启就能验证自己的 mod。玩家侧则有更好用的 Mod 面板、进度保护、问题反馈导出，以及可选的辅助功能。

本 mod 采用模块化结构：KitLib Core 负责加载，卫星模块可按需开关；某个可选模块加载失败时，不会拖垮核心，也不会影响依赖 KitLib 的其他 mod。

[url=https://sts2-devmod.wrxinyue.org/]文档[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]Steam 创意工坊[/url] / Nexus

[quote]
[b]注意：[/b] KitLib [b]0.40.0[/b] 起，模组面板、开发工具和 AI 已拆成独立 mod。请按需订阅对应物品；只订阅 KitLib 不再包含这些功能。
[/quote]

[b]组成[/b]

按需安装配套产品：

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b]（本仓库宿主）— 先加载，再提供其他 mod 在初始化时调用的 API；同时负责设置、进度保护、主题和快捷键。
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitModPanel/README.zh-CN.md]KitModPanel[/url][/b]（[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384]创意工坊[/url]）— 主菜单模组列表与各模组设置页；装有 STS2-RitsuLib 时一并显示其设置页。
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitDevTools/README.zh-CN.md]KitDevTools[/url][/b]（[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840]创意工坊[/url]）— 标题画面 Dev Mode、局内侧栏、回放、作弊与联机调试。
[*][b][url=https://github.com/WRXinYue/STS2-KitLib/blob/main/mods/KitAI/README.zh-CN.md]KitAI[/url][/b] — 可选的 AI 托管 / 自动游玩。
[/list]

[b]宿主[/b]

[list]
[*]加载器（[i]KitLib.dll[/i] + [i]KitLib.Core.dll[/i]），启动时按游戏版本选择 [i]lib/&lt;api&gt;[/i] 变体
[*]进度保护（避免开发/测试改动写进正式进度）
[*]主题、快捷键、KitLib 设置（Mods → KitLib）
[*]主菜单角标按钮（共用图标列）
[*]可选模块独立加载；加载失败不影响宿主和其他依赖 KitLib 的 mod
[/list]

[b]API（[i]KitLib.Abstractions[/i]）[/b]

给其他 mod 在初始化时调用，不必依赖开发面板：

[list]
[*]主菜单角标
[*]局内突变：卡牌、遗物、药水、Power
[*]作弊 / 局内数值（金币、生命、能量等）
[*]日志流
[/list]

编译时在 csproj 引用 [i]eng/KitLib.ContentMod.props[/i]（[i]KitLib.Abstractions.dll[/i]）。运行时按实际用到的能力依赖 KitLib 主体与对应卫星模块。细节见 [url=https://sts2-devmod.wrxinyue.org/]文档站[/url]。

[b]安装[/b]

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b]（必需）以及 [b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384]KitModPanel[/url][/b] / [b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840]KitDevTools[/url][/b] / [b]KitAI[/b]：Steam 创意工坊，或 [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] / Nexus 的发布包。
[*][b]辅助工具[/b]（[i]KitLib.Mcp[/i] 等）：同一 Releases / Nexus 页下载对应平台可执行文件。
[/list]

[b]致谢[/b]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[*][url=https://github.com/boardengineer/RunReplays]RunReplays[/url]
[/list]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
