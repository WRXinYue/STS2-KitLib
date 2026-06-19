Modular in-game toolkit for Slay the Spire 2. KitLib ships as a thin Core host with optional satellite modules for the dev rail, cheats, AI, logging, and main-menu mod settings. Use it for test runs, cheats, scripting, and mod debugging without leaving the game. Content mods can reference NuGet [i]STS2.KitLib.Abstractions[/i] and ship [i]kitlib.compat.toml[/i] for version checks.

[b]Getting started[/b]

[list]
[*][b]Main menu → Mods → KitLib[/b] — Satellite load profiles (Minimal / Standard / Full / Custom), hotkeys, accent theme, compat warnings, progress protection, optional live log terminal on startup.
[*][b]During a run[/b] — Hover the left-edge [b]peek tab[/b] to expand the dev rail, then click a panel icon.
[*][b]Title screen[/b] — [b]Dev Mode[/b] for test runs, snapshots, diagnostics, and multiplayer dev tools.
[*][b]Settings → Sidebar / Game[/b] — Reorder rail tabs, hide panels, combat overlays, game speed, skip animations.
[*][b]Normal runs[/b] — Title [b]Dev Mode → Normal run[/b] cycles Disabled / Toolkit / Cheat Mode.
[/list]

Install from [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] or build from source ([i]python scripts/init.py[/i], then [i]make sync-full[/i]). One package supports pinned stable and beta STS2 builds; a startup banner appears when the mod build mismatches your game.

[b]Features at a glance[/b]

| Area | Highlights |
| --- | --- |
| [b]Gameplay[/b] | Cheats, cards, relics, powers, potions, enemies, events, rooms, presets |
| [b]Automation[/b] | Hooks, SpireScratch scripts, AI Host (solo), MCP, KitLog CLI |
| [b]Debug[/b] | Logs, combat stats, enemy intents, console, Harmony analysis, mod feedback |
| [b]Utility[/b] | Save/load slots, themes & overlays |

Panel-by-panel help: [b][url=docs/pages/index.md]docs site[/url][/b] ([i]make docs[/i]) — [url=docs/pages/guide/panels/index.md]Rail panels[/url].

[b]Contributing[/b]

See [b][url=CONTRIBUTING.md]CONTRIBUTING.md[/url][/b] or open an issue / PR on [url=https://github.com/WRXinYue/STS2-KitLib]GitHub[/url].

[b]Changelog[/b]

See [url=https://github.com/WRXinYue/STS2-KitLib/blob/main/CHANGELOG.md]CHANGELOG.md[/url].

[b]Acknowledgments[/b]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[b]License[/b]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]

[line]

《杀戮尖塔 2》模块化游戏内工具箱。KitLib 以轻量 Core 宿主加载可选卫星模块，覆盖开发侧栏、作弊、AI、日志与主菜单 Mod 设置。内容 mod 可引用 NuGet [i]STS2.KitLib.Abstractions[/i]，并随包发布 [i]kitlib.compat.toml[/i] 做版本检查。

[b]快速上手[/b]

[list]
[*][b]主菜单 → Mods → KitLib[/b] — 模块加载档位、快捷键、强调色、兼容提示、进度保护、可选启动时打开实时日志终端。
[*][b]局内[/b] — 鼠标移到左侧 [b]peek 标签[/b] 展开 dev 侧栏，点击图标打开面板。
[*][b]标题画面[/b] — [b]开发模式[/b]：测试局、快照、诊断、联机开发工具。
[*][b]设置 → 侧栏 / 游戏[/b] — 排序/隐藏面板、战斗 overlay、游戏速度、跳过动画。
[*][b]普通 run[/b] — 标题 [b]开发模式 → Normal run[/b] 在关闭 / 工具箱 / 作弊模式间切换。
[/list]

可从 [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] 安装，或源码构建（[i]python scripts/init.py[/i]，再 [i]make sync-full[/i]）。同一安装包支持 stable 与 beta；版本不匹配时启动会显示提示横幅。

[b]功能概览[/b]

| 类别 | 内容 |
| --- | --- |
| [b]玩法[/b] | 作弊、卡牌、遗物、能力、药水、敌人、事件、房间、预设 |
| [b]自动化[/b] | 钩子、SpireScratch 脚本、AI 托管（单人）、MCP、KitLog CLI |
| [b]调试[/b] | 日志、战斗统计、敌人意图、控制台、Harmony 分析、Mod 反馈 |
| [b]工具[/b] | 存读档槽、主题与 overlay |

各面板说明：[b][url=docs/pages/index.md]文档站[/url][/b]（[i]make docs[/i]）— [url=docs/pages/guide/panels/index.md]轨道面板[/url]。

[b]参与贡献[/b]

见 [b][url=CONTRIBUTING.md]CONTRIBUTING.md[/url][/b] 或在 [url=https://github.com/WRXinYue/STS2-KitLib]GitHub[/url] 提 issue / PR。

[b]更新日志[/b]

见 [url=https://github.com/WRXinYue/STS2-KitLib/blob/main/CHANGELOG.md]CHANGELOG.md[/url]。

[b]致谢[/b]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[b]许可证[/b]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
