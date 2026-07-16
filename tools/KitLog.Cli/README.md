# KitLog CLI (deprecated)

> **Deprecated:** KitLog is replaced by the embedded **Dev Viewer** at `http://127.0.0.1:9878/#/logs` (terminal-style logs in the browser). The in-game log viewer and AI panel open this URL instead of launching `kitlog`.

This project remains buildable for users who still want a terminal tail (`kitlog attach`, `kitlog tail`), but it is no longer deployed with `make deploy-tools` or required by KitLib.

Cross-platform log viewer for KitLib.

Primary path: **structured named pipe** (`KitLib-log-{pid}`) via `kitlog attach`.
Fallback: plain-text `godot.log` tail (`kitlog tail`, legacy regex coloring).

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
kitlog tail -f --tail 40 --filter ai             # legacy godot.log tail
kitlog tail --file "C:/path/to/godot.log" --level warn
```

### attach (recommended)

Connects to the game's per-process pipe and renders logs from structured fields (`mod`, `scope`, `lvl`) — no regex tag parsing.

`--sync-viewer` mirrors in-game log viewer filters from pipe frames (not disk).

If the pipe is unavailable (game not running or older KitLib), `attach` automatically falls back to `godot.log` unless `--no-fallback` is set.

Pipe name: `KitLib-log-{pid}` (also shown by `kitlog list` and `kitlog path`).

### Filters

- `--filter ai` — preset for `[AutoPlay|AiHost|MpAi|LanLocal|Companion]`
- `--filter` also accepts a .NET regex
- `--sync-viewer` — mirror in-game log viewer filters (pipe only; `attach` recommended)

### Log locations (fallback tail)

KitLog scans STS2 user data:

- Windows: `%APPDATA%/SlayTheSpire2/logs/godot.log`
- Linux: `~/.local/share/SlayTheSpire2/...` (and common Flatpak paths)
- macOS: `~/Library/Application Support/SlayTheSpire2/...`

## In-game integration (legacy)

Older KitLib builds launched `kitlog attach` from the log viewer. Current builds open the **Dev Viewer** in the system browser instead.

Dual-instance: use `kitlog attach --pid <pid>` per window. `kitlog list` enumerates running STS2 processes.

## Content mods

Use `KitLog.Info("MyMod", "message")` from `KitLib.dll` at runtime (writes to game log + pipe stream).
