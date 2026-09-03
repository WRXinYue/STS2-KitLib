#!/usr/bin/env python3
"""Legacy flat-layout staging helper (prefer package_bundle.py --zip-all for release zips)."""

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
if str(_REPO / "scripts") not in sys.path:
    sys.path.insert(0, str(_REPO / "scripts"))

from lib.bundle_build import build_bundle  # noqa: E402
from lib.mod_products import MODULES_SUBDIR, PRODUCT_ORDER, PRODUCTS, product_build_dir  # noqa: E402
from lib.release_assets import RELEASE_PROFILES, mod_zip_path  # noqa: E402

CORE_DLL = "KitLib.Core.dll"
ABSTRACTIONS_DLL = "KitLib.Abstractions.dll"
ABSTRACTIONS_RUNTIME_DLLS = [
    "Semver.dll",
    "Microsoft.Extensions.Primitives.dll",
]

_SKIP_PACKAGE_NAMES = {"GodotSharp.dll"}
_SKIP_PACKAGE_ROOT_NAMES = {
    "kitlib-variants.manifest",
    MODULES_SUBDIR,
}
_SKIP_PACKAGE_SUFFIXES = {".pdb"}
_SKIP_PACKAGE_NAME_SUFFIXES = (".deps.json", ".runtimeconfig.json")


def _all_satellite_stems() -> set[str]:
    stems: set[str] = set()
    for product in PRODUCTS.values():
        stems.update(product.satellite_dlls)
    return stems


def _resolve_abstractions_dll() -> Path:
    candidate = product_build_dir("KitLib") / ABSTRACTIONS_DLL
    if candidate.is_file():
        return candidate
    raise FileNotFoundError(f"Missing {ABSTRACTIONS_DLL} build output. Run make build first.")


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


def _resolve_abstractions_runtime_dll(dll_name: str) -> Path:
    candidate = product_build_dir("KitLib") / dll_name
    if candidate.is_file():
        return candidate
    package_folder = dll_name[:-4].lower()
    if dll_name == "Microsoft.Extensions.Primitives.dll":
        package_folder = "microsoft.extensions.primitives"
    nuget = _resolve_nuget_lib_dll(package_folder, dll_name)
    if nuget is not None:
        return nuget
    raise FileNotFoundError(f"Missing {dll_name}. Run dotnet restore.")


def _has_variant_core(bundle_dir: Path) -> bool:
    lib = bundle_dir / "lib"
    if not lib.is_dir():
        return False
    return any((child / CORE_DLL).is_file() for child in lib.iterdir() if child.is_dir())


def _assert_kitlib_bundle(bundle_dir: Path) -> None:
    required = ["KitLib.dll", ABSTRACTIONS_DLL, *ABSTRACTIONS_RUNTIME_DLLS]
    missing = [name for name in required if not (bundle_dir / name).is_file()]
    if missing:
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}.")
    if not _has_variant_core(bundle_dir):
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing lib/<api>/{CORE_DLL}.")


def _read_version() -> str:
    data = json.loads((_REPO / "KitLib.json").read_text(encoding="utf-8"))
    return str(data["version"])


def _dotnet_build(*, configuration: str, sts2_profile: str) -> None:
    sts2_dir = subprocess.check_output(
        [sys.executable, str(_REPO / "scripts" / "resolve_sts2_profile_dir.py"), sts2_profile],
        text=True,
    ).strip()
    build_bundle(configuration=configuration, sts2_profile=sts2_profile, sts2_dir=sts2_dir)


def _should_package_root_item(item: Path, satellite_stems: set[str]) -> bool:
    if item.name in _SKIP_PACKAGE_NAMES:
        return False
    if item.name in _SKIP_PACKAGE_ROOT_NAMES:
        return False
    if item.name.endswith("-variants.manifest"):
        return False
    lower = item.name.lower()
    if any(lower.endswith(suffix) for suffix in _SKIP_PACKAGE_NAME_SUFFIXES):
        return False
    if item.suffix.lower() in _SKIP_PACKAGE_SUFFIXES:
        return False
    if item.suffix.lower() == ".dll" and item.stem in satellite_stems:
        return False
    return True


def _resolve_satellite_dll(product_id: str, mod_id: str) -> Path | None:
    bundled = product_build_dir(product_id) / MODULES_SUBDIR / f"{mod_id}.dll"
    if bundled.is_file():
        return bundled
    subdir = _REPO / "build" / mod_id / f"{mod_id}.dll"
    if subdir.is_file():
        return subdir
    return None


def _stage_product(dist_root: Path, product_id: str) -> Path:
    product = PRODUCTS[product_id]
    dst = dist_root / product.id
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True)
    modules_dst = dst / MODULES_SUBDIR
    modules_dst.mkdir(parents=True)
    satellite_stems = _all_satellite_stems()
    build_dir = product_build_dir(product.id)

    if product.id == "KitLib":
        if not build_dir.is_dir() or not any(build_dir.iterdir()):
            raise FileNotFoundError("Missing Core build output under build/KitLib/. Run make build first.")
        for item in build_dir.iterdir():
            if not _should_package_root_item(item, satellite_stems):
                continue
            target = dst / item.name
            if item.is_dir():
                shutil.copytree(item, target)
            else:
                shutil.copy2(item, target)
        shutil.copy2(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL)
        for runtime_dll in ABSTRACTIONS_RUNTIME_DLLS:
            shutil.copy2(_resolve_abstractions_runtime_dll(runtime_dll), dst / runtime_dll)
        _assert_kitlib_bundle(dst)
    else:
        entry = build_dir / product.entry_dll
        if entry.is_file():
            shutil.copy2(entry, dst / product.entry_dll)
        for item in build_dir.iterdir() if build_dir.is_dir() else []:
            if not _should_package_root_item(item, satellite_stems):
                continue
            target = dst / item.name
            if item.is_dir():
                shutil.copytree(item, target)
            else:
                shutil.copy2(item, target)

    for mod_id in product.satellite_dlls:
        dll = _resolve_satellite_dll(product.id, mod_id)
        if dll is None:
            print(f"Note: optional module DLL missing, skipped: {mod_id}.dll ({product.id})")
            continue
        shutil.copy2(dll, modules_dst / f"{mod_id}.dll")

    if product.manifest_path.is_file():
        shutil.copy2(product.manifest_path, dst / "mod_manifest.json")

    return dst


def _stage_all_products(dist_root: Path) -> Path:
    for product_id in PRODUCT_ORDER:
        _stage_product(dist_root, product_id)
    return dist_root


def _zip_products(stage_root: Path, zip_path: Path) -> None:
    zip_path.parent.mkdir(parents=True, exist_ok=True)
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for product_id in PRODUCT_ORDER:
            product_dir = stage_root / product_id
            if not product_dir.is_dir():
                continue
            for root, _, files in os.walk(product_dir):
                for name in files:
                    full = Path(root) / name
                    rel = full.relative_to(stage_root)
                    zf.write(full, rel.as_posix())


def _package_release(
    version: str,
    *,
    configuration: str,
    skip_build: bool,
) -> Path:
    dist = _REPO / "build" / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    dist.mkdir(parents=True)

    if not skip_build:
        _dotnet_build(configuration=configuration, sts2_profile="beta")

    _stage_all_products(dist)
    zip_path = mod_zip_path(_REPO, version)
    _zip_products(dist, zip_path)
    print(f"Packaged release zip: {zip_path.name} ({', '.join(PRODUCT_ORDER)})")
    return zip_path


def main() -> int:
    ap = argparse.ArgumentParser(description="Package KitLib multi-product releases.")
    ap.add_argument("--version", default="", help="Override version (default: KitLib.json)")
    ap.add_argument("--skip-build", action="store_true", help="Use existing build/ artifacts")
    ap.add_argument(
        "-c",
        "--configuration",
        default="Debug",
        help="dotnet build configuration (use Release for publish zips)",
    )
    ap.add_argument(
        "--sts2-profile",
        choices=RELEASE_PROFILES,
        default="",
        help="Ignored; KitLib always builds against beta refs.",
    )
    ap.add_argument(
        "--stage-dir",
        type=Path,
        default=None,
        help="Stage all product trees under STAGE-DIR/ and exit (no zip).",
    )
    args = ap.parse_args()

    if args.stage_dir is not None:
        if not args.skip_build:
            _dotnet_build(configuration=args.configuration, sts2_profile="beta")
        stage_root = args.stage_dir.resolve()
        if stage_root.exists():
            shutil.rmtree(stage_root)
        stage_root.mkdir(parents=True)
        _stage_all_products(stage_root)
        print(f"Staged products: {stage_root}")
        return 0

    version = args.version.strip() or _read_version()
    _package_release(
        version,
        configuration=args.configuration,
        skip_build=args.skip_build,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
