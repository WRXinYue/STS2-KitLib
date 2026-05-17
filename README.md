# DevMode

**English** | [中文](./README.zh-CN.md)

A developer mode mod for Slay the Spire 2.

![DevMode](./assets/devmode.png)

## Features

- Developer panel accessible from the main menu and during runs
- Configurable relics, cards, gold, and encounters for repeatable test setups
- Enemy encounter system with unified select UI, combat spawning, and idle animation preview
- SpireScratch script runner with live reload, event hooks, and in-game log viewer
- i18n support with English and Simplified Chinese localization
- Extensible panel registry — other mods can add custom tabs to the DevMode rail

## Extending DevMode

Other mods can register custom rail tabs via `DevPanelRegistry`. See **[Developer → Dev panel registry](docs/pages/developer/extending/panel-registry.md)** for the full API reference, code examples, and icon usage.

## Documentation site

The **[docs/](docs/)** folder is a **[Valaxy](https://valaxy.site/)** site. Use **pnpm** (via Corepack):

```bash
cd docs
corepack enable && corepack prepare pnpm@10.24.0 --activate
pnpm install && pnpm dev
```

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for collaboration norms, K&R brace style, formatting commands, and localization.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.

## Acknowledgments

- [STS2-KaylaMod](https://github.com/mugongzi520/STS2-KaylaMod)

## License

MIT
