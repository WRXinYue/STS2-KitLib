# KitLib Dev Viewer

Vue 3 + Vite developer panel embedded in KitLib.Dev at `http://127.0.0.1:9878/`.

**Tabs:** Logs (terminal-style via [xterm.js](https://xtermjs.org/)) · Combat stats timeline

Replaces the deprecated `kitlog` terminal for in-game log tailing.

## Stack

- Vue 3.5 + Vite 8 + TypeScript 5.8
- `@xterm/xterm` + `@xterm/addon-fit` — live log stream (`/api/logs/ws`)
- [shadcn/vue](https://www.shadcn-vue.com/docs) — Card, Input, Select (combat tab)
- `vue-i18n` — English / 中文

## Routes

| URL | Tab |
|-----|-----|
| `http://127.0.0.1:9878/#/logs` | Live logs (default) |
| `http://127.0.0.1:9878/#/logs?preset=ai` | AI-scoped log preset |
| `http://127.0.0.1:9878/#/combat` | Combat stats |

## APIs

- **WebSocket** `/api/logs/ws` — structured log frames + filter sync from in-game log viewer
- **WebSocket** `/api/ws` — live combat `stats`; client can send `requestStats`
- **HTTP** `GET /api/export/json` — download full stats bundle as JSON
- **HTTP** `GET /api/live` — latest combat snapshot

In-game log viewer and AI panel buttons open the browser dev viewer instead of `kitlog`.

## Develop

```bash
cd tools/dev-viewer
pnpm install
pnpm dev
```

Sample combat data loads automatically when no embedded payload is present. Log stream requires the game + KitLib.Dev.

## Build (copies shell into mod)

```bash
pnpm build
# or from repo root:
make build-dev-viewer
```

Output:

- `dist/index.html` — single-file viewer shell
- `src/KitLib.Modules.Dev/CombatStats/viewer-shell.html` — embedded by `KitLib.Dev`

Run `pnpm build` before packaging KitLib when the viewer UI changes.

## i18n

| Locale | How |
|--------|-----|
| English | default, or `?lang=en` |
| 中文 | browser `zh*`, `?lang=zh`, or header language tabs |

Locale choice is saved in `localStorage`.

## Add components

```bash
pnpm dlx shadcn-vue@latest add <component>
```
