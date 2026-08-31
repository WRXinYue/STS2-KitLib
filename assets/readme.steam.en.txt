Foundation library and host for Slay the Spire 2 mods.

KitLib loads first, then exposes APIs other mods call from their initializer. It also runs settings, progress protection, theme, and hotkeys.

[url=https://sts2-devmod.wrxinyue.org/]Docs[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · Steam Workshop / Nexus

[h3]APIs (KitLib.Abstractions)[/h3]

[list]
[*]Main-menu corner buttons (shared icon stack)
[*]Run mutations: cards, relics, potions, powers
[*]Cheat / run-stat toggles (gold, HP, energy, …)
[*]Logging
[/list]

[h3]Host[/h3]

[list]
[*]Loader (KitLib.dll + KitLib.Core.dll)
[*]Progress protection, theme, hotkeys, KitLib settings
[*]Optional modules load independently; load failures do not affect the host
[/list]

[url=./LICENSE]MIT[/url]
