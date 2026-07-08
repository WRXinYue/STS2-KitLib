---
title:
  en: KitLib
  zh-CN: KitLib

features:
  title:
    en: Overview
    zh-CN: 概览
  subtitle:
    en: In-game toolkit for Slay the Spire 2
    zh-CN: 《杀戮尖塔 2》游戏内工具库
  text:
    en: >-
      KitLib serves mod authors and players: test runs, in-run editing, logs, and multiplayer debugging
      without leaving the game — plus a better Mod panel, progress protection, and feedback export.
      Core loads optional satellite modules; a failed optional module should not break KitLib or mods that depend on it.
    zh-CN: >-
      KitLib 面向 mod 开发者与玩家：局内测试、改内容、看日志、联机调试，以及更好用的 Mod 面板、
      进度保护与问题反馈。Core 加载可选卫星模块；单个模块异常不应拖垮核心与依赖 KitLib 的其他 mod。

  cards:
    - title:
        en: Install & modules
        zh-CN: 安装与模块
      details:
        en: >-
          Install from Steam Workshop or Nexus. In Mods → KitLib → Modules, pick a load profile
          or toggle satellite modules (restart required). See /guide/install/
        zh-CN: >-
          从 Steam 创意工坊或 Nexus 安装。在 Mods → KitLib → 模块 选择加载方案，或单独开关卫星模块（需重启）。
          详见 /guide/install/
    - title:
        en: Dev rail & Dev Mode
        zh-CN: 开发侧栏与 Dev Mode
      details:
        en: >-
          In-run rail: browsers, cheats, saves, presets, logs, AI, hooks, scripts.
          Title Dev Mode: test runs, pseudo co-op, diagnostics.
          Also: kitlog CLI, mod feedback ZIP, progress guard, MCP.
          See /guide/panels/ and /guide/title-dev-mode/
        zh-CN: >-
          局内侧栏：浏览器、作弊、存档、预设、日志、AI、钩子、脚本等。
          标题 Dev Mode：测试局、伪联机、诊断。
          另含 kitlog、Mod 反馈、进度保护、MCP。详见 /guide/panels/ 与 /guide/title-dev-mode/
    - title:
        en: For mod developers
        zh-CN: Mod 开发者
      details:
        en: >-
          Add eng/KitLib.ContentMod.props to your csproj; depend on KitLib modules at runtime as needed.
          Register rail tabs, logging, and AI hooks.
          See /developer/extending/panel-registry/ and /developer/dev/
        zh-CN: >-
          csproj 引用 eng/KitLib.ContentMod.props；运行时按需依赖卫星模块。
          可注册侧栏、日志、AI 等扩展。详见 /developer/extending/panel-registry/ 与 /developer/dev/
---
