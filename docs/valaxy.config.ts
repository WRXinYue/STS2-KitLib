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
      { locale: 'nav.home', link: '/' },
      {
        locale: 'nav.guide',
        link: '/guide/preface',
        subNav: [
          { locale: 'nav.intro', link: '/guide/preface' },
          { locale: 'nav.install', link: '/guide/install' },
          { locale: 'nav.panels_overview', link: '/guide/panels' },
          { text: { en: 'Progress protection', 'zh-CN': '进度保护' }, link: '/guide/progress-protection' },
          { text: { en: 'Title Dev Mode', 'zh-CN': '标题开发模式' }, link: '/guide/title-dev-mode' },
          { text: { en: 'Mod feedback', 'zh-CN': 'Mod 反馈' }, link: '/guide/mod-feedback' },
          { text: 'MCP', link: '/guide/mcp' },
        ],
      },
      {
        locale: 'nav.extending',
        link: '/developer/extending/panel-registry',
        subNav: [
          { locale: 'nav.dev_panel', link: '/developer/extending/panel-registry' },
          { locale: 'nav.mod_runtime', link: '/developer/extending/mod-runtime' },
          { locale: 'nav.kitlib_log', link: '/developer/extending/kitlib-log' },
          { locale: 'nav.sts2_compat', link: '/developer/extending/sts2-compat' },
          { text: { en: 'Mod AI integration', 'zh-CN': 'Mod AI 集成' }, link: '/developer/extending/mod-ai-integration' },
          { locale: 'nav.api_profiles', link: '/developer/sts2-api-profiles' },
          { text: { en: 'Architecture', 'zh-CN': '架构' }, link: '/developer/architecture' },
          { text: { en: 'AI algorithm', 'zh-CN': 'AI 算法' }, link: '/developer/ai-algorithm' },
          { text: { en: 'LAN co-op testing', 'zh-CN': 'LAN 联机测试' }, link: '/developer/lan-host-drive-afk' },
          {
            locale: 'nav.contributing',
            link: '/developer/dev',
          },
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
