# DevMode

[English](./README.md) | **中文**

《杀戮尖塔 2》全功能游戏内工具箱：测试、作弊、脚本与 Mod 调试一体化。

![DevMode](https://raw.githubusercontent.com/WRXinYue/STS2-DevMode/main/assets/devmode.png)

## 面板一览

- **作弊** — 无敌、无限能量/格挡、伤害倍率、冻结敌人、数值锁定、地图覆盖、奖励调整 — 所有玩法修改项集中一处
- **卡牌浏览器** — 浏览全卡库，按类型/稀有度/费用/卡池/角色筛选，编辑费用/伤害/格挡，添加至任意牌堆（手牌/抽牌/弃牌/牌组/消耗），查看升级对比；筛选与搜索条件跨会话记忆
- **遗物浏览器** — 浏览并添加遗物
- **能力浏览器** — 浏览并施加能力，支持 4 种目标模式（自身、所有敌人、指定、友军）；一键创建「战斗开始自动施加」钩子
- **药水浏览器** — 图标网格展示；一键创建「战斗开始自动使用」钩子
- **敌人遭遇** — 按房间或地图节点替换遭遇，预览各节点遭遇内容；支持待机动画预览
- **钩子** — 定义自动化「触发器 → 条件 → 动作」规则（例如战斗开始添加卡牌、每回合开始施加能力）
- **脚本** — SpireScratch 可视化积木脚本（Blockly）；通过 WebSocket 实时热重载，无需重启游戏
- **存档管理** — 命名存档槽，支持动态增删；可携带卡牌/遗物/金币开新种子局；完整存档详情视图
- **控制台** — 全部原版及 DevMode 控制台命令的可搜索参考手册
- **日志查看器** — 游戏内日志流，可配置噪音过滤规则
- **Mod 反馈** — 为任意 Mod 作者导出 ZIP 问题报告包（降噪日志、Mod 列表、Harmony 转储、框架快照）；隐私模式自动抹去用户路径
- **Harmony 分析** — 查看所有激活的 Harmony 补丁，按 owner ID 筛选，智能摘要排版
- **设置** — 外观主题（Dark / OLED / Light / Warm）、游戏速度、跳过动画

## 协作与贡献

协作流程、K&R 代码风格、`dotnet format` / `make format`、Python 与本地化等说明见 **[CONTRIBUTING.md](CONTRIBUTING.md)**，或在 [GitHub](https://github.com/WRXinYue/STS2-DevMode) 提交 Issue / PR。

## 更新日志

版本历史请参阅 [CHANGELOG.zh-CN.md](https://github.com/WRXinYue/STS2-DevMode/blob/main/CHANGELOG.zh-CN.md)。

## 致谢

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## 许可证

[MIT](https://github.com/WRXinYue/STS2-DevMode/blob/main/LICENSE)
