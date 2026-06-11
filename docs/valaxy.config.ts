import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { resolve } from 'node:path'
import type { ThemeConfig } from 'valaxy-theme-nova'
import { defineValaxyConfig } from 'valaxy'
import modManifest from '../KitLib.json'

const __dirname = fileURLToPath(new URL('.', import.meta.url))

/** Vite plugin: sync root CHANGELOG files into docs/pages/ with frontmatter. */
function changelogSync() {
  const rootDir = resolve(__dirname, '..')
  const pagesDir = resolve(__dirname, 'pages')

  const entries = [
    { src: resolve(rootDir, 'CHANGELOG.md'), dest: resolve(pagesDir, 'changelog.md') },
    { src: resolve(rootDir, 'CHANGELOG.zh-CN.md'), dest: resolve(pagesDir, 'changelog-zh-cn.md') },
  ]

  const frontmatter = [
    '---',
    'title:',
    '  en: Changelog',
    '  zh-CN: 更新日志',
    'top: 9000',
    'cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp',
    '---',
    '',
    '',
  ].join('\n')

  function sync() {
    for (const { src, dest } of entries) {
      if (!existsSync(src)) continue
      writeFileSync(dest, frontmatter + readFileSync(src, 'utf-8'))
    }
  }

  return {
    name: 'changelog-sync',
    buildStart() { sync() },
    configureServer(server: any) {
      const watched = entries.map(e => e.src).filter(existsSync)
      server.watcher.add(watched)
      server.watcher.on('change', (path: string) => {
        if (watched.includes(path)) sync()
      })
    },
  }
}

export default defineValaxyConfig<ThemeConfig>({
  theme: 'nova',

  vite: {
    plugins: [changelogSync()],
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
        ],
      },
      {
        locale: 'nav.extending',
        link: '/developer/extending/panel-registry',
        subNav: [
          { locale: 'nav.dev_panel', link: '/developer/extending/panel-registry' },
          { locale: 'nav.mod_runtime', link: '/developer/extending/mod-runtime' },
          { locale: 'nav.sts2_compat', link: '/developer/extending/sts2-compat' },
          { locale: 'nav.api_profiles', link: '/developer/sts2-api-profiles' },
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
      },
      img: 'https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp',
    },

    footer: {
      since: 2026,
    },
  },
})
