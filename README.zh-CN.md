# KitLib

[English](./README.md) | **中文**

《杀戮尖塔 2》模块化游戏内工具箱。KitLib 以轻量 Core 宿主加载可选卫星模块，覆盖开发侧栏、作弊、AI、日志与主菜单 Mod 设置。内容 mod 可引用 NuGet STS2.KitLib.Abstractions，并随包发布 kitlib.compat.toml 做版本检查。

## 快速上手

- **主菜单 → Mods → KitLib** — 模块加载档位、快捷键、强调色、兼容提示、进度保护、可选启动时打开实时日志终端。
- **局内** — 鼠标移到左侧 **peek 标签** 展开 dev 侧栏，点击图标打开面板。
- **标题画面** — **开发模式**：测试局、快照、诊断、联机开发工具。
- **设置 → 侧栏 / 游戏** — 排序/隐藏面板、战斗 overlay、游戏速度、跳过动画。
- **普通 run** — 标题 **开发模式 → Normal run** 在关闭 / 工具箱 / 作弊模式间切换。

可从 [Releases](https://github.com/WRXinYue/STS2-KitLib/releases) 安装，或源码构建（python scripts/init.py，再 make sync-full）。同一安装包支持 stable 与 beta；版本不匹配时启动会显示提示横幅。

## 功能概览

- **玩法** — 作弊、卡牌、遗物、能力、药水、敌人、事件、房间、预设
- **自动化** — 钩子、SpireScratch 脚本、AI 托管（单人）、MCP、KitLog CLI
- **调试** — 日志、战斗统计、敌人意图、控制台、Harmony 分析、Mod 反馈
- **工具** — 存读档槽、主题与 overlay

各面板说明：**[文档站](docs/pages/index.md)**（make docs）— [轨道面板](docs/pages/guide/panels/index.md)。

## 参与贡献

见 **[CONTRIBUTING.md](CONTRIBUTING.md)** 或在 [GitHub](https://github.com/WRXinYue/STS2-KitLib) 提 issue / PR。

## 更新日志

见 [CHANGELOG.md](https://github.com/WRXinYue/STS2-KitLib/blob/main/CHANGELOG.md)。

## 致谢

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## 许可证

[MIT](https://github.com/WRXinYue/STS2-KitLib/blob/main/LICENSE)
