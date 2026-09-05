# KitLib

[English](./README.md) | **中文**

《杀戮尖塔 2》模组基础库和宿主。

KitLib 同时面向 mod 开发者与普通玩家。开发者可以开测试局，在游戏里用左侧开发面板直接改卡牌、数值、敌人状态，看日志和战斗信息，也支持伪联机、双开 LAN 等联机调试；配合 Hook 与自动化，少重启就能验证自己的 mod。玩家侧则有更好用的 Mod 面板、进度保护、问题反馈导出，以及可选的辅助功能。

本 mod 采用模块化结构：KitLib Core 负责加载，卫星模块可按需开关；某个可选模块加载失败时，不会拖垮核心，也不会影响依赖 KitLib 的其他 mod。

[文档](https://sts2-devmod.wrxinyue.org/) · [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) · [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669) / Nexus

> **注意：** KitLib **0.40.0** 起，模组面板、开发工具和 AI 已拆成独立 mod。请按需订阅对应物品；只订阅 KitLib 不再包含这些功能。

## 组成

按需安装配套产品：

- **[KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669)**（本仓库宿主）— 先加载，再提供其他 mod 在初始化时调用的 API；同时负责设置、进度保护、主题和快捷键。
- **[KitModPanel](./mods/KitModPanel/README.zh-CN.md)**（[创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384)）— 主菜单模组列表与各模组设置页；装有 STS2-RitsuLib 时一并显示其设置页。
- **[KitDevTools](./mods/KitDevTools/README.zh-CN.md)**（[创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840)）— 标题画面 Dev Mode、局内侧栏、回放、作弊与联机调试。

## 宿主

- 加载器（`KitLib.dll` + `KitLib.Core.dll`），启动时按游戏版本选择 `lib/<api>` 变体
- 进度保护（避免开发/测试改动写进正式进度）
- 主题、快捷键、KitLib 设置（Mods → KitLib）
- 主菜单角标按钮（共用图标列）
- 可选模块独立加载；加载失败不影响宿主和其他依赖 KitLib 的 mod

## API（`KitLib.Abstractions`）

给其他 mod 在初始化时调用，不必依赖开发面板：

- 主菜单角标
- 局内突变：卡牌、遗物、药水、Power
- 作弊 / 局内数值（金币、生命、能量等）
- 日志流

编译时在 csproj 引用 `eng/KitLib.ContentMod.props`（`KitLib.Abstractions.dll`）。运行时按实际用到的能力依赖 KitLib 主体与对应卫星模块。细节见 [文档站](https://sts2-devmod.wrxinyue.org/)。

## 安装

- **[KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669)**（必需）以及 **[KitModPanel](https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384)** / **[KitDevTools](https://steamcommunity.com/sharedfiles/filedetails/?id=3793490840)**：Steam 创意工坊，或 [GitHub Releases](https://github.com/WRXinYue/STS2-KitLib/releases) / Nexus 的发布包。
- **辅助工具**（`KitLib.Mcp` 等）：同一 Releases / Nexus 页下载对应平台可执行文件。

## 致谢

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)
- [RunReplays](https://github.com/boardengineer/RunReplays)

[MIT](./LICENSE)
