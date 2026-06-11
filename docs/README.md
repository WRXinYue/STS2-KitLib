# KitLib documentation

Documentation for **[KitLib](https://github.com/WRXinYue/STS2-KitLib)** — a modular in-game toolkit for Slay the Spire 2 (Core + satellite modules, extension APIs, and optional dev-rail workflows).

## Channels

| Channel | Location | Audience |
| --- | --- | --- |
| **Valaxy site** (`docs/pages/`) | Built with `make docs` | Install, user guide, **mod author / extension API**, changelog |
| **In-game Manual** | `manual/` | Short per-panel help while playing (not a full modding course) |
| **Repo notes** | `docs/*.md` here (not all on the site) | Architecture, AI notes, maintainer runbooks |

KitLib is **not** only the dev rail: satellites cover cheats, AI, logging, mod settings, and compat tooling. The site’s **Developer** section documents how other mods integrate with the library.

## Valaxy site

Stack: [Valaxy](https://valaxy.site/) + **pnpm** (`docs/package.json`).

```bash
make docs        # dev server
make docs-build  # static output → docs/dist/
```

Deploy `docs/dist/` to any static host (`vercel.json` includes SPA rewrites).

### Extension & compatibility

- **[STS2 version compatibility](pages/developer/extending/sts2-compat.md)** — dual-profile `#if`, `kitlib.compat.toml`, Abstractions API
- **[STS2 API profiles (maintainers)](pages/developer/sts2-api-profiles.md)** — LFS refs, `make build-profiles`, CI
- **[Panel registry](pages/developer/extending/panel-registry.md)** · **[Mod runtime](pages/developer/extending/mod-runtime.md)**

## Repo-only Markdown

Contributor notes not published as Valaxy routes:

- [architecture.md](./architecture.md) — Core + satellite boundaries and load order
- [sts2-api-profiles.md](./sts2-api-profiles.md) — source for the maintainer site page
- [ai-algorithm.md](./ai-algorithm.md), [lan-host-drive-afk.md](./lan-host-drive-afk.md), …

## Writing guide

Valaxy Markdown (containers, frontmatter, i18n): [Markdown writing guide](https://oceanus.wrxinyue.org/guide/writing/markdown).
