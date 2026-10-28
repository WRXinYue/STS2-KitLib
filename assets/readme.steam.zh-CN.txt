《杀戮尖塔 2》模组基础库和宿主。

KitLib 同时面向 mod 开发者与普通玩家。开发者可以开测试局，在游戏里用左侧开发面板直接改卡牌、数值、敌人状态，看日志和战斗信息，也支持伪联机、双开 LAN 等联机调试；配合 Hook 与自动化，少重启就能验证自己的 mod。玩家侧则有更好用的 Mod 面板、进度保护、问题反馈导出，以及可选的辅助功能。

本 mod 采用模块化结构：KitLib Core 负责加载，卫星模块可按需开关；某个可选模块加载失败时，不会拖垮核心，也不会影响依赖 KitLib 的其他 mod。

[url=https://sts2-devmod.wrxinyue.org/]文档[/url] · [url=https://github.com/WRXinYue/STS2-KitLib/releases]Releases[/url] · [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]Steam 创意工坊[/url] / Nexus

[quote]
[b]注意：[/b] KitLib [b]0.44.0[/b] 起，模组面板与开发工具重新合并进 KitLib。只需订阅 KitLib 本体即可。
[/quote]

[h3]组成[/h3]

只有 KitLib 一个发布包；卫星模块会自动加载：

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b] — 宿主：先加载，再提供其他 mod 在初始化时调用的 API；同时负责设置、进度保护、主题和快捷键。
[*][b]模组面板[/b] — 主菜单模组列表与各模组设置页；装有 STS2-RitsuLib 时一并显示其设置页。
[*][b]开发工具[/b] — 标题画面 Dev Mode、局内侧栏、回放、作弊与联机调试。
[/list]

[h3]模组面板[/h3]

主菜单模组列表与各模组设置页。补充官方模组管理，不是替代品。

[list]
[*]启用 / 禁用模组（需重启生效）；对局中只读。显示加载状态、版本、工坊 / 本地、安装体积
[*]侧滑可启用或禁用该 mod；需要重启才生效的 mod、本地覆盖重复项与有更新的工坊条目会在行内标出
[*]显示 KitLib 与各 mod 注册的设置页；装有 STS2-RitsuLib 时一并显示其设置页
[*]从主菜单角标、设置 → 通用 → KitLib，或可配置快捷键打开；手柄用 LB / RB 切换分页
[*]在游戏模组列表中禁用 [b]Mod Panel[/b] 即可完全关闭 Mods 界面；中文等 CJK 使用游戏区域字体（含 Wine / Linux）
[/list]

[h3]开发工具[/h3]

[list]
[*][b]标题画面（Dev Mode）[/b] — 新建测试 / 指定种子 / AutoSlay、读取存档或反馈 ZIP（不覆盖正式存档）、伪联机主机、解锁全部进度、日志
[*][b]回放[/b] — 官方 .mcr 与录制的整局 .replay（房间时间线、倍速 / 单步，默认保留最新 5 份）
[*][b]局内侧栏[/b] — 卡牌、遗物、敌人、能力、药水、事件、房间浏览器，以及命令、作弊、预设、敌方意图、钩子、存读档、日志、设置
[*][b]其他[/b] — 战斗统计与统计环、跳过动画、游戏速度、Harmony 分析、RitsuLib 框架摘要、性能叠层、面向外部 Agent 的 MCP 桥接、反馈 ZIP、双开 LAN 测试辅助
[/list]

[h3]宿主[/h3]

[list]
[*]加载器（KitLib.dll + KitLib.Core.dll），启动时按游戏版本选择 lib/<api> 变体
[*]进度保护（避免开发/测试改动写进正式进度）
[*]主题、快捷键、KitLib 设置（Mods → KitLib）
[*]主菜单角标按钮（共用图标列）
[*]可选模块独立加载；加载失败不影响宿主和其他依赖 KitLib 的 mod
[/list]

[h3]API（KitLib.Abstractions）[/h3]

给其他 mod 在初始化时调用，不必依赖开发面板：

[list]
[*]主菜单角标
[*]局内突变：卡牌、遗物、药水、Power
[*]作弊 / 局内数值（金币、生命、能量等）
[*]日志流
[/list]

编译时在 csproj 引用 eng/KitLib.ContentMod.props（KitLib.Abstractions.dll）。运行时按实际用到的能力依赖 KitLib 主体与对应卫星模块。细节见 [url=https://sts2-devmod.wrxinyue.org/]文档站[/url]。

[h3]安装[/h3]

[list]
[*][b][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669]KitLib[/url][/b]（必需，已包含模组面板与开发工具）：Steam 创意工坊，或 [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] / Nexus 的发布包。
[*][b]辅助工具[/b]（KitLib.Mcp 等）：同一 Releases / Nexus 页下载对应平台可执行文件。
[/list]

[h3]致谢[/h3]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[*][url=https://github.com/boardengineer/RunReplays]RunReplays[/url]
[/list]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
