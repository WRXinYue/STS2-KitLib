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

CORE_DLL = "KitLib.dll"
ABSTRACTIONS_DLL = "KitLib.Abstractions.dll"
ABSTRACTIONS_RUNTIME_DLLS = [
    "Semver.dll",
    "Microsoft.Extensions.Primitives.dll",
]

# Build artifacts that must not ship in mods/KitLib/ (STS2 treats some JSON as mod manifests).
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
    required = [CORE_DLL, ABSTRACTIONS_DLL, *ABSTRACTIONS_RUNTIME_DLLS]
    missing = [name for name in required if not (bundle_dir / name).is_file()]
    if missing:
        raise FileNotFoundError(f"KitLib bundle incomplete under {bundle_dir}: missing {', '.join(missing)}.")


def _read_version() -> str:
    data = json.loads((_REPO / "KitLib.json").read_text(encoding="utf-8"))
    return str(data["version"])


def _dotnet_build() -> None:
    subprocess.run(
        ["dotnet", "build", str(_REPO / "KitLib.sln")],
        cwd=_REPO,
        check=True,
    )


def _should_package_root_item(item: Path) -> bool:
    if item.name == MODULES_SUBDIR and item.is_dir():
        return False
    lower = item.name.lower()
    if any(lower.endswith(suffix) for suffix in _SKIP_PACKAGE_NAME_SUFFIXES):
        return False
    if item.suffix.lower() in _SKIP_PACKAGE_SUFFIXES:
        return False
    if item.suffix.lower() == ".dll" and item.stem in BUNDLE_DLLS:
        return False
    return True


def _resolve_dll(mod_id: str) -> Path | None:
    bundled = _REPO / "build" / BUNDLE_ID / MODULES_SUBDIR / f"{mod_id}.dll"
    subdir = _REPO / "build" / mod_id / f"{mod_id}.dll"
    if bundled.is_file():
        return bundled
    if subdir.is_file():
        return subdir
    return None


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
                    if module_file.is_file():
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
        core_dll = _resolve_dll(BUNDLE_ID)
        if core_dll is None:
            raise FileNotFoundError("Missing Core build output")
        shutil.copy2(core_dll, dst / "KitLib.dll")
        shutil.copy2(_REPO / "KitLib.json", dst / "mod_manifest.json")

    shutil.copy2(_resolve_abstractions_dll(), dst / ABSTRACTIONS_DLL)
    for runtime_dll in ABSTRACTIONS_RUNTIME_DLLS:
        shutil.copy2(_resolve_abstractions_runtime_dll(runtime_dll), dst / runtime_dll)
    _assert_core_bundle(dst)

    for mod_id in BUNDLE_DLLS:
        dll = _resolve_dll(mod_id)
        if dll is not None:
            shutil.copy2(dll, modules_dst / f"{mod_id}.dll")

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
    args = ap.parse_args()

    version = args.version.strip() or _read_version()
    dist = _REPO / "build" / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    dist.mkdir(parents=True)

    if not args.skip_build:
        _dotnet_build()

    bundle_dir = _stage_bundle(dist)

    main_zip = _REPO / "build" / f"KitLib-v{version}.zip"
    _zip_dir(bundle_dir, main_zip)

    print(f"Packaged release zip: {main_zip.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
