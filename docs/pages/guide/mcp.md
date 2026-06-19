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

### Tools

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

### Agent debug loop

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

### Build proxy

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

### Client configuration

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

### HTTP bridge (manual test)

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
（维护者向长文；用户向说明见 README.zh-CN.md 与 [文档站](/guide/panels/)。）
:::
