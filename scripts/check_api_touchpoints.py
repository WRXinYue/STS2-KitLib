#!/usr/bin/env python3
"""Run KitLib.ApiCheck against the pinned beta sts2.dll."""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO_ROOT = _SCRIPT_DIR.parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_dotenv  # noqa: E402
from lib.sts2_profiles import format_profile_paths, pinned_version  # noqa: E402

_API_CHECK_PROJECT = _REPO_ROOT / "tools" / "KitLib.ApiCheck" / "KitLib.ApiCheck.csproj"
_MANIFEST = _REPO_ROOT / "eng" / "api_touchpoints.yaml"


def _run_profile(profile: str, dll: Path, manifest: Path) -> int:
    cmd = [
        "dotnet",
        "run",
        "--project",
        str(_API_CHECK_PROJECT),
        "-c",
        "Release",
        "--no-launch-profile",
        "--",
        "--dll",
        str(dll),
        "--profile",
        profile,
        "--manifest",
        str(manifest),
    ]
    print(f"\n=== check-api: {profile} (pinned {pinned_version(profile)}) ===")
    proc = subprocess.run(cmd, cwd=_REPO_ROOT)
    return proc.returncode


def main() -> int:
    ap = argparse.ArgumentParser(description="Check API touchpoints against beta sts2.dll")
    ap.add_argument("--repo-root", type=Path, default=_REPO_ROOT)
    ap.add_argument("--manifest", type=Path, default=_MANIFEST)
    args = ap.parse_args()

    load_dotenv(args.repo_root / ".env")

    if not args.manifest.is_file():
        print(f"Manifest missing: {args.manifest}. Run: make extract-touchpoints", file=sys.stderr)
        return 1

    try:
        dlls = format_profile_paths(args.repo_root)
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1

    exit_code = 0
    for profile, dll in dlls.items():
        code = _run_profile(profile, dll, args.manifest)
        if code != 0:
            exit_code = code
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
