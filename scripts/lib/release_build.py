"""Build KitLib release assets directly, without shelling back through ``make``.

The release scripts used to run ``make zip-full`` / ``make zip-mcp`` from Python, which meant
``make -> python -> make -> python`` and duplicated the Makefile's dotnet/zip arguments. These
helpers keep a single non-interactive implementation that both the release CLI and the publish
scripts call.
"""

from __future__ import annotations

import subprocess
import sys
import zipfile
from pathlib import Path

from lib.release_assets import (
    mcp_exe_path,
    mcp_publish_command,
    mcp_zip_path,
    mod_zip_path,
)


class ReleaseBuildError(RuntimeError):
    """A release asset could not be built."""


def _run(cmd: list[str], repo_root: Path) -> None:
    printable = " ".join(cmd)
    print(f"$ {printable}", flush=True)
    try:
        subprocess.run(cmd, cwd=repo_root, check=True)
    except subprocess.CalledProcessError as ex:
        raise ReleaseBuildError(f"Command failed ({ex.returncode}): {printable}") from ex


def build_mod_zip(repo_root: Path, version: str) -> Path:
    """Build the multi-API bundle and its release zip (the Makefile's ``zip-full`` target)."""
    zip_path = mod_zip_path(repo_root, version)
    _run(
        [sys.executable, str(repo_root / "scripts" / "package_bundle.py"), "--zip-all", "-c", "Release"],
        repo_root,
    )
    if not zip_path.is_file():
        raise ReleaseBuildError(f"Release zip missing after build: {zip_path}")
    return zip_path


def build_mcp_zip(repo_root: Path, version: str, rid: str) -> Path:
    """Publish and zip the legacy self-contained MCP stdio proxy (the ``zip-mcp`` target)."""
    zip_path = mcp_zip_path(repo_root, version, rid)
    exe_path = mcp_exe_path(repo_root, rid)
    if not exe_path.is_file():
        _run(mcp_publish_command(repo_root, rid), repo_root)
    if not exe_path.is_file():
        raise ReleaseBuildError(f"KitLib.Mcp not found after publish: {exe_path}")

    zip_path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(exe_path, exe_path.name)
    print(f"Wrote {zip_path}", flush=True)
    return zip_path
