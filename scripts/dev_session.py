#!/usr/bin/env python3
"""Deploy DevMode, launch STS2, and wait for the in-game MCP bridge."""

from __future__ import annotations

import argparse
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_dotenv  # noqa: E402


def _repo_root(explicit: Path | None) -> Path:
    return (explicit or _SCRIPT_DIR.parent).resolve()


def _run_sync(repo_root: Path) -> int:
    print("Running: make sync")
    result = subprocess.run(["make", "sync"], cwd=repo_root, check=False)
    if result.returncode != 0:
        print("make sync failed.", file=sys.stderr)
    return result.returncode


def _run_launch(repo_root: Path) -> int:
    launch = repo_root / "scripts" / "launch_sts2.py"
    print(f"Running: {launch}")
    result = subprocess.run([sys.executable, str(launch), "--repo-root", str(repo_root)], check=False)
    if result.returncode != 0:
        print("Game launch failed.", file=sys.stderr)
    return result.returncode


def _wait_bridge(port: int, timeout_sec: float) -> bool:
    url = f"http://127.0.0.1:{port}/health"
    deadline = time.monotonic() + timeout_sec
    print(f"Waiting for MCP bridge at {url} (timeout {timeout_sec:.0f}s)...")

    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=2) as resp:
                body = resp.read().decode("utf-8", errors="replace")
                if resp.status == 200 and "ok" in body.lower():
                    print(f"Bridge ready: {body.strip()}")
                    return True
        except (urllib.error.URLError, TimeoutError, OSError):
            pass
        time.sleep(1.0)

    print(f"Timed out waiting for MCP bridge on port {port}.", file=sys.stderr)
    return False


def main() -> int:
    ap = argparse.ArgumentParser(description="KitLib agent session bootstrap (L1 orchestrator).")
    ap.add_argument("--repo-root", type=Path, default=None, help="Repository root")
    ap.add_argument("--sync", action="store_true", help="Run make sync before launch")
    ap.add_argument("--launch", action="store_true", help="Launch STS2")
    ap.add_argument(
        "--wait-bridge",
        type=float,
        metavar="SEC",
        default=0,
        help="Poll GET /health until ready or timeout (seconds)",
    )
    ap.add_argument("--port", type=int, default=9877, help="MCP bridge port (default 9877)")
    args = ap.parse_args()

    repo_root = _repo_root(args.repo_root)
    load_dotenv(repo_root / ".env")

    if not args.sync and not args.launch and args.wait_bridge <= 0:
        ap.print_help()
        return 1

    if args.sync:
        code = _run_sync(repo_root)
        if code != 0:
            return code

    if args.launch:
        code = _run_launch(repo_root)
        if code != 0:
            return code

    if args.wait_bridge > 0:
        if not _wait_bridge(args.port, args.wait_bridge):
            return 1

    print("")
    print("Next MCP steps (via your MCP client):")
    print("  1. dev_list_save_slots")
    print("  2. dev_load_save_slot(slot_id) OR dev_start_test_run(seed?)")
    print("  3. Poll dev_get_session until runActive is true")
    print("  4. get_game_state / combat_action / map_action")
    print(f"Bridge health: http://127.0.0.1:{args.port}/health")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
