#!/usr/bin/env python3
"""Launch STS2 via Steam (macOS/Linux) or direct exe (Windows)."""

from __future__ import annotations

import argparse
import sys
import time
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_dotenv  # noqa: E402
from lib.steam import (  # noqa: E402
    ensure_steam_appid_file,
    launch_sts2_via_steam,
    read_sts2_dir_from_local_props,
    resolve_sts2_dir,
    resolve_sts2_executable,
)
from lib.sts2_launch import (  # noqa: E402
    DEFAULT_JOIN_CLIENT_ID,
    build_launch_argv,
    launch_detached,
)


def _resolve_game_root(repo_root: Path) -> Path | None:
    return read_sts2_dir_from_local_props(repo_root) or resolve_sts2_dir()


def _launch_windows(
    game_root: Path,
    *,
    fastmp: str | None,
    client_id: int,
    log: bool,
    extra: list[str],
) -> int:
    exe = resolve_sts2_executable(game_root)
    if not exe:
        print(f"No STS2 executable found under {game_root}", file=sys.stderr)
        return 1
    ensure_steam_appid_file(game_root)
    if fastmp == "dual":
        host_argv = build_launch_argv(exe, log=log, fastmp="host", extra=extra)
        join_argv = build_launch_argv(exe, log=log, fastmp="join", client_id=client_id, extra=extra)
        launch_detached(exe, game_root, host_argv)
        print("Waiting 8s before launching join client (host ENet bind on :33771)...")
        time.sleep(8)
        launch_detached(exe, game_root, join_argv)
        return 0

    argv = build_launch_argv(
        exe,
        log=log,
        fastmp=fastmp,
        client_id=client_id,
        extra=extra,
    )
    launch_detached(exe, game_root, argv)
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="Launch Slay the Spire 2.")
    ap.add_argument(
        "--repo-root",
        type=Path,
        default=None,
        help="Repository root (defaults to parent of scripts/)",
    )
    ap.add_argument(
        "--fastmp",
        choices=("host", "join", "dual"),
        default=None,
        help="Fast multiplayer mode: host, join client, or dual-launch both (Windows only)",
    )
    ap.add_argument(
        "--client-id",
        type=int,
        default=DEFAULT_JOIN_CLIENT_ID,
        help=f"Join client id for --fastmp join/dual (default: {DEFAULT_JOIN_CLIENT_ID})",
    )
    ap.add_argument(
        "--no-log",
        action="store_true",
        help="Omit --log (default: enabled for dev launches)",
    )
    ap.add_argument(
        "extra",
        nargs="*",
        help="Extra arguments forwarded to SlayTheSpire2.exe",
    )
    args = ap.parse_args()

    root = (args.repo_root or _SCRIPT_DIR.parent).resolve()
    load_dotenv(root / ".env")

    game_root = _resolve_game_root(root)
    if game_root:
        print(f"Sts2Dir: {game_root}")
    else:
        print("Sts2Dir not configured; launching via Steam default install.")

    if sys.platform == "win32":
        if not game_root:
            print(
                "STS2 install not found. Run `make init` to generate local.props.",
                file=sys.stderr,
            )
            return 1
        return _launch_windows(
            game_root,
            fastmp=args.fastmp,
            client_id=args.client_id,
            log=not args.no_log,
            extra=args.extra,
        )

    if args.fastmp:
        print("Fast multiplayer launch is only supported on Windows.", file=sys.stderr)
        return 1

    launch_sts2_via_steam()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
