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
:::

::: zh-CN
更换已加载的 mod 组合时，原版存档过滤可能把 mod 角色进度从 `progress.save` 里清掉或归零。KitLib 会在过滤前备份，并帮你恢复。
:::

### Automatic backup{lang="en"}

### 自动备份{lang="zh-CN"}

::: en
- On startup, when the loaded mod fingerprint differs from the last session, KitLib copies the active profile’s `progress.save` (and optional `prefs.save` / `current_run.save`) **before** vanilla filtering runs.
- Keeps up to **10 backups per profile** (oldest removed).
- Toggle: **Settings → Progress protection → Auto-backup on mod set change** (on by default).
:::

::: zh-CN
- 启动时若 mod 指纹与上次不同，KitLib 会在原版过滤运行**之前**复制当前档案的 `progress.save`（以及可选的 `prefs.save` / `current_run.save`）。
- 每个档案最多保留 **10 份**备份（最旧的会被删掉）。
- 开关：**设置 → 进度保护 → mod 组合变更时自动备份**（默认开启）。
:::

### Startup restore prompt{lang="en"}

### 启动恢复提示{lang="zh-CN"}

::: en
- After progress loads on the title screen, KitLib scans recent backups for mod character stats that are missing or degraded in the current save (e.g. Ascension / wins reset to zero while a backup still has progress).
- If recoverable data exists, a **Restore** / **Not now** dialog appears on the main menu.
- Toggle: **Settings → Progress protection → Prompt on mod character progress loss** (on by default).
- You can also restore anytime from **Dev Mode → Progress protection** or **Mods → KitLib → Progress protection**.
:::

::: zh-CN
- 标题画面加载进度后，KitLib 会扫描近期备份，查找当前存档里缺失或退化的 mod 角色数据（例如进阶/胜场被清零，但备份里还有进度）。
- 若可恢复，主菜单会弹出 **恢复** / **暂不** 对话框。
- 开关：**设置 → 进度保护 → mod 角色进度丢失时提示**（默认开启）。
- 也可随时从 **开发模式 → 进度保护** 或 **Mods → KitLib → 进度保护** 手动恢复。
:::

### Manual restore{lang="en"}

### 手动恢复{lang="zh-CN"}

::: en
1. Title screen → **Dev Mode → Progress protection**
2. Choose a backup → **Restore**, or open **Details** first
3. Confirm; KitLib writes a `progress.save.pre_restore_{timestamp}` next to the active save before overwriting
4. Reload the main menu or restart the game so progress reloads from disk
:::

::: zh-CN
1. 标题画面 → **开发模式 → 进度保护**
2. 选一条备份 → **恢复**，或先打开 **详情**
3. 确认后，KitLib 会在覆盖前于当前存档旁写入 `progress.save.pre_restore_{timestamp}`
4. 重新加载主菜单或重启游戏，让进度从磁盘重新读入
:::

### File locations{lang="en"}

### 文件位置{lang="zh-CN"}

::: en
**KitLib user data root** (settings, snapshots, backups):

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
:::

::: zh-CN
**KitLib 用户数据根目录**（设置、快照、备份）：

```text
%AppData%\SlayTheSpire2\steam\{SteamId}\mod_data\KitLib\
```

**档案备份**（每条备份一个文件夹）：

```text
...\mod_data\KitLib\profile_backups\{yyyyMMdd_HHmmss}_profile{N}\
  progress.save
  backup_meta.json    # 时间戳、mod 指纹、已复制文件
  prefs.save          # 可选
  current_run.save    # 可选
```

**当前游戏进度**（路径取决于原版或 mod 档案布局）：

```text
...\steam\{SteamId}\profile{N}\saves\progress.save
...\steam\{SteamId}\modded\profile{N}\saves\progress.save   # 使用 mod 存档时
```

macOS/Linux 上，`%AppData%` 指游戏账号作用域的用户数据目录（见 Godot `user://steam/{userId}/`）。
:::

### Troubleshooting{lang="en"}

### 排错{lang="zh-CN"}

::: en
- Look for log lines prefixed **`[ProgressGuard]`** (startup scan, restore, prompts) or **`[ModChangeGuard]`** (fingerprint change, backup creation).
- If you build from source, deploy with **`make sync`** so the game loads the latest DLL.
:::

::: zh-CN
- 留意日志前缀 **`[ProgressGuard]`**（启动扫描、恢复、提示）或 **`[ModChangeGuard]`**（指纹变更、创建备份）。
- 若从源码构建，用 **`make sync`** 部署，确保游戏加载的是最新 DLL。
:::
