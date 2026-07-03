#!/usr/bin/env python3
"""Build the mod zip, pack NuGet packages, and push to a NuGet feed.

Publishes:
    STS2.KitLib              — mod content under Content/KitLib/
    STS2.KitLib.ModVariantLoader — runtime loader for dual API variant content mods

Environment variables:
    NUGET_API_KEY  - API key for the target feed (required unless --dry-run)
    NUGET_SOURCE   - Feed URL (default: https://api.nuget.org/v3/index.json)

Usage:
    python scripts/publish_nuget.py [--version X.Y.Z] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.dotenv import load_release_config  # noqa: E402
from lib import nuget as nuget_ops  # noqa: E402

load_release_config(_REPO_ROOT)


def main() -> int:
    ap = argparse.ArgumentParser(description="Pack and push KitLib NuGet packages.")
    ap.add_argument("--version", default="", help="Semver (default: KitLib.json)")
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

    if not shutil.which("dotnet"):
        print("dotnet not found. Install .NET SDK and ensure it is on PATH.", file=sys.stderr)
        return 1

    if not args.skip_build:
        print("Building and packaging (make zip)...")
        subprocess.run(["make", "zip"], cwd=_REPO_ROOT, check=True)
    else:
        print("Skipping make zip (--skip-build).")

    print(f"NuGet package version: {version}")

    try:
        kitlib = nuget_ops.run_pack(_REPO_ROOT, package_version=version)
        abstractions = nuget_ops.run_pack_abstractions(_REPO_ROOT, package_version=version)
        mod_variant_loader = nuget_ops.run_pack_mod_variant_loader(_REPO_ROOT, package_version=version)
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1

    print(f"Packed: {kitlib.name}")
    print(f"Packed: {abstractions.name}")
    print(f"Packed: {mod_variant_loader.name}")

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
    for package in (kitlib, abstractions, mod_variant_loader):
        print(f"  {package.name}")
        try:
            nuget_ops.run_push(package, source=source, api_key=api_key)
        except subprocess.CalledProcessError:
            print(f"dotnet nuget push failed for {package.name}", file=sys.stderr)
            return 1

    print(f"Done! Published {kitlib.name}, {abstractions.name}, and {mod_variant_loader.name} to {source}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
