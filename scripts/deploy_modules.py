#!/usr/bin/env python3
"""Deploy KitLib as a single mods/KitLib/ bundle (satellite DLLs under modules/)."""

from __future__ import annotations

import argparse
import os
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
ABSTRACTIONS_RUNTIME_DLLS = [
    "Semver.dll",
    "Microsoft.Extensions.Primitives.dll",
]


def _nuget_package_roots() -> list[Path]:
    roots: list[Path] = []
    env = os.environ.get("NUGET_PACKAGES")
    if env:
        roots.append(Path(env))
    repo_packages = _REPO / "packages"
    if repo_packages.is_dir():
        roots.append(repo_packages)
    global_packages = Path.home() / ".nuget" / "packages"
    if global_packages.is_dir():
        roots.append(global_packages)
    return roots


def _resolve_nuget_lib_dll(package_folder: str, dll_name: str) -> Path | None:
    lib_candidates = [
        "lib/net9.0",
        "lib/net8.0",
        "lib/net6.0",
        "lib/net5.0",
        "lib/netstandard2.1",
        "lib/netstandard2.0",
        "lib/netcoreapp3.0",
    ]
    for packages_root in _nuget_package_roots():
        package_dir = packages_root / package_folder
        if not package_dir.is_dir():
            continue
        versions = sorted(package_dir.iterdir(), reverse=True)
        for version_dir in versions:
            if not version_dir.is_dir():
                continue
            for lib_sub in lib_candidates:
                candidate = version_dir / lib_sub / dll_name
                if candidate.is_file():
                    return candidate
    return None


def _resolve_abstractions_dll() -> Path:
    for candidate in (
        _REPO / "build" / ABSTRACTIONS_DLL,
        _REPO / "build" / BUNDLE_ID / ABSTRACTIONS_DLL,
    ):
        if candidate.is_file():
            return candidate
    raise FileNotFoundError(f"Missing {ABSTRACTIONS_DLL} build output. Run dotnet build / make build-all first.")


def _resolve_abstractions_runtime_dll(dll_name: str) -> Path:
    for candidate in (
        _REPO / "build" / dll_name,
        _REPO / "build" / BUNDLE_ID / dll_name,
    ):
        if candidate.is_file():
            return candidate
    package_folder = dll_name[:-4].lower()
    if dll_name == "Microsoft.Extensions.Primitives.dll":
        package_folder = "microsoft.extensions.primitives"
    nuget = _resolve_nuget_lib_dll(package_folder, dll_name)
    if nuget is not None:
        return nuget
    raise FileNotFoundError(f"Missing {dll_name}. Run dotnet restore (repo packages/ or global NuGet cache).")


def _assert_core_bundle(bundle_dir: Path) -> None:
    required = [CORE_DLL, ABSTRACTIONS_DLL, *ABSTRACTIONS_RUNTIME_DLLS]
    missing = [name for name in required if not (bundle_dir / name).is_file()]
    if missing:
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}.")


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


def _copy_file_safe(src: Path, dst: Path) -> bool:
    try:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        return True
    except PermissionError:
        print(
            f"Warning: could not update locked file (close Slay the Spire 2, then re-run sync): {dst}",
            file=sys.stderr,
        )
        return False


def _copy_tree_safe(src_dir: Path, dst: Path) -> list[Path]:
    failed: list[Path] = []
    for item in src_dir.rglob("*"):
        if item.is_dir():
            continue
        rel = item.relative_to(src_dir)
        target = dst / rel
        if not _copy_file_safe(item, target):
            failed.append(target)
    return failed


def _try_reset_bundle_dir(dst: Path) -> bool:
    if not dst.exists():
        dst.mkdir(parents=True)
        return True
    try:
        shutil.rmtree(dst)
        dst.mkdir(parents=True)
        return True
    except PermissionError:
        print(
            f"Note: {dst} is in use; updating files in place. " "Close the game if sync reports locked DLLs.",
            file=sys.stderr,
        )
        dst.mkdir(parents=True, exist_ok=True)
        return False


def _copy_core_bundle(src_dir: Path, dst: Path) -> list[Path]:
    failed: list[Path] = []
    for item in src_dir.iterdir():
        if not _should_deploy_root_item(item):
            continue
        target = dst / item.name
        if item.is_dir():
            failed.extend(_copy_tree_safe(item, target))
        elif not _copy_file_safe(item, target):
            failed.append(target)
    return failed


def _clean_legacy_root_dlls(bundle_dir: Path) -> None:
    for mod_id in BUNDLE_DLLS:
        legacy = bundle_dir / f"{mod_id}.dll"
        if not legacy.is_file():
            continue
        try:
            legacy.unlink()
            print(f"Removed legacy root DLL: {legacy.name}")
        except PermissionError:
            print(
                f"Warning: could not remove locked legacy DLL: {legacy.name}",
                file=sys.stderr,
            )


def _deploy_bundle(mods_root: Path) -> list[Path]:
    dst = mods_root / BUNDLE_ID
    _try_reset_bundle_dir(dst)
    modules_dst = dst / MODULES_SUBDIR
    modules_dst.mkdir(parents=True, exist_ok=True)

    failed: list[Path] = []
    core_src = _REPO / "build" / BUNDLE_ID
    if core_src.is_dir() and any(core_src.iterdir()):
        failed.extend(_copy_core_bundle(core_src, dst))
        bundled_modules = core_src / MODULES_SUBDIR
        if bundled_modules.is_dir():
            for item in bundled_modules.iterdir():
                if item.is_file() and not _copy_file_safe(item, modules_dst / item.name):
                    failed.append(modules_dst / item.name)
    else:
        core_dll = _resolve_dll(BUNDLE_ID)
        if core_dll is None:
            raise FileNotFoundError(f"Missing Core build output under build/{BUNDLE_ID}/ or build/KitLib.dll")
        if not _copy_file_safe(core_dll, dst / "KitLib.dll"):
            failed.append(dst / "KitLib.dll")
        manifest = _REPO / "KitLib.json"
        if manifest.is_file() and not _copy_file_safe(manifest, dst / "mod_manifest.json"):
            failed.append(dst / "mod_manifest.json")

    if not _copy_file_safe(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL):
        failed.append(dst / ABSTRACTIONS_DLL)
    for runtime_dll in ABSTRACTIONS_RUNTIME_DLLS:
        target = dst / runtime_dll
        if not _copy_file_safe(_resolve_abstractions_runtime_dll(runtime_dll), target):
            failed.append(target)
    _assert_core_bundle(dst)

    copied = 0
    for mod_id in BUNDLE_DLLS:
        dll = _resolve_dll(mod_id)
        if dll is None:
            print(f"Note: optional module DLL missing, skipped: {mod_id}.dll")
            continue
        target = modules_dst / f"{mod_id}.dll"
        if _copy_file_safe(dll, target):
            copied += 1
        else:
            failed.append(target)

    _clean_legacy_root_dlls(dst)

    manifest = _REPO / "KitLib.json"
    if manifest.is_file():
        _copy_file_safe(manifest, dst / "mod_manifest.json")

    print(f"Deployed bundle -> {dst} ({copied} satellite DLL(s) in {MODULES_SUBDIR}/)")
    return failed


def _remove_legacy_folders(mods_root: Path) -> None:
    for mod_id in BUNDLE_DLLS:
        legacy = mods_root / mod_id
        if not legacy.exists() or not legacy.is_dir():
            continue
        try:
            shutil.rmtree(legacy)
            print(f"Removed legacy mod folder: {legacy}")
        except PermissionError:
            print(f"Warning: could not remove legacy mod folder: {legacy}", file=sys.stderr)


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
    failed = _deploy_bundle(mods_root)
    if failed:
        names = ", ".join(path.name for path in failed)
        print(
            f"Deploy incomplete: {len(failed)} locked file(s): {names}. " "Close Slay the Spire 2 and run make sync again.",
            file=sys.stderr,
        )
        return 1
    print(f"Done: single mod at {mods_root / BUNDLE_ID} (hot-load from {MODULES_SUBDIR}/)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
