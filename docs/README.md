# KitLib documentation

All published documentation lives under **`docs/pages/`** and is built with [Valaxy](https://valaxy.site/).

```bash
make docs        # dev server
make docs-build  # static output → docs/dist/
```

## Structure

| Area | Path | Audience |
| --- | --- | --- |
| **KitLib** | `pages/kitlib/` | Install, intro, progress protection, architecture, contributing |
| **KitAI** | `pages/kitai/` | AI host, algorithm, LAN co-op |
| **API** | `pages/api/` | Public Abstractions/Core bridges for content mods |
| **Changelog** | `pages/changelog*.md` | Generated from root `CHANGELOG.md` at build time (gitignored; do not commit) |

## Repo vs game install

- **Repo:** KitLib host sources under `src/` (`Core`, `Loader`, `Modules.User`, `Modules.Cheat`); sibling products under `mods/KitModPanel|KitDevTools|KitAI`.
- **Game:** `make sync` deploys four folders into the game’s `mods/` (`KitLib`, `KitModPanel`, `KitDevTools`, `KitAI`).

Details: [Architecture](pages/kitlib/architecture.md).

Remote/static builds must run **`make docs-build` from the repo root** (not `docs/` alone) so `../CHANGELOG.md` exists. If sources are missing, `scripts/sync-changelog.mjs` fails the build instead of publishing a site without `/changelog`.

## Writing

Valaxy Markdown (containers, frontmatter, i18n): [Markdown writing guide](https://oceanus.wrxinyue.org/guide/writing/markdown).
