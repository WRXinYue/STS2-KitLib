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
if str(_REPO / "scripts") not in sys.path:
    sys.path.insert(0, str(_REPO / "scripts"))

from lib.bundle_build import build_bundle  # noqa: E402
from lib.release_assets import RELEASE_PROFILES, mod_zip_path  # noqa: E402

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

ENTRY_DLL = "KitLib.dll"
CORE_DLL = "KitLib.Core.dll"
ABSTRACTIONS_DLL = "KitLib.Abstractions.dll"
MOD_VARIANT_LOADER_DLL = "KitLib.ModVariantLoader.dll"
ABSTRACTIONS_RUNTIME_DLLS = [
    "Semver.dll",
    "Microsoft.Extensions.Primitives.dll",
]

_SKIP_PACKAGE_NAMES = {"GodotSharp.dll"}
_SKIP_PACKAGE_ROOT_NAMES = {
    "kitlib-variants.manifest",
    "lib",
    MODULES_SUBDIR,
}
_SKIP_PACKAGE_SUFFIXES = {".pdb"}
_SKIP_PACKAGE_NAME_SUFFIXES = (".deps.json", ".runtimeconfig.json")


def _resolve_abstractions_dll() -> Path:
    candidate = _REPO / "build" / BUNDLE_ID / ABSTRACTIONS_DLL
    if candidate.is_file():
        return candidate
    raise FileNotFoundError(f"Missing {ABSTRACTIONS_DLL} build output. Run dotnet build / make build-all first.")


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


def _assert_core_bundle(bundle_dir: Path) -> None:
    required = [ENTRY_DLL, CORE_DLL, ABSTRACTIONS_DLL, MOD_VARIANT_LOADER_DLL, *ABSTRACTIONS_RUNTIME_DLLS]
    missing = [name for name in required if not (bundle_dir / name).is_file()]
    if missing:
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}.")


def _read_version() -> str:
    data = json.loads((_REPO / "KitLib.json").read_text(encoding="utf-8"))
    return str(data["version"])


def _dotnet_build(*, configuration: str, sts2_profile: str) -> None:
    sts2_dir = subprocess.check_output(
        [sys.executable, str(_REPO / "scripts" / "resolve_sts2_profile_dir.py"), sts2_profile],
        text=True,
    ).strip()
    build_bundle(configuration=configuration, sts2_profile=sts2_profile, sts2_dir=sts2_dir)


def _package_profile(
    version: str,
    profile: str,
    *,
    configuration: str,
    skip_build: bool,
) -> Path:
    dist = _REPO / "build" / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    dist.mkdir(parents=True)

    if not skip_build:
        _dotnet_build(configuration=configuration, sts2_profile=profile)

    bundle_dir = _stage_bundle(dist)
    zip_path = mod_zip_path(_REPO, version, profile)
    _zip_dir(bundle_dir, zip_path)
    print(f"Packaged release zip: {zip_path.name} ({profile})")
    return zip_path


def _restage_dist_for_profile(profile: str, *, configuration: str) -> None:
    """Leave build/dist/KitLib staged (stable profile) for downstream packaging."""
    dist = _REPO / "build" / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    dist.mkdir(parents=True)
    _dotnet_build(configuration=configuration, sts2_profile=profile)
    _stage_bundle(dist)


def _should_package_root_item(item: Path) -> bool:
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
    if item.suffix.lower() == ".dll" and item.stem in BUNDLE_DLLS:
        return False
    return True


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
                    if module_file.is_file() and module_file.suffix.lower() == ".dll":
                        shutil.copy2(module_file, modules_dst / module_file.name)
                continue
            if not _should_package_root_item(item):
                continue
            target = dst / item.name
            if item.is_dir():
                shutil.copytree(item, target)
            else:
                shutil.copy2(item, target)
    else:
        raise FileNotFoundError("Missing Core build output under build/KitLib/. Run make build first.")

    shutil.copy2(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL)
    variant_loader = _REPO / "build" / BUNDLE_ID / MOD_VARIANT_LOADER_DLL
    if not variant_loader.is_file():
        raise FileNotFoundError(f"Missing {MOD_VARIANT_LOADER_DLL}. Run make build first.")
    shutil.copy2(variant_loader, dst / MOD_VARIANT_LOADER_DLL)
    for runtime_dll in ABSTRACTIONS_RUNTIME_DLLS:
        shutil.copy2(_resolve_abstractions_runtime_dll(runtime_dll), dst / runtime_dll)
    _assert_core_bundle(dst)

    manifest = _REPO / "KitLib.json"
    if manifest.is_file():
        shutil.copy2(manifest, dst / "mod_manifest.json")

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
        help="STS2 API profile (default: resolve_sts2_compile_profile.py)",
    )
    ap.add_argument(
        "--all-profiles",
        action="store_true",
        help="Build and zip stable + beta (KitLib-vX.zip and KitLib-vX-beta.zip)",
    )
    ap.add_argument(
        "--stage-dir",
        type=Path,
        default=None,
        help="Stage mods/KitLib/ tree at STAGE-DIR/KitLib/ and exit (no zip).",
    )
    args = ap.parse_args()

    if args.all_profiles and args.sts2_profile:
        print("ERROR: use --all-profiles or --sts2-profile, not both.", file=sys.stderr)
        return 1

    if not args.skip_build and args.stage_dir is None:
        if args.all_profiles:
            pass
        elif args.sts2_profile:
            _dotnet_build(configuration=args.configuration, sts2_profile=args.sts2_profile)
        else:
            profile = subprocess.check_output(
                [sys.executable, str(_REPO / "scripts" / "resolve_sts2_compile_profile.py")],
                text=True,
            ).strip()
            _dotnet_build(configuration=args.configuration, sts2_profile=profile)

    if args.stage_dir is not None:
        stage_root = args.stage_dir.resolve()
        if stage_root.exists():
            shutil.rmtree(stage_root)
        stage_root.mkdir(parents=True)
        bundle_dir = _stage_bundle(stage_root)
        print(f"Staged bundle: {bundle_dir}")
        return 0

    version = args.version.strip() or _read_version()

    if args.all_profiles:
        for profile in RELEASE_PROFILES:
            _package_profile(
                version,
                profile,
                configuration=args.configuration,
                skip_build=False,
            )
        _restage_dist_for_profile("stable", configuration=args.configuration)
        return 0

    profile = args.sts2_profile or subprocess.check_output(
        [sys.executable, str(_REPO / "scripts" / "resolve_sts2_compile_profile.py")],
        text=True,
    ).strip()
    _package_profile(
        version,
        profile,
        configuration=args.configuration,
        skip_build=args.skip_build,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
