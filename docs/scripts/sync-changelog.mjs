/**
 * Copy root CHANGELOG*.md into docs/pages/ with Valaxy frontmatter.
 * Run before `valaxy build` — pages are gitignored and must be generated at build time.
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const docsDir = resolve(__dirname, '..')
const rootDir = resolve(docsDir, '..')
const pagesDir = resolve(docsDir, 'pages')

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

/** @param {{ failOnMissing?: boolean }} [options] */
export function syncChangelog(options = {}) {
  const { failOnMissing = true } = options
  const missing = []

  for (const { src, dest } of entries) {
    if (!existsSync(src)) {
      missing.push(src)
      continue
    }
    writeFileSync(dest, frontmatter + readFileSync(src, 'utf-8'))
  }

  if (missing.length > 0) {
    const msg =
      'changelog-sync: missing source file(s):\n' +
      missing.map(p => `  - ${p}`).join('\n') +
      '\nBuild from the repo root (make docs-build) so ../CHANGELOG.md is available.'

    if (failOnMissing) {
      throw new Error(msg)
    }
    console.warn(msg)
  }
}

if (process.argv[1]?.replace(/\\/g, '/').endsWith('sync-changelog.mjs')) {
  syncChangelog()
}
