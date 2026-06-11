#!/usr/bin/env python3
"""Launch STS2 via Steam (macOS/Linux) or direct exe (Windows)."""

from __future__ import annotations

import argparse
import subprocess
import sys
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


def _launch_direct(exe: Path, game_root: Path) -> None:
    ensure_steam_appid_file(game_root)
    cmd = [str(exe), "--log", "--rendering-driver", "opengl3"]
    print(f"Launching: {' '.join(cmd)}")
    subprocess.Popen(
        cmd,
        cwd=game_root,
        creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP,
        close_fds=True,
    )


def main() -> int:
    ap = argparse.ArgumentParser(description="Launch Slay the Spire 2.")
    ap.add_argument(
        "--repo-root",
        type=Path,
        default=None,
        help="Repository root (defaults to parent of scripts/)",
    )
    args = ap.parse_args()

    root = (args.repo_root or _SCRIPT_DIR.parent).resolve()
    load_dotenv(root / ".env")

    game_root = read_sts2_dir_from_local_props(root) or resolve_sts2_dir()
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
        exe = resolve_sts2_executable(game_root)
        if not exe:
            print(f"No STS2 executable found under {game_root}", file=sys.stderr)
            return 1
        _launch_direct(exe, game_root)
        return 0

    # macOS/Linux: must go through Steam or the game shows "Steam failed to initialize".
    launch_sts2_via_steam()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
