KitLib serves both mod authors and players. Authors can start test runs, use the left-edge dev panel in-game to edit cards, stats, and enemy state, read logs and combat info, and debug multiplayer (pseudo co-op, dual-instance LAN)—with hooks, scripts, and automation to validate mods with fewer restarts. Players get a better Mod panel, progress protection, feedback export, and optional assist features.

KitLib is modular: KitLib Core handles loading; satellite modules are optional. If an optional module fails to load, it should not take down the core or block other mods that depend on KitLib.

[h3]Install[/h3]

[list]
[*][b]KitLib mod[/b] — Steam Workshop or Nexus.
[*][b]Auxiliary tools[/b] (kitlog CLI, KitLib.Mcp) — [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] or Nexus.
[/list]

[h3]For content mod authors[/h3]

[list]
[*]Reference eng/KitLib.ContentMod.props at compile time (local KitLib.Abstractions.dll).
[*]Depend on KitLib core and satellite modules at runtime as needed.
[*]See the developer pages on the docs site for integration details.
[/list]

[h3]Docs[/h3]

[list]
[*]Docs: [url=https://sts2-devmod.wrxinyue.org/]sts2-devmod.wrxinyue.org[/url]
[*]Contributing: [url=CONTRIBUTING.md]CONTRIBUTING.md[/url]
[/list]

[h3]Acknowledgments[/h3]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[h3]License[/h3]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]

[hr][/hr]

KitLib 同时面向 mod 开发者与普通玩家。开发者可以开测试局，在游戏里用左侧开发面板直接改卡牌、数值、敌人状态，看日志和战斗信息，也支持伪联机、双开 LAN 等联机调试；配合 Hook、脚本和自动化，少重启就能验证自己的 mod。玩家侧则有更好用的 Mod 面板、进度保护、问题反馈导出，以及可选的辅助功能。

本 mod 采用模块化结构：KitLib Core 负责加载，卫星模块可按需开关；某个可选模块加载失败时，不会拖垮核心，也不会影响依赖 KitLib 的其他 mod。

[h3]功能[/h3]

[h3]标题画面 · Dev Mode[/h3]

[list]
[*]新建测试
[*]指定种子新建测试
[*]读取存档（槽位选择）
[*]伪联机（主机）一键开跑
[*]解锁全部进度（时间线、进阶、图鉴）
[*]诊断：日志查看器
[*]诊断：Mod 反馈 / Bug 报告
[/list]

[h3]局内侧栏[/h3]

[list]
[*]卡牌浏览器
[*]遗物浏览器
[*]敌人浏览器
[*]能力浏览器
[*]药水浏览器
[*]事件浏览器
[*]房间传送
[*]命令手册
[*]作弊面板
[*]AI 托管
[*]预设管理器
[*]卡牌测试
[*]敌方意图
[*]战斗统计
[*]钩子规则
[*]脚本管理
[*]Harmony 补丁分析
[*]框架桥接
[*]Mod 反馈 / Bug 报告
[*]存档 / 读档
[*]设置
[*]日志查看器
[*]侧栏展开 / 收起（Peek 标签与快捷键）
[*]侧栏标签排序与隐藏
[*]侧栏锁定
[/list]

[h3]卡牌[/h3]

[list]
[*]浏览与搜索卡牌（手牌、抽牌堆、弃牌堆、牌组等来源）
[*]按类型、稀有度、费用、Mod 来源筛选
[*]添加卡牌（临时 / 永久）
[*]升级卡牌
[*]删除卡牌
[*]编辑卡牌数值（费用、伤害、格挡等）
[*]编辑卡牌附魔
[*]卡牌预设保存与应用
[/list]

[h3]遗物[/h3]

[list]
[*]浏览遗物图鉴
[*]添加遗物
[*]删除遗物
[/list]

[h3]敌人[/h3]

[list]
[*]选择遭遇战并加入战斗
[*]选择单体怪物加入战斗
[*]地图节点遭遇战预览与覆盖
[*]楼层遭遇战覆盖
[*]编辑敌人 HP
[*]击杀 / 复制敌人
[*]清除敌人能力
[/list]

[h3]能力[/h3]

[list]
[*]浏览 Buff / Debuff
[*]对目标施加能力
[*]清除当前能力
[/list]

[h3]药水[/h3]

[list]
[*]搜索并选取药水
[*]添加药水到玩家
[/list]

[h3]事件[/h3]

[list]
[*]搜索事件
[*]直接触发事件
[*]远古事件入口选项
[/list]

[h3]房间[/h3]

[list]
[*]进入商店
[*]进入营火
[*]进入宝箱房
[*]进入地图
[*]进入测试房
[/list]

[h3]作弊 · 玩家[/h3]

[list]
[*]无敌
[*]无限护盾
[*]无限能量
[*]无限星辉
[*]上帝模式
[*]清除负面效果
[*]始终玩家回合
[*]抽牌至上限
[*]每回合额外抽牌
[*]防御倍率
[/list]

[h3]作弊 · 敌人[/h3]

[list]
[*]冻结敌人
[*]超高伤害 / 一击必杀
[*]击杀全部敌人
[*]友方怪物自动行动
[*]伤害倍率
[/list]

[h3]作弊 · 物品栏与经济[/h3]

[list]
[*]编辑金币
[*]金钱倍率
[*]商店免费购买
[/list]

[h3]作弊 · 状态[/h3]

[list]
[*]编辑能量上限
[*]编辑药水槽数量
[*]最高分数追踪
[*]分数倍率
[/list]

[h3]作弊 · 奖励[/h3]

[list]
[*]始终奖励药水
[*]始终升级卡牌奖励
[*]最大卡牌奖励稀有度
[/list]

[h3]作弊 · 地图[/h3]

[list]
[*]未知地图点位始终给予宝藏
[*]地图调试跳转
[*]地图改写（全部宝箱 / 全部精英 / 全部 Boss）
[*]地图改写保留最终 Boss
[/list]

[h3]作弊 · 数值锁定[/h3]

[list]
[*]锁定 / 编辑金币
[*]锁定 / 编辑当前 HP
[*]锁定 / 编辑最大 HP
[*]锁定 / 编辑当前能量
[*]锁定 / 编辑最大能量
[*]锁定 / 编辑星辉
[*]锁定 / 编辑充能球栏位
[/list]

[h3]存档与快照[/h3]

[list]
[*]多槽位存档
[*]多槽位读档
[*]存档槽自定义名称
[*]快速存档
[*]快速读档
[*]战斗检查点重打本局
[*]回合检查点重回本回合
[*]指定种子重启
[*]新建测试（侧栏存档面板）
[/list]

[h3]预设[/h3]

[list]
[*]保存当前局为预设
[*]应用预设（可选卡牌 / 遗物 / 状态 / 战斗快照范围）
[*]预设导入（剪贴板）
[*]预设导出
[*]预设删除
[/list]

[h3]卡牌测试[/h3]

[list]
[*]卡牌加入测试队列
[*]战斗中注入并打出卡牌
[*]进入测试房（BigDummy）
[/list]

[h3]日志[/h3]

[list]
[*]局内 / 主菜单日志查看器
[*]按级别筛选（Info / Warn / Error）
[*]文本搜索与噪音抑制
[*]按 Mod 来源筛选
[*]复制单条 / 全部日志
[*]打开日志文件夹
[*]启动 kitlog 实时跟踪
[*]启动时自动打开 kitlog 终端（Mod 设置）
[*]侧栏日志标签错误 / 警告提醒
[/list]

[h3]AI[/h3]

[list]
[*]单人 AI 托管（自动跑图 / 战斗 / 奖励）
[*]游戏内 AI HUD
[*]AI 决策终端与 kitlog 跟踪
[*]伪联机：战斗托管模拟队友
[*]伪联机：SyncBot 模拟远端 ACK
[*]伪联机：生成幻影玩家
[*]联机：AI 代打在线队友
[*]联机：LAN 客机 AFK 由主机代打
[/list]

[h3]钩子与脚本[/h3]

[list]
[*]钩子规则编辑器（触发 / 条件 / 动作）
[*]SpireScratch 脚本启用 / 禁用 / 重载
[*]打开脚本文件夹与可视化编辑器
[*]Hook 规则迁移为脚本
[/list]

[h3]调试与分析[/h3]

[list]
[*]战斗统计（概览 / 按卡牌 / 承伤 / 按回合）
[*]敌方意图链预览与回合覆盖
[*]Harmony 补丁分析报告
[*]框架桥接（RitsuLib 与 Harmony 摘要）
[*]性能叠层（过渡 / 帧尖峰）
[*]性能 trace 文件（CSV）
[/list]

[h3]MCP 桥接（Dev 模块）[/h3]

[list]
[*]获取游戏状态快照
[*]战斗 / 地图操作
[*]存档槽管理与测试局启动
[*]卡牌 / 怪物 / 敌人操作
[*]作弊与数值设置
[/list]

[h3]主菜单 Mod 设置（Mods → KitLib）[/h3]

[list]
[*]主题色
[*]局内 DevMode 级别（普通局关闭 / Dev 面板 / 作弊工具）
[*]模块加载方案（精简 / 标准 / 完整 / 自定义）
[*]可选模块开关（Panel、AI、Cheat、Dev）
[*]联机作弊 opt-in
[*]启动时打开实时日志终端
[*]进度保护（自动备份、恢复、丢失提示）
[*]性能叠层与 trace 开关
[*]快捷键绑定（侧栏、存读、重打战斗 / 回合等）
[/list]

[h3]Mod 面板[/h3]

[list]
[*]已安装模组列表（本地 / 创意工坊来源）
[*]模组启用 / 禁用
[*]模组加载状态预览
[*]各模组 RitsuLib 设置页嵌入展示
[/list]

[h3]Mod 反馈[/h3]

[list]
[*]填写标题与复现步骤
[*]选择附加游戏日志
[*]隐私模式（路径脱敏）
[*]导出 ZIP（报告、Mod 列表、日志、Harmony 转储、框架快照）
[/list]

[h3]安装[/h3]

[list]
[*][b]KitLib 本体[/b]：通过 Steam 创意工坊或 Nexus 安装。
[*][b]辅助工具[/b]（kitlog 命令行、KitLib.Mcp 等）：从 [url=https://github.com/WRXinYue/STS2-KitLib/releases]GitHub Releases[/url] 或 Nexus 下载对应平台的可执行文件。
[/list]

[h3]给内容 Mod 作者[/h3]

[list]
[*]内容 mod 编译期引用 eng/KitLib.ContentMod.props（本地 KitLib.Abstractions.dll）。
[*]运行时按需依赖 KitLib 主体与对应卫星模块。
[*]如果你需要更细的接入说明，优先看文档站中的开发者页面。
[/list]

[h3]文档[/h3]

[list]
[*]文档入口：[url=https://sts2-devmod.wrxinyue.org/]sts2-devmod.wrxinyue.org[/url]
[*]贡献说明：[url=CONTRIBUTING.md]CONTRIBUTING.md[/url]
[/list]

[h3]致谢[/h3]

[list]
[*][url=https://github.com/mugongzi520/STS2-KaylaMod]STS2-KaylaMod[/url]
[/list]

[h3]许可证[/h3]

[url=https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE]MIT[/url]
