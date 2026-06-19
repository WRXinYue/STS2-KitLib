---
title:
  en: Title Dev Mode
  zh-CN: 标题开发模式
top: 9920
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
On the main menu, **Dev Mode** replaces separate dev buttons with one submenu:

- **New Test** — Start a quick test run
- **New Test (Seed)** — Test run with an optional seed
- **Load Save** — Load a DevMode snapshot slot (disabled when no slots exist)
- **Normal run: …** — Cycle **Disabled** → **Dev Mode** → **Cheat Mode** for non-test runs
- **Multiplayer** — Multiplayer dev submenu (see below)
- **Unlock All Progress** — Unlock timeline epochs, Ascension 10, and compendium entries (confirmation required)
- **Diagnostics** — **Logs** and **Mod feedback** (progress protection also under **Mods → KitLib**)
- **Progress protection** — Backup status, restore, per-backup **Details** (same flow in **Mods → KitLib**)
- **Back** — Return to the stock main menu

**Multiplayer** submenu:

- **Multiplayer cheat: ON/OFF** — Opt in to synced multiplayer cheat sessions
- **Pseudo Co-op Test (Host)** — Host with character/seed pickers; optional SyncBot, phantom player (NetId 1001), AI teammate
- **LAN Multiplayer** — Open the built-in multiplayer test scene

Restore from **Progress protection** is title-screen only. Prefer matching the backup’s mod set when possible.

## Multiplayer & co-op testing (dev)

These features are **opt-in** from DevPanel → **AI Host**. They do not change vanilla solo hand-play or draw speed unless you enable AI / cheats yourself.

- **AI Host (solo)** — `StrongStrategy` (default) drives your character locally: DeckPlan macro scoring, shallow combat search, lethal checks. Set `AutoPlayStrategy` to `Simple` for legacy heuristics. Use for single-player automation.
- **SyncBot** — Simulates remote peer ACKs and default choices on one machine; optional phantom player (NetId 1001). Use for host-only co-op smoke tests without a second client.
- **Pseudo Co-op preset** — Hand-play host + AI teammate for phantom/offline peers via action queue. Use for solo host with simulated teammate.
- **LAN host-drive + AFK** — Host hand-plays local player; AI enqueues combat for connected ENet client; client AFK blocks local combat input; map votes mirrored. Use for two game instances on one PC (auto preset on dual launch).

**Dual-instance LAN (recommended):** launch host + client on the same machine → presets apply automatically; host logs `LAN host preset applied`, client logs `AFK client enabled`.

Detailed architecture, verification checklist, and desync history: **[LAN host-drive & AFK co-op](/developer/lan-host-drive-afk)** · [Documentation index](/)
:::

::: zh-CN
（维护者向长文；用户向说明见 README.zh-CN.md 与 [文档站](/guide/panels/)。）
:::
