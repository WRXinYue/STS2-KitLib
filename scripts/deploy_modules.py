#!/usr/bin/env python3
"""Deploy all KitLib module build outputs into the game mods/ folder."""

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

MOD_IDS = [
    "KitLib",
    "KitLib.Shared",
    "KitLib.Features",
    "KitLib.User",
    "KitLib.Cheat",
    "KitLib.Dev",
    "KitLib.AI",
    "KitLib.Panel",
]


def _mods_root(game_root: Path) -> Path:
    mac = game_root / "SlayTheSpire2.app" / "Contents" / "MacOS" / "mods"
    if mac.parent.parent.parent.exists():
        return mac
    return game_root / "mods"


def _resolve_build_source(mod_id: str) -> Path:
    subdir = _REPO / "build" / mod_id
    flat_dll = _REPO / "build" / f"{mod_id}.dll"
    if subdir.is_dir() and any(subdir.iterdir()):
        return subdir
    if flat_dll.is_file():
        return flat_dll
    raise FileNotFoundError(f"Missing build output for {mod_id}: {subdir} or {flat_dll}")


def _deploy_mod(mod_id: str, mods_root: Path) -> None:
    src = _resolve_build_source(mod_id)
    dst = mods_root / mod_id
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True)

    if src.is_dir():
        for item in src.iterdir():
            target = dst / item.name
            if item.is_dir():
                shutil.copytree(item, target)
            else:
                shutil.copy2(item, target)
    else:
        shutil.copy2(src, dst / f"{mod_id}.dll")
        manifest = _REPO / mod_id / f"{mod_id}.json"
        if manifest.is_file():
            shutil.copy2(manifest, dst / "mod_manifest.json")

    if mod_id == "KitLib":
        abstractions = _REPO / "build" / "KitLib.Abstractions.dll"
        if abstractions.is_file():
            shutil.copy2(abstractions, dst / "KitLib.Abstractions.dll")


def main() -> int:
    ap = argparse.ArgumentParser(description="Deploy KitLib modules to game mods/.")
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

    for mod_id in MOD_IDS:
        _deploy_mod(mod_id, mods_root)
        print(f"Deployed {mod_id} -> {mods_root / mod_id}")

    print(f"Done: {len(MOD_IDS)} modules -> {mods_root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
