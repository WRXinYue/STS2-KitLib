"""Locate the official STS2 ModUploader build for the current operating system."""

from __future__ import annotations

import os
import platform
import sys
from pathlib import Path


def platform_asset() -> str:
    """GitHub release asset suffix for this machine (e.g. ``osx-arm64``)."""
    machine = platform.machine().lower()
    if sys.platform == "darwin":
        return "osx-arm64" if machine in {"arm64", "aarch64"} else "osx-x64"
    if sys.platform.startswith("linux"):
        return "linux-x64"
    if sys.platform.startswith("win"):
        return "win-arm64" if machine in {"arm64", "aarch64"} else "win-x64"
    raise RuntimeError(f"Unsupported platform: {sys.platform}/{platform.machine()}")


def executable_name() -> str:
    return "ModUploader.exe" if sys.platform.startswith("win") else "ModUploader"


def default_install_dir() -> Path:
    """Per-user install location shared by setup and upload."""
    xdg_data = os.environ.get("XDG_DATA_HOME", "").strip()
    base = Path(xdg_data).expanduser() if xdg_data else Path.home() / ".local" / "share"
    return base / "kitlib" / "sts2-mod-uploader" / platform_asset()


def candidate_uploaders(repo_root: Path) -> list[Path]:
    """Existing uploader paths, in priority order, without touching the network."""
    roots = [
        default_install_dir(),
        repo_root / "tools" / "sts2-mod-uploader" / platform_asset(),
        repo_root / "build" / "tools" / "sts2-mod-uploader" / platform_asset(),
    ]
    names = list(dict.fromkeys([executable_name(), "ModUploader.exe", "ModUploader"]))
    found: list[Path] = []
    for root in roots:
        for name in names:
            candidate = root / name
            if candidate.is_file():
                found.append(candidate)
    return found
