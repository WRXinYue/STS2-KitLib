---
title:
  en: MCP
  zh-CN: MCP
top: 9940
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Connect any [Model Context Protocol](https://modelcontextprotocol.io) client (Claude Desktop, IDE MCP plugins, etc.) to a running STS2 session with **KitLib** loaded (`KitLib.Dev` satellite). KitLib starts an in-game HTTP bridge on port **9877**; the stdio proxy in `tools/KitLib.Mcp` (built with the official [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)) forwards MCP messages to `http://127.0.0.1:9877/messages`.

**Requires:** Slay the Spire 2 running with **KitLib** (Dev module) loaded for tool execution (start the game before or keep it running while the client connects). Tool listing works without the game.
:::

::: zh-CN
任意支持 [Model Context Protocol](https://modelcontextprotocol.io) 的客户端（Claude Desktop、IDE MCP 插件等）可连接到正在运行的 STS2 会话（需加载 **KitLib**，含 `KitLib.Dev` 卫星模块）。KitLib 在游戏内启动 **9877** 端口的 HTTP 桥；`tools/KitLib.Mcp` 里的 stdio 代理（基于官方 [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)）把 MCP 消息转发到 `http://127.0.0.1:9877/messages`。

**前提：** 杀戮尖塔 2 已运行且加载 **KitLib**（Dev 模块），工具才会真正执行（可先开游戏再连客户端，或保持游戏运行）。仅列出工具不需要游戏在跑。
:::

### Tools{lang="en"}

### 工具列表{lang="zh-CN"}

::: en
- **`get_game_state`** — Current run snapshot (HP, gold, deck, combat, enemies, …). In combat: `playerPowers[]` (`id`, `modelId`, `amount`), `phase` / `isPlayPhaseActive`, `enemies[].index` + `powers[]`
- **`combat_action`** — Play a card, end turn, or use a potion. `play_card` success returns `afterState` (player powers + enemy HP) unless pseudo-coop queued
- **`map_action`** — Map node, rewards, events, shop, rest
- **`dev_get_session`** — Run active, game phase, dev-run flag, blocking startup prompts
- **`dev_list_save_slots`** — Save slots with metadata and `debugNotes` for AI selection
- **`dev_tag_save_slot`** — Set `debugNotes` on a slot (e.g. `combat:ironclad-act1-boss`)
- **`dev_load_save_slot`** — Load a DevMode save (async; poll `dev_get_session`)
- **`dev_start_test_run`** — New test run from main menu (opens character select; optional seed)
- **`dev_list_cards`** — List cards in deck / hand / draw / discard / exhaust (or all piles)
- **`dev_add_card`** — Add a card to a pile (`card_id`, `target`, `duration`, `upgrade_levels`)
- **`dev_remove_card`** — Remove a card by `card_id` or `pile_index` from a pile
- **`dev_list_monsters`** — List monster model IDs (for `dev_add_monster`)
- **`dev_list_enemies`** — List enemies currently in combat (index, HP, monsterId)
- **`dev_add_monster`** — Add a monster mid-combat (DevMode enemy panel / `dmenemy spawn`)
- **`dev_set_cheat`** — Toggle cheats or set multipliers (`freeze_enemies`, `damage_multiplier`, …)
- **`dev_set_stat`** — Set gold/energy/HP values or enable stat locks

Health check: `GET http://127.0.0.1:9877/health`
:::

::: zh-CN
- **`get_game_state`** — 当前局快照（生命、金币、牌组、战斗、敌人等）。战斗中含 `playerPowers[]`（`id`、`modelId`、`amount`）、`phase` / `isPlayPhaseActive`、`enemies[].index` + `powers[]`
- **`combat_action`** — 出牌、结束回合或使用药水。`play_card` 成功会返回 `afterState`（玩家能力 + 敌人生命），伪联机排队时除外
- **`map_action`** — 地图节点、奖励、事件、商店、休息
- **`dev_get_session`** — 是否在局、游戏阶段、是否 dev 局、阻塞的启动提示
- **`dev_list_save_slots`** — 存档槽及元数据、`debugNotes`（供 AI 选档）
- **`dev_tag_save_slot`** — 给槽位写 `debugNotes`（如 `combat:ironclad-act1-boss`）
- **`dev_load_save_slot`** — 加载 DevMode 存档（异步；轮询 `dev_get_session`）
- **`dev_start_test_run`** — 主菜单开新测试局（会打开选角色；可选种子）
- **`dev_list_cards`** — 列出牌组 / 手牌 / 抽牌堆 / 弃牌堆 / 消耗堆（或全部堆）
- **`dev_add_card`** — 往某堆加牌（`card_id`、`target`、`duration`、`upgrade_levels`）
- **`dev_remove_card`** — 按 `card_id` 或 `pile_index` 从某堆移除
- **`dev_list_monsters`** — 怪物 model ID 列表（供 `dev_add_monster`）
- **`dev_list_enemies`** — 当前战斗中的敌人（index、HP、monsterId）
- **`dev_add_monster`** — 战斗中加怪（Dev 敌人面板 / `dmenemy spawn`）
- **`dev_set_cheat`** — 开关作弊或设倍率（`freeze_enemies`、`damage_multiplier` 等）
- **`dev_set_stat`** — 设金币/能量/生命或开启数值锁定

健康检查：`GET http://127.0.0.1:9877/health`
:::

### Agent debug loop{lang="en"}

### Agent 调试循环{lang="zh-CN"}

::: en
Bootstrap a session (deploy mod, launch game, wait for bridge):

```bash
make dev-session
```

Typical MCP agent flow after the bridge is ready:

1. **`dev_list_save_slots`** — Pick a slot by `debugNotes`, `name`, floor, or character.
2. **`dev_load_save_slot`** — Load the chosen slot, **or** **`dev_start_test_run`** when no suitable save exists (character select is manual in this MVP).
3. Poll **`dev_get_session`** every 1–2s until `runActive` is `true` (load is async; allow up to ~30s).
4. **`get_game_state`** + **`combat_action`** / **`map_action`** to drive the run.

Before a debugging session, tag saves with **`dev_tag_save_slot`** so agents can pick the right snapshot later. Suggested `debugNotes` format: `combat:ironclad-act1-boss`, `map:shop-test`.

**Known blockers**

- Startup **progress loss** prompts must be dismissed manually (`blockingPrompts` in `dev_get_session`).
- `GET /health` can respond while the Godot main thread is stuck; if MCP tool calls time out, stop and investigate.
- After changing mod code, run **`make sync`** and restart the game.

**Not in this MVP:** auto character select, hang watchdog (kill/restart/reload), or autonomous code fixes.
:::

::: zh-CN
启动会话（部署 mod、开游戏、等桥就绪）：

```bash
make dev-session
```

桥就绪后典型的 MCP agent 流程：

1. **`dev_list_save_slots`** — 按 `debugNotes`、`name`、层数或角色选槽。
2. **`dev_load_save_slot`** — 加载所选槽，**或** 没有合适存档时用 **`dev_start_test_run`**（本 MVP 选角色仍需手动）。
3. 每 1–2 秒轮询 **`dev_get_session`**，直到 `runActive` 为 `true`（加载异步，最多约 30 秒）。
4. **`get_game_state`** + **`combat_action`** / **`map_action`** 驱动对局。

调试前可用 **`dev_tag_save_slot`** 给存档打标签，方便 agent 以后选对快照。建议 `debugNotes` 格式：`combat:ironclad-act1-boss`、`map:shop-test`。

**已知阻塞**

- 启动时的 **进度丢失** 提示需手动关掉（看 `dev_get_session` 的 `blockingPrompts`）。
- `GET /health` 可能在 Godot 主线程卡死时仍返回；MCP 工具超时则应停手排查。
- 改 mod 代码后执行 **`make sync`** 并重启游戏。

**本 MVP 尚未包含：** 自动选角色、挂死看门狗（杀进程/重启/重载）、或自主改代码。
:::

### Build proxy{lang="en"}

### 构建代理{lang="zh-CN"}

::: en
Local dev (DLL; used by `dotnet exec` config below):

```bash
dotnet build tools/KitLib.Mcp/KitLib.Mcp.csproj -c Release
```

Self-contained executable (repo Makefile; default RID is your host OS):

```bash
make build-tools
```

Output: `build/tools/KitLib.Mcp/<rid>/publish/KitLib.Mcp.exe` (Windows) or `KitLib.Mcp` (macOS/Linux).

Cross-compile manually, for example:

```bash
dotnet publish tools/KitLib.Mcp/KitLib.Mcp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish tools/KitLib.Mcp/KitLib.Mcp.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```
:::

::: zh-CN
本地开发（DLL；下面 `dotnet exec` 配置会用到）：

```bash
dotnet build tools/KitLib.Mcp/KitLib.Mcp.csproj -c Release
```

自包含可执行文件（仓库 Makefile；默认 RID 为当前系统）：

```bash
make build-tools
```

输出：`build/tools/KitLib.Mcp/<rid>/publish/KitLib.Mcp.exe`（Windows）或 `KitLib.Mcp`（macOS/Linux）。

手动交叉编译示例：

```bash
dotnet publish tools/KitLib.Mcp/KitLib.Mcp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish tools/KitLib.Mcp/KitLib.Mcp.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```
:::

### Client configuration{lang="en"}

### 客户端配置{lang="zh-CN"}

::: en
Add a **`devmode`** entry under `mcpServers` in your MCP client config (stdio transport). This is **one server among many** — keep your existing entries and only add or update the `devmode` block. Exact config file path depends on the client; see its MCP documentation.

Paste one of the blocks below into your existing MCP client config (merge with your other `mcpServers` entries). Default port is **9877** (must match `McpConfig.Port` in the mod). Override with `--port` on the proxy only if you also change the mod source and rebuild KitLib.

**Cross-platform development** (`dotnet exec`; paths are relative to the repo / workspace root):

```json
{
  "mcpServers": {
    "devmode": {
      "command": "dotnet",
      "args": [
        "exec",
        "tools/KitLib.Mcp/bin/Release/net8.0/KitLib.Mcp.dll",
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

Requires **.NET 8** runtime (`dotnet --list-runtimes` should include `Microsoft.NETCore.App 8.x`).

**Published proxy** (after `make build-tools` or `dotnet publish`; adjust the path):

Windows:

```json
{
  "mcpServers": {
    "devmode": {
      "command": "C:/path/to/KitLib.Mcp.exe",
      "args": ["--port", "9877"]
    }
  }
}
```

macOS / Linux:

```json
{
  "mcpServers": {
    "devmode": {
      "command": "/path/to/KitLib.Mcp",
      "args": ["--port", "9877"]
    }
  }
}
```
:::

::: zh-CN
在 MCP 客户端配置的 `mcpServers` 下添加 **`devmode`** 条目（stdio 传输）。这是**众多服务器之一**——保留现有条目，只新增或更新 `devmode` 块。配置文件路径因客户端而异，见其 MCP 文档。

把下面某一块粘贴进现有配置（与其他 `mcpServers` 合并）。默认端口 **9877**（须与 mod 里 `McpConfig.Port` 一致）。只有同时改 mod 源码并重编 KitLib 时，才在代理上用 `--port` 覆盖。

**跨平台开发**（`dotnet exec`；路径相对仓库根目录）：

```json
{
  "mcpServers": {
    "devmode": {
      "command": "dotnet",
      "args": [
        "exec",
        "tools/KitLib.Mcp/bin/Release/net8.0/KitLib.Mcp.dll",
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

需要 **.NET 8** 运行时（`dotnet --list-runtimes` 应包含 `Microsoft.NETCore.App 8.x`）。

**已发布代理**（`make build-tools` 或 `dotnet publish` 之后；按实际路径改）：

Windows：

```json
{
  "mcpServers": {
    "devmode": {
      "command": "C:/path/to/KitLib.Mcp.exe",
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
      "command": "/path/to/KitLib.Mcp",
      "args": ["--port", "9877"]
    }
  }
}
```
:::

### HTTP bridge (manual test){lang="en"}

### HTTP 桥（手动测试）{lang="zh-CN"}

::: en
With the game running and KitLib loaded:

```bash
curl -s http://127.0.0.1:9877/health
```

```bash
curl -s -X POST http://127.0.0.1:9877/messages \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dev_get_session\",\"arguments\":{}}}"
```
:::

::: zh-CN
游戏运行且 KitLib 已加载时：

```bash
curl -s http://127.0.0.1:9877/health
```

```bash
curl -s -X POST http://127.0.0.1:9877/messages \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dev_get_session\",\"arguments\":{}}}"
```
:::
