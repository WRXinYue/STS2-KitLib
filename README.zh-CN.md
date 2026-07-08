# KitLib

[English](./README.md) | **中文**

KitLib 是《杀戮尖塔 2》的游戏内开发工具箱。
目标很简单：**少切窗口、少重启游戏、更快验证 mod 和玩法改动**。

它是模块化结构：`KitLib` Core + 可选卫星模块（User / Panel / Cheat / AI / Dev 等）。你可以按需开启，不必全装全开。

## 这东西能做什么

- 在游戏里直接开开发侧栏：改数值、发卡、发遗物、加能力、改敌人状态、传送房间、事件测试。
- 做开发存档：快速存读、槽位存读、战斗检查点，方便复现问题。
- 看调试信息：日志查看器、战斗统计、敌人意图、诊断与反馈导出。
- 做自动化：脚本、Hook、MCP 接口、KitLog CLI、单机 AI 托管。
- 配置体验：侧栏标签排序/隐藏、主题强调色、快捷键、游戏速度、跳过动画。

## 3 分钟上手

1. 安装后进主菜单：Mods -> KitLib。
2. 在 KitLib 设置里先做三件事：
   - 选模块档位（Minimal / Standard / Full / Custom）
   - 看看快捷键（默认有快速存读等）
   - 按需要开关动画跳过、速度和 overlay
3. 开一局后，把鼠标移到左侧 `peek` 标签，展开 dev 侧栏开始用。

## 常用入口

- **主菜单 -> Mods -> KitLib**
  模块档位、快捷键、主题、兼容提示、进度保护等总设置。

- **标题画面 -> 开发模式**
  开测试局、存读档、诊断、联机开发相关工具。

- **局内左侧 dev 侧栏**
  作弊、浏览器、日志、战斗统计、敌人意图、脚本、AI 等。

## 重点功能说明

### 存档与快照

- 支持快速存读与多槽位存读。
- 支持战斗内检查点（用于“重打本局/本回合”一类调试）。
- 存档槽支持自定义名称，方便标记测试场景。

### 日志

- 单开默认走官方 `godot.log`（减少重复日志）。
- 双开时按进程生成 `session.log`，避免两个窗口日志互相混杂。
- 可选 `kitlog` 命令行工具，支持跟随输出和筛选同步。

### AI 与自动化

- AI Host 可做单机自动操作测试。
- 支持脚本和 Hook。
- 提供 MCP 桥接，便于 Cursor / Claude 等 Agent 驱动游戏测试。

### Mod 反馈

- 可在游戏内导出反馈包（日志、已加载模组信息、诊断材料），便于问题上报。

## 安装与构建

- 推荐直接从 [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) 下载。
- 源码构建常用流程：
  - `python scripts/init.py`
  - `make sync-full`

同一套仓库支持 stable 与 beta。版本不匹配时，启动会给出提示横幅。

## 给内容 Mod 作者

- 内容 mod 编译期引用 `eng/KitLib.ContentMod.props`（本地 `KitLib.Abstractions.dll`）。
- 运行时按需依赖 KitLib 主体与对应卫星模块。
- 如果你需要更细的接入说明，优先看文档站中的开发者页面。

## 文档

- 文档入口：[sts2-devmod.wrxinyue.org](https://sts2-devmod.wrxinyue.org/)
- 贡献说明：[`CONTRIBUTING.md`](CONTRIBUTING.md)

## 致谢

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## 许可证

[MIT](https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE)
