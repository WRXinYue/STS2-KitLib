#!/usr/bin/env python3
"""Print MSBuild Sts2Profile (always beta)."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_dotenv  # noqa: E402
from lib.sts2_profiles import resolve_compile_profile  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser(description="Print Sts2Profile for MSBuild (beta).")
    ap.add_argument("--repo-root", type=Path, default=_SCRIPT_DIR.parent)
    ap.add_argument("--sts2-dir", type=Path, default=None)
    args = ap.parse_args()

    load_dotenv(args.repo_root / ".env")
    print(resolve_compile_profile(repo_root=args.repo_root, sts2_dir=args.sts2_dir))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
