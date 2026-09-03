#!/usr/bin/env python3
"""Build a multi-API KitLib variant pack (loader + lib/<api>/Core + modules).

Inspired by LustTravel2's package_bundle.py.

Layout:
  build/KitLib-release/
    KitLib.dll
    KitLib.Abstractions.dll, KitLib.ModVariantLoader.dll
    Semver.dll, Microsoft.Extensions.Primitives.dll
    mod_manifest.json
    lib/0.107.1/KitLib.Core.dll, modules/*.dll, compat-target.txt
    lib/0.110.1/...
  build/KitModPanel-release/, KitDevTools-release/, KitAI-release/:
    <Product>.dll, mod_manifest.json
    lib/<api>/<Product>.dll (KitModPanel) or lib/<api>/modules/*.dll

Usage:
  python scripts/package_bundle.py
  python scripts/package_bundle.py --no-zip
  python scripts/package_bundle.py --zip-only
  python scripts/package_bundle.py --deploy
  python scripts/package_bundle.py --stage-dir build/steam-stage
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO = _SCRIPT_DIR.parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.bundle_build import build_bundle  # noqa: E402
from lib.dotenv import load_dotenv  # noqa: E402
from lib.mod_products import PRODUCT_ORDER, PRODUCTS, product_build_dir  # noqa: E402
from lib.release_assets import product_zip_path, read_product_version  # noqa: E402
from lib.sts2_profiles import (  # noqa: E402
    VARIANT_TARGETS,
    resolve_profile_dir,
    variant_profile,
)

BUNDLE_ID = "KitLib"
STAGING_DIR = _REPO / "build" / f"{BUNDLE_ID}-release"
BUILD_DIR = _REPO / "build" / BUNDLE_ID
LOADER_PROJECT = _REPO / "src" / "KitLib.Loader" / "KitLib.Loader.csproj"
VARIANT_LOADER_PROJECT = _REPO / "src" / "KitLib.ModVariantLoader" / "KitLib.ModVariantLoader.csproj"
MANIFEST_SRC = _REPO / "KitLib.json"
COMPAT_MARKER = "compat-target.txt"
CORE_DLL = "KitLib.Core.dll"
VARIANT_LOADER_DLL = "KitLib.ModVariantLoader.dll"
MODULES_SUBDIR = "modules"
SIBLING_VARIANT_PRODUCTS = ("KitModPanel", "KitDevTools", "KitAI")
SHARED_ROOT_FILES = [
    "KitLib.Abstractions.dll",
    "Semver.dll",
    "Microsoft.Extensions.Primitives.dll",
]
_ZIP_ROOT_FILES = [
    "KitLib.dll",
    *SHARED_ROOT_FILES,
    VARIANT_LOADER_DLL,
    "mod_manifest.json",
    "mod_image.png",
]


def fail(msg: str) -> None:
    raise SystemExit(msg)


def _dotnet(args: list[str]) -> None:
    cmd = ["dotnet", *args]
    print("+", " ".join(cmd))
    subprocess.run(cmd, cwd=_REPO, check=True)


def _read_version() -> str:
    data = json.loads(MANIFEST_SRC.read_text(encoding="utf-8"))
    return str(data["version"])


def _read_mods_dir() -> Path | None:
    props = _REPO / "local.props"
    if not props.is_file():
        return None
    text = props.read_text(encoding="utf-8", errors="replace")
    import re

    match = re.search(r"<Sts2ModsRoot>([^<]+)</Sts2ModsRoot>", text)
    if match:
        return Path(os.path.expandvars(match.group(1).strip())).expanduser()
    match = re.search(r"<Sts2Dir>([^<]+)</Sts2Dir>", text)
    if not match:
        return None
    sts2_dir = Path(os.path.expandvars(match.group(1).strip())).expanduser()
    mods = sts2_dir / "mods"
    return mods if mods.is_dir() else sts2_dir / "mods"


def _sibling_staging(product_id: str) -> Path:
    return _REPO / "build" / f"{product_id}-release"


def _resolve_mod_image(product_id: str) -> Path | None:
    candidates = [
        _REPO / "mods" / product_id / "mod_image.png",
        _REPO / "mods" / product_id / "src" / "Panel" / "Assets" / "mod_image.png",
    ]
    if product_id == "KitLib":
        candidates.append(_REPO / "assets" / "mod_image.png")
    for src in candidates:
        if src.is_file():
            return src
    return None


def _copy_mod_image(product_id: str, dest: Path) -> None:
    src = _resolve_mod_image(product_id)
    if src is None:
        return
    shutil.copy2(src, dest / "mod_image.png")


def _resolve_satellite_dll(product_id: str, dll_name: str) -> Path | None:
    bundled = product_build_dir(product_id) / MODULES_SUBDIR / f"{dll_name}.dll"
    if bundled.is_file():
        return bundled
    alt = _REPO / "build" / dll_name / f"{dll_name}.dll"
    return alt if alt.is_file() else None


def _clear_staging(*, product_id: str | None = None) -> None:
    if product_id in (None, "KitLib"):
        if STAGING_DIR.exists():
            shutil.rmtree(STAGING_DIR)
        STAGING_DIR.mkdir(parents=True)
    sibling_ids = SIBLING_VARIANT_PRODUCTS
    if product_id in SIBLING_VARIANT_PRODUCTS:
        sibling_ids = (product_id,)
    elif product_id == "KitLib":
        sibling_ids = ()
    for product in sibling_ids:
        dest = _sibling_staging(product)
        if dest.exists():
            shutil.rmtree(dest)
        dest.mkdir(parents=True)


def _stage_variant(
    compat: str,
    profile: str,
    *,
    configuration: str,
    product_id: str | None = None,
) -> None:
    sts2_dir = resolve_profile_dir(profile, repo_root=_REPO)
    build_bundle(
        configuration=configuration,
        sts2_profile=profile,
        sts2_dir=str(sts2_dir),
        kitlib_personal_compat=True,
        product_id=product_id,
    )

    if product_id not in SIBLING_VARIANT_PRODUCTS:
        variant_dir = STAGING_DIR / "lib" / compat
        modules_dst = variant_dir / MODULES_SUBDIR
        variant_dir.mkdir(parents=True)
        modules_dst.mkdir(parents=True)

        core_src = BUILD_DIR / "lib" / compat / CORE_DLL
        if not core_src.is_file():
            fail(f"Missing {core_src} after build for {compat} ({profile}).")

        shutil.copy2(core_src, variant_dir / CORE_DLL)
        # KitLib product owns User only; other satellites ship in sibling product mods.
        for dll_name in PRODUCTS["KitLib"].satellite_dlls:
            src = product_build_dir("KitLib") / MODULES_SUBDIR / f"{dll_name}.dll"
            if not src.is_file():
                src = _REPO / "build" / dll_name / f"{dll_name}.dll"
            if src.is_file():
                shutil.copy2(src, modules_dst / f"{dll_name}.dll")
            else:
                print(f"[bundle] Warning: missing {dll_name}.dll for variant {compat}")

        (variant_dir / COMPAT_MARKER).write_text(compat + "\n", encoding="utf-8", newline="\n")
        print(f"[bundle] Staged variant {compat} ({profile})")

    if product_id is None:
        _stage_sibling_variant(compat)
    elif product_id in SIBLING_VARIANT_PRODUCTS:
        _stage_sibling_variant(compat, product_id=product_id)


def _stage_sibling_variant(compat: str, *, product_id: str | None = None) -> None:
    product_ids = SIBLING_VARIANT_PRODUCTS
    if product_id in SIBLING_VARIANT_PRODUCTS:
        product_ids = (product_id,)
    for pid in product_ids:
        product = PRODUCTS[pid]
        variant_dir = _sibling_staging(pid) / "lib" / compat
        variant_dir.mkdir(parents=True)
        if product.variant_implementation:
            impl = product_build_dir(pid) / "lib" / compat / product.entry_dll
            if not impl.is_file():
                fail(f"Missing variant implementation {impl} for {pid}.")
            shutil.copy2(impl, variant_dir / product.entry_dll)
        else:
            modules_dst = variant_dir / MODULES_SUBDIR
            modules_dst.mkdir(parents=True)
            for dll_name in product.satellite_dlls:
                src = _resolve_satellite_dll(pid, dll_name)
                if src is None:
                    print(f"[bundle] Warning: missing {dll_name}.dll for {pid} variant {compat}")
                    continue
                shutil.copy2(src, modules_dst / f"{dll_name}.dll")
        (variant_dir / COMPAT_MARKER).write_text(compat + "\n", encoding="utf-8", newline="\n")
        print(f"[bundle] Staged {pid} variant {compat}")


def _stage_sibling_roots(*, product_id: str | None = None) -> None:
    product_ids = SIBLING_VARIANT_PRODUCTS
    if product_id in SIBLING_VARIANT_PRODUCTS:
        product_ids = (product_id,)
    elif product_id == "KitLib":
        return
    for pid in product_ids:
        product = PRODUCTS[pid]
        staging = _sibling_staging(pid)
        build_dir = product_build_dir(pid)
        entry = build_dir / product.entry_dll
        if not entry.is_file():
            fail(f"Missing {entry} for {pid}.")
        shutil.copy2(entry, staging / product.entry_dll)
        if product.manifest_path.is_file():
            shutil.copy2(product.manifest_path, staging / "mod_manifest.json")
        _copy_mod_image(pid, staging)


def _stage_shared_root(*, configuration: str) -> None:
    for name in SHARED_ROOT_FILES:
        src = BUILD_DIR / name
        if not src.is_file():
            src = _REPO / "src" / "KitLib.Abstractions" / "bin" / configuration / "net9.0" / name
        if not src.is_file():
            fail(f"Missing shared DLL {name}. Run dotnet restore and retry.")
        shutil.copy2(src, STAGING_DIR / name)

    if MANIFEST_SRC.is_file():
        shutil.copy2(MANIFEST_SRC, STAGING_DIR / "mod_manifest.json")
    _copy_mod_image("KitLib", STAGING_DIR)


def _build_loader(*, configuration: str, profile: str) -> None:
    sts2_dir = resolve_profile_dir(profile, repo_root=_REPO)
    _dotnet(
        [
            "build",
            str(LOADER_PROJECT),
            "-c",
            configuration,
            "-v:q",
            f"-p:Sts2Dir={sts2_dir}",
            f"-p:Sts2Profile={profile}",
            "-p:KitLibPersonalCompat=true",
        ]
    )
    loader_out = BUILD_DIR / "KitLib.dll"
    if not loader_out.is_file():
        fail(f"Loader build did not produce {loader_out}")
    shutil.copy2(loader_out, STAGING_DIR / "KitLib.dll")

    _dotnet(
        [
            "build",
            str(VARIANT_LOADER_PROJECT),
            "-c",
            configuration,
            "-v:q",
            f"-p:Sts2Dir={sts2_dir}",
            f"-p:Sts2Profile={profile}",
            "-p:KitLibPersonalCompat=true",
        ]
    )
    variant_src = BUILD_DIR / VARIANT_LOADER_DLL
    if not variant_src.is_file():
        variant_src = (
            _REPO / "src" / "KitLib.ModVariantLoader" / "bin" / configuration / "net9.0" / VARIANT_LOADER_DLL
        )
    if not variant_src.is_file():
        fail(f"ModVariantLoader build did not produce {variant_src}")
    shutil.copy2(variant_src, STAGING_DIR / VARIANT_LOADER_DLL)


def _assert_staging(staging: Path, *, product_id: str | None = None) -> None:
    bundle_product = product_id or BUNDLE_ID
    product = PRODUCTS.get(bundle_product)
    if bundle_product in SIBLING_VARIANT_PRODUCTS and product is not None:
        required = [
            staging / product.entry_dll,
            staging / "mod_manifest.json",
            staging / "lib",
        ]
    else:
        required = [
            staging / "KitLib.dll",
            staging / "mod_manifest.json",
            staging / "lib",
            staging / VARIANT_LOADER_DLL,
        ]
    missing = [str(path.relative_to(_REPO)) for path in required if not path.exists()]
    if missing:
        fail(f"Staging incomplete: {', '.join(missing)}")


def build_bundle_tree(
    *,
    configuration: str = "Release",
    skip_build: bool = False,
    targets: list[tuple[str, str]] | None = None,
    product_id: str | None = None,
) -> Path:
    if product_id is not None and product_id not in PRODUCTS:
        fail(f"Unknown product: {product_id}")

    if skip_build:
        if product_id in SIBLING_VARIANT_PRODUCTS:
            _assert_staging(_sibling_staging(product_id), product_id=product_id)
            return _sibling_staging(product_id)
        _assert_staging(STAGING_DIR, product_id=product_id)
        return STAGING_DIR

    targets = targets or VARIANT_TARGETS
    for compat, _profile in targets:
        resolve_profile_dir(variant_profile(compat), repo_root=_REPO)

    _clear_staging(product_id=product_id)

    for compat, profile in targets:
        _stage_variant(compat, profile, configuration=configuration, product_id=product_id)

    if product_id in (None, "KitLib"):
        _stage_shared_root(configuration=configuration)
        _build_loader(configuration=configuration, profile=targets[-1][1])
    _stage_sibling_roots(product_id=product_id)

    staging = STAGING_DIR if product_id in (None, "KitLib") else _sibling_staging(product_id)
    print(f"[bundle] Staging ready: {staging}")
    return staging


def _iter_zip_entries(staging: Path) -> list[tuple[Path, str]]:
    entries: list[tuple[Path, str]] = []
    for name in _ZIP_ROOT_FILES:
        path = staging / name
        if path.is_file():
            entries.append((path, f"{BUNDLE_ID}/{name}"))

    lib_root = staging / "lib"
    if not lib_root.is_dir():
        return entries

    for lib_dir in sorted(p for p in lib_root.iterdir() if p.is_dir()):
        for rel_name in (CORE_DLL, COMPAT_MARKER):
            path = lib_dir / rel_name
            if path.is_file():
                arc = path.relative_to(staging).as_posix()
                entries.append((path, f"{BUNDLE_ID}/{arc}"))
        modules = lib_dir / MODULES_SUBDIR
        if modules.is_dir():
            for module_dll in sorted(modules.glob("*.dll")):
                arc = module_dll.relative_to(staging).as_posix()
                entries.append((module_dll, f"{BUNDLE_ID}/{arc}"))
    return entries


def _staging_path(product_id: str) -> Path:
    if product_id == BUNDLE_ID:
        return STAGING_DIR
    return _sibling_staging(product_id)


def _iter_product_zip_entries(staging: Path, product_id: str) -> list[tuple[Path, str]]:
    if product_id == BUNDLE_ID:
        return _iter_zip_entries(staging)

    entries: list[tuple[Path, str]] = []
    for path in sorted(p for p in staging.rglob("*") if p.is_file()):
        arc = path.relative_to(staging).as_posix()
        entries.append((path, f"{product_id}/{arc}"))
    return entries


def package_product_zip(staging: Path, product_id: str, *, version: str = "") -> Path:
    _assert_staging(staging, product_id=product_id)
    entries = _iter_product_zip_entries(staging, product_id)
    if not entries:
        fail(f"No files to package for {product_id}.")

    version = version.strip() or read_product_version(_REPO, product_id)
    zip_path = product_zip_path(_REPO, product_id, version)
    zip_path.parent.mkdir(parents=True, exist_ok=True)

    fd, tmp = tempfile.mkstemp(prefix="zip-", suffix=".tmp", dir=zip_path.parent)
    os.close(fd)
    try:
        with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as zf:
            for src, arc in entries:
                zf.write(src, arc)
        os.replace(tmp, zip_path)
    except BaseException:
        try:
            if os.path.isfile(tmp):
                os.remove(tmp)
        except OSError:
            pass
        raise

    print(f"[bundle] Packaged {zip_path.name} ({len(entries)} files)")
    return zip_path


def package_zip(staging: Path, *, version: str = "") -> Path:
    return package_product_zip(staging, BUNDLE_ID, version=version)


def package_all_product_zips() -> list[Path]:
    zips: list[Path] = []
    for product_id in PRODUCT_ORDER:
        staging = _staging_path(product_id)
        zips.append(package_product_zip(staging, product_id))
    return zips


def _copy_staging_to(stage_root: Path, *, product_id: str | None = None) -> Path:
    bundle_id = product_id or BUNDLE_ID
    if bundle_id == BUNDLE_ID:
        source = STAGING_DIR
    else:
        source = _sibling_staging(bundle_id)
    bundle_dir = stage_root / bundle_id
    if stage_root.exists():
        shutil.rmtree(stage_root)
    stage_root.mkdir(parents=True)
    shutil.copytree(source, bundle_dir)
    print(f"[bundle] Staged bundle: {bundle_dir}")
    return bundle_dir


def _deploy_to_game() -> None:
    mods_root = _read_mods_dir()
    if mods_root is None:
        fail("Cannot resolve game mods dir. Run make init (local.props) or set Sts2Dir.")
    dest = mods_root / BUNDLE_ID
    if dest.exists():
        shutil.rmtree(dest)
    shutil.copytree(STAGING_DIR, dest)
    print(f"[bundle] Deployed KitLib multi-API pack to {dest}")
    for product_id in SIBLING_VARIANT_PRODUCTS:
        src = _sibling_staging(product_id)
        sibling_dest = mods_root / product_id
        if sibling_dest.exists():
            shutil.rmtree(sibling_dest)
        shutil.copytree(src, sibling_dest)
        print(f"[bundle] Deployed {product_id} multi-API pack to {sibling_dest}")


def main() -> int:
    ap = argparse.ArgumentParser(description="Build multi-API KitLib release bundle.")
    ap.add_argument("-c", "--configuration", default="Release")
    ap.add_argument("--skip-build", action="store_true", help="Use existing build/KitLib-release/")
    ap.add_argument("--deploy", action="store_true", help="Copy bundle into game mods/KitLib/")
    ap.add_argument(
        "--stage-dir",
        type=Path,
        default=None,
        help="Copy mods/KitLib/ tree to STAGE-DIR/KitLib/ (no zip).",
    )
    ap.add_argument("--zip-only", action="store_true", help="Zip existing build/*-release/ staging (KitLib only unless --zip-all)")
    ap.add_argument("--zip-all", action="store_true", help="Zip every product into build/<Product>-vX.Y.Z.zip")
    ap.add_argument("--no-zip", action="store_true", help="Build staging only; do not write release zip")
    ap.add_argument("--version", default="", help="Override version for zip name (default: KitLib.json)")
    ap.add_argument(
        "--targets",
        default="",
        help="Comma-separated compat targets (default: 0.107.1,0.110.1)",
    )
    ap.add_argument(
        "--product",
        default="",
        help="Build/stage one product only (KitLib, KitModPanel, KitDevTools, KitAI). Default: all products.",
    )
    args = ap.parse_args()
    load_dotenv(_REPO / ".env")

    product_id = args.product.strip() or None
    if product_id is not None and product_id not in PRODUCTS:
        fail(f"Unknown product: {product_id}")

    targets = VARIANT_TARGETS
    if args.targets.strip():
        labels = [t.strip() for t in args.targets.split(",") if t.strip()]
        targets = [(label, variant_profile(label)) for label in labels]

    try:
        if args.zip_all and args.zip_only:
            for product_id in PRODUCT_ORDER:
                _assert_staging(_staging_path(product_id), product_id=product_id)
            package_all_product_zips()
            return 0

        if args.zip_only:
            staging = STAGING_DIR
            _assert_staging(staging, product_id=product_id)
            package_zip(staging, version=args.version)
            return 0

        staging = build_bundle_tree(
            configuration=args.configuration,
            skip_build=args.skip_build,
            targets=targets,
            product_id=product_id,
        )

        if args.stage_dir is not None:
            _copy_staging_to(args.stage_dir.resolve(), product_id=product_id)

        if args.deploy:
            _deploy_to_game()

        if args.zip_all:
            package_all_product_zips()
        elif not args.no_zip and args.stage_dir is None:
            package_zip(staging, version=args.version)
    except RuntimeError as ex:
        fail(str(ex))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
