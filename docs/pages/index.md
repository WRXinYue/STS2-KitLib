---
title:
  en: KitLib
  zh-CN: KitLib

features:
  title:
    en: Overview
    zh-CN: 概览
  subtitle:
    en: Foundation library and host for Slay the Spire 2
    zh-CN: 《杀戮尖塔 2》基础库与宿主
  text:
    en: >-
      KitLib is a foundation library: declare it in your mod manifest, reference KitLib.Abstractions, and call its APIs.
      Satellite modules (KitModPanel, KitLib.Hud, KitLib.Dev) add in-game UI. A failed module should not break KitLib or mods that depend on it.
    zh-CN: >-
      KitLib 是基础库：在清单中声明、引用 KitLib.Abstractions、调用公开 API。
      卫星模块（KitModPanel、KitLib.Hud、KitLib.Dev）提供游戏内 UI。某个模块异常不应拖垮核心与依赖 KitLib 的其他 mod。

  cards:
    - title:
        en: Install
        zh-CN: 安装
      details:
        en: >-
          Install from Steam Workshop or Nexus. One KitLib package includes the mod panel, HUD, and dev tools.
          See /kitlib/install/
        zh-CN: >-
          从 Steam 创意工坊或 Nexus 安装。一个 KitLib 包即包含模组面板、HUD 与开发工具。
          详见 /kitlib/install/
    - title:
        en: Extension API
        zh-CN: 扩展 API
      details:
        en: >-
          Cards, relics, potions, powers, cheats & run stats, mod settings pages, and logging.
          See /api/
        zh-CN: >-
          卡牌、遗物、药水、Power、作弊与局内数值、Mod 设置页、日志。
          详见 /api/
---
