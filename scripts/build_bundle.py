#!/usr/bin/env python3
"""CLI wrapper for scripts.lib.bundle_build."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.bundle_build import build_bundle  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser(description="Build KitLib mod bundle projects only.")
    ap.add_argument("-c", "--configuration", default="Debug")
    ap.add_argument("--sts2-profile", choices=("stable", "beta"), default="")
    ap.add_argument("--sts2-dir", default="")
    args = ap.parse_args()

    build_bundle(
        configuration=args.configuration,
        sts2_profile=args.sts2_profile or None,
        sts2_dir=args.sts2_dir or None,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
