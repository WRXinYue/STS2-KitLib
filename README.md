# KitLib

**English** | [中文](./README.zh-CN.md)

All-in-one in-game toolkit for Slay the Spire 2 — test builds, cheat, script, and debug mods without leaving the game.

![KitLib](https://raw.githubusercontent.com/WRXinYue/STS2-DevMode/main/assets/devmode.png)

## Getting started

- **During a run** — Hover the left-edge **peek tab** to expand the dev rail, then click a panel icon. Browser panels slide in from the left; combat overlays use the game’s right edge or floating windows.
- **Title screen** — Click **KITLIB** for test runs, snapshots, diagnostics, progress protection, and multiplayer dev tools (no run required).
- **Settings → Sidebar** — Drag to reorder rail tabs and hide panels you do not need. **Harmony analysis**, **Scripts**, and **Frameworks** start hidden; enable them here when needed.
- **Settings → Game** — **In-game right sidebar** (combat shortcuts + stats rail), game speed, skip animations, overlay toggles.
- **Normal runs** — From title **KITLIB**, cycle **Normal run: Disabled / Toolkit / Cheat Mode** to keep the rail available outside test runs.

Install from [Releases](https://github.com/WRXinYue/STS2-DevMode/releases) or build from source (`python scripts/init.py`, then `make sync-full`). Steam **beta** builds need the matching beta mod package.

### Install layout (0.13+)

KitLib installs as **one game mod**. Satellite modules are DLLs under `mods/KitLib/modules/`; Core hot-loads them at startup (missing or conflicting modules are skipped).

```text
mods/KitLib/
  mod_manifest.json
  KitLib.dll
  KitLib.Abstractions.dll
  modules/
    KitLib.User.dll
    KitLib.ModPanel.dll
    KitLib.Panel.dll
    KitLib.Cheat.dll
    KitLib.Dev.dll
    KitLib.AI.dll
```

| Module DLL | Role |
|------------|------|
| `KitLib.User` | Logs, progress guard, manual, crash recovery |
| `KitLib.ModPanel` | Main-menu **Mods** settings panel + RitsuLib bridge |
| `KitLib.Panel` | Dev rail + title-screen DEVMODE entry |
| `KitLib.Cheat` | Cheat tab registration + runtime hooks |
| `KitLib.Dev` | Hooks, scripts, Harmony/MCP tools |
| `KitLib.AI` | AI Host, autoplay, companions |

**Packages**

- **KitLib** or **KitLib-Full** zip — extract the single `KitLib/` folder into `mods/`.
- **Base modules** — keep `KitLib.User.dll` and `KitLib.ModPanel.dll` for logs and mod settings.
- **Optional modules** — delete other DLLs under `modules/` to disable features (e.g. remove `KitLib.Panel.dll` for no dev rail; `KitLib.AI.dll` disables AI host).
- **Content-mod authors** — NuGet `STS2.KitLib.Abstractions`; runtime needs `KitLib.dll` + any satellite DLLs you depend on under `modules/`.

Build and deploy: `make sync-full`. Package zips: `make zip-full`.

## Panels

### Gameplay & content

- **Cheats** — God mode, infinite energy/block/stars, damage multipliers, enemy freeze, stat locks, map overrides (free travel while map is open), reward tweaks; some options limited in **multiplayer**
- **Cards** — Full card library; filter by type/rarity/cost/pool/character/**mod source**; **show hidden cards**; right-click a filter chip to **exclude**; edit stats and enchantments; add to any pile; upgrade preview; filters persist across sessions
- **Relics** — Browse and add relics; **mod source** filter
- **Powers** — Apply powers (self, all enemies, specific, allies); one-click Auto-Apply hooks; **mod source** filter
- **Potions** — Visual grid; one-click Auto-Apply hooks; **mod source** filter
- **Enemies** — Replace encounters by room or map node; preview content; idle animation preview; edit per-turn enemy intents
- **Events** — Browse and trigger event flows; **mod source** filter
- **Rooms** — Inspect and jump between room types; teleport to ancient shop locations
- **Presets** — Save/load combat and run snapshots (hand, deck, relics, etc.)

### Automation & AI

- **Hooks** — Trigger → Condition → Action rules (e.g. add a card on combat start, apply a power on draw)
- **Scripts** — SpireScratch visual scripting (Blockly); live reload via WebSocket
- **AI Host** — Rule-based bot for **solo** runs (map, combat, rewards). Default **StrongStrategy** (DeckPlan + combat search); set `AutoPlayStrategy: Simple` in settings for legacy heuristics. Disabled during multiplayer hand-play to avoid desync; use Pseudo Co-op / LAN presets instead (see below)
- **MCP** — Expose game state and actions to MCP clients while the game is running — see **[MCP](#mcp)**
- **KitLog CLI** — Optional cross-platform `kitlog` tail for session logs — see **[KitLog CLI](#kitlog-cli)**

### Developer & debug

- **Enemy intents** — **Enemy intents** rail tab: next-turn preview; optional **draggable overlay** during fights (off by default); intent badges on the combat sidebar stack when an enemy has multiple intents
- **Combat stats** — Live damage/block/heal breakdown by card, source, and turn; pie chart sidebar; run totals; JSON export; `dmstats` console command; slim **right-rail bars** in solo; **draggable top-right MP overlay** in co-op
- **Console** — Searchable reference for native and DevMode commands
- **Logs** — See **[Logs](#logs)** below
- **Harmony analysis** — Inspect active patches; filter by owner; smart summary
- **Frameworks** — Loaded mod framework snapshot
- **Mod feedback** — See **[Mod feedback](#mod-feedback)** below

### Utility

- **Save / Load** — Named DevMode snapshot slots (separate from vanilla `progress.save`); carry cards/relics/gold into a new seed; slot detail view
- **Manual** — In-game documentation browser (one page per tool)
- **Settings** — Theme (Dark / OLED / Light / Warm), game speed, skip animations, rail layout, combat overlays, **progress protection** and **crash recovery** toggles

## In-combat overlays

These are optional and mostly **off by default** — turn them on under **Settings → Game** or their panel.

- **In-game right sidebar** — Live contribution bars, enemy intent preview rail, compact combat tools (add encounter/monster, kill enemies). Default: **Off**
- **Enemy intent overlay** — Draggable float with next-turn intents. Default: **Off**
- **Multiplayer combat stats overlay** — Draggable top-right score bars per player in co-op. Default: **On**

During fights, intent badges on the right sidebar stack vertically when an enemy has multiple intents. Opening the full **Combat stats** panel can merge flush with the right rail when the browser is nearly full width.

**Multiplayer cheat sync** — When hosting with **Multiplayer cheat** enabled (title **DEVMODE → Multiplayer**), cheats, card/relic/potion edits, combat enemy tools, powers, and per-player cheat flags can sync across clients (all peers need DevMode).

## Logs

Open from the in-run **Logs** rail tab or title screen **DEVMODE → Diagnostics → Logs**.

- **Live + file history** — Streams new log lines and hydrates earlier lines from the session log (`mod_data/KitLib/instances/{pid}/session.log`, with fallback to Godot `user://logs/`).
- **Filters** — Level chips (All / ≥ Info / ≥ Warn / Error), text search, per-mod source toggles, and toggleable **noise suppression** rules (known benign patterns with hit counts).
- **Presentation** — Mod vs game source coloring; session boundary markers between DevMode restarts.
- **Stats sidebar** — Entry counts by level and mod; **source pie chart**.
- **Copy all** — Copy the currently filtered log text to the clipboard.
- **Alerts** — The **Logs** rail icon blinks on unseen Warn/Error until you open the viewer. The peek tab blinks until your first rail hover (then stays dismissed).

## KitLog CLI

Optional standalone tool for tailing KitLib session logs outside the game (like `KitLib.Mcp`, not bundled in the main mod zip).

```bash
make build-kitlog    # build/tools/KitLog.Cli/<rid>/publish/kitlog
make zip-kitlog      # build/KitLog.Cli-vX.X.X-<rid>.zip
```

```bash
kitlog list
kitlog path --pid <game-pid>
kitlog tail -f --filter ai --pid <game-pid>
```

See **[tools/KitLog.Cli/README.md](tools/KitLog.Cli/README.md)** for install paths and filters. Content mods can log via `KitLog.Info("MyMod", "…")` on `KitLib.dll`.

## Mod feedback

Open from the in-run rail or title screen **DEVMODE → Diagnostics → Mod Feedback**.

Fill in a title and description, optionally attach a game log tail, and export a **ZIP report** for mod authors. **Privacy mode** replaces user-data paths with `<user-data>` in all text files.

Typical ZIP contents:

- `report.txt` — Your description and environment summary
- `mods.txt` — Loaded mod list
- `logs-filtered.txt` — DevMode-filtered log excerpt
- `harmony-patches.txt` — Active Harmony patch dump
- `framework-bridge.txt` — Framework snapshot
- `combat-stats.json` — Current combat stats export (if in a fight)
- `game-logs/` — Optional attached vanilla log tail

Reports are written under `user://devmode-reports/` (account-scoped user data, same tree as `mod_data/KitLib/`).

When DevMode detects an unhandled error or an abnormal exit, it can open a dialog that links here with a **prefilled crash summary** — see **[Crash recovery](#crash-recovery)** below.

## Crash recovery

DevMode can prompt you to export a feedback ZIP after serious failures (without spamming a popup on every log line).

### In-game error dialog

- On an **unhandled .NET exception**, DevMode writes a crash report and tries to show a dialog: **View logs**, **Export feedback ZIP**, or **Close**.
- The export form is prefilled with an automatic summary (exception type, message, stack excerpt, DevMode version).

### Next-launch prompt

- If the game **exits abnormally** (e.g. kill process) and the previous session did not shut down cleanly, the **main menu** offers the same export flow on next startup.
- Session markers live under `mod_data/KitLib/instances/{pid}/session.active`; pending reports under `mod_data/KitLib/pending-crash-report.json`.

### Settings

- Toggle: **Settings → Crash recovery → Prompt to export feedback on crash** (on by default).
- Progress-loss restore prompts take priority if both would show on startup.

Look for log lines prefixed **`[DevMode CrashRecovery]`**.

## Title screen (DEVMODE)

On the main menu, **DEVMODE** replaces separate dev buttons with one submenu:

- **New Test** — Start a quick test run
- **New Test (Seed)** — Test run with an optional seed
- **Load Save** — Load a DevMode snapshot slot (disabled when no slots exist)
- **Normal run: …** — Cycle **Disabled** → **Dev Mode** → **Cheat Mode** for non-test runs
- **Multiplayer** — Multiplayer dev submenu (see below)
- **Unlock All Progress** — Unlock timeline epochs, Ascension 10, and compendium entries (confirmation required)
- **Diagnostics** — **Logs** and **Mod feedback**
- **Progress protection** — Backup status, restore, per-backup **Details**
- **Back** — Return to the stock main menu

**Multiplayer** submenu:

- **Multiplayer cheat: ON/OFF** — Opt in to synced multiplayer cheat sessions
- **Pseudo Co-op Test (Host)** — Host with character/seed pickers; optional SyncBot, phantom player (NetId 1001), AI teammate
- **LAN Multiplayer** — Open the built-in multiplayer test scene

Restore from **Progress protection** is title-screen only. Prefer matching the backup’s mod set when possible.

## Progress protection

Changing the loaded mod set can cause vanilla save filtering to strip or zero mod character stats in `progress.save`. DevMode backs up and helps you recover that progress.

### Automatic backup

- On startup, when the loaded mod fingerprint differs from the last session, DevMode copies the active profile’s `progress.save` (and optional `prefs.save` / `current_run.save`) **before** vanilla filtering runs.
- Keeps up to **10 backups per profile** (oldest removed).
- Toggle: **Settings → Progress protection → Auto-backup on mod set change** (on by default).

### Startup restore prompt

- After progress loads on the title screen, DevMode scans recent backups for mod character stats that are missing or degraded in the current save (e.g. Ascension / wins reset to zero while a backup still has progress).
- If recoverable data exists, a **Restore** / **Not now** dialog appears on the main menu.
- Toggle: **Settings → Progress protection → Prompt on mod character progress loss** (on by default).
- You can also restore anytime from **DEVMODE → Progress protection**.

### Manual restore

1. Title screen → **DEVMODE → Progress protection**
2. Choose a backup → **Restore**, or open **Details** first
3. Confirm; DevMode writes a `progress.save.pre_restore_{timestamp}` next to the active save before overwriting
4. Reload the main menu or restart the game so progress reloads from disk

### File locations

**DevMode user data root** (settings, snapshots, backups):

```text
%AppData%\SlayTheSpire2\steam\{SteamId}\mod_data\DevMode\
```

**Profile backups** (one folder per backup):

```text
...\mod_data\DevMode\profile_backups\{yyyyMMdd_HHmmss}_profile{N}\
  progress.save
  backup_meta.json    # timestamp, mod fingerprint, copied files
  prefs.save          # optional
  current_run.save    # optional
```

**Active game progress** (path depends on vanilla vs modded profile layout):

```text
...\steam\{SteamId}\profile{N}\saves\progress.save
...\steam\{SteamId}\modded\profile{N}\saves\progress.save   # when using modded saves
```

On macOS/Linux, `%AppData%` is the game’s account-scoped user data directory (see Godot `user://steam/{userId}/`).

### Troubleshooting

- Look for log lines prefixed **`[ProgressGuard]`** (startup scan, restore, prompts) or **`[ModChangeGuard]`** (fingerprint change, backup creation).
- If you build from source, deploy with **`make sync`** so the game loads the latest DLL.

## Multiplayer & co-op testing (dev)

These features are **opt-in** from DevPanel → **AI Host**. They do not change vanilla solo hand-play or draw speed unless you enable AI / cheats yourself.

- **AI Host (solo)** — `StrongStrategy` (default) drives your character locally: DeckPlan macro scoring, shallow combat search, lethal checks. Set `AutoPlayStrategy` to `Simple` for legacy heuristics. Use for single-player automation.
- **SyncBot** — Simulates remote peer ACKs and default choices on one machine; optional phantom player (NetId 1001). Use for host-only co-op smoke tests without a second client.
- **Pseudo Co-op preset** — Hand-play host + AI teammate for phantom/offline peers via action queue. Use for solo host with simulated teammate.
- **LAN host-drive + AFK** — Host hand-plays local player; AI enqueues combat for connected ENet client; client AFK blocks local combat input; map votes mirrored. Use for two game instances on one PC (auto preset on dual launch).

**Dual-instance LAN (recommended):** launch host + client on the same machine → presets apply automatically; host logs `LAN host preset applied`, client logs `AFK client enabled`.

Detailed architecture, verification checklist, and desync history: **[docs/lan-host-drive-afk.md](./docs/lan-host-drive-afk.md)** · [docs index](./docs/README.md)

## Mod AI integration

DevMode exposes a **soft-dependency** AI platform for content mods. DevMode owns the loop, snapshot capture, action execution, and vanilla combat scoring; your mod bridge supplies character semantics (snapshot extensions, strategy rules, score tweaks).

**Requires:** DevMode loaded at runtime. Reference `KitLib.dll` at compile time only (do not bundle it in your mod).

### Registration (mod init)

Call from your mod’s `[ModInitializer]` after DevMode is available:

```csharp
using KitLib.AI.Core;
using KitLib.Companion;

CompanionBridge.RegisterCharacterStrategy(
    "YOUR_CHARACTER_MODEL_ID",
    myStrategy,
    new CharacterAiProfile(SupportsNonCombat: true));

CompanionBridge.RegisterSnapshotContributor(mySnapshotContributor);
CompanionBridge.RegisterMoveModifier(myMoveModifier);

// Optional per-spawn override (e.g. custom companion summon):
CompanionBridge.RegisterStrategy(netId, overrideStrategy);
```

**Strategy resolution order:** per-`netId` registry → `CharacterAiRegistry` by character model id → `StrongStrategy` fallback (`Simple` if `AutoPlayStrategy=Simple`).

### DeckPlan and character packs

`DeckPlanInferer` builds a weight vector (thin/thick, attack, block, exhaust, scaling, …) from deck + relics + ascension. Vanilla characters register `IDeckPlanContributor` packs under `src/KitLib.Modules.AI/AI/Characters/Vanilla/`.

Mods can adjust deck planning and card tags:

```csharp
CompanionBridge.RegisterDeckPlanContributor(myDeckPlanContributor);
CompanionBridge.RegisterCardTagProvider(myCardTagProvider);
```

`ICardTagProvider` merges extra tags into `CardCatalog` for macro scoring. `IDeckPlanContributor.AdjustPlan` mutates `DeckPlan.Builder` before each macro decision.

A10 regression seeds: `tools/ai-bench/` (fixed seed list + log parser). Algorithm details: **[docs/ai-algorithm.md](./docs/ai-algorithm.md)**.

### Snapshot extensions

`GameSnapshot` writes mod data under `snapshot["extensions"][yourKey]`. Implement `IAiSnapshotContributor`:

```csharp
public interface IAiSnapshotContributor {
    string ExtensionKey { get; }  // e.g. "lusttravel2", "winefox"
    void Enrich(JsonObject snapshot, Player player, GamePhase phase);
}
```

Strategies **must** read `extensions.*`; DevMode does not hard-code mod power types.

### Combat scoring

`CombatScorer.PickBestCombatMove(snapshot)` scores play-card and end-turn moves using vanilla heuristics (threat vs block, lethal, energy efficiency, target selection). Mods adjust scores via `IAiMoveModifier`:

```csharp
public interface IAiMoveModifier {
    bool AppliesTo(string? characterId);
    int ModifyScore(JsonObject snapshot, GameAction move, int baseScore);
}
```

Implement `IDecisionMaker` for full control, or delegate non-combat phases to `SimpleStrategy` and use `CombatScorer` inside combat.

### Companion full pipeline

By default, pseudo-coop companions only run AI during **combat**. For map/events/rewards/rest/shop, set `EnableNonCombatAi: true` on `CompanionSpawnRequest`:

```csharp
CompanionBridge.TrySummon(new CompanionSpawnRequest(
    character,
    EnableNonCombatAi: true,
    MirrorMapVotes: true));
```

`CompanionDecisionHost` runs `GameLoop` for registered companions in overlay phases when `CharacterAiProfile.SupportsNonCombat` is true. Map votes still mirror the host by default (`MirrorMapVotes`).

### Reference bridges

| Mod | Bridge project | Registers |
|-----|----------------|-----------|
| LustTravel2 (FoxHime) | `LustTravel2.DevModeBridge` | stamina snapshot, FoxHime strategy + move modifier |
| WineFox (CombatMaid) | `STS2_CombatMaid` | craft/stress snapshot, WineFox strategy + move modifier |

Build a bridge DLL against a fresh `KitLib.dll` (`build/KitLib/KitLib.dll` after `dotnet build`). Ship the bridge as a separate mod with `"dependencies": ["YourContentMod"]` (DevMode is runtime-only).

## MCP

Connect any [Model Context Protocol](https://modelcontextprotocol.io) client (Claude Desktop, IDE MCP plugins, etc.) to a running STS2 session with DevMode loaded. DevMode starts an in-game HTTP bridge on port **9877**; the stdio proxy in `tools/KitLib.Mcp` (built with the official [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)) forwards MCP messages to `http://127.0.0.1:9877/messages`.

**Requires:** Slay the Spire 2 running with **DevMode** loaded for tool execution (start the game before or keep it running while the client connects). Tool listing works without the game.

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

- Startup **crash recovery** or **progress loss** prompts must be dismissed manually (`blockingPrompts` in `dev_get_session`).
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

Paste one of the blocks below into your existing MCP client config (merge with your other `mcpServers` entries). Default port is **9877** (must match `McpConfig.Port` in the mod). Override with `--port` on the proxy only if you also change the mod source and rebuild DevMode.

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

With the game running and DevMode loaded:

```bash
curl -s http://127.0.0.1:9877/health
```

```bash
curl -s -X POST http://127.0.0.1:9877/messages \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dev_get_session\",\"arguments\":{}}}"
```

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for collaboration norms, K&R brace style, formatting commands, and localization, or open an issue / PR on [GitHub](https://github.com/WRXinYue/STS2-DevMode).

## Changelog

See [CHANGELOG.md](https://github.com/WRXinYue/STS2-DevMode/blob/main/CHANGELOG.md) for version history.

## Acknowledgments

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## License

[MIT](https://github.com/WRXinYue/STS2-DevMode/blob/main/LICENSE)
