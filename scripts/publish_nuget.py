#!/usr/bin/env python3
"""Build the mod zip, pack a NuGet package, and push to a NuGet feed.

Environment variables:
    NUGET_API_KEY  - API key for the target feed (required unless --dry-run)
    NUGET_SOURCE   - Feed URL (default: https://api.nuget.org/v3/index.json)

Usage:
    python scripts/publish_nuget.py [--version X.Y.Z] [--beta] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.dotenv import load_dotenv  # noqa: E402
from lib import nuget as nuget_ops  # noqa: E402

load_dotenv(_REPO_ROOT / ".env")


def _sts2_beta_version(raw: str) -> str:
    return raw.strip().lstrip("v") or "0.105.1"


def _resolve_sts2_beta_version(cli_value: str) -> str:
    if cli_value.strip():
        return _sts2_beta_version(cli_value)
    return _sts2_beta_version(os.environ.get("STS2_GAME_BETA_VERSION", "0.105.1"))


def main() -> int:
    ap = argparse.ArgumentParser(description="Pack and push DevMode to NuGet.")
    ap.add_argument("--version", default="", help="Semver (default: KitLib.json)")
    ap.add_argument(
        "--beta",
        action="store_true",
        help="Use STS2 Steam beta build (make zip-beta, STS2.KitLib.Beta package id).",
    )
    ap.add_argument(
        "--sts2-beta-version",
        default="",
        help="STS2 beta game version label (default: STS2_GAME_BETA_VERSION or 0.105.1).",
    )
    ap.add_argument(
        "--source",
        default="",
        help="NuGet feed URL (default: NUGET_SOURCE env or nuget.org).",
    )
    ap.add_argument("--api-key", default="", help="NuGet API key (default: NUGET_API_KEY env).")
    ap.add_argument(
        "--skip-build",
        action="store_true",
        help="Skip make zip; require existing build/dist/KitLib/.",
    )
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Build and pack only; do not push.",
    )
    args = ap.parse_args()

    version = args.version.strip()
    if not version:
        manifest = json.loads((_REPO_ROOT / "KitLib.json").read_text(encoding="utf-8"))
        version = str(manifest["version"])
        print(f"Version auto-detected from KitLib.json: {version}")

    sts2_beta_version = _resolve_sts2_beta_version(args.sts2_beta_version)
    pkg_version = nuget_ops.package_version(version, beta=args.beta, sts2_beta_version=sts2_beta_version)

    if not shutil.which("dotnet"):
        print("dotnet not found. Install .NET SDK and ensure it is on PATH.", file=sys.stderr)
        return 1

    if not args.skip_build:
        make_target = "zip-beta" if args.beta else "zip"
        print(f"Building and packaging (make {make_target})...")
        env = os.environ.copy()
        if args.beta:
            env["STS2_GAME_BETA_VERSION"] = sts2_beta_version
        subprocess.run(["make", make_target], cwd=_REPO_ROOT, check=True, env=env)
    else:
        print("Skipping make zip (--skip-build).")

    if args.beta:
        print(f"STS2 Steam beta branch game version: v{sts2_beta_version}")
    print(f"NuGet package version: {pkg_version}")

    try:
        package = nuget_ops.run_pack(
            _REPO_ROOT,
            package_version=pkg_version,
            beta=args.beta,
        )
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1

    print(f"Packed: {package}")

    if args.dry_run:
        print("Dry run — skipping NuGet push.")
        return 0

    try:
        api_key = nuget_ops.resolve_api_key(args.api_key or None)
        source = nuget_ops.resolve_source(args.source or None)
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1

    print(f"Pushing to {source} ...")
    try:
        nuget_ops.run_push(package, source=source, api_key=api_key)
    except subprocess.CalledProcessError:
        print("dotnet nuget push failed", file=sys.stderr)
        return 1

    print(f"Done! Published {package.name} to {source}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
