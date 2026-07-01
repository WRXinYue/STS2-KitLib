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
:::

::: zh-CN
主菜单 **开发模式** 把分散的开发按钮收进一个子菜单：

- **新测试** — 快速开一局测试
- **新测试（种子）** — 可填种子的测试局
- **读档** — 加载 DevMode 快照槽（无槽位时灰掉）
- **普通局：…** — 在非测试局之间切换 **关闭** → **开发模式** → **作弊模式**
- **多人** — 多人开发子菜单（见下）
- **解锁全部进度** — 解锁时间线纪元、进阶 10、图鉴条目（需确认）
- **诊断** — **日志** 与 **Mod 反馈**（进度保护也可在 **Mods → KitLib**）
- **进度保护** — 备份状态、恢复、每条备份的 **详情**（**Mods → KitLib** 里同样流程）
- **返回** — 回到原版主菜单

**多人** 子菜单：

- **多人作弊：开/关** — 加入同步的多人作弊局
- **伪联机测试（主机）** — 主机选角色/种子；可选 SyncBot、幻影玩家（NetId 1001）、AI 队友
- **LAN 多人** — 打开内置多人测试场景

**进度保护** 的恢复只能在标题画面操作。尽量让当前 mod 组合与备份时一致。
:::

## Multiplayer & co-op testing (dev){lang="en"}

## 多人与联机测试（开发向）{lang="zh-CN"}

::: en
These features are **opt-in** from DevPanel → **AI Host**. They do not change vanilla solo hand-play or draw speed unless you enable AI / cheats yourself.

- **AI Host (solo)** — `StrongStrategy` (default) drives your character locally: DeckPlan macro scoring, shallow combat search, lethal checks. Set `AutoPlayStrategy` to `Simple` for legacy heuristics. Use for single-player automation.
- **SyncBot** — Simulates remote peer ACKs and default choices on one machine; optional phantom player (NetId 1001). Use for host-only co-op smoke tests without a second client.
- **Pseudo Co-op preset** — Hand-play host + AI teammate for phantom/offline peers via action queue. Use for solo host with simulated teammate.
- **LAN host-drive + AFK** — Host hand-plays local player; AI enqueues combat for connected ENet client; client AFK blocks local combat input; map votes mirrored. Use for two game instances on one PC (auto preset on dual launch).

**Dual-instance LAN (recommended):** launch host + client on the same machine → presets apply automatically; host logs `LAN host preset applied`, client logs `AFK client enabled`.

Detailed architecture, verification checklist, and desync history: **[LAN host-drive & AFK co-op](/developer/lan-host-drive-afk)** · [Documentation index](/)
:::

::: zh-CN
这些功能需在 Dev 面板 → **AI Host** 里主动开启，不会默认改变单机手打或抽牌速度。

- **AI Host（单机）** — 默认 `StrongStrategy` 本地代打：DeckPlan 宏观评分、浅层战斗搜索、斩杀判定。`AutoPlayStrategy` 设为 `Simple` 可退回旧启发式。用于单机自动化。
- **SyncBot** — 单机模拟远端 ACK 和默认选项；可选幻影玩家（NetId 1001）。主机侧联机冒烟测试，无需第二个客户端。
- **伪联机预设** — 主机手打 + AI 队友，通过动作队列为幻影/离线 peer 代打。适合单机主机带模拟队友。
- **LAN 主机代打 + AFK** — 主机手打本地角色；AI 为已连接的 ENet 客机排队战斗输入；客机 AFK 会屏蔽本地战斗输入；地图投票由主机镜像。适合同一台电脑开两个游戏实例（双开时自动套用预设）。

**双开 LAN（推荐）：** 同机启动主机 + 客机 → 预设自动生效；主机日志 `LAN host preset applied`，客机 `AFK client enabled`。

架构细节、验收清单与历史 desync 记录：**[LAN 主机代打与 AFK 联机](/developer/lan-host-drive-afk)** · [文档索引](/)
:::
