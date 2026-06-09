#!/usr/bin/env python3
"""Package KitLib as a single mods/KitLib/ release zip (satellite DLLs under modules/)."""

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

BUNDLE_ID = "KitLib"
MODULES_SUBDIR = "modules"

BUNDLE_DLLS = [
    "KitLib.User",
    "KitLib.ModPanel",
    "KitLib.Panel",
    "KitLib.Cheat",
    "KitLib.Dev",
    "KitLib.AI",
]

OPTIONAL_MODULE_IDS = BUNDLE_DLLS

CORE_DLL = "KitLib.dll"
ABSTRACTIONS_DLL = "KitLib.Abstractions.dll"


def _resolve_abstractions_dll() -> Path:
    for candidate in (
        _REPO / "build" / ABSTRACTIONS_DLL,
        _REPO / "build" / BUNDLE_ID / ABSTRACTIONS_DLL,
    ):
        if candidate.is_file():
            return candidate
    raise FileNotFoundError(
        f"Missing {ABSTRACTIONS_DLL} build output. Run dotnet build / make build-all first."
    )


def _assert_core_bundle(bundle_dir: Path) -> None:
    missing = [
        name
        for name in (CORE_DLL, ABSTRACTIONS_DLL)
        if not (bundle_dir / name).is_file()
    ]
    if missing:
        raise FileNotFoundError(
            f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}."
        )


def _read_version() -> str:
    data = json.loads((_REPO / "KitLib.json").read_text(encoding="utf-8"))
    return str(data["version"])


def _dotnet_build() -> None:
    subprocess.run(
        ["dotnet", "build", str(_REPO / "KitLib.sln")],
        cwd=_REPO,
        check=True,
    )


def _resolve_dll(mod_id: str) -> Path | None:
    bundled = _REPO / "build" / BUNDLE_ID / MODULES_SUBDIR / f"{mod_id}.dll"
    subdir = _REPO / "build" / mod_id / f"{mod_id}.dll"
    flat = _REPO / "build" / f"{mod_id}.dll"
    if bundled.is_file():
        return bundled
    if subdir.is_file():
        return subdir
    if flat.is_file():
        return flat
    return None


def _stage_bundle(dist_root: Path) -> Path:
    dst = dist_root / BUNDLE_ID
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True)
    modules_dst = dst / MODULES_SUBDIR
    modules_dst.mkdir(parents=True)

    core_src = _REPO / "build" / BUNDLE_ID
    if core_src.is_dir() and any(core_src.iterdir()):
        for item in core_src.iterdir():
            if item.name == MODULES_SUBDIR and item.is_dir():
                for module_file in item.iterdir():
                    if module_file.is_file():
                        shutil.copy2(module_file, modules_dst / module_file.name)
                continue
            if item.suffix.lower() == ".dll" and item.stem in BUNDLE_DLLS:
                continue
            target = dst / item.name
            if item.is_dir():
                shutil.copytree(item, target)
            else:
                shutil.copy2(item, target)
    else:
        core_dll = _resolve_dll(BUNDLE_ID)
        if core_dll is None:
            raise FileNotFoundError("Missing Core build output")
        shutil.copy2(core_dll, dst / "KitLib.dll")
        shutil.copy2(_REPO / "KitLib.json", dst / "mod_manifest.json")

    shutil.copy2(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL)
    _assert_core_bundle(dst)

    for mod_id in BUNDLE_DLLS:
        dll = _resolve_dll(mod_id)
        if dll is not None:
            shutil.copy2(dll, modules_dst / f"{mod_id}.dll")

    readme = dst / "INSTALL.txt"
    readme.write_text(
        "KitLib single-mod install\n"
        "=======================\n"
        "Extract this KitLib/ folder into your STS2 mods directory:\n"
        "  mods/KitLib/\n\n"
        "Required next to KitLib.dll: KitLib.Abstractions.dll (do not delete).\n"
        "Satellite module DLLs live under mods/KitLib/modules/. Core hot-loads\n"
        "them at startup; a module is skipped when missing, conflicting, or\n"
        "failing to initialize.\n\n"
        "Base modules: KitLib.User.dll and KitLib.ModPanel.dll (main-menu Mods settings).\n"
        "Optional: delete other DLLs under modules/ to disable features\n"
        "(e.g. remove modules/KitLib.Panel.dll for no DEVMODE rail; KitLib.AI.dll disables AI host).\n",
        encoding="utf-8",
    )
    return dst


def _stage_optional_module(mod_id: str, dist_root: Path) -> Path | None:
    dll = _resolve_dll(mod_id)
    if dll is None:
        return None
    dst = dist_root / mod_id
    modules_dst = dst / MODULES_SUBDIR
    modules_dst.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dll, modules_dst / f"{mod_id}.dll")
    readme = dst / "INSTALL.txt"
    readme.write_text(
        f"Optional KitLib module: {mod_id}\n" f"Copy modules/{mod_id}.dll into mods/KitLib/modules/.\n",
        encoding="utf-8",
    )
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
    ap = argparse.ArgumentParser(description="Package KitLib single-mod releases.")
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

    bundle_dir = _stage_bundle(dist)

    main_zip = _REPO / "build" / f"KitLib-v{version}.zip"
    _zip_dir(bundle_dir, main_zip)

    full_zip = _REPO / "build" / f"KitLib-Full-v{version}.zip"
    _zip_dir(bundle_dir, full_zip)

    for mod_id in OPTIONAL_MODULE_IDS:
        optional = _stage_optional_module(mod_id, dist)
        if optional is None:
            continue
        _zip_dir(optional, _REPO / "build" / f"{mod_id}-v{version}.zip")

    print(f"Packaged main zip: {main_zip.name}")
    print(f"Packaged full zip: {full_zip.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
