"""Release zip naming and paths (stable/beta mod + optional tools)."""

from __future__ import annotations

import os
from pathlib import Path

RELEASE_PROFILES = ("stable", "beta")


def tools_rid(cli_value: str = "") -> str:
    if cli_value.strip():
        return cli_value.strip()
    env = os.environ.get("TOOLS_RID", "").strip()
    if env:
        return env
    return "win-x64" if os.name == "nt" else "linux-x64"


def mod_zip_name(version: str, profile: str = "stable") -> str:
    if profile == "beta":
        return f"KitLib-v{version}-beta.zip"
    return f"KitLib-v{version}.zip"


def mod_zip_path(repo_root: Path, version: str, profile: str = "stable") -> Path:
    return repo_root / "build" / mod_zip_name(version, profile)


def mcp_zip_name(version: str, rid: str) -> str:
    return f"KitLib.Mcp-v{version}-{rid}.zip"


def kitlog_zip_name(version: str, rid: str) -> str:
    return f"KitLog.Cli-v{version}-{rid}.zip"


def mcp_zip_path(repo_root: Path, version: str, rid: str) -> Path:
    return repo_root / "build" / mcp_zip_name(version, rid)


def kitlog_zip_path(repo_root: Path, version: str, rid: str) -> Path:
    return repo_root / "build" / kitlog_zip_name(version, rid)


def github_release_assets(repo_root: Path, version: str, rid: str) -> list[Path]:
    return [
        mod_zip_path(repo_root, version, "stable"),
        mod_zip_path(repo_root, version, "beta"),
        mcp_zip_path(repo_root, version, rid),
        kitlog_zip_path(repo_root, version, rid),
    ]
