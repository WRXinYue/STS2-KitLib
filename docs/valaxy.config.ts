import { existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { resolve } from 'node:path'
import type { ThemeConfig } from 'valaxy-theme-nova'
import { defineValaxyConfig } from 'valaxy'
import modManifest from '../KitLib.json'
import { syncChangelog } from './scripts/sync-changelog.mjs'

const __dirname = fileURLToPath(new URL('.', import.meta.url))

// Generate gitignored pages before Valaxy scans docs/pages/ (remote builds need full repo checkout).
syncChangelog()

/** Vite plugin: re-sync on dev watch when root CHANGELOG files change. */
function changelogWatch() {
  const rootDir = resolve(__dirname, '..')
  const watched = [
    resolve(rootDir, 'CHANGELOG.md'),
    resolve(rootDir, 'CHANGELOG.zh-CN.md'),
  ].filter(existsSync)

  return {
    name: 'changelog-watch',
    configureServer(server: any) {
      server.watcher.add(watched)
      server.watcher.on('change', (path: string) => {
        if (watched.includes(path)) syncChangelog({ failOnMissing: false })
      })
    },
  }
}

export default defineValaxyConfig<ThemeConfig>({
  theme: 'nova',

  vite: {
    plugins: [changelogWatch()],
  },

  siteConfig: {
    title: 'KitLib',
    url: 'https://kitlib-sts2.local',
    description: 'Modular in-game toolkit for Slay the Spire 2 — documentation',
    lang: 'en',
    languages: ['en', 'zh-CN'],

    author: {
      name: 'WRXinYue',
    },

    search: {
      enable: false,
    },
  },

  themeConfig: {
    colors: {
      primary: '#BB6516',
    },

    navTitle: { en: 'KitLib', 'zh-CN': 'KitLib' },

    nav: [
      {
        locale: 'nav.kitlib',
        link: '/kitlib/preface',
        subNav: [
          { locale: 'nav.intro', link: '/kitlib/preface' },
          { locale: 'nav.install', link: '/kitlib/install' },
          { text: { en: 'Progress protection', 'zh-CN': '进度保护' }, link: '/kitlib/progress-protection' },
          { text: { en: 'Architecture', 'zh-CN': '架构' }, link: '/kitlib/architecture' },
          { locale: 'nav.contributing', link: '/kitlib/contributing' },
        ],
      },
      {
        locale: 'nav.api',
        link: '/api/',
        subNav: [
          { text: { en: 'Overview', 'zh-CN': '概览' }, link: '/api/' },
          { locale: 'nav.game_ops', link: '/api/game-ops' },
          { locale: 'nav.cards', link: '/api/cards' },
          { locale: 'nav.relics', link: '/api/relics' },
          { locale: 'nav.potions', link: '/api/potions' },
          { locale: 'nav.power', link: '/api/power' },
          { locale: 'nav.runtime_cheat', link: '/api/runtime-cheat' },
          { locale: 'nav.mod_settings', link: '/api/mod-settings' },
          { locale: 'nav.kitlib_log', link: '/api/kitlib-log' },
        ],
      },
      {
        locale: 'nav.changelog',
        link: '/changelog',
        subNav: [
          { text: 'English', link: '/changelog' },
          { text: '中文', link: '/changelog-zh-cn' },
        ],
      },
      {
        text: `v${modManifest.version}`,
        link: 'https://github.com/WRXinYue/STS2-KitLib/releases',
      },
    ],

    navTools: [['toggleLocale', 'toggleTheme']],

    hero: {
      title: { en: 'KITLIB', 'zh-CN': 'KITLIB' },
      motto: {
        en: 'Modular in-game toolkit & extension APIs for Slay the Spire 2',
        'zh-CN': '《杀戮尖塔 2》模块化游戏内工具库与扩展接口',
      }
    },

    footer: {
      since: 2026,
    },
  },
})
