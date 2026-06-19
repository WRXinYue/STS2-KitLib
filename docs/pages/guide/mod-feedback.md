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
（维护者向长文；用户向说明见 README.zh-CN.md 与 [文档站](/guide/panels/)。）
:::
