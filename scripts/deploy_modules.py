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

_SKIP_DEPLOY_SUFFIXES = {".pdb"}
_SKIP_DEPLOY_NAME_SUFFIXES = (".deps.json", ".runtimeconfig.json")
_SKIP_DEPLOY_NAMES: set[str] = {"GodotSharp.dll"}

ENTRY_DLL = "KitLib.dll"
CORE_DLL = "KitLib.Core.dll"
ABSTRACTIONS_DLL = "KitLib.Abstractions.dll"
MOD_VARIANT_LOADER_DLL = "KitLib.ModVariantLoader.dll"
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
    candidate = _REPO / "build" / BUNDLE_ID / ABSTRACTIONS_DLL
    if candidate.is_file():
        return candidate
    raise FileNotFoundError(f"Missing {ABSTRACTIONS_DLL} build output. Run dotnet build / make build-all first.")


def _resolve_abstractions_runtime_dll(dll_name: str) -> Path:
    candidate = _REPO / "build" / BUNDLE_ID / dll_name
    if candidate.is_file():
        return candidate
    package_folder = dll_name[:-4].lower()
    if dll_name == "Microsoft.Extensions.Primitives.dll":
        package_folder = "microsoft.extensions.primitives"
    nuget = _resolve_nuget_lib_dll(package_folder, dll_name)
    if nuget is not None:
        return nuget
    raise FileNotFoundError(f"Missing {dll_name}. Run dotnet restore (repo packages/ or global NuGet cache).")


def _resolve_mod_variant_loader_dll() -> Path | None:
    candidate = _REPO / "build" / BUNDLE_ID / MOD_VARIANT_LOADER_DLL
    if candidate.is_file():
        return candidate
    built = _REPO / "src" / "KitLib.ModVariantLoader" / "bin" / "Debug" / "net9.0" / MOD_VARIANT_LOADER_DLL
    if built.is_file():
        return built
    release = built.parent.parent.parent / "Release" / "net9.0" / MOD_VARIANT_LOADER_DLL
    if release.is_file():
        return release
    return None


def _assert_core_bundle(bundle_dir: Path) -> None:
    required = [ENTRY_DLL, CORE_DLL, ABSTRACTIONS_DLL, *ABSTRACTIONS_RUNTIME_DLLS, MOD_VARIANT_LOADER_DLL]
    missing = [name for name in required if not (bundle_dir / name).is_file()]
    if missing:
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}.")


def _mods_root(game_root: Path) -> Path:
    mac = game_root / "SlayTheSpire2.app" / "Contents" / "MacOS" / "mods"
    if mac.parent.parent.parent.exists():
        return mac
    return game_root / "mods"


def _resolve_satellite_dll(mod_id: str) -> Path | None:
    bundled = _REPO / "build" / BUNDLE_ID / MODULES_SUBDIR / f"{mod_id}.dll"
    if bundled.is_file():
        return bundled
    subdir = _REPO / "build" / mod_id / f"{mod_id}.dll"
    if subdir.is_file():
        return subdir
    return None


def _should_deploy_root_item(item: Path) -> bool:
    if item.name in (MODULES_SUBDIR, "lib"):
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


def _remove_stale_legacy_artifacts(bundle_dir: Path) -> None:
    legacy_manifest = bundle_dir / "kitlib-variants.manifest"
    if legacy_manifest.is_file():
        try:
            legacy_manifest.unlink()
        except OSError:
            print(f"Warning: could not remove stale {legacy_manifest.name}", file=sys.stderr)

    legacy_lib = bundle_dir / "lib"
    if legacy_lib.is_dir():
        try:
            shutil.rmtree(legacy_lib)
        except OSError:
            print("Warning: could not remove stale lib/ directory", file=sys.stderr)


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
            f"Note: {dst} is in use; updating files in place. Close the game if sync reports locked DLLs.",
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


def _deploy_bundle(mods_root: Path) -> list[Path]:
    dst = mods_root / BUNDLE_ID
    _try_reset_bundle_dir(dst)
    modules_dst = dst / MODULES_SUBDIR
    modules_dst.mkdir(parents=True, exist_ok=True)

    failed: list[Path] = []
    core_src = _REPO / "build" / BUNDLE_ID
    if not core_src.is_dir() or not any(core_src.iterdir()):
        raise FileNotFoundError(f"Missing Core build output under build/{BUNDLE_ID}/. Run make build first.")

    failed.extend(_copy_core_bundle(core_src, dst))

    if not _copy_file_safe(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL):
        failed.append(dst / ABSTRACTIONS_DLL)
    for runtime_dll in ABSTRACTIONS_RUNTIME_DLLS:
        target = dst / runtime_dll
        if not _copy_file_safe(_resolve_abstractions_runtime_dll(runtime_dll), target):
            failed.append(target)
    variant_loader = _resolve_mod_variant_loader_dll()
    if variant_loader is None:
        raise FileNotFoundError(f"Missing {MOD_VARIANT_LOADER_DLL}. Build KitLib.sln (includes KitLib.ModVariantLoader).")
    if not _copy_file_safe(variant_loader, dst / MOD_VARIANT_LOADER_DLL):
        failed.append(dst / MOD_VARIANT_LOADER_DLL)
    _assert_core_bundle(dst)
    _remove_stale_legacy_artifacts(dst)

    copied = 0
    for mod_id in BUNDLE_DLLS:
        dll = _resolve_satellite_dll(mod_id)
        if dll is None:
            print(f"Note: optional module DLL missing, skipped: {mod_id}.dll")
            continue
        target = modules_dst / f"{mod_id}.dll"
        if _copy_file_safe(dll, target):
            copied += 1
        else:
            failed.append(target)

    manifest = _REPO / "KitLib.json"
    if manifest.is_file():
        _copy_file_safe(manifest, dst / "mod_manifest.json")

    print(f"Deployed bundle -> {dst} ({copied} satellite DLL(s) in {MODULES_SUBDIR}/)")
    return failed


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

    failed = _deploy_bundle(mods_root)
    if failed:
        names = ", ".join(path.name for path in failed)
        print(
            f"Deploy incomplete: {len(failed)} locked file(s): {names}. Close Slay the Spire 2 and run make sync again.",
            file=sys.stderr,
        )
        return 1
    print(f"Done: single mod at {mods_root / BUNDLE_ID} (hot-load from {MODULES_SUBDIR}/)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
