---
title:
  en: KitLib
  zh-CN: KitLib

features:
  title:
    en: Overview
    zh-CN: 概览
  subtitle:
    en: Modular toolkit for Slay the Spire 2
    zh-CN: 《杀戮尖塔 2》模块化工具库
  text:
    en: >-
      KitLib is a single in-game mod with a Core host and optional satellite modules — dev rail, cheats,
      AI autoplay, mod settings, logging, and more. Pick the DLLs you need under `modules/`, or ship content
      mods against `KitLib.Abstractions` with version-compat sidecars and panel APIs.
    zh-CN: >-
      KitLib 是游戏内的单一 mod：Core 宿主 + 可选卫星模块 —— 开发侧栏、作弊、AI 托管、Mod 设置、日志等。
      可在 `modules/` 按需保留 DLL；内容 mod 也可引用 `KitLib.Abstractions`，使用版本兼容 sidecar 与面板 API。

  cards:
    - title:
        en: Modular install
        zh-CN: 模块化安装
      details:
        en: >-
          One `mods/KitLib/` folder: Core hot-loads `KitLib.User`, `KitLib.Panel`, `KitLib.Cheat`, `KitLib.Dev`,
          `KitLib.AI`, `KitLib.ModPanel`, and more. Remove satellite DLLs to disable features without uninstalling KitLib.
        zh-CN: >-
          仅一个 `mods/KitLib/` 目录：Core 热加载 `KitLib.User`、`KitLib.Panel`、`KitLib.Cheat`、`KitLib.Dev`、
          `KitLib.AI`、`KitLib.ModPanel` 等。删除卫星 DLL 即可关闭功能，无需卸载整个 KitLib。
    - title:
        en: Dev rail & Dev Mode
        zh-CN: 开发侧栏与开发模式
      details:
        en: >-
          `KitLib.Panel` adds the left-edge rail (cards, cheats, logs, presets, …) and title-screen Dev Mode for test runs,
          snapshots, and diagnostics — the original DevMode-style workflow, now one module among many.
        zh-CN: >-
          `KitLib.Panel` 提供左侧轨道（卡牌、作弊、日志、预设等）与标题画面开发模式（测试局、快照、诊断）——
          即原 DevMode 式工作流，现为众多模块之一。
    - title:
        en: Cheats, AI & automation
        zh-CN: 作弊、AI 与自动化
      details:
        en: >-
          `KitLib.Cheat` for runtime tweaks; `KitLib.AI` for solo autoplay and companions; `KitLib.Dev` for hooks,
          SpireScratch scripts, Harmony tools, and MCP. Enable only what your run needs.
        zh-CN: >-
          `KitLib.Cheat` 负责运行时调节；`KitLib.AI` 负责单人托管与 companion；`KitLib.Dev` 提供钩子、
          SpireScratch 脚本、Harmony 与 MCP。按需在 `modules/` 中启用。
    - title:
        en: Logs, manual & mod panel
        zh-CN: 日志、手册与 Mod 面板
      details:
        en: >-
          `KitLib.User` covers session logs, progress guard, crash recovery, and the in-game Manual rail tab.
          `KitLib.ModPanel` adds the main-menu Mods settings UI and compatibility banners for other mods.
        zh-CN: >-
          `KitLib.User` 提供会话日志、进度保护、崩溃恢复与游戏内 Manual 轨道。
          `KitLib.ModPanel` 提供主菜单 Mod 设置与对其他 mod 的兼容提示。
    - title:
        en: Extension API
        zh-CN: 扩展 API
      details:
        en: >-
          Register dev-rail tabs (`DevPanelRegistry`), read mod catalogs (`ModRuntime`), declare constraints
          (`kitlib.compat.toml`), and target dual STS2 API profiles (`STS2_BETA106PLUS` / `Sts2ProfileMap`).
          NuGet: `STS2.KitLib.Abstractions`.
        zh-CN: >-
          注册 dev 轨道标签（`DevPanelRegistry`）、读取 mod 目录（`ModRuntime`）、声明约束（`kitlib.compat.toml`）、
          对接双 STS2 API profile（`STS2_BETA106PLUS` / `Sts2ProfileMap`）。NuGet：`STS2.KitLib.Abstractions`。
    - title:
        en: Stable & beta STS2
        zh-CN: Stable 与 beta 游戏版本
      details:
        en: >-
          One release zip supports pinned stable and beta game builds via runtime profile detection and optional
          dual-profile compile refs for mod authors.
        zh-CN: >-
          同一发布包通过运行时 profile 识别支持钉死的 stable / beta 游戏版本；mod 作者可选用双 profile 编译 ref。
---
