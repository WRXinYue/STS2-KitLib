#!/usr/bin/env python3
"""Talk to the in-game KitLib DevMode MCP server without an MCP client.

Two protocols are exposed by the mod:

* ``POST /mcp`` (full) and ``POST /mcp-core`` (Core tool profile) — MCP Streamable HTTP,
  used by the ``mcp-*`` commands below (``--core`` selects the Core profile).
* ``POST /messages`` — legacy JSON-RPC escape hatch used by the stdio proxy
  (``health`` / ``tools`` / ``call``).

Requires: STS2 running with the KitLib mod loaded (make dev-session).

Usage:
  python3 scripts/mcp_bridge.py health [--port 9877]
  python3 scripts/mcp_bridge.py tools [--port 9877]
  python3 scripts/mcp_bridge.py call <tool> ['{"json": "args"}'] [--port 9877]

  python3 scripts/mcp_bridge.py mcp-info [--port 9877] [--core]
  python3 scripts/mcp_bridge.py mcp-tools [--port 9877] [--core]
  python3 scripts/mcp_bridge.py mcp-call <tool> ['{"json": "args"}'] [--port 9877] [--core]
"""

from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.request

PROTOCOL_VERSION = "2026-07-28"


def _post(url: str, payload: dict, *, headers: dict[str, str] | None = None, timeout: float = 60.0):
    request = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream", **(headers or {})},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return response.status, response.headers, response.read()
    except urllib.error.HTTPError as exc:
        return exc.code, exc.headers, exc.read()


def _parse_mcp_body(content_type: str, body: bytes) -> dict:
    text = body.decode("utf-8", errors="replace")
    if "text/event-stream" in content_type:
        for line in text.splitlines():
            if line.startswith("data:"):
                return json.loads(line[5:].strip())
        raise RuntimeError(f"no data event in SSE response: {text[:200]!r}")
    return json.loads(text)


class McpHttpClient:
    """Minimal MCP Streamable HTTP client (JSON responses; SSE tolerated)."""

    def __init__(self, port: int, core: bool = False) -> None:
        self.url = f"http://127.0.0.1:{port}/{'mcp-core' if core else 'mcp'}"
        self.session_id: str | None = None
        self.protocol_version: str | None = None
        self._next_id = 1

    def _send(self, method: str, params: dict | None = None, *, notification: bool = False):
        payload: dict = {"jsonrpc": "2.0", "method": method}
        if not notification:
            payload["id"] = self._next_id
            self._next_id += 1
        if params is not None:
            payload["params"] = params

        headers = {}
        if self.session_id:
            headers["Mcp-Session-Id"] = self.session_id
        if self.protocol_version:
            headers["MCP-Protocol-Version"] = self.protocol_version

        status, response_headers, body = _post(self.url, payload, headers=headers)
        session_header = response_headers.get("Mcp-Session-Id")
        if session_header:
            self.session_id = session_header

        if status == 202 or not body:
            return None
        if status != 200:
            raise RuntimeError(f"HTTP {status}: {body.decode('utf-8', errors='replace')[:300]}")

        message = _parse_mcp_body(response_headers.get("Content-Type", ""), body)
        if "error" in message:
            error = message["error"]
            raise RuntimeError(f"{error.get('code')}: {error.get('message')}")
        return message.get("result")

    def initialize(self) -> dict:
        result = self._send("initialize", {
            "protocolVersion": PROTOCOL_VERSION,
            "capabilities": {},
            "clientInfo": {"name": "kitlib-mcp-bridge-cli", "version": "1.0.0"},
        })
        assert result is not None
        self.protocol_version = result.get("protocolVersion")
        self._send("notifications/initialized", notification=True)
        return result

    def list_tools(self) -> list[dict]:
        result = self._send("tools/list", {})
        return (result or {}).get("tools", [])

    def call_tool(self, name: str, arguments: dict):
        return self._send("tools/call", {"name": name, "arguments": arguments})


def _legacy_rpc(port: int, method: str, params: dict | None = None, timeout: float = 60.0) -> dict:
    payload = {"jsonrpc": "2.0", "id": 1, "method": method}
    if params is not None:
        payload["params"] = params
    request = urllib.request.Request(
        f"http://127.0.0.1:{port}/messages",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        body = json.loads(response.read().decode("utf-8"))
    if "error" in body:
        raise RuntimeError(body["error"].get("message") or json.dumps(body["error"]))
    return body.get("result", {})


def _print(value) -> None:
    print(json.dumps(value, ensure_ascii=False, indent=2))


def main() -> int:
    parser = argparse.ArgumentParser(description="KitLib DevMode MCP server CLI")
    parser.add_argument(
        "command",
        choices=["health", "tools", "call", "mcp-info", "mcp-tools", "mcp-call"],
    )
    parser.add_argument("tool", nargs="?", help="tool name for 'call' / 'mcp-call'")
    parser.add_argument("args", nargs="?", default="{}", help="JSON object of tool arguments")
    parser.add_argument("--port", type=int, default=9877, help="bridge port (default 9877)")
    parser.add_argument("--core", action="store_true", help="use the /mcp-core tool profile")
    args = parser.parse_args()

    try:
        if args.command == "health":
            with urllib.request.urlopen(f"http://127.0.0.1:{args.port}/health", timeout=3) as response:
                print(response.read().decode("utf-8", errors="replace"))
            return 0

        if args.command in ("mcp-info", "mcp-tools", "mcp-call"):
            client = McpHttpClient(args.port, core=args.core)
            info = client.initialize()
            if args.command == "mcp-info":
                _print(info)
            elif args.command == "mcp-tools":
                _print([{"name": t.get("name"), "description": t.get("description")} for t in client.list_tools()])
            else:
                if not args.tool:
                    parser.error("'mcp-call' needs a tool name")
                _print(client.call_tool(args.tool, json.loads(args.args)))
            return 0

        if args.command == "tools":
            tools = _legacy_rpc(args.port, "tools/list").get("tools", [])
            _print([{"name": t.get("name"), "description": t.get("description")} for t in tools])
            return 0

        if not args.tool:
            parser.error("'call' needs a tool name")
        _print(_legacy_rpc(args.port, "tools/call", {
            "name": args.tool,
            "arguments": json.loads(args.args),
        }))
        return 0
    except (urllib.error.URLError, ConnectionError) as exc:
        print(f"bridge unreachable on port {args.port}: {exc}", file=sys.stderr)
        print("Start the game first: make dev-session (or make sync + launch).", file=sys.stderr)
        return 1
    except Exception as exc:  # noqa: BLE001 - surface bridge errors verbatim
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
