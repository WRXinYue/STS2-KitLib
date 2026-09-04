#!/usr/bin/env python3
"""Deploy KitLib family products to game mods/ (KitLib, KitModPanel, KitDevTools, KitAI)."""

from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.mod_products import (  # noqa: E402
    MODULES_SUBDIR,
    PRODUCT_ORDER,
    PRODUCTS,
    product_build_dir,
)
from lib.steam import read_sts2_dir_from_local_props  # noqa: E402
from lib.sts2_profiles import (  # noqa: E402
    pinned_version,
    resolve_compile_profile,
    resolve_deploy_compat_target,
)

_REPO = _SCRIPT_DIR.parent

_SKIP_DEPLOY_SUFFIXES = {".pdb"}
_SKIP_DEPLOY_NAME_SUFFIXES = (".deps.json", ".runtimeconfig.json")
_SKIP_DEPLOY_NAMES: set[str] = {"GodotSharp.dll"}

CORE_DLL = "KitLib.Core.dll"
FACADE_DLL = "KitLib.Abstractions.dll"


def _has_variant_core(bundle_dir: Path) -> bool:
    lib = bundle_dir / "lib"
    if not lib.is_dir():
        return False
    return any((child / CORE_DLL).is_file() for child in lib.iterdir() if child.is_dir())


def _has_variant_facade(bundle_dir: Path) -> bool:
    lib = bundle_dir / "lib"
    if not lib.is_dir():
        return False
    return any((child / FACADE_DLL).is_file() for child in lib.iterdir() if child.is_dir())


def _assert_kitlib_bundle(bundle_dir: Path) -> None:
    missing = [name for name in ("KitLib.dll",) if not (bundle_dir / name).is_file()]
    if missing:
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}.")
    if not _has_variant_core(bundle_dir):
        raise FileNotFoundError(
            f"KitLib bundle incomplete under {bundle_dir}: missing lib/<api>/{CORE_DLL}."
        )
    if not _has_variant_facade(bundle_dir):
        raise FileNotFoundError(
            f"KitLib bundle incomplete under {bundle_dir}: missing lib/<api>/{FACADE_DLL}."
        )


def _mods_root(game_root: Path) -> Path:
    mac = game_root / "SlayTheSpire2.app" / "Contents" / "MacOS" / "mods"
    if mac.parent.parent.parent.exists():
        return mac
    return game_root / "mods"


def _resolve_satellite_dll(product_id: str, mod_id: str) -> Path | None:
    bundled = product_build_dir(product_id) / MODULES_SUBDIR / f"{mod_id}.dll"
    if bundled.is_file():
        return bundled
    subdir = _REPO / "build" / mod_id / f"{mod_id}.dll"
    if subdir.is_file():
        return subdir
    return None


def _all_satellite_stems() -> set[str]:
    stems: set[str] = set()
    for product in PRODUCTS.values():
        stems.update(product.satellite_dlls)
    return stems


def _should_deploy_root_item(item: Path, satellite_stems: set[str]) -> bool:
    if item.name in (MODULES_SUBDIR, "obj"):
        return False
    if item.name in _SKIP_DEPLOY_NAMES:
        return False
    lower = item.name.lower()
    if any(lower.endswith(suffix) for suffix in _SKIP_DEPLOY_NAME_SUFFIXES):
        return False
    if item.suffix.lower() in _SKIP_DEPLOY_SUFFIXES:
        return False
    if item.suffix.lower() == ".dll" and item.stem in satellite_stems:
        return False
    if item.name == CORE_DLL:
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

    # Flat multi-product layout: remove legacy all-in-one modules leftover when splitting.
    if bundle_dir.name == "KitLib":
        modules = bundle_dir / MODULES_SUBDIR
        if modules.is_dir():
            keep = set(PRODUCTS["KitLib"].satellite_dlls)
            for dll in modules.glob("*.dll"):
                if dll.stem in keep:
                    continue
                try:
                    dll.unlink()
                except OSError:
                    print(f"Warning: could not remove stale satellite {dll.name}", file=sys.stderr)


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


def _copy_build_root(src_dir: Path, dst: Path, satellite_stems: set[str]) -> list[Path]:
    failed: list[Path] = []
    if not src_dir.is_dir():
        return failed
    for item in src_dir.iterdir():
        if not _should_deploy_root_item(item, satellite_stems):
            continue
        target = dst / item.name
        if item.is_dir():
            failed.extend(_copy_tree_safe(item, target))
        elif not _copy_file_safe(item, target):
            failed.append(target)
    return failed


def _deploy_product(mods_root: Path, product_id: str, compat_target: str) -> list[Path]:
    product = PRODUCTS[product_id]
    dst = mods_root / product.id
    _try_reset_bundle_dir(dst)
    modules_dst = dst / MODULES_SUBDIR
    if product.satellite_dlls:
        modules_dst = dst / "lib" / compat_target / MODULES_SUBDIR
        modules_dst.mkdir(parents=True, exist_ok=True)
    failed: list[Path] = []
    satellite_stems = _all_satellite_stems()
    build_dir = product_build_dir(product.id)

    if product.id == "KitLib":
        if not build_dir.is_dir() or not any(build_dir.iterdir()):
            raise FileNotFoundError(f"Missing Core build output under build/{product.id}/. Run make build first.")
        failed.extend(_copy_build_root(build_dir, dst, satellite_stems))
        _assert_kitlib_bundle(dst)
        _remove_stale_legacy_artifacts(dst)
    else:
        entry_src = build_dir / product.entry_dll
        if not entry_src.is_file():
            # Loader may output to build/<id>/ directly after build
            alt = _REPO / "build" / product.id / product.entry_dll
            if alt.is_file():
                entry_src = alt
        if entry_src.is_file():
            if not _copy_file_safe(entry_src, dst / product.entry_dll):
                failed.append(dst / product.entry_dll)
        else:
            print(f"Warning: missing product entry {product.entry_dll} for {product.id}", file=sys.stderr)
        failed.extend(_copy_build_root(build_dir, dst, satellite_stems))

    for mod_id in product.satellite_dlls:
        dll = _resolve_satellite_dll(product.id, mod_id)
        if dll is None:
            print(f"Note: optional module DLL missing, skipped: {mod_id}.dll ({product.id})")
            continue
        target = modules_dst / f"{mod_id}.dll"
        if not _copy_file_safe(dll, target):
            failed.append(target)

    if product.satellite_dlls:
        (modules_dst.parent / "compat-target.txt").write_text(compat_target + "\n", encoding="utf-8")

    if product.manifest_path.is_file():
        _copy_file_safe(product.manifest_path, dst / "mod_manifest.json")

    if not product.satellite_dlls:
        leftover_modules = dst / MODULES_SUBDIR
        if leftover_modules.is_dir():
            try:
                shutil.rmtree(leftover_modules)
            except OSError:
                print(
                    f"Warning: could not remove stale {leftover_modules} "
                    f"(close the game and re-run deploy).",
                    file=sys.stderr,
                )

    print(f"Deployed {product.id} -> {dst}")
    return failed


def main() -> int:
    ap = argparse.ArgumentParser(description="Deploy KitLib product mods to game mods/.")
    ap.add_argument("--game-root", type=Path, default=None, help="STS2 install dir (default: local.props Sts2Dir)")
    ap.add_argument(
        "--product",
        action="append",
        dest="products",
        choices=list(PRODUCT_ORDER),
        help="Deploy only the given product (repeatable). Default: all.",
    )
    args = ap.parse_args()

    game_root = args.game_root
    if game_root is None:
        game_root = read_sts2_dir_from_local_props(_REPO)
    if game_root is None:
        print("Sts2Dir not set. Run make init or pass --game-root.", file=sys.stderr)
        return 1

    game_root = game_root.resolve()
    mods_root = _mods_root(game_root)
    mods_root.mkdir(parents=True, exist_ok=True)

    compat_target = resolve_deploy_compat_target(game_root=game_root, repo_root=_REPO)
    compile_target = pinned_version(resolve_compile_profile(repo_root=_REPO, sts2_dir=game_root))
    if compat_target != compile_target:
        print(
            f"Warning: last build used profile {compile_target} but game is v{compat_target}; "
            f"run make sync again so binaries match the installed game.",
            file=sys.stderr,
        )
    print(f"Deploy compat target: lib/{compat_target}/ (game v{compat_target})")

    products = tuple(args.products) if args.products else PRODUCT_ORDER
    failed: list[Path] = []
    for product_id in products:
        failed.extend(_deploy_product(mods_root, product_id, compat_target))

    if failed:
        names = ", ".join(path.name for path in failed)
        print(
            f"Deploy incomplete: {len(failed)} locked file(s): {names}. Close Slay the Spire 2 and run make sync again.",
            file=sys.stderr,
        )
        return 1
    print(f"Done: deployed {', '.join(products)} under {mods_root}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
