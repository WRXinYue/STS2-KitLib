Foundation library and host for Slay the Spire 2 mods.

KitLib loads first, then exposes APIs other mods call from their initializer. It also runs settings, progress protection, theme, and hotkeys.

[url=https://sts2-devmod.wrxinyue.org/]Docs[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · Steam Workshop / Nexus

[b]APIs ([i]KitLib.Abstractions[/i])[/b]

[list]
[*]Main-menu corner buttons (shared icon stack)
[*]Run mutations: cards, relics, potions, powers
[*]Cheat / run-stat toggles (gold, HP, energy, …)
[*]Logging
[/list]

[b]Host[/b]

[list]
[*]Loader ([i]KitLib.dll[/i] + [i]KitLib.Core.dll[/i])
[*]Progress protection, theme, hotkeys, KitLib settings
[*]Optional modules load independently; load failures do not affect the host
[/list]

[url=./LICENSE]MIT[/url]

[line]

《杀戮尖塔 2》模组基础库和宿主。

KitLib 先加载，再提供其他 mod 在初始化时调用的 API。同时负责设置、进度保护，以及本仓库子 mod 用到的运行时。

[url=https://sts2-devmod.wrxinyue.org/]文档[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · Steam 创意工坊 / Nexus

[b]API（[i]KitLib.Abstractions[/i]）[/b]

[list]
[*]主菜单角标（共用图标列）
[*]局内突变：卡牌、遗物、药水、Power
[*]作弊 / 局内数值（金币、生命、能量等）
[*]日志流
[/list]

[b]宿主[/b]

[list]
[*]加载器（[i]KitLib.dll[/i] + [i]KitLib.Core.dll[/i]）
[*]进度保护、主题、快捷键、KitLib 设置
[*]按需加载子 mod；加载失败不影响宿主运行
[/list]

[b]子 mod[/b]

| Mod | 作用 |
| --- | --- |
| [url=./mods/KitModPanel/README.zh-CN.md]KitModPanel[/url] | 主菜单模组列表与设置 |
| [url=./mods/KitDevTools/README.zh-CN.md]KitDevTools[/url] | Dev Mode、侧栏、浏览器、作弊 UI、存档、日志、MCP |
| [url=./mods/KitAI/README.zh-CN.md]KitAI[/url] | AI 托管 / 自动游玩 |

[url=./LICENSE]MIT[/url]
