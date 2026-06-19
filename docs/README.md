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
| **Changelog** | `pages/changelog*.md` | Synced from root `CHANGELOG.md` on build |

## Writing

Valaxy Markdown (containers, frontmatter, i18n): [Markdown writing guide](https://oceanus.wrxinyue.org/guide/writing/markdown).

Maintainer-only notes: `pages/developer/notes/`.
