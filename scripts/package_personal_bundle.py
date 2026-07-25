#!/usr/bin/env python3
"""Build a personal multi-API KitLib bundle (loader + lib/<api>/Core + modules).

Inspired by LustTravel2's package_bundle.py. Not used by CI or public releases.

Layout:
  build/KitLib-personal/
    KitLib.dll
    KitLib.Abstractions.dll
    Semver.dll, Microsoft.Extensions.Primitives.dll
    mod_manifest.json
    lib/0.107.1/KitLib.Core.dll, modules/*.dll, compat-target.txt
    lib/0.109.0/...

Usage:
  python scripts/package_personal_bundle.py
  python scripts/package_personal_bundle.py --deploy
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO = _SCRIPT_DIR.parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.bundle_build import build_bundle  # noqa: E402
from lib.dotenv import load_dotenv  # noqa: E402
from lib.sts2_profiles import (  # noqa: E402
    PERSONAL_VARIANT_TARGETS,
    resolve_profile_dir,
    variant_profile,
)

BUNDLE_ID = "KitLib"
STAGING_DIR = _REPO / "build" / f"{BUNDLE_ID}-personal"
BUILD_DIR = _REPO / "build" / BUNDLE_ID
LOADER_PROJECT = _REPO / "src" / "KitLib.Loader" / "KitLib.Loader.csproj"
MANIFEST_SRC = _REPO / "KitLib.json"
COMPAT_MARKER = "compat-target.txt"
CORE_DLL = "KitLib.Core.dll"
MODULES_SUBDIR = "modules"
SHARED_ROOT_DLLS = [
    "KitLib.Abstractions.dll",
    "Semver.dll",
    "Microsoft.Extensions.Primitives.dll",
]


def fail(msg: str) -> None:
    raise SystemExit(msg)


def _dotnet(args: list[str]) -> None:
    cmd = ["dotnet", *args]
    print("+", " ".join(cmd))
    subprocess.run(cmd, cwd=_REPO, check=True)


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

    core_src = BUILD_DIR / CORE_DLL
    if not core_src.is_file():
        fail(f"Missing {core_src} after build for {compat} ({profile}).")

    shutil.copy2(core_src, variant_dir / CORE_DLL)
    modules_src = BUILD_DIR / MODULES_SUBDIR
    if modules_src.is_dir():
        for module_dll in modules_src.glob("*.dll"):
            shutil.copy2(module_dll, modules_dst / module_dll.name)

    (variant_dir / COMPAT_MARKER).write_text(compat + "\n", encoding="utf-8", newline="\n")
    print(f"[personal] Staged variant {compat} ({profile})")


def _stage_shared_root(*, configuration: str) -> None:
    for name in SHARED_ROOT_DLLS:
        src = BUILD_DIR / name
        if not src.is_file():
            src = _REPO / "src" / "KitLib.Abstractions" / "bin" / configuration / "net9.0" / name
        if not src.is_file():
            fail(f"Missing shared DLL {name}. Run dotnet restore and retry.")
        shutil.copy2(src, STAGING_DIR / name)

    if MANIFEST_SRC.is_file():
        shutil.copy2(MANIFEST_SRC, STAGING_DIR / "mod_manifest.json")


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


def _deploy_to_game() -> None:
    mods_root = _read_mods_dir()
    if mods_root is None:
        fail("Cannot resolve game mods dir. Run make init (local.props) or set Sts2Dir.")
    dest = mods_root / BUNDLE_ID
    if dest.exists():
        shutil.rmtree(dest)
    shutil.copytree(STAGING_DIR, dest)
    print(f"[personal] Deployed to {dest}")


def build_personal_bundle(
    *,
    configuration: str = "Debug",
    deploy: bool = False,
    targets: list[tuple[str, str]] | None = None,
) -> Path:
    targets = targets or PERSONAL_VARIANT_TARGETS
    for compat, _profile in targets:
        resolve_profile_dir(variant_profile(compat), repo_root=_REPO)

    _clear_staging()
    for compat, profile in targets:
        _stage_variant(compat, profile, configuration=configuration)

    _stage_shared_root(configuration=configuration)
    _build_loader(configuration=configuration, profile=targets[-1][1])

    print(f"[personal] Bundle ready: {STAGING_DIR}")
    if deploy:
        _deploy_to_game()
    return STAGING_DIR


def main() -> int:
    ap = argparse.ArgumentParser(description="Build personal multi-API KitLib bundle.")
    ap.add_argument("-c", "--configuration", default="Debug")
    ap.add_argument("--deploy", action="store_true", help="Copy bundle into game mods/KitLib/")
    ap.add_argument(
        "--targets",
        default="",
        help="Comma-separated compat targets (default: 0.107.1,0.109.0)",
    )
    args = ap.parse_args()
    load_dotenv(_REPO / ".env")

    targets = PERSONAL_VARIANT_TARGETS
    if args.targets.strip():
        labels = [t.strip() for t in args.targets.split(",") if t.strip()]
        targets = [(label, variant_profile(label)) for label in labels]

    try:
        build_personal_bundle(
            configuration=args.configuration,
            deploy=args.deploy,
            targets=targets,
        )
    except RuntimeError as ex:
        fail(str(ex))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
