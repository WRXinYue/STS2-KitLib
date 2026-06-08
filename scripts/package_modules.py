#!/usr/bin/env python3
"""Package KitLib Core, Features, satellites, and Full distribution zips."""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

_REPO = Path(__file__).resolve().parent.parent

MOD_PROJECTS = [
    "KitLib.csproj",
    "KitLib.Shared/KitLib.Shared.csproj",
    "KitLib.Features/KitLib.Features.csproj",
    "KitLib.User/KitLib.User.csproj",
    "KitLib.Cheat/KitLib.Cheat.csproj",
    "KitLib.Dev/KitLib.Dev.csproj",
    "KitLib.AI/KitLib.AI.csproj",
    "KitLib.Panel/KitLib.Panel.csproj",
]

SATELLITE_IDS = [
    "KitLib",
    "KitLib.Shared",
    "KitLib.Features",
    "KitLib.User",
    "KitLib.Cheat",
    "KitLib.Dev",
    "KitLib.AI",
    "KitLib.Panel",
]

FULL_MODULES = [
    "KitLib",
    "KitLib.Shared",
    "KitLib.Features",
    "KitLib.User",
    "KitLib.Cheat",
    "KitLib.Dev",
    "KitLib.AI",
    "KitLib.Panel",
]


def _read_version() -> str:
    data = json.loads((_REPO / "KitLib.json").read_text(encoding="utf-8"))
    return str(data["version"])


def _dotnet_build() -> None:
    subprocess.run(
        ["dotnet", "build", str(_REPO / "KitLib.sln")],
        cwd=_REPO,
        check=True,
    )


def _stage_mod(mod_id: str, dist_root: Path) -> Path:
    src = _REPO / "build" / mod_id
    flat_dll = _REPO / "build" / f"{mod_id}.dll"
    dst = dist_root / mod_id
    if dst.exists():
        shutil.rmtree(dst)
    if src.is_dir():
        shutil.copytree(src, dst)
    elif flat_dll.is_file():
        dst.mkdir(parents=True)
        shutil.copy2(flat_dll, dst / f"{mod_id}.dll")
        manifest_src = _REPO / mod_id / f"{mod_id}.json"
        if manifest_src.is_file():
            shutil.copy2(manifest_src, dst / "mod_manifest.json")
    else:
        raise FileNotFoundError(f"Missing build output for {mod_id}: {src} or {flat_dll}")
    return dst


def _zip_dir(src_dir: Path, zip_path: Path) -> None:
    zip_path.parent.mkdir(parents=True, exist_ok=True)
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(src_dir):
            for name in files:
                full = Path(root) / name
                rel = full.relative_to(src_dir.parent)
                zf.write(full, rel.as_posix())


def main() -> int:
    ap = argparse.ArgumentParser(description="Package modular KitLib releases.")
    ap.add_argument("--version", default="", help="Override version (default: KitLib.json)")
    ap.add_argument("--skip-build", action="store_true", help="Use existing build/ artifacts")
    args = ap.parse_args()

    version = args.version.strip() or _read_version()
    dist = _REPO / "build" / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    dist.mkdir(parents=True)

    if not args.skip_build:
        _dotnet_build()

    for mod_id in SATELLITE_IDS:
        _stage_mod(mod_id, dist)

    # Per-module zips
    for mod_id in SATELLITE_IDS:
        if mod_id == "KitLib":
            zip_name = f"KitLib-v{version}.zip"
        else:
            zip_name = f"{mod_id}-v{version}.zip"
        _zip_dir(dist / mod_id, _REPO / "build" / zip_name)

    # Full bundle
    full_root = dist / "KitLib-Full"
    if full_root.exists():
        shutil.rmtree(full_root)
    full_root.mkdir(parents=True)
    readme = full_root / "MODULAR_INSTALL.txt"
    readme.write_text(
        "KitLib modular install\n"
        "======================\n"
        "Extract each subfolder into your STS2 mods directory:\n"
        "  mods/KitLib/\n"
        "  mods/KitLib.Shared/\n"
        "  mods/KitLib.Features/\n"
        "  mods/KitLib.User/ (recommended)\n"
        "  mods/KitLib.Panel/\n"
        "  mods/KitLib.Cheat/\n"
        "  mods/KitLib.Dev/\n"
        "  mods/KitLib.AI/ (optional)\n\n"
        "Minimum for content-mod bridges: KitLib + KitLib.Shared + KitLib.Features\n"
        "Recommended player set: above + KitLib.User\n"
        "Full dev experience: all folders in this archive.\n",
        encoding="utf-8",
    )
    for mod_id in FULL_MODULES:
        shutil.copytree(dist / mod_id, full_root / mod_id)

    full_zip = _REPO / "build" / f"KitLib-Full-v{version}.zip"
    _zip_dir(full_root, full_zip)

    print(f"Packaged Core zip: build/KitLib-v{version}.zip")
    for mod_id in SATELLITE_IDS:
        if mod_id == "KitLib":
            continue
        print(f"Packaged: build/{mod_id}-v{version}.zip")
    print(f"Packaged Full zip: {full_zip.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
