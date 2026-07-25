#!/usr/bin/env python3
"""Resolve STS2 ref game root for Makefile / build scripts."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_dotenv  # noqa: E402
from lib.sts2_profiles import DEFAULT_PROFILE, _PINNED_VERSIONS, resolve_profile_dir  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser(description="Print STS2 ref root for MSBuild Sts2Dir.")
    ap.add_argument(
        "profile",
        nargs="?",
        choices=tuple(_PINNED_VERSIONS),
        default=DEFAULT_PROFILE,
    )
    ap.add_argument(
        "--repo-root",
        type=Path,
        default=_SCRIPT_DIR.parent,
    )
    args = ap.parse_args()

    load_dotenv(args.repo_root / ".env")
    try:
        print(resolve_profile_dir(args.profile, repo_root=args.repo_root))
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
