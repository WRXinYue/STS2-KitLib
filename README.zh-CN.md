# DevMode

[English](./README.md) | **中文**

《杀戮尖塔 2》全功能游戏内工具箱：测试、作弊、脚本与 Mod 调试一体化。

![DevMode](https://raw.githubusercontent.com/WRXinYue/STS2-DevMode/main/assets/devmode.png)

## 快速上手

- **局内** — 鼠标移到左侧 **peek 标签** 展开 dev 侧栏，点击图标打开面板。浏览器面板从左侧滑入；战斗 overlay 在游戏右侧或浮动窗口。
- **标题画面** — 点击 **DEVMODE** 可开测试局、读快照、诊断、进度保护、联机开发工具（无需进 run）。
- **设置 → 侧栏（Sidebar）** — 拖拽排序、隐藏不需要的标签。**Harmony 分析**、**脚本**、**框架** 默认隐藏，需要时在此开启。
- **设置 → 游戏（Game）** — **局内右侧边栏**（战斗快捷 + 统计 rail）、游戏速度、跳过动画、overlay 开关。
- **普通 run** — 标题 **DEVMODE** 中切换 **Normal run: 关闭 / Dev Mode / Cheat Mode**，在非测试局也保留侧栏。

可从 [Releases](https://github.com/WRXinYue/STS2-DevMode/releases) 安装，或源码构建（`python scripts/init.py`，再 `make sync`）。Steam **beta** 分支需使用对应的 beta mod 包。

## 面板一览

### 玩法与内容

- **作弊** — 无敌、无限能量/格挡/星星、伤害倍率、冻结敌人、数值锁定、地图覆盖（地图打开时自由跳转）、奖励调整；**联机**下部分选项受限
- **卡牌** — 全卡库浏览；按类型/稀有度/费用/卡池/角色/**Mod 来源**筛选；**显示隐藏卡牌**；右键筛选 chip **排除**；编辑数值与附魔；添加至任意牌堆；升级对比；筛选条件跨会话记忆
- **遗物** — 浏览并添加遗物；**Mod 来源**筛选
- **能力** — 施加能力（自身、所有敌人、指定、友军）；一键创建「战斗开始自动施加」钩子；**Mod 来源**筛选
- **药水** — 图标网格；一键创建「战斗开始自动使用」钩子；**Mod 来源**筛选
- **敌人** — 按房间或地图节点替换遭遇；预览内容；待机动画预览；编辑敌人每回合意图
- **事件** — 浏览与触发事件流程；**Mod 来源**筛选
- **房间** — 查看与跳转房间类型；传送到远古商店位置
- **预设** — 保存/加载战斗与 run 快照（手牌、牌组、遗物等）

### 自动化与 AI

- **钩子** — 「触发器 → 条件 → 动作」规则（如战斗开始加牌、抽牌时施加能力）
- **脚本** — SpireScratch 可视化积木（Blockly）；WebSocket 热重载
- **AI 托管** — 规则 AI 驱动 **单人** run（地图、战斗、奖励）。联机手打时自动禁用，避免 desync；联机请用下方 Pseudo Co-op / LAN 预设
- **MCP** — 游戏运行时向 MCP 客户端暴露状态与操作 — 见 **[MCP](#mcp)**

### 开发者与调试

- **敌人意图** — **敌人意图** rail 标签：下回合预览；可选战斗内 **可拖拽 overlay**（默认关）；多意图敌人在战斗侧栏上纵向堆叠 badge
- **战斗统计** — 按卡牌/来源/回合统计伤害/格挡/治疗；饼图侧栏；整 run 合计；JSON 导出；`dmstats` 控制台命令；单人 **右侧 slim 条**；联机 **可拖拽右上角 overlay**
- **控制台** — 原版与 DevMode 命令可搜索参考
- **日志** — 见下方 **[日志](#日志)**
- **Harmony 分析** — 查看激活补丁；按 owner 筛选；智能摘要
- **框架** — 已加载 Mod 框架快照
- **Mod 反馈** — 见下方 **[Mod 反馈](#mod-反馈)**

### 工具

- **存档** — DevMode 命名快照槽（与 vanilla `progress.save` 独立）；携带卡牌/遗物/金币开新种子；存档详情
- **手册** — 游戏内文档浏览器（每个工具一页）
- **设置** — 主题（Dark / OLED / Light / Warm）、游戏速度、跳过动画、侧栏布局、战斗 overlay、**进度保护**与**崩溃恢复**开关

## 战斗 overlay

多为**默认关闭**，在 **设置 → 游戏** 或对应面板中开启。

- **局内右侧边栏** — 实时贡献条、敌人意图 preview rail、战斗快捷（加遭遇/怪物、击杀）。默认：**关**
- **敌人意图 overlay** — 可拖拽浮动窗，显示下回合意图。默认：**关**
- **联机战斗统计 overlay** — 联机时右上角可拖拽各玩家得分条。默认：**开**

打开完整 **战斗统计** 面板且浏览器几乎全宽时，可与右侧 rail 对齐合并。

**联机作弊同步** — 主机在标题 **DEVMODE → Multiplayer** 开启 **Multiplayer cheat** 后，作弊、卡牌/遗物/药水编辑、战斗敌人工具、能力及 per-player 作弊标记可跨客户端同步（所有 peer 需安装 DevMode）。

## 日志

从局内 **日志** rail 标签，或标题 **DEVMODE → Diagnostics → Logs** 打开。

- **实时 + 文件历史** — 流式接收新日志，并从会话日志回填更早行（`mod_data/DevMode/instances/{pid}/session.log`，回退 Godot `user://logs/`）。
- **筛选** — 级别 chip（全部 / ≥ Info / ≥ Warn / Error）、文本搜索、按 mod 来源开关、可切换的**噪音抑制**规则（已知无害模式 + 命中次数）。
- **展示** — mod 与游戏来源分色；DevMode 重启之间的会话边界标记。
- **统计侧栏** — 按级别与 mod 计数；**来源饼图**。
- **复制全部** — 将当前筛选结果复制到剪贴板。
- **提醒** — 出现未读 Warn/Error 时 **日志** rail 图标闪烁，打开查看器后清除。peek 标签在首次 hover 侧栏前闪烁（之后永久关闭）。

## Mod 反馈

从局内 rail 或标题 **DEVMODE → Diagnostics → Mod Feedback** 打开。

填写标题与描述，可选附加游戏日志尾部，导出供 mod 作者使用的 **ZIP 报告**。**隐私模式** 会将用户数据路径替换为 `<user-data>`。

ZIP 典型内容：

- `report.txt` — 描述与环境摘要
- `mods.txt` — 已加载 mod 列表
- `logs-filtered.txt` — DevMode 过滤后的日志摘录
- `harmony-patches.txt` — Harmony 补丁转储
- `framework-bridge.txt` — 框架快照
- `combat-stats.json` — 当前战斗统计（若在战斗中）
- `game-logs/` — 可选附加的原版日志尾部

报告写入 `user://devmode-reports/`（账号作用域用户数据，与 `mod_data/DevMode/` 同树）。

当 DevMode 检测到未捕获异常或异常退出时，可弹出对话框并**预填崩溃摘要**跳转至此导出流程 — 见下方 **[崩溃恢复](#崩溃恢复)**。

## 崩溃恢复

DevMode 可在严重故障后提示导出反馈 ZIP（不会对每条日志 Error 都弹窗）。

### 局内错误对话框

- 发生 **未捕获 .NET 异常** 时，DevMode 写入崩溃报告并尽量弹出对话框：**查看日志**、**导出反馈 ZIP** 或 **关闭**。
- 导出表单会预填自动摘要（异常类型、消息、堆栈节选、DevMode 版本）。

### 下次启动提示

- 若游戏 **异常退出**（如强杀进程）且上次会话未正常关闭，**主菜单** 在下次启动时提供相同导出流程。
- 会话标记位于 `mod_data/DevMode/instances/{pid}/session.active`；待处理报告位于 `mod_data/DevMode/pending-crash-report.json`。

### 设置

- 开关：**设置 → 崩溃恢复 → 崩溃时提示导出反馈包**（默认开启）。
- 若与进度丢失恢复提示同时满足，优先显示进度保护弹窗。

关注日志前缀 **`[DevMode CrashRecovery]`**。

## 标题画面（DEVMODE）

主菜单 **DEVMODE** 合并原分散 dev 按钮为一个子菜单：

- **New Test** — 快速测试局
- **New Test (Seed)** — 可填种子的测试局
- **Load Save** — 读取 DevMode 快照槽（无槽位时禁用）
- **Normal run: …** — 在非测试局循环 **关闭 / Dev Mode / Cheat Mode**
- **Multiplayer** — 联机开发子菜单（见下）
- **Unlock All Progress** — 解锁时间线纪元、进阶 10、图鉴（需确认）
- **Diagnostics** — **日志** 与 **Mod 反馈**
- **进度保护** — 备份状态、恢复、每条 **详情**
- **Back** — 返回原版主菜单

**Multiplayer** 子菜单：

- **Multiplayer cheat: ON/OFF** — 联机作弊同步 opt-in
- **Pseudo Co-op Test (Host)** — 选角色/种子；可选 SyncBot、幻影玩家（NetId 1001）、AI 队友
- **LAN Multiplayer** — 打开内置联机测试场景

从 **进度保护** 恢复仅限标题画面；尽量让当前 mod 集与备份时一致。

## 进度保护

更换已加载 mod 集时，原版存档过滤可能清掉或归零 mod 角色在 `progress.save` 中的进度。DevMode 会在过滤前自动备份，并帮助恢复。

### 自动备份

- 启动时若 mod 指纹与上次会话不同，DevMode 会在原版过滤运行**之前**复制当前 profile 的 `progress.save`（以及可选的 `prefs.save` / `current_run.save`）。
- 每个 profile 最多保留 **10 份**备份（超出则删最旧）。
- 开关：**设置 → 进度保护 → mod 集变化时自动备份**（默认开启）。

### 启动恢复提示

- 标题画面加载进度后，DevMode 会扫描最近备份，查找当前存档中缺失或降级的 mod 角色进度（例如进阶/胜场被归零，但备份里仍有数据）。
- 若存在可恢复数据，主菜单会弹出 **恢复** / **暂不** 对话框。
- 开关：**设置 → 进度保护 → mod 角色进度丢失时提示恢复**（默认开启）。
- 也可随时从 **DEVMODE → 进度保护** 手动恢复。

### 手动恢复

1. 标题画面 → **DEVMODE → 进度保护**
2. 选择备份 → **恢复**，或先打开 **详情**
3. 确认后，DevMode 会在覆盖前于当前存档目录写入 `progress.save.pre_restore_{timestamp}`
4. 重新进入主菜单或重启游戏，以便从磁盘重新加载进度

### 文件位置

**DevMode 用户数据根目录**（设置、快照、备份等）：

```text
%AppData%\SlayTheSpire2\steam\{SteamId}\mod_data\DevMode\
```

**Profile 备份**（每次备份一个文件夹）：

```text
...\mod_data\DevMode\profile_backups\{yyyyMMdd_HHmmss}_profile{N}\
  progress.save
  backup_meta.json    # 时间戳、mod 指纹、已复制文件列表
  prefs.save          # 可选
  current_run.save    # 可选
```

**游戏当前进度**（路径随 vanilla / modded profile 布局而定）：

```text
...\steam\{SteamId}\profile{N}\saves\progress.save
...\steam\{SteamId}\modded\profile{N}\saves\progress.save   # 使用 modded 存档时
```

macOS / Linux 下 `%AppData%` 对应游戏账号作用域的用户数据目录（Godot `user://steam/{userId}/`）。

### 排查

- 关注日志前缀 **`[ProgressGuard]`**（启动扫描、恢复、弹窗）与 **`[ModChangeGuard]`**（指纹变化、创建备份）。
- 若从源码构建，请用 **`make sync`** 部署，确保游戏加载最新 DLL。

## 联机与共斗测试（开发向）

以下功能均在 DevPanel → **AI 托管** 中**手动开启**。未开启时不影响 vanilla 单人手打，也不改抽牌速度或抽牌动画。

- **AI 托管（单人）** — `SimpleStrategy` 本地代打你的角色。适用于单人自动化。
- **SyncBot** — 单机模拟远程 peer 的 ACK 与默认选项；可选幻影玩家（NetId 1001）。适用于无双开时的主机 co-op 冒烟测试。
- **Pseudo Co-op 预设** — 主机手打 + AI 队友（幻影/离线 peer，走动作队列）。适用于单机主机 + 模拟队友。
- **LAN 主机代打 + 客机 AFK** — 主机手打本机；AI 为真实 ENet 客户端 enqueue 战斗；客机 AFK 拦截本地战斗输入；地图投票镜像。适用于同机双开（启动时自动 preset）。

**LAN 双开（推荐）：** 同机启动主机 + 客机 → 自动应用 preset；主机 log 见 `LAN host preset applied`，客机见 `AFK client enabled`。

架构说明、复测标准与历史 desync 记录：**[docs/lan-host-drive-afk.md](./docs/lan-host-drive-afk.md)** · [文档索引](./docs/README.md)

## MCP

通过 [Model Context Protocol](https://modelcontextprotocol.io) 将 run 状态与操作暴露给任意 MCP 客户端（Claude Desktop、IDE MCP 插件等）。Mod 内 HTTP 桥接默认监听 **9877**；`tools/DevMode.Mcp` 中的 stdio 代理（基于官方 [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)）将 MCP 消息转发到 `http://127.0.0.1:9877/messages`。

**前提：** 执行工具调用时，《杀戮尖塔 2》须已运行且 **DevMode** 已加载（先开游戏，或保持游戏运行后再连接 MCP 客户端）。列出工具名称无需启动游戏。

### 工具

- **`get_game_state`** — 当前 run 快照（生命、金币、牌组、战斗、敌人等）。战斗中含 `playerPowers[]`（`id`、`modelId`、`amount`）、`phase` / `isPlayPhaseActive`、`enemies[].index` 与 `powers[]`
- **`combat_action`** — 出牌、结束回合、使用药水。`play_card` 成功时返回 `afterState`（玩家 power + 敌人 HP），伪联机排队时不含
- **`map_action`** — 地图节点、奖励、事件、商店、休息
- **`dev_get_session`** — run 是否活跃、游戏阶段、Dev 模式标志、阻塞型启动弹窗
- **`dev_list_save_slots`** — 存档列表（含 `debugNotes`，供 AI 选档）
- **`dev_tag_save_slot`** — 为存档设置 `debugNotes`（如 `combat:ironclad-act1-boss`）
- **`dev_load_save_slot`** — 读取 DevMode 存档（异步；需轮询 `dev_get_session`）
- **`dev_start_test_run`** — 从主菜单新开测试 run（打开角色选择；可选 seed）
- **`dev_list_cards`** — 列出 deck / hand / draw / discard / exhaust 牌堆（或全部）
- **`dev_add_card`** — 向牌堆加牌（`card_id`、`target`、`duration`、`upgrade_levels`）
- **`dev_remove_card`** — 按 `card_id` 或 `pile_index` 从牌堆删牌
- **`dev_list_monsters`** — 列出怪物 model ID（供 `dev_add_monster` 使用）
- **`dev_list_enemies`** — 列出当前战斗中的敌人（index、HP、monsterId）
- **`dev_add_monster`** — 战中添加指定怪物（同 DevMode 敌人面板 / `dmenemy spawn`）
- **`dev_set_cheat`** — 切换作弊开关或设置倍率（`freeze_enemies`、`damage_multiplier` 等）
- **`dev_set_stat`** — 设置金币/能量/生命等数值，或启用 stat lock

健康检查：`GET http://127.0.0.1:9877/health`

### Agent 调试流程

一键部署、启动游戏并等待 MCP bridge：

```bash
make dev-session
```

Bridge 就绪后，典型 MCP Agent 流程：

1. **`dev_list_save_slots`** — 按 `debugNotes`、`name`、楼层或角色选档。
2. **`dev_load_save_slot`** 加载所选存档，**或** 无合适存档时 **`dev_start_test_run`**（本 MVP 需手动选角色）。
3. 每 1–2 秒轮询 **`dev_get_session`**，直到 `runActive` 为 `true`（读档异步，最多约 30 秒）。
4. 使用 **`get_game_state`** + **`combat_action`** / **`map_action`** 驱动 run。

调试前可用 **`dev_tag_save_slot`** 标注存档用途，便于 Agent 后续自动选档。建议 `debugNotes` 格式：`combat:ironclad-act1-boss`、`map:shop-test`。

**已知阻塞**

- 启动时的**崩溃恢复**或**进度丢失**弹窗需手动关闭（`dev_get_session` 的 `blockingPrompts` 会提示）。
- `GET /health` 在主线程卡死时仍可能返回 ok；若 MCP 工具调用超时，应停止自动化并排查。
- 修改 mod 代码后需 **`make sync`** 并重启游戏。

**本 MVP 不含：** 自动选角色、卡死看门狗（杀进程/重启/读档）、自动改代码闭环。

### 编译代理

本地开发（DLL；供下方 `dotnet exec` 配置使用）：

```bash
dotnet build tools/DevMode.Mcp/DevMode.Mcp.csproj -c Release
```

自包含可执行文件（仓库 Makefile；默认 RID 为当前系统）：

```bash
make build-tools
```

输出路径：`build/tools/DevMode.Mcp/<rid>/publish/DevMode.Mcp.exe`（Windows）或 `DevMode.Mcp`（macOS/Linux）。

手动交叉编译示例：

```bash
dotnet publish tools/DevMode.Mcp/DevMode.Mcp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish tools/DevMode.Mcp/DevMode.Mcp.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

### 客户端配置

在 MCP 客户端配置的 `mcpServers` 下**新增或更新 `devmode` 条目**（stdio 传输）。这只是众多 MCP 服务器中的一个——**保留你已有的其他条目**，只改 `devmode` 块。配置文件路径因客户端而异，请参阅该客户端的 MCP 文档。

将下方任一配置块粘贴进现有 MCP 客户端配置（与已有 `mcpServers` 条目合并）。默认端口 **9877**（须与 mod 内 `McpConfig.Port` 一致）。若改端口，代理的 `--port` 须与 mod 源码一并修改并重新编译 DevMode。

**跨平台开发**（`dotnet exec`；路径相对于仓库 / 工作区根目录）：

```json
{
  "mcpServers": {
    "devmode": {
      "command": "dotnet",
      "args": [
        "exec",
        "tools/DevMode.Mcp/bin/Release/net8.0/DevMode.Mcp.dll",
        "--",
        "--port",
        "9877"
      ]
    },
    "your-other-mcp-server": {
      "command": "...",
      "args": ["..."]
    }
  }
}
```

需要 **.NET 8** 运行时（`dotnet --list-runtimes` 中应有 `Microsoft.NETCore.App 8.x`）。

**已 publish 的代理**（`make build-tools` 或 `dotnet publish` 后；按实际路径修改 `command`）：

Windows：

```json
{
  "mcpServers": {
    "devmode": {
      "command": "C:/path/to/DevMode.Mcp.exe",
      "args": ["--port", "9877"]
    }
  }
}
```

macOS / Linux：

```json
{
  "mcpServers": {
    "devmode": {
      "command": "/path/to/DevMode.Mcp",
      "args": ["--port", "9877"]
    }
  }
}
```

### HTTP 桥接（手动测试）

游戏已运行且 DevMode 已加载时：

```bash
curl -s http://127.0.0.1:9877/health
```

```bash
curl -s -X POST http://127.0.0.1:9877/messages \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dev_get_session\",\"arguments\":{}}}"
```

## 协作与贡献

协作流程、K&R 代码风格、`dotnet format` / `make format`、Python 与本地化等说明见 **[CONTRIBUTING.md](CONTRIBUTING.md)**，或在 [GitHub](https://github.com/WRXinYue/STS2-DevMode) 提交 Issue / PR。

## 更新日志

版本历史请参阅 [CHANGELOG.zh-CN.md](https://github.com/WRXinYue/STS2-DevMode/blob/main/CHANGELOG.zh-CN.md)。

## 致谢

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## 许可证

[MIT](https://github.com/WRXinYue/STS2-DevMode/blob/main/LICENSE)
