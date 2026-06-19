---
title:
  en: Progress protection
  zh-CN: 进度保护
top: 9930
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Changing the loaded mod set can cause vanilla save filtering to strip or zero mod character stats in `progress.save`. KitLib backs up and helps you recover that progress.

### Automatic backup

- On startup, when the loaded mod fingerprint differs from the last session, KitLib copies the active profile’s `progress.save` (and optional `prefs.save` / `current_run.save`) **before** vanilla filtering runs.
- Keeps up to **10 backups per profile** (oldest removed).
- Toggle: **Settings → Progress protection → Auto-backup on mod set change** (on by default).

### Startup restore prompt

- After progress loads on the title screen, KitLib scans recent backups for mod character stats that are missing or degraded in the current save (e.g. Ascension / wins reset to zero while a backup still has progress).
- If recoverable data exists, a **Restore** / **Not now** dialog appears on the main menu.
- Toggle: **Settings → Progress protection → Prompt on mod character progress loss** (on by default).
- You can also restore anytime from **Dev Mode → Progress protection** or **Mods → KitLib → Progress protection**.

### Manual restore

1. Title screen → **Dev Mode → Progress protection**
2. Choose a backup → **Restore**, or open **Details** first
3. Confirm; KitLib writes a `progress.save.pre_restore_{timestamp}` next to the active save before overwriting
4. Reload the main menu or restart the game so progress reloads from disk

### File locations

**KitLib user data root** (settings, snapshots, backups). Legacy `mod_data/DevMode` migrates here on first launch:

```text
%AppData%\SlayTheSpire2\steam\{SteamId}\mod_data\KitLib\
```

**Profile backups** (one folder per backup):

```text
...\mod_data\KitLib\profile_backups\{yyyyMMdd_HHmmss}_profile{N}\
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
:::

::: zh-CN
（维护者向长文；用户向说明见 README.zh-CN.md 与 [文档站](/guide/panels/)。）
:::
