# KitLib

[English](./README.md) | **中文**

KitLib 同时面向 mod 开发者与普通玩家。开发者可以开测试局，在游戏里用左侧开发面板直接改卡牌、数值、敌人状态，看日志和战斗信息，也支持伪联机、双开 LAN 等联机调试；配合 Hook 与自动化，少重启就能验证自己的 mod。玩家侧则有更好用的 Mod 面板、进度保护、问题反馈导出，以及可选的辅助功能。

本 mod 采用模块化结构：`KitLib` Core 负责加载，卫星模块可按需开关；某个可选模块加载失败时，不会拖垮核心，也不会影响依赖 KitLib 的其他 mod。

> 目前计划持续优化已有功能；为减轻可维护性和心智负担，会删掉一些不必要的功能，优先保证后续代码质量。

## 功能

### Dev Mode（标题画面）

- 新建测试 / 指定种子
- 读档、伪联机一键开跑
- 解锁全部进度
- 日志查看、Mod 反馈

### 局内侧栏

- 卡牌 / 遗物 / 敌人 / 能力 / 药水 / 事件浏览器
- 房间传送、命令手册
- 作弊、预设、卡牌测试
- 敌方意图、战斗统计
- AI 托管、钩子
- Harmony 分析、框架桥接
- 存档 / 读档、设置、日志、Mod 反馈
- 侧栏展开收起、标签排序隐藏、快捷键

### 浏览器（局内改内容）

- 卡牌：浏览搜索、增删改与附魔
- 遗物、药水、能力、事件
- 敌人：遭遇战选择与地图覆盖
- 房间：商店、营火、宝箱、测试房等

### 作弊

- 玩家战斗（无敌、能量 / 护盾 / 星辉、回合与抽牌等）
- 敌人（冻结、伤害、击杀等）
- 经济与商店（金币、倍率、免费购买）
- 状态与奖励（能量上限、药水槽、分数、卡牌奖励）
- 地图（调试跳转、地图改写）
- 数值锁定（金币、HP、能量、星辉、球栏位等）

### 存档与预设

- 多槽位存读、快速存读、战斗 / 回合检查点
- 预设保存、应用、导入导出

### 日志

- 局内 / 主菜单日志查看与筛选
- kitlog 实时跟踪（可选）

### AI 与联机

- 单人 AI 托管与 HUD
- 伪联机、双开 LAN、队友托管与 SyncBot

### 自动化

- 钩子规则
- MCP 桥接（供外部 Agent / 工具调用）

### 调试

- 战斗统计、敌方意图、性能叠层与 trace
- Harmony 与框架（RitsuLib）摘要

### 主菜单 Mod 设置（Mods → KitLib）

- 可选模块开关
- 主题、快捷键、局内 DevMode 级别
- 进度保护、联机作弊 opt-in

### Mod 面板

- 模组列表（来源、加载状态）、启用 / 禁用
- 各模组设置页嵌入展示

### Mod 反馈

- 填写复现步骤，导出 ZIP（日志、Mod 列表、诊断信息）

## 安装

- **KitLib 本体**：通过 Steam 创意工坊或 Nexus 安装。
- **辅助工具**（`kitlog` 命令行、`KitLib.Mcp` 等）：从 [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) 或 Nexus 下载对应平台的可执行文件。

## 给 Mod 开发者

- 编译时在 csproj 引用 `eng/KitLib.ContentMod.props`（`KitLib.Abstractions.dll`）。
- 运行时按 mod 实际用到的能力，依赖 KitLib 主体与对应卫星模块。
- 扩展 API、日志、AI 接入等见 [文档站](https://sts2-devmod.wrxinyue.org/) 开发者章节。

## 致谢

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## 许可证

[MIT](https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE)
