# KitLib MCP

The MCP server lives **inside** the KitLib mod (KitLib.Dev) and speaks the MCP **Streamable HTTP**
transport, so it updates with the mod, needs no per-platform executable, and works on every platform
the game runs on.

## Endpoints

| Endpoint | Meaning |
|---|---|
| `POST http://127.0.0.1:9877/mcp` | MCP Streamable HTTP, **full** tool set (26 tools) |
| `POST http://127.0.0.1:9877/mcp-core` | Same transport, **Core** tool profile (10 tools: state / combat / map / wait / session / read-text) for token-sensitive clients |
| `GET /health` | Readiness + version / tool counts / uptime (used by `make dev-session`) |
| `POST /messages` | Legacy JSON-RPC used by the optional stdio proxy below |

The game must be running with KitLib and its **Dev Tools** module enabled
(game mod list → KitLib → Dev Tools). The server starts with the main menu.

## Registering with MCP clients

### DSH (`@deepseek-ai/dsh-mcp-client`)

```yaml
- id: mcp-kitlib
  name: '@deepseek-ai/dsh-mcp-client'
  config:
    serverName: kitlib
    transport: streamable-http
    url: http://127.0.0.1:9877/mcp
    toolCallTimeoutMs: 120000
```

### Cursor / Claude Code / other clients

Point a `type: "http"` (or `streamable-http`) server at `http://127.0.0.1:9877/mcp`.
Clients that only support stdio can launch the proxy below instead.

## Stdio proxy (legacy, optional)

`tools/KitLib.Mcp` forwards stdio MCP to the legacy `/messages` endpoint. It is **not published**
any more — the in-game Streamable HTTP server is the supported path — and it never ships inside the
mod. Build it locally only when a client lacks Streamable HTTP support:

```bash
make build-tools TOOLS_RID=win-x64   # or osx-arm64, osx-x64, linux-x64, linux-arm64
```

To attach it to a GitHub Release anyway, pass `--with-mcp` to `scripts/publish_release.py` (or set
`INCLUDE_MCP_EXE=1`). `make zip-mcp` / `make nexus-mcp` remain as manual channels.

## Configuration

- **Port**: KitLib setting `McpPort` (default `9877`) or environment variable `KITLIB_MCP_PORT`.
  The game reads it at startup, so restart after changing it. Clients must be updated to match.
- **Auth (optional)**: set `KITLIB_MCP_TOKEN` to require `Authorization: Bearer <token>` on `/mcp*`
  and `/messages` (`/health` stays open). MCP clients support `headers` in their server config.
- **Tool profile**: `/mcp` = all tools, `/mcp-core` = read/drive-play subset.

## Protocol behaviour

- Negotiated MCP revisions: `2026-07-28`, `2025-11-25`, `2025-06-18`, `2025-03-26` (requested
  revision is echoed; unknown revisions fall back to `2025-03-26`).
- Sessions: `Mcp-Session-Id` issued on `initialize`, unknown sessions answer HTTP 404 with
  JSON-RPC `-32001` so clients re-initialize; up to 64 sessions kept in LRU order.
- Notifications (no `id`) answer `202 Accepted`; JSON-only responses (no SSE stream).
- `tools/list` entries include MCP `title`, `annotations` (`readOnlyHint` / `destructiveHint` /
  `idempotentHint` / `openWorldHint`) and `_meta` hints (`anthropic/searchHint`,
  `anthropic/alwaysLoad` for `get_game_state` / `dev_get_session`).
- `tools/call` returns both `content` (text for the model) and `structuredContent` (same JSON as a
  value); tool-level failures come back as `isError: true` results instead of JSON-RPC errors.

`tools/KitLib.Mcp` remains the reference for tool schemas; the in-game server is the source of truth.
