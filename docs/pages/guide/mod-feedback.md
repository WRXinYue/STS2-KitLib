---
title:
  en: Mod feedback
  zh-CN: Mod 反馈
top: 9910
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Open from the in-run rail or title screen **Dev Mode → Diagnostics → Mod Feedback**.

Fill in a title and description, optionally attach a game log tail, and export a **ZIP report** for mod authors. **Privacy mode** replaces user-data paths with `<user-data>` in all text files.

Typical ZIP contents:

- `report.txt` — Your description and environment summary
- `mods.txt` — Loaded mod list
- `logs-filtered.txt` — KitLib-filtered log excerpt
- `harmony-patches.txt` — Active Harmony patch dump
- `framework-bridge.txt` — Framework snapshot
- `combat-stats.json` — Current combat stats export (if in a fight)
- `game-logs/` — Optional attached vanilla log tail

Reports are written under `user://devmode-reports/` (account-scoped user data, same tree as `mod_data/KitLib/`).
:::

::: zh-CN
从局内侧栏或标题画面 **开发模式 → 诊断 → Mod 反馈** 打开。

填写标题和说明，可选附带游戏日志尾部，导出给 Mod 作者的 **ZIP 报告包**。**隐私模式**会把用户数据路径替换成 `<user-data>`。

ZIP 通常包含：

- `report.txt` — 你的描述和环境摘要
- `mods.txt` — 已加载 mod 列表
- `logs-filtered.txt` — KitLib 过滤后的日志摘录
- `harmony-patches.txt` — Harmony 补丁快照
- `framework-bridge.txt` — 框架桥接信息
- `combat-stats.json` — 当前战斗统计（若在战斗中）
- `game-logs/` — 可选附带的原版日志尾部

报告写在 `user://devmode-reports/`（与 `mod_data/KitLib/` 同账号目录树）。
:::
