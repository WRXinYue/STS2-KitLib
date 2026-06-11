# KitLog CLI

Cross-platform log viewer for KitLib session logs.

Primary path: **structured named pipe** (`KitLib-log-{pid}`) via `kitlog attach`.
Fallback: plain-text `session.log` tail (`kitlog tail`, legacy regex coloring).

Optional companion to the in-game log viewer — distributed separately from the main mod zip (like `KitLib.Mcp`).

## Build

```bash
make build-kitlog
# or
dotnet publish tools/KitLog.Cli/KitLog.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `build/tools/KitLog.Cli/<rid>/publish/kitlog` (or `kitlog.exe` on Windows).

Package: `make zip-kitlog` → `build/KitLog.Cli-vX.X.X-<rid>.zip`

## Commands

```bash
kitlog list
kitlog path [--pid 12345]
kitlog attach --pid 12345 --follow --sync-viewer --tail 0
kitlog attach --pid 12345 --no-fallback          # pipe only
kitlog tail -f --tail 40 --filter ai --pid 12345 # legacy file tail
kitlog tail --file "C:/path/to/session.log" --level warn
```

### attach (recommended)

Connects to the game's per-process pipe and renders logs from structured fields (`mod`, `scope`, `lvl`) — no regex tag parsing.

If the pipe is unavailable (game not running or older KitLib), `attach` automatically falls back to `session.log` unless `--no-fallback` is set.

Pipe name: `KitLib-log-{pid}` (also shown by `kitlog list` and `kitlog path`).

### Filters

- `--filter ai` — preset for `[AutoPlay|AiHost|MpAi|LanLocal|Companion]`
- `--filter` also accepts a .NET regex
- `--sync-viewer` — mirror in-game log viewer filters from `instances/{pid}/log-viewer-filter.json`

### Log locations (fallback tail)

KitLog scans STS2 user data:

- Windows: `%APPDATA%/SlayTheSpire2/steam/*/mod_data/KitLib/instances/*/session.log`
- Linux: `~/.local/share/SlayTheSpire2/...` (and common Flatpak paths)
- macOS: `~/Library/Application Support/SlayTheSpire2/...`

Falls back to `logs/godot.log` when no instance log exists.

## In-game integration

The in-game log viewer **kitlog** button launches `kitlog attach --follow --sync-viewer --tail 0` so the terminal receives structured live logs (with session replay on connect) and mirrors viewer filters. Filter changes are written to `instances/{pid}/log-viewer-filter.json`.

The AI panel **Open kitlog tail** button uses `--filter ai` instead. Both launch `kitlog` from `PATH` or `mods/KitLib/tools/`.

`session.log` remains a plain-text mirror for grep and offline inspection; it does not contain ANSI codes or JSON frames.

## Content mods

Use `KitLog.Info("MyMod", "message")` from `KitLib.dll` at runtime (writes to game log + session mirror + pipe stream).
