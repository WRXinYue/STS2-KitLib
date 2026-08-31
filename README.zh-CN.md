# KitLib

[English](./README.md) | **中文**

《杀戮尖塔 2》模组基础库和宿主。

KitLib 先加载，再提供其他 mod 在初始化时调用的 API。同时负责设置、进度保护、主题和快捷键。

[文档](https://sts2-devmod.wrxinyue.org/) · [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) · Steam 创意工坊 / Nexus

## API（`KitLib.Abstractions`）

- 主菜单角标（共用图标列）
- 局内突变：卡牌、遗物、药水、Power
- 作弊 / 局内数值（金币、生命、能量等）
- 日志流

## 宿主

- 加载器（`KitLib.dll` + `KitLib.Core.dll`）
- 进度保护、主题、快捷键、KitLib 设置
- 可选模块独立加载；加载失败不影响宿主运行

[MIT](./LICENSE)
