# KitLib documentation

All published documentation lives under **`docs/pages/`** and is built with [Valaxy](https://valaxy.site/).

```bash
make docs        # dev server
make docs-build  # static output → docs/dist/
```

## Structure

| Area | Path | Audience |
| --- | --- | --- |
| **User guide** | `pages/guide/` | Install, rail panels, MCP, progress protection, Dev Mode |
| **Developer** | `pages/developer/` | Extension API, architecture, AI, maintainer runbooks |
| **Changelog** | `pages/changelog*.md` | Generated from root `CHANGELOG.md` at build time (gitignored; do not commit) |

Remote/static builds must run **`make docs-build` from the repo root** (not `docs/` alone) so `../CHANGELOG.md` exists. If sources are missing, `scripts/sync-changelog.mjs` fails the build instead of publishing a site without `/changelog`.

## Writing

Valaxy Markdown (containers, frontmatter, i18n): [Markdown writing guide](https://oceanus.wrxinyue.org/guide/writing/markdown).

Maintainer-only notes: `pages/developer/notes/`.
