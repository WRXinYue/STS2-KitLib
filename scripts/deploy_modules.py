#!/usr/bin/env python3
"""Deploy KitLib as a single mods/KitLib/ bundle (satellite DLLs under modules/)."""

from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.steam import read_sts2_dir_from_local_props  # noqa: E402

_REPO = _SCRIPT_DIR.parent

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

LEGACY_MOD_FOLDERS = [BUNDLE_ID, *BUNDLE_DLLS]

# Build artifacts that must not land in mods/KitLib/ (game treats some as mod manifests).
_SKIP_DEPLOY_SUFFIXES = {".pdb"}
_SKIP_DEPLOY_NAME_SUFFIXES = (".deps.json", ".runtimeconfig.json")
_SKIP_DEPLOY_NAMES: set[str] = set()

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


def _mods_root(game_root: Path) -> Path:
    mac = game_root / "SlayTheSpire2.app" / "Contents" / "MacOS" / "mods"
    if mac.parent.parent.parent.exists():
        return mac
    return game_root / "mods"


def _resolve_dll(mod_id: str) -> Path | None:
    subdir = _REPO / "build" / mod_id / f"{mod_id}.dll"
    flat = _REPO / "build" / f"{mod_id}.dll"
    bundled = _REPO / "build" / BUNDLE_ID / MODULES_SUBDIR / f"{mod_id}.dll"
    if bundled.is_file():
        return bundled
    if subdir.is_file():
        return subdir
    if flat.is_file():
        return flat
    return None


def _should_deploy_root_item(item: Path) -> bool:
    if item.name == MODULES_SUBDIR and item.is_dir():
        return False
    if item.name in _SKIP_DEPLOY_NAMES:
        return False
    lower = item.name.lower()
    if any(lower.endswith(suffix) for suffix in _SKIP_DEPLOY_NAME_SUFFIXES):
        return False
    if item.suffix.lower() in _SKIP_DEPLOY_SUFFIXES:
        return False
    if item.suffix.lower() == ".dll" and item.stem in BUNDLE_DLLS:
        return False
    return True


def _copy_core_bundle(src_dir: Path, dst: Path) -> None:
    for item in src_dir.iterdir():
        if not _should_deploy_root_item(item):
            continue
        target = dst / item.name
        if item.is_dir():
            if target.exists():
                shutil.rmtree(target)
            shutil.copytree(item, target)
        else:
            shutil.copy2(item, target)


def _clean_legacy_root_dlls(bundle_dir: Path) -> None:
    for mod_id in BUNDLE_DLLS:
        legacy = bundle_dir / f"{mod_id}.dll"
        if legacy.is_file():
            legacy.unlink()
            print(f"Removed legacy root DLL: {legacy.name}")


def _deploy_bundle(mods_root: Path) -> None:
    dst = mods_root / BUNDLE_ID
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True)
    modules_dst = dst / MODULES_SUBDIR
    modules_dst.mkdir(parents=True)

    core_src = _REPO / "build" / BUNDLE_ID
    if core_src.is_dir() and any(core_src.iterdir()):
        _copy_core_bundle(core_src, dst)
        bundled_modules = core_src / MODULES_SUBDIR
        if bundled_modules.is_dir():
            for item in bundled_modules.iterdir():
                if item.is_file():
                    shutil.copy2(item, modules_dst / item.name)
    else:
        core_dll = _resolve_dll(BUNDLE_ID)
        if core_dll is None:
            raise FileNotFoundError(f"Missing Core build output under build/{BUNDLE_ID}/ or build/KitLib.dll")
        shutil.copy2(core_dll, dst / "KitLib.dll")
        manifest = _REPO / "KitLib.json"
        if manifest.is_file():
            shutil.copy2(manifest, dst / "mod_manifest.json")

    shutil.copy2(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL)
    _assert_core_bundle(dst)

    copied = 0
    for mod_id in BUNDLE_DLLS:
        dll = _resolve_dll(mod_id)
        if dll is None:
            print(f"Note: optional module DLL missing, skipped: {mod_id}.dll")
            continue
        shutil.copy2(dll, modules_dst / f"{mod_id}.dll")
        copied += 1

    _clean_legacy_root_dlls(dst)

    manifest = _REPO / "KitLib.json"
    if manifest.is_file():
        shutil.copy2(manifest, dst / "mod_manifest.json")

    print(f"Deployed bundle -> {dst} ({copied} satellite DLL(s) in {MODULES_SUBDIR}/)")


def _remove_legacy_folders(mods_root: Path) -> None:
    for mod_id in BUNDLE_DLLS:
        legacy = mods_root / mod_id
        if legacy.exists() and legacy.is_dir():
            shutil.rmtree(legacy)
            print(f"Removed legacy mod folder: {legacy}")


def main() -> int:
    ap = argparse.ArgumentParser(description="Deploy KitLib bundle to game mods/KitLib/.")
    ap.add_argument("--game-root", type=Path, default=None, help="STS2 install dir (default: local.props Sts2Dir)")
    args = ap.parse_args()

    game_root = args.game_root
    if game_root is None:
        game_root = read_sts2_dir_from_local_props(_REPO)
    if game_root is None:
        print("Sts2Dir not set. Run make init or pass --game-root.", file=sys.stderr)
        return 1

    mods_root = _mods_root(game_root.resolve())
    mods_root.mkdir(parents=True, exist_ok=True)

    _remove_legacy_folders(mods_root)
    _deploy_bundle(mods_root)
    print(f"Done: single mod at {mods_root / BUNDLE_ID} (hot-load from {MODULES_SUBDIR}/)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
