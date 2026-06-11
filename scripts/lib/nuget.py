"""NuGet pack/push helpers for KitLib release scripts."""

from __future__ import annotations

import os
import subprocess
from pathlib import Path

KITLIB_PACKAGE_ID = "STS2.KitLib"
ABSTRACTIONS_PACKAGE_ID = "STS2.KitLib.Abstractions"


def resolve_api_key(explicit: str | None = None) -> str:
    key = (explicit or os.environ.get("NUGET_API_KEY") or "").strip()
    if not key:
        raise RuntimeError("NuGet API key missing. Set NUGET_API_KEY in .env or pass --api-key.")
    return key


def resolve_source(explicit: str | None = None) -> str:
    source = (explicit or os.environ.get("NUGET_SOURCE") or "https://api.nuget.org/v3/index.json").strip()
    if not source:
        raise RuntimeError("NuGet source missing. Set NUGET_SOURCE in .env or pass --source.")
    return source


def nupkg_path(out_dir: Path, package_id: str, version: str) -> Path:
    return out_dir / f"{package_id}.{version}.nupkg"


def run_pack(
    repo_root: Path,
    *,
    package_version: str,
    configuration: str = "Release",
) -> Path:
    dist_dll = repo_root / "build" / "dist" / "KitLib" / "KitLib.dll"
    if not dist_dll.is_file():
        raise RuntimeError(f"Mod dist not found: {dist_dll}\nRun make zip first.")

    out_dir = repo_root / "build" / "nuget"
    out_dir.mkdir(parents=True, exist_ok=True)

    cmd = [
        "dotnet",
        "pack",
        str(repo_root / "src" / "KitLib.Core" / "KitLib.Core.csproj"),
        "-c",
        configuration,
        "--no-build",
        "-o",
        str(out_dir),
        "-p:PackKitLib=true",
        f"-p:PackageVersion={package_version}",
    ]
    subprocess.run(cmd, cwd=repo_root, check=True)

    package = nupkg_path(out_dir, KITLIB_PACKAGE_ID, package_version)
    if not package.is_file():
        raise RuntimeError(f"dotnet pack produced no package: {package}")
    return package


def run_pack_abstractions(
    repo_root: Path,
    *,
    package_version: str,
    configuration: str = "Release",
) -> Path:
    out_dir = repo_root / "build" / "nuget"
    out_dir.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            "dotnet",
            "pack",
            str(repo_root / "src" / "KitLib.Abstractions" / "KitLib.Abstractions.csproj"),
            "-c",
            configuration,
            "-o",
            str(out_dir),
            f"-p:Version={package_version}",
        ],
        cwd=repo_root,
        check=True,
    )
    package = nupkg_path(out_dir, ABSTRACTIONS_PACKAGE_ID, package_version)
    if not package.is_file():
        raise RuntimeError(f"dotnet pack produced no package: {package}")
    return package


def run_push(package: Path, *, source: str, api_key: str) -> None:
    if not package.is_file():
        raise RuntimeError(f"Package not found: {package}")
    subprocess.run(
        [
            "dotnet",
            "nuget",
            "push",
            str(package),
            "--source",
            source,
            "--api-key",
            api_key,
            "--skip-duplicate",
        ],
        check=True,
    )
