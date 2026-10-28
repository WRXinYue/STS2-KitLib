#!/usr/bin/env python3
"""Build the KitLib release pack (loader + complete lib/<api>/ units).

Layout:
  build/KitLib-release/
    KitLib.dll
    mod_manifest.json
    mod_image.png
    lib/0.107.1/KitLib.Core.dll, KitLib.Abstractions.dll (facade), compat-target.txt,
               modules/{KitModPanel.dll, KitLib.Hud.dll, KitLib.Dev.dll}
    lib/0.110.1/...

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
from lib.mod_products import PRODUCTS, product_build_dir  # noqa: E402
from lib.release_assets import product_zip_path, read_product_version  # noqa: E402
from lib.sts2_profiles import (  # noqa: E402
    VARIANT_TARGETS,
    resolve_profile_dir,
    variant_profile,
)

BUNDLE_ID = "KitLib"
STAGING_DIR = _REPO / "build" / f"{BUNDLE_ID}-release"
BUILD_DIR = _REPO / "build" / BUNDLE_ID
LOADER_PROJECT = _REPO / "src" / "KitLib" / "Loader" / "KitLib.Loader.csproj"
MANIFEST_SRC = _REPO / "KitLib.json"
COMPAT_MARKER = "compat-target.txt"
CORE_DLL = "KitLib.Core.dll"
FACADE_DLL = "KitLib.Abstractions.dll"
MODULES_SUBDIR = "modules"
_ZIP_ROOT_FILES = [
    "KitLib.dll",
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


def _resolve_mod_image() -> Path | None:
    candidates = [
        _REPO / "assets" / "mod_image.png",
        _REPO / "mods" / BUNDLE_ID / "mod_image.png",
    ]
    for src in candidates:
        if src.is_file():
            return src
    return None


def _copy_mod_image(dest: Path) -> None:
    src = _resolve_mod_image()
    if src is None:
        return
    shutil.copy2(src, dest / "mod_image.png")


def _stage_satellites(modules_dst: Path, compat: str) -> None:
    for dll_name in PRODUCTS[BUNDLE_ID].satellite_dlls:
        src = product_build_dir(BUNDLE_ID) / MODULES_SUBDIR / f"{dll_name}.dll"
        if not src.is_file():
            print(f"[bundle] Warning: missing {dll_name}.dll for variant {compat}")
            continue
        shutil.copy2(src, modules_dst / f"{dll_name}.dll")


def _clear_staging() -> None:
    if STAGING_DIR.exists():
        shutil.rmtree(STAGING_DIR)
    STAGING_DIR.mkdir(parents=True)


def _stage_variant(compat: str, profile: str, *, configuration: str) -> None:
    sts2_dir = resolve_profile_dir(profile, repo_root=_REPO)
    build_bundle(
        configuration=configuration,
        sts2_profile=profile,
        sts2_dir=str(sts2_dir),
        kitlib_personal_compat=True,
    )

    variant_dir = STAGING_DIR / "lib" / compat
    modules_dst = variant_dir / MODULES_SUBDIR
    variant_dir.mkdir(parents=True)
    modules_dst.mkdir(parents=True)

    src_variant = BUILD_DIR / "lib" / compat
    if not src_variant.is_dir():
        fail(f"Missing {src_variant} after build for {compat} ({profile}).")
    core_src = src_variant / CORE_DLL
    if not core_src.is_file():
        fail(f"Missing {core_src} after build for {compat} ({profile}).")
    facade_src = src_variant / FACADE_DLL
    if not facade_src.is_file():
        fail(f"Missing facade {facade_src} after build for {compat} ({profile}).")

    shutil.copytree(src_variant, variant_dir, dirs_exist_ok=True)
    _stage_satellites(modules_dst, compat)

    (variant_dir / COMPAT_MARKER).write_text(compat + "\n", encoding="utf-8", newline="\n")
    print(f"[bundle] Staged variant {compat} ({profile})")


def _stage_shared_root() -> None:
    if MANIFEST_SRC.is_file():
        shutil.copy2(MANIFEST_SRC, STAGING_DIR / "mod_manifest.json")
    _copy_mod_image(STAGING_DIR)


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


def _assert_staging(staging: Path) -> None:
    required = [
        staging / "KitLib.dll",
        staging / "mod_manifest.json",
        staging / "lib",
    ]
    missing = [str(path.relative_to(_REPO)) for path in required if not path.exists()]
    if missing:
        fail(f"Staging incomplete: {', '.join(missing)}")


def build_bundle_tree(
    *,
    configuration: str = "Release",
    skip_build: bool = False,
    targets: list[tuple[str, str]] | None = None,
) -> Path:
    if skip_build:
        _assert_staging(STAGING_DIR)
        return STAGING_DIR

    targets = targets or VARIANT_TARGETS
    for compat, _profile in targets:
        resolve_profile_dir(variant_profile(compat), repo_root=_REPO)

    _clear_staging()

    for compat, profile in targets:
        _stage_variant(compat, profile, configuration=configuration)

    _stage_shared_root()
    _build_loader(configuration=configuration, profile=targets[-1][1])

    print(f"[bundle] Staging ready: {STAGING_DIR}")
    return STAGING_DIR


def _iter_zip_entries(staging: Path) -> list[tuple[Path, str]]:
    entries: list[tuple[Path, str]] = []
    for name in _ZIP_ROOT_FILES:
        path = staging / name
        if path.is_file():
            entries.append((path, f"{BUNDLE_ID}/{name}"))

    lib_root = staging / "lib"
    if not lib_root.is_dir():
        return entries

    for path in sorted(p for p in lib_root.rglob("*") if p.is_file()):
        arc = path.relative_to(staging).as_posix()
        entries.append((path, f"{BUNDLE_ID}/{arc}"))
    return entries


def package_zip(staging: Path, *, version: str = "") -> Path:
    _assert_staging(staging)
    entries = _iter_zip_entries(staging)
    if not entries:
        fail("No files to package.")

    version = version.strip() or read_product_version(_REPO, BUNDLE_ID)
    zip_path = product_zip_path(_REPO, BUNDLE_ID, version)
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


def _copy_staging_to(stage_root: Path) -> Path:
    bundle_dir = stage_root / BUNDLE_ID
    if stage_root.exists():
        shutil.rmtree(stage_root)
    stage_root.mkdir(parents=True)
    shutil.copytree(STAGING_DIR, bundle_dir)
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
    print(f"[bundle] Deployed KitLib pack to {dest}")


def main() -> int:
    ap = argparse.ArgumentParser(description="Build the KitLib release bundle.")
    ap.add_argument("-c", "--configuration", default="Release")
    ap.add_argument("--skip-build", action="store_true", help="Use existing build/KitLib-release/")
    ap.add_argument("--deploy", action="store_true", help="Copy bundle into game mods/KitLib/")
    ap.add_argument(
        "--stage-dir",
        type=Path,
        default=None,
        help="Copy mods/KitLib/ tree to STAGE-DIR/KitLib/ (no zip).",
    )
    ap.add_argument("--zip-only", action="store_true", help="Zip the existing build/KitLib-release/ staging")
    ap.add_argument("--zip-all", action="store_true", help="Zip the release bundle into build/KitLib-vX.Y.Z.zip")
    ap.add_argument("--no-zip", action="store_true", help="Build staging only; do not write release zip")
    ap.add_argument("--version", default="", help="Override version for zip name (default: KitLib.json)")
    ap.add_argument(
        "--targets",
        default="",
        help="Comma-separated compat targets (default: 0.107.1,0.110.1)",
    )
    ap.add_argument(
        "--product",
        default=BUNDLE_ID,
        help="Accepted for CLI compatibility; only KitLib is published.",
    )
    args = ap.parse_args()
    load_dotenv(_REPO / ".env")

    if args.product.strip() not in PRODUCTS:
        fail(f"Unknown product: {args.product.strip()}")

    targets = VARIANT_TARGETS
    if args.targets.strip():
        labels = [t.strip() for t in args.targets.split(",") if t.strip()]
        targets = [(label, variant_profile(label)) for label in labels]

    try:
        if args.zip_only:
            _assert_staging(STAGING_DIR)
            package_zip(STAGING_DIR, version=args.version)
            return 0

        staging = build_bundle_tree(
            configuration=args.configuration,
            skip_build=args.skip_build,
            targets=targets,
        )

        if args.stage_dir is not None:
            _copy_staging_to(args.stage_dir.resolve())

        if args.deploy:
            _deploy_to_game()

        if not args.no_zip and args.stage_dir is None:
            package_zip(staging, version=args.version)
    except RuntimeError as ex:
        fail(str(ex))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
