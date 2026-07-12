---
title:
  en: Log export
  zh-CN: 日志导出
top: 9910
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Open the **Log Viewer** from the in-run rail or title screen **Dev Mode → Diagnostics → Logs**, then click **Log Export** in the header to open the slide-out export panel.

Choose which log file to include and export a **ZIP package**. **Privacy mode** replaces user-data paths with `<user-data>` in text files.

Typical ZIP contents:

- `harmony-patches.txt` — Active Harmony patch dump
- `combat-stats.json` — Combat stats
- `godot.log` — Vanilla game log

Exports are written under `user://devmode-reports/` (account-scoped user data, same tree as `mod_data/KitLib/`).
:::

::: zh-CN
从局内侧栏或标题画面 **开发模式 → 诊断 → 日志** 打开 **日志查看器**，点击标题栏 **日志导出** 滑出导出面板。

选择要包含的日志文件，导出 **ZIP 包**。**隐私模式**会把用户数据路径替换成 `<user-data>`。

ZIP 通常包含：

- `harmony-patches.txt` — Harmony 补丁快照
- `combat-stats.json` — 战斗统计
- `godot.log` — 原版游戏日志

导出写在 `user://devmode-reports/`（与 `mod_data/KitLib/` 同账号目录树）。
:::
