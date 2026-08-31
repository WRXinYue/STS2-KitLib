# KitModPanel

[English](./README.md) | **中文**

《杀戮尖塔 2》主菜单模组列表与设置。补充官方模组管理，不是替代品。

[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3793495384)

> **注意：** KitLib **0.40.0** 起本面板已拆成独立 mod。请单独订阅；仅订阅 KitLib ≥0.40.0 不再包含此功能。

## 运行要求

- **[KitLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747619669)** ≥0.40.0（必需）
- **STS2-RitsuLib**（可选）— 已加载时，各模组向 RitsuLib 注册的设置页会显示在本面板中
- **KitDevTools**（可选）— Harmony 补丁数量与检查报告；进度保护页

在游戏模组列表中禁用 **KitModPanel**，即可完全关闭 Mods 界面。

## 打开方式

- 主菜单 **模组** 面板
- 设置 → 通用 → KitLib 入口
- 可配置快捷键（KitLib → 快捷键）

手柄：侧栏切换模组；LB / RB 切换设置分页。

## 模组列表

- 沿用官方扫描结果：启用 / 禁用（需重启生效）。对局中只读
- 加载状态、版本、工坊 / 本地、安装体积
- 点击来源标签可打开安装目录
- 本地覆盖重复项会在列表中标出

## 设置

- **RitsuLib 模组** — 与各模组向 STS2-RitsuLib 注册的页面相同，由本面板承载（未安装 RitsuLib 时列表仍可用，只是不显示这些页）
- **KitLib** — 通用（主题 / 正式开局 / 侧栏）、进度保护、性能叠层、快捷键
- 其他模组也可同样注册 KitLib 原生设置页

中文等 CJK 使用游戏区域字体（含 Wine / Linux）。

[MIT](../../LICENSE)
